using System;
using System.Collections.Generic;
using System.Drawing;

namespace EgyGroupScanner
{
    public enum ColorMode { Color, Gray, BlackWhite }
    public enum ScanSource { Glass, Adf, Duplex }
    public enum PaperSize { A4, A3, A5, Legal, Letter }
    public enum BackendKind { Twain, Wia }

    public class ScannerInfo
    {
        public string Name;
        public string Id;
        public BackendKind Kind;
        public override string ToString()
        {
            return Name + (Kind == BackendKind.Twain ? "   [TWAIN]" : "   [WIA]");
        }
    }

    public class ScanSettings
    {
        public ColorMode Mode = ColorMode.Color;
        public int Dpi = 300;
        public ScanSource Source = ScanSource.Glass;
        public PaperSize Paper = PaperSize.A4;
        public int Brightness;          // -100..100 (software)
        public int Contrast;            // -100..100 (software)
        public int Threshold = 128;     // 0..255 (software, black/white only)
        public bool ShowDriverUi;       // open the manufacturer's driver window

        public SizeF PaperInches()
        {
            switch (Paper)
            {
                case PaperSize.A3: return new SizeF(11.69f, 16.54f);
                case PaperSize.A5: return new SizeF(5.83f, 8.27f);
                case PaperSize.Legal: return new SizeF(8.5f, 14f);
                case PaperSize.Letter: return new SizeF(8.5f, 11f);
                default: return new SizeF(8.27f, 11.69f);
            }
        }
    }

    public interface IScanBackend : IDisposable
    {
        List<ScannerInfo> ListScanners();
        // Callbacks may arrive on any thread; the UI marshals them itself.
        void Scan(ScannerInfo scanner, ScanSettings settings,
                  Action<Bitmap> onPage, Action onDone, Action<string> onError);
    }
}
