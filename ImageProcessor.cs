using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace EgyGroupScanner
{
    // Same result on every scanner model: colour mode, brightness, contrast
    // and black/white threshold are applied here after the scan.
    public static class ImageProcessor
    {
        public static Bitmap Process(Bitmap src, ScanSettings s)
        {
            int w = src.Width, h = src.Height;
            var dst = new Bitmap(w, h, PixelFormat.Format24bppRgb);
            dst.SetResolution(s.Dpi, s.Dpi);

            float c = 1f + s.Contrast / 100f;
            float b = s.Brightness / 100f;
            float t = (1f - c) / 2f + b;
            bool gray = s.Mode != ColorMode.Color;

            float[][] m;
            if (gray)
            {
                float r = 0.299f * c, g = 0.587f * c, bl = 0.114f * c;
                m = new[]
                {
                    new[] { r,  r,  r,  0f, 0f },
                    new[] { g,  g,  g,  0f, 0f },
                    new[] { bl, bl, bl, 0f, 0f },
                    new[] { 0f, 0f, 0f, 1f, 0f },
                    new[] { t,  t,  t,  0f, 1f }
                };
            }
            else
            {
                m = new[]
                {
                    new[] { c,  0f, 0f, 0f, 0f },
                    new[] { 0f, c,  0f, 0f, 0f },
                    new[] { 0f, 0f, c,  0f, 0f },
                    new[] { 0f, 0f, 0f, 1f, 0f },
                    new[] { t,  t,  t,  0f, 1f }
                };
            }

            using (var ia = new ImageAttributes())
            using (var g = Graphics.FromImage(dst))
            {
                ia.SetColorMatrix(new ColorMatrix(m));
                g.DrawImage(src, new Rectangle(0, 0, w, h), 0, 0, w, h, GraphicsUnit.Pixel, ia);
            }

            if (s.Mode == ColorMode.BlackWhite)
                Threshold(dst, s.Threshold);

            return dst;
        }

        static void Threshold(Bitmap bmp, int th)
        {
            var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            var data = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
            try
            {
                int stride = data.Stride;
                var row = new byte[Math.Abs(stride)];
                for (int y = 0; y < bmp.Height; y++)
                {
                    IntPtr p = IntPtr.Add(data.Scan0, y * stride);
                    Marshal.Copy(p, row, 0, row.Length);
                    for (int x = 0; x < bmp.Width; x++)
                    {
                        int i = x * 3;
                        byte v = row[i + 1] >= th ? (byte)255 : (byte)0;
                        row[i] = row[i + 1] = row[i + 2] = v;
                    }
                    Marshal.Copy(row, 0, p, row.Length);
                }
            }
            finally { bmp.UnlockBits(data); }
        }
    }
}
