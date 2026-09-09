namespace WpfApp2.Services
{
    /// <summary>
    /// 一帧已转成 BGR24 的像素，给界面做成 BitmapSource。不持有 SDK 缓冲。
    /// </summary>
    public sealed class CameraFrame
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public byte[] Bgr24 { get; set; }
    }
}
