using System;

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

        /// <summary>像素深拷贝。预览继续持有原帧，Halcon 只处理这份拷贝。</summary>
        public CameraFrame Clone()
        {
            byte[] copy = null;
            if (Bgr24 != null)
            {
                copy = new byte[Bgr24.Length];
                Buffer.BlockCopy(Bgr24, 0, copy, 0, Bgr24.Length);
            }

            return new CameraFrame
            {
                Width = Width,
                Height = Height,
                Bgr24 = copy
            };
        }
    }
}
