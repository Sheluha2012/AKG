using System;
using System.Numerics;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WpfApp1
{
    public class Texture
    {
        private readonly int[] _pixels;
        public int Width { get; }
        public int Height { get; }

        public Texture(string filePath)
        {
            var bitmap = new BitmapImage(new Uri(filePath, UriKind.RelativeOrAbsolute));
            var cb = new FormatConvertedBitmap(bitmap, PixelFormats.Bgr32, null, 0);
            Width = cb.PixelWidth;
            Height = cb.PixelHeight;
            _pixels = new int[Width * Height];
            cb.CopyPixels(_pixels, Width * 4, 0);
        }

        public Vector3 Sample(float u, float v)
        {
            int x = (int)((u % 1.0f + 1.0f) % 1.0f * (Width - 1));
            int y = (int)((1.0f - (v % 1.0f + 1.0f) % 1.0f) * (Height - 1));

            int color = _pixels[Math.Clamp(y * Width + x, 0, _pixels.Length - 1)];
            float r = ((color >> 16) & 0xFF) / 255f;
            float g = ((color >> 8) & 0xFF) / 255f;
            float b = (color & 0xFF) / 255f;

            return new Vector3(r, g, b);
        }
    }
}