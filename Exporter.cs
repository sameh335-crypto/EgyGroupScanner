using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace EgyGroupScanner
{
    public static class Exporter
    {
        static int DpiOf(Bitmap b)
        {
            int d = (int)Math.Round(b.HorizontalResolution);
            return d > 0 ? d : 300;
        }

        static ImageCodecInfo Codec(ImageFormat f)
        {
            return ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == f.Guid);
        }

        static void SaveJpeg(Bitmap b, Stream s, int quality)
        {
            using (var p = new EncoderParameters(1))
            {
                p.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)quality);
                b.Save(s, Codec(ImageFormat.Jpeg), p);
            }
        }

        // Multi-page PDF. Colour/gray pages are stored as JPEG, B/W pages as PNG.
        public static void SavePdf(IList<Bitmap> pages, string path, int jpegQuality)
        {
            var streams = new List<MemoryStream>();
            try
            {
                using (var doc = new PdfDocument())
                {
                    foreach (var bmp in pages)
                    {
                        var ms = new MemoryStream();
                        streams.Add(ms);
                        if (IsBlackWhite(bmp)) bmp.Save(ms, ImageFormat.Png);
                        else SaveJpeg(bmp, ms, jpegQuality);
                        ms.Position = 0;

                        double dpi = DpiOf(bmp);
                        var page = doc.AddPage();
                        page.Width = XUnit.FromInch(bmp.Width / dpi);
                        page.Height = XUnit.FromInch(bmp.Height / dpi);
                        using (var gfx = XGraphics.FromPdfPage(page))
                        using (var img = XImage.FromStream(ms))
                            gfx.DrawImage(img, 0, 0, page.Width.Point, page.Height.Point);
                    }
                    doc.Save(path);
                }
            }
            finally { foreach (var s in streams) s.Dispose(); }
        }

        // JPG / PNG: one file per page (name_1, name_2 ...) when there is more than one.
        public static string SaveImages(IList<Bitmap> pages, string path, ImageFormat fmt, int jpegQuality)
        {
            string dir = Path.GetDirectoryName(path);
            string name = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);
            string first = null;
            for (int i = 0; i < pages.Count; i++)
            {
                string p = pages.Count == 1 ? path : Path.Combine(dir, name + "_" + (i + 1) + ext);
                if (first == null) first = p;
                if (fmt.Guid == ImageFormat.Jpeg.Guid)
                    using (var fs = new FileStream(p, FileMode.Create)) SaveJpeg(pages[i], fs, jpegQuality);
                else
                    pages[i].Save(p, fmt);
            }
            return first;
        }

        public static string SaveTiff(IList<Bitmap> pages, string path)
        {
            if (pages.Count == 1) { pages[0].Save(path, ImageFormat.Tiff); return path; }
            var codec = Codec(ImageFormat.Tiff);
            var enc = System.Drawing.Imaging.Encoder.SaveFlag;
            using (var ep = new EncoderParameters(1))
            {
                ep.Param[0] = new EncoderParameter(enc, (long)EncoderValue.MultiFrame);
                pages[0].Save(path, codec, ep);
                for (int i = 1; i < pages.Count; i++)
                {
                    ep.Param[0] = new EncoderParameter(enc, (long)EncoderValue.FrameDimensionPage);
                    pages[0].SaveAdd(pages[i], ep);
                }
                ep.Param[0] = new EncoderParameter(enc, (long)EncoderValue.Flush);
                pages[0].SaveAdd(ep);
            }
            return path;
        }

        // Cheap check on a few pixels: pure black/white pages go to PNG (smaller + lossless).
        static bool IsBlackWhite(Bitmap b)
        {
            int stepX = Math.Max(1, b.Width / 40), stepY = Math.Max(1, b.Height / 40);
            for (int y = 0; y < b.Height; y += stepY)
                for (int x = 0; x < b.Width; x += stepX)
                {
                    var c = b.GetPixel(x, y);
                    if (!(c.R == c.G && c.G == c.B && (c.R == 0 || c.R == 255))) return false;
                }
            return true;
        }
    }
}
