using System;

namespace Brick2Life.Vision
{
    // Top-left origin, row y * width + x, tightly packed RGB. Matches the capture pixel convention.
    public sealed class RgbImage
    {
        private readonly byte[] rgb;
        public int Width { get; }
        public int Height { get; }

        public RgbImage(int width, int height, byte[] rgb)
        {
            if (width < 1 || height < 1 || rgb == null || rgb.Length != width * (long)height * 3)
                throw new ArgumentException("RGB image must be tightly packed.");
            Width = width; Height = height; this.rgb = (byte[])rgb.Clone();
        }

        public bool TryRead(double pixelX, double pixelY, out byte r, out byte g, out byte b)
        {
            int x = (int)Math.Floor(pixelX + 0.5), y = (int)Math.Floor(pixelY + 0.5);
            if (x < 0 || y < 0 || x >= Width || y >= Height) { r = g = b = 0; return false; }
            int index = (y * Width + x) * 3;
            r = rgb[index]; g = rgb[index + 1]; b = rgb[index + 2];
            return true;
        }

        public void SetPixel(int x, int y, byte r, byte g, byte b)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return;
            int index = (y * Width + x) * 3;
            rgb[index] = r; rgb[index + 1] = g; rgb[index + 2] = b;
        }
    }
}
