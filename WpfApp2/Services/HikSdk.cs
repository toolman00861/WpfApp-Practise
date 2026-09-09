using System.Collections.Generic;
using MvCamCtrl.NET;

namespace WpfApp2.Services
{
    /// <summary>
    /// 把官方 CCameraInfo 转成我们自己的 CameraInfo。
    /// 虚拟相机必须带上 MV_VIR_*，BasicDemo 默认只枚举真 GigE/USB。
    /// </summary>
    internal static class HikSdk
    {
        public static readonly uint LayerMask =
            CSystem.MV_GIGE_DEVICE
            | CSystem.MV_USB_DEVICE
            | CSystem.MV_VIR_GIGE_DEVICE
            | CSystem.MV_VIR_USB_DEVICE;

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

        public static int EnumerateRaw(out List<CCameraInfo> devices)
        {
            devices = new List<CCameraInfo>();
            return CSystem.EnumDevices(LayerMask, ref devices);
        }

        public static string GetSerial(CCameraInfo info)
        {
            CameraInfo described = Describe(info);
            return described == null ? "" : described.Serial ?? "";
        }

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
