using System.Collections.Generic;
using MvCamCtrl.NET;

namespace WpfApp2.Services
{
    /// <summary>
    /// 海康 MvCamCtrl.NET 的薄封装。官方 C# 示例：
    /// <c>MVS\Development\Samples\C#\MvCamCtrlNet\BasicDemo\BasicDemo.cs</c>
    ///
    /// 本类只做三件事，不持有 CCamera 句柄：
    /// 1. 枚举：CSystem.EnumDevices（对照 BasicDemo.DeviceListAcq）
    /// 2. 把官方 CCameraInfo 转成我们自己的 CameraInfo（对照 Demo 里强转 CGigECameraInfo / CUSBCameraInfo）
    /// 3. 按序列号找回官方对象，给 CreateHandle 用
    ///
    /// 和 BasicDemo 的差别：官方默认只枚举真 GigE/USB；虚拟相机必须再加 MV_VIR_*。
    /// </summary>
    internal static class HikSdk
    {
        /// <summary>
        /// EnumDevices 的传输层掩码。各 bit 可按或组合。
        /// 官方 Demo：<c>CSystem.MV_GIGE_DEVICE | CSystem.MV_USB_DEVICE</c>
        /// 我们额外打开虚拟层，才能看见 MVS 里启动的 Vir… 设备。
        /// </summary>
        public static readonly uint LayerMask =
            CSystem.MV_GIGE_DEVICE
            | CSystem.MV_USB_DEVICE
            | CSystem.MV_VIR_GIGE_DEVICE
            | CSystem.MV_VIR_USB_DEVICE;

        /// <summary>
        /// 对照 BasicDemo.ShowErrorMsg。SDK 失败码是 int，习惯写成 8 位十六进制（如 0x80000004）。
        /// 常见：MV_E_ACCESS_DENIED 已被独占；MV_E_BUSY 设备忙；MV_E_HANDLE 句柄无效。
        /// </summary>
        public static string FormatError(int nRet)
        {
            string hex = "0x" + nRet.ToString("X8");
            if (nRet == CErrorDefine.MV_E_ACCESS_DENIED)
            {
                return hex + " 无访问权限（设备已被独占）";
            }

            if (nRet == CErrorDefine.MV_E_BUSY)
            {
                return hex + " 设备忙";
            }

            if (nRet == CErrorDefine.MV_E_HANDLE)
            {
                return hex + " 句柄无效";
            }

            return hex;
        }

        /// <summary>
        /// 对照 <c>CSystem.EnumDevices(layer, ref list)</c>。
        /// 只搜索、不打开。返回 MV_OK(0) 才算成功；list 里是官方 CCameraInfo，CreateHandle 必须用它。
        /// </summary>
        public static int EnumerateRaw(out List<CCameraInfo> devices)
        {
            devices = new List<CCameraInfo>();
            return CSystem.EnumDevices(LayerMask, ref devices);
        }

        /// <summary>
        /// 从官方设备信息取出序列号。GigE / USB 字段同名但类型不同，必须先按 nTLayerType 强转。
        /// </summary>
        public static string GetSerial(CCameraInfo info)
        {
            CameraInfo described = Describe(info);
            return described == null ? "" : described.Serial ?? "";
        }

        /// <summary>
        /// 对照 BasicDemo.DeviceListAcq 里的显示逻辑：
        /// GigE → (CGigECameraInfo)info；USB → (CUSBCameraInfo)info。
        /// 虚拟设备和真机用同一套结构体，靠 nTLayerType 区分 MV_VIR_*。
        /// </summary>
        public static CameraInfo Describe(CCameraInfo info)
        {
            if (info == null)
            {
                return null;
            }

            if (info.nTLayerType == CSystem.MV_GIGE_DEVICE
                || info.nTLayerType == CSystem.MV_VIR_GIGE_DEVICE)
            {
                CGigECameraInfo gige = (CGigECameraInfo)info;
                return new CameraInfo
                {
                    Serial = gige.chSerialNumber,
                    Model = gige.chModelName,
                    Manufacturer = gige.chManufacturerName,
                    Transport = info.nTLayerType == CSystem.MV_VIR_GIGE_DEVICE ? "虚拟GigE" : "GigE"
                };
            }

            if (info.nTLayerType == CSystem.MV_USB_DEVICE
                || info.nTLayerType == CSystem.MV_VIR_USB_DEVICE)
            {
                CUSBCameraInfo usb = (CUSBCameraInfo)info;
                return new CameraInfo
                {
                    Serial = usb.chSerialNumber,
                    Model = usb.chModelName,
                    Manufacturer = usb.chManufacturerName,
                    Transport = info.nTLayerType == CSystem.MV_VIR_USB_DEVICE ? "虚拟USB" : "USB"
                };
            }

            return new CameraInfo
            {
                Serial = "",
                Model = "",
                Manufacturer = "",
                Transport = "其他(" + info.nTLayerType + ")"
            };
        }

        /// <summary>
        /// BasicDemo 用下拉框索引选设备；我们按序列号找回同一条 CCameraInfo。
        /// CreateHandle 必须拿枚举当时的官方对象，不能自己 new 一个空的。
        /// </summary>
        public static CCameraInfo FindBySerial(List<CCameraInfo> devices, string serial)
        {
            if (devices == null || string.IsNullOrWhiteSpace(serial))
            {
                return null;
            }

            string want = serial.Trim();
            for (int i = 0; i < devices.Count; i++)
            {
                string got = GetSerial(devices[i]);
                if (!string.IsNullOrEmpty(got)
                    && string.Equals(got.Trim(), want, System.StringComparison.OrdinalIgnoreCase))
                {
                    return devices[i];
                }
            }

            return null;
        }
    }
}
