namespace WpfApp2.Services
{
    /// <summary>
    /// 枚举结果的只读摘要。界面和配置只用序列号，不拿 SDK 句柄。
    /// </summary>
    public sealed class CameraInfo
    {
        public string Serial { get; set; }
        public string Model { get; set; }
        public string Manufacturer { get; set; }
        /// <summary>GigE / USB / 虚拟USB 等，方便对照 MVS Client。</summary>
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
