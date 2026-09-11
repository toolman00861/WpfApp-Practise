namespace WpfApp2.Services
{
    /// <summary>
    /// 枚举结果的只读摘要。对照官方 CGigECameraInfo / CUSBCameraInfo 里的
    /// chSerialNumber、chModelName、chManufacturerName、nTLayerType。
    /// 界面和 spec.json 只用序列号；CreateHandle 仍必须拿 HikSdk 找回的官方 CCameraInfo。
    /// </summary>
    public sealed class CameraInfo
    {
        public string Serial { get; set; }
        public string Model { get; set; }
        public string Manufacturer { get; set; }
        /// <summary>GigE / USB / 虚拟USB 等，对应 nTLayerType，方便对照 MVS Client。</summary>
        public string Transport { get; set; }

        public string DisplayText
        {
            get
            {
                return (Transport ?? "") + "  " + (Model ?? "") + "  [" + (Serial ?? "") + "]";
            }
        }

        public override string ToString()
        {
            return DisplayText;
        }
    }
}
