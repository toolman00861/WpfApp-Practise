using System;
using System.Runtime.InteropServices;
using HalconDotNet;

namespace WpfApp2.Services
{
    /// <summary>
    /// Halcon 练习入口。界面只给 CameraFrame（BGR24 字节），不直接碰 HObject，也不碰海康 CCamera。
    /// 与海康的交界：CameraService.ConvertPixelType(BGR8_Packed) → 本类 gen_image_interleaved("bgr")。
    /// 流水线：字节 → HImage → 算子 → 再拷回 BGR24，给 WPF Image 用。
    /// HObject 用完必须 Dispose，否则实时点几下内存就会涨。
    /// </summary>
    public static class HalconService
    {
        private static readonly object Sync = new object();

        public static string LastError { get; private set; }

        /// <summary>只探活、记版本，不占相机。</summary>
        public static void Init()
        {
            lock (Sync)
            {
                LastError = null;
                try
                {
                    HTuple version;
                    HOperatorSet.GetSystem("version", out version);
                    AppLog.Info("Halcon " + version.S);
                }
                catch (Exception ex)
                {
                    LastError = "Halcon 初始化失败：" + ex.Message;
                    AppLog.Error("Halcon 初始化失败（未安装、位数不匹配或没有许可）", ex);
                }
            }
        }

        /// <summary>彩色图转灰度。结果仍是 BGR24，三个通道同一套灰值，WPF 才能直接显示。</summary>
        public static bool TryToGray(CameraFrame input, out CameraFrame output)
        {
            output = null;
            lock (Sync)
            {
                LastError = null;
                HObject color = null;
                HObject gray = null;
                try
                {
                    if (!TryCreateImage(input, out color))
                    {
                        return false;
                    }

                    if (!TryAsGray(color, out gray))
                    {
                        return false;
                    }

                    return TryToBgrFrame(gray, out output);
                }
                catch (HOperatorException ex)
                {
                    return Fail("转灰度失败：" + ex.Message);
                }
                finally
                {
                    DisposeObj(color);
                    DisposeObj(gray);
                }
            }
        }

        /// <summary>
        /// 先转灰度，再算整图均值和标准差。均值大约 0～255：太低偏暗，太高偏亮。
        /// </summary>
        public static bool TryMeasureGray(CameraFrame input, out CameraFrame output, out double mean, out double deviation)
        {
            output = null;
            mean = 0;
            deviation = 0;
            lock (Sync)
            {
                LastError = null;
                HObject color = null;
                HObject gray = null;
                try
                {
                    if (!TryCreateImage(input, out color))
                    {
                        return false;
                    }

                    if (!TryAsGray(color, out gray))
                    {
                        return false;
                    }

                    HTuple hvMean;
                    HTuple hvDeviation;
                    HOperatorSet.Intensity(gray, gray, out hvMean, out hvDeviation);
                    mean = hvMean.D;
                    deviation = hvDeviation.D;
                    return TryToBgrFrame(gray, out output);
                }
                catch (HOperatorException ex)
                {
                    return Fail("测亮度失败：" + ex.Message);
                }
                finally
                {
                    DisposeObj(color);
                    DisposeObj(gray);
                }
            }
        }

        /// <summary>
        /// 灰度后按 [minGray, maxGray] 二值化。落在区间里的像素变白（255），其余变黑。
        /// </summary>
        public static bool TryThreshold(CameraFrame input, int minGray, int maxGray, out CameraFrame output)
        {
            output = null;
            lock (Sync)
            {
                LastError = null;
                if (minGray < 0 || maxGray > 255 || minGray > maxGray)
                {
                    return Fail("阈值必须满足 0 ≤ 下限 ≤ 上限 ≤ 255。");
                }

                HObject color = null;
                HObject gray = null;
                HObject region = null;
                HObject binary = null;
                try
                {
                    if (!TryCreateImage(input, out color))
                    {
                        return false;
                    }

                    if (!TryAsGray(color, out gray))
                    {
                        return false;
                    }

                    HTuple width;
                    HTuple height;
                    HOperatorSet.GetImageSize(gray, out width, out height);
                    HOperatorSet.Threshold(gray, out region, minGray, maxGray);
                    HOperatorSet.RegionToBin(region, out binary, 255, 0, width, height);
                    return TryToBgrFrame(binary, out output);
                }
                catch (HOperatorException ex)
                {
                    return Fail("二值化失败：" + ex.Message);
                }
                finally
                {
                    DisposeObj(color);
                    DisposeObj(gray);
                    DisposeObj(region);
                    DisposeObj(binary);
                }
            }
        }

        /// <summary>
        /// 海康链路终点：CameraService.CopyToBgr24 已经 ConvertPixelType 成 PixelType_Gvsp_BGR8_Packed。
        /// Halcon 用 gen_image_interleaved 按 "bgr" 读进来，内部会拆成 3 个通道。
        /// 指针必须钉住，否则 GC 一搬数组，Halcon 就读到野指针。
        /// </summary>
        private static bool TryCreateImage(CameraFrame frame, out HObject image)
        {
            image = null;
            if (frame == null || frame.Bgr24 == null || frame.Width <= 0 || frame.Height <= 0)
            {
                return Fail("没有可用的图像。");
            }

            int need = frame.Width * frame.Height * 3;
            if (frame.Bgr24.Length < need)
            {
                return Fail("像素长度不够。");
            }

            GCHandle pin = GCHandle.Alloc(frame.Bgr24, GCHandleType.Pinned);
            try
            {
                IntPtr pointer = pin.AddrOfPinnedObject();
                HOperatorSet.GenImageInterleaved(
                    out image,
                    pointer,
                    "bgr",
                    frame.Width,
                    frame.Height,
                    0,
                    "byte",
                    frame.Width,
                    frame.Height,
                    0,
                    0,
                    -1,
                    0);
                return true;
            }
            finally
            {
                pin.Free();
            }
        }

        /// <summary>1 通道原样拷贝；3 通道走 rgb1_to_gray。</summary>
        private static bool TryAsGray(HObject image, out HObject gray)
        {
            gray = null;
            HOperatorSet.CountChannels(image, out HTuple channels);
            if (channels.I == 1)
            {
                HOperatorSet.CopyImage(image, out gray);
                return true;
            }

            if (channels.I == 3)
            {
                HOperatorSet.Rgb1ToGray(image, out gray);
                return true;
            }

            return Fail("只处理 1 或 3 通道图，当前是 " + channels.I + " 通道。");
        }

        /// <summary>
        /// 统一吐 BGR24。灰度会先 compose3 成假彩色，再 interleave_channels 拉成与 CameraFrame 相同的内存布局。
        /// GetImagePointer1 的指针只在这个 HObject 活着时有效，所以这里立刻拷走。
        /// </summary>
        private static bool TryToBgrFrame(HObject image, out CameraFrame frame)
        {
            frame = null;
            HObject color = null;
            HObject interleaved = null;
            try
            {
                HTuple width;
                HTuple height;
                HOperatorSet.GetImageSize(image, out width, out height);

                HTuple channels;
                HOperatorSet.CountChannels(image, out channels);
                HObject rgb = image;
                if (channels.I == 1)
                {
                    HOperatorSet.Compose3(image, image, image, out color);
                    rgb = color;
                }

                HOperatorSet.InterleaveChannels(rgb, out interleaved, "bgr", 0, 0);

                HTuple pointer;
                HTuple type;
                HTuple pointerWidth;
                HTuple pointerHeight;
                HOperatorSet.GetImagePointer1(interleaved, out pointer, out type, out pointerWidth, out pointerHeight);

                int need = width.I * height.I * 3;
                var copy = new byte[need];
                Marshal.Copy(pointer.IP, copy, 0, need);
                frame = new CameraFrame
                {
                    Width = width.I,
                    Height = height.I,
                    Bgr24 = copy
                };
                return true;
            }
            finally
            {
                DisposeObj(color);
                DisposeObj(interleaved);
            }
        }

        private static void DisposeObj(HObject obj)
        {
            obj?.Dispose();
        }

        private static bool Fail(string message)
        {
            LastError = message;
            AppLog.Warn(message);
            return false;
        }
    }
}
