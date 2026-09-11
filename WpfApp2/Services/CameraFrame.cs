namespace WpfApp2.Services
{
    /// <summary>
    /// 一帧已转成 BGR24 的像素。来源是海康 ConvertPixelType(PixelType_Gvsp_BGR8_Packed)，
    /// 布局与 WPF PixelFormats.Bgr24、Halcon gen_image_interleaved("bgr") 一致。
    /// 不持有 SDK 的 CFrameout：GetImageBuffer 的缓冲在拷贝后立刻 FreeImageBuffer。
    /// </summary>
    public sealed class CameraFrame
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public byte[] Bgr24 { get; set; }
    }
}
