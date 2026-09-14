using System;
using System.Collections.Generic;
using System.IO;
using MvCamCtrl.NET;
using MvCamCtrl.NET.CameraParams;

namespace WpfApp2.Services
{
    /// <summary>
    /// 一台相机的 SDK 会话。对应官方 BasicDemo / Grab_Callback 的 <c>CCamera m_MyCamera</c>。
    ///
    /// 官方调用顺序（C 接口名在括号里，方便对照文档）：
    ///   EnumDevices (MV_CC_EnumDevices)
    ///   → CreateHandle (MV_CC_CreateHandle)
    ///   → OpenDevice (MV_CC_OpenDevice)
    ///   → [GigE] GIGE_GetOptimalPacketSize + SetIntValue("GevSCPSPacketSize")
    ///   → SetEnumValue("TriggerMode", OFF)
    ///   → StartGrabbing (MV_CC_StartGrabbing)
    ///   → 循环 GetImageBuffer (MV_CC_GetImageBuffer) / 用完 FreeImageBuffer
    ///   → StopGrabbing → CloseDevice → DestroyHandle
    ///
    /// 取流方式：我们用拉模式 GetImageBuffer（对照 BasicDemo.ReceiveThreadProcess），
    /// 不用 Grab_Callback 的 RegisterImageCallBackEx。
    /// 预览循环是唯一的 GetImageBuffer 调用方；Halcon 只拷贝缓存的最新帧，不再取流。
    /// 由 CameraHub 创建和关闭，界面不要直接 Dispose。
    /// </summary>
    public sealed class CameraService : IDisposable
    {
        private readonly object _gate = new object();
        /// <summary>只保护最新预览帧指针。与 <c>_gate</c> 分开，Halcon 拷贝时不跟 GetImageBuffer 抢锁。</summary>
        private readonly object _latestGate = new object();
        /// <summary>官方 Demo 里的 m_MyCamera。一个 CCamera = 一个设备句柄。</summary>
        private readonly CCamera _device = new CCamera();
        private CameraFrame _latest;
        private bool _handleCreated;
        private uint _layerType;

        public bool IsOpen { get; private set; }
        public bool IsGrabbing { get; private set; }
        public string Serial { get; private set; }
        public string LastError { get; private set; }

        /// <summary>
        /// 对照 BasicDemo.bnOpen_Click。按序列号打开。
        /// 空序列号且本机只有一台时用那一台；两台以上必须写序列号（官方用下拉框索引）。
        /// </summary>
        public bool Open(string serialOrNull)
        {
            lock (_gate)
            {
                LastError = null;
                if (IsOpen)
                {
                    CloseCore();
                }

                List<CCameraInfo> devices;
                int nRet = HikSdk.EnumerateRaw(out devices);
                if (nRet != CErrorDefine.MV_OK)
                {
                    return Fail("枚举失败 " + HikSdk.FormatError(nRet));
                }

                if (devices.Count == 0)
                {
                    return Fail("没有发现相机。请先在 MVS 里启动虚拟相机，并确认 Client 没有占用。");
                }

                CCameraInfo selected;
                string serial = (serialOrNull ?? "").Trim();
                if (string.IsNullOrEmpty(serial))
                {
                    if (devices.Count != 1)
                    {
                        return Fail("本机有 " + devices.Count + " 台相机，必须指定序列号。");
                    }

                    selected = devices[0];
                    serial = HikSdk.GetSerial(selected);
                }
                else
                {
                    selected = HikSdk.FindBySerial(devices, serial);
                    if (selected == null)
                    {
                        return Fail("找不到序列号 " + serial + "。先点「刷新设备」看列表。");
                    }
                }

                // 官方 Demo 没有 IsDeviceAccessible。虚拟相机驱动常把「上次没关干净」记成独占，
                // 查询结果不可靠：true 也可能 Open 失败，false 也可能其实能开。只打日志，真正以 OpenDevice 为准。
                bool exclusiveOk = CSystem.IsDeviceAccessible(ref selected, MV_ACCESS_MODE.MV_ACCESS_EXCLUSIVE);
                AppLog.Info("准备打开 " + serial + "  独占可达=" + exclusiveOk);

                // 创建设备句柄。还没跟相机通信，只是把枚举信息绑到 CCamera 上。
                nRet = _device.CreateHandle(ref selected);
                if (nRet != CErrorDefine.MV_OK)
                {
                    return Fail("CreateHandle 失败 " + HikSdk.FormatError(nRet));
                }

                _handleCreated = true;
                _layerType = selected.nTLayerType;

                // 真正占用设备。默认独占。上次进程没 DestroyHandle 时驱动会一直占着，
                // 任务管理器里已经看不到 exe。先按官方 OpenDevice()，失败再用 WithSwitch 抢一次。
                nRet = _device.OpenDevice();
                if (nRet == CErrorDefine.MV_E_ACCESS_DENIED)
                {
                    AppLog.Warn("OpenDevice 独占失败，改用 ExclusiveWithSwitch 再试 " + serial);
                    nRet = _device.OpenDevice((uint)MV_ACCESS_MODE.MV_ACCESS_EXCLUSIVEWITHSWITCH, 0);
                }

                if (nRet != CErrorDefine.MV_OK)
                {
                    _device.DestroyHandle();
                    _handleCreated = false;
                    return Fail("OpenDevice 失败 " + HikSdk.FormatError(nRet)
                        + "。打开「虚拟相机工具」，把 " + serial + " 先离线再上线；工具要开着，MVS 主程序不要取流。");
                }

                // 对照 Grab_Callback：只对真 GigE 调最佳包长。虚拟 USB / 虚拟 GigE 跳过。
                if (_layerType == CSystem.MV_GIGE_DEVICE)
                {
                    int packetSize = _device.GIGE_GetOptimalPacketSize();
                    if (packetSize > 0)
                    {
                        nRet = _device.SetIntValue("GevSCPSPacketSize", (uint)packetSize);
                        if (nRet != CErrorDefine.MV_OK)
                        {
                            AppLog.Warn("设置 GevSCPSPacketSize 失败 " + HikSdk.FormatError(nRet));
                        }
                    }
                }

                // Off = 连续出流，对应官方「连续模式」。练习时不用软触发（TriggerMode On + TriggerSoftware）。
                nRet = _device.SetEnumValue("TriggerMode", (uint)MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_OFF);
                if (nRet != CErrorDefine.MV_OK)
                {
                    AppLog.Warn("设置 TriggerMode 失败 " + HikSdk.FormatError(nRet));
                }

                Serial = serial;
                IsOpen = true;
                AppLog.Info("已打开相机 " + Serial);
                return true;
            }
        }

        /// <summary>
        /// 对照 <c>CCamera.StartGrabbing</c>。开始往 SDK 内部队列送帧，还不会把图交给你。
        /// 之后才能 GetImageBuffer。官方是 Open 和 StartGrab 分成两个按钮，我们 OpenStations 里连着调。
        /// </summary>
        public bool StartGrabbing()
        {
            lock (_gate)
            {
                LastError = null;
                if (!IsOpen)
                {
                    return Fail("尚未 Open。");
                }

                if (IsGrabbing)
                {
                    return true;
                }

                int nRet = _device.StartGrabbing();
                if (nRet != CErrorDefine.MV_OK)
                {
                    return Fail("StartGrabbing 失败 " + HikSdk.FormatError(nRet));
                }

                IsGrabbing = true;
                AppLog.Info("开始采集 " + Serial);
                return true;
            }
        }

        /// <summary>
        /// 对照 BasicDemo.bnSaveBmp_Click：取一帧 → SaveImageToFile(BMP)。
        /// 官方 Demo 存的是上次 ReceiveThread 克隆下来的图；我们每次现场 GetImageBuffer。
        /// MethodValue=2 官方示例 BMP/JPG 都这么填（JPG 时表示质量）。
        /// </summary>
        public bool TryGrabOne(string bmpPath, int timeoutMs = 3000)
        {
            lock (_gate)
            {
                LastError = null;
                CFrameout frame;
                if (!TakeFrame(timeoutMs, true, out frame))
                {
                    return false;
                }

                try
                {
                    if (string.IsNullOrWhiteSpace(bmpPath))
                    {
                        return Fail("bmpPath 为空。");
                    }

                    string dir = Path.GetDirectoryName(bmpPath);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    CSaveImgToFileParam save = new CSaveImgToFileParam();
                    save.Image = frame.Image;
                    save.ImageType = MV_SAVE_IAMGE_TYPE.MV_IMAGE_BMP;
                    save.MethodValue = 2;
                    save.ImagePath = bmpPath;
                    int nRet = _device.SaveImageToFile(ref save);
                    if (nRet != CErrorDefine.MV_OK)
                    {
                        return Fail("SaveImageToFile 失败 " + HikSdk.FormatError(nRet));
                    }

                    AppLog.Info("已保存 " + bmpPath + "  " + frame.Image.Width + "x" + frame.Image.Height);
                    return true;
                }
                finally
                {
                    // 与 GetImageBuffer 成对。不释放，SDK 内部队列会逐渐耗尽。
                    _device.FreeImageBuffer(ref frame);
                }
            }
        }

        /// <summary>
        /// 取一帧并转成 BGR24 拷贝。只给预览循环用（对照 BasicDemo.ReceiveThreadProcess）。
        /// GetImageBuffer → ConvertPixelType(BGR8_Packed) → 拷走 → FreeImageBuffer，并更新最新帧缓存。
        /// Halcon 不要调这个，走 <see cref="TryCloneLatestFrame"/>，避免第二条拉流跟预览抢同一台 CCamera。
        /// 超时不算异常，预览循环会一直重试。
        /// </summary>
        public bool TryGrabFrame(out CameraFrame frame, int timeoutMs = 400)
        {
            frame = null;
            lock (_gate)
            {
                LastError = null;
                CFrameout raw;
                if (!TakeFrame(timeoutMs, false, out raw))
                {
                    return false;
                }

                try
                {
                    if (!CopyToBgr24(raw.Image, out frame))
                    {
                        return false;
                    }

                    PublishLatest(frame);
                    return true;
                }
                finally
                {
                    _device.FreeImageBuffer(ref raw);
                }
            }
        }

        /// <summary>
        /// 拷贝预览缓存的最新一帧。对照官方存图：用接收线程已经拿到的图，不再 GetImageBuffer。
        /// 没有预览过则失败。停预览后仍可处理最后一帧。
        /// </summary>
        public bool TryCloneLatestFrame(out CameraFrame frame)
        {
            CameraFrame snapshot;
            lock (_latestGate)
            {
                snapshot = _latest;
            }

            if (snapshot == null || snapshot.Bgr24 == null || snapshot.Width <= 0 || snapshot.Height <= 0)
            {
                frame = null;
                return Fail("还没有预览帧。请先打开工位并开始预览。");
            }

            frame = snapshot.Clone();
            return true;
        }

        public void Close()
        {
            lock (_gate)
            {
                CloseCore();
            }
        }

        public void Dispose()
        {
            Close();
        }

        /// <summary>
        /// 对照官方关闭顺序：StopGrabbing → CloseDevice → DestroyHandle。
        /// 顺序不能反：先毁句柄再关设备会 MV_E_HANDLE。
        /// 每一步失败也继续往下走，避免 StopGrabbing 失败后句柄一直占着。
        /// </summary>
        private void CloseCore()
        {
            string serial = Serial;
            try
            {
                if (IsGrabbing)
                {
                    int nRet = _device.StopGrabbing();
                    if (nRet != CErrorDefine.MV_OK)
                    {
                        AppLog.Warn("StopGrabbing 失败 " + HikSdk.FormatError(nRet));
                    }

                    IsGrabbing = false;
                }

                if (IsOpen)
                {
                    int nRet = _device.CloseDevice();
                    if (nRet != CErrorDefine.MV_OK)
                    {
                        AppLog.Warn("CloseDevice 失败 " + HikSdk.FormatError(nRet));
                    }

                    IsOpen = false;
                }
            }
            finally
            {
                if (_handleCreated)
                {
                    int nRet = _device.DestroyHandle();
                    if (nRet != CErrorDefine.MV_OK)
                    {
                        AppLog.Warn("DestroyHandle 失败 " + HikSdk.FormatError(nRet));
                    }

                    _handleCreated = false;
                }

                if (!string.IsNullOrEmpty(serial))
                {
                    AppLog.Info("已关闭相机 " + serial);
                }

                Serial = null;
                PublishLatest(null);
            }
        }

        private void PublishLatest(CameraFrame frame)
        {
            lock (_latestGate)
            {
                _latest = frame;
            }
        }

        /// <summary>
        /// 对照 <c>CCamera.GetImageBuffer(ref CFrameout, timeoutMs)</c>。
        /// 阻塞等到一帧或超时。CFrameout.Image 指向 SDK 内部缓冲，调用方用完必须 FreeImageBuffer。
        /// </summary>
        private bool TakeFrame(int timeoutMs, bool logFail, out CFrameout frame)
        {
            frame = new CFrameout();
            if (!IsOpen)
            {
                return Fail("尚未 Open。", logFail);
            }

            if (!IsGrabbing)
            {
                return Fail("尚未 StartGrabbing。", logFail);
            }

            int nRet = _device.GetImageBuffer(ref frame, timeoutMs);
            if (nRet != CErrorDefine.MV_OK)
            {
                return Fail("GetImageBuffer 超时或失败 " + HikSdk.FormatError(nRet), logFail);
            }

            return true;
        }

        /// <summary>
        /// 对照 BasicDemo 里 CPixelConvertParam：相机原始像素（Bayer/Mono/YUV 等）→ BGR8_Packed。
        /// WPF PixelFormats.Bgr24 和 Halcon gen_image_interleaved("bgr") 都吃这个布局：每像素 B,G,R 紧挨着。
        /// 必须 BlockCopy 走，因为 Convert 的输出缓冲随后会被 SDK 复用。
        /// </summary>
        private bool CopyToBgr24(CImage image, out CameraFrame frame)
        {
            frame = null;
            if (image == null)
            {
                return Fail("空图像。", false);
            }

            CPixelConvertParam convert = new CPixelConvertParam();
            convert.InImage = image;
            convert.OutImage.PixelType = MvGvspPixelType.PixelType_Gvsp_BGR8_Packed;
            int nRet = _device.ConvertPixelType(ref convert);
            if (nRet != CErrorDefine.MV_OK)
            {
                return Fail("ConvertPixelType 失败 " + HikSdk.FormatError(nRet), false);
            }

            byte[] data = convert.OutImage.ImageData;
            int width = (int)convert.OutImage.Width;
            int height = (int)convert.OutImage.Height;
            int need = width * height * 3;
            if (data == null || width <= 0 || height <= 0 || data.Length < need)
            {
                return Fail("转换后的像素长度不够。", false);
            }

            var copy = new byte[need];
            Buffer.BlockCopy(data, 0, copy, 0, need);
            frame = new CameraFrame
            {
                Width = width,
                Height = height,
                Bgr24 = copy
            };
            return true;
        }

        private bool Fail(string message)
        {
            return Fail(message, true);
        }

        private bool Fail(string message, bool log)
        {
            LastError = message;
            if (log)
            {
                AppLog.Warn(message);
            }

            return false;
        }
    }
}
