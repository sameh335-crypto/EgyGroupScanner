using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using NTwain;
using NTwain.Data;

namespace EgyGroupScanner
{
    // TWAIN: uses the official Ricoh TWAIN driver (ADF, duplex, full capabilities).
    // Written against NTwain 3.7.x. All TWAIN code is isolated in this file.
    public class TwainBackend : IScanBackend
    {
        readonly IntPtr _hwnd;
        TwainSession _session;
        Action<Bitmap> _onPage;
        Action _onDone;
        Action<string> _onError;
        bool _finished;

        public TwainBackend(IntPtr hwnd) { _hwnd = hwnd; }

        void EnsureSession()
        {
            if (_session != null) return;

            // 32-bit app => legacy twain_32.dll, works on Windows 7/10/11 (32 and 64 bit)
            PlatformInfo.Current.PreferNewDSM = false;

            var appId = TWIdentity.CreateFromAssembly(DataGroups.Image, Assembly.GetEntryAssembly());
            var ses = new TwainSession(appId);
            ses.DataTransferred += OnDataTransferred;
            ses.TransferError += (s, e) => Fail("حصل خطأ أثناء نقل الصورة من الجهاز");
            ses.SourceDisabled += (s, e) => Finish();

            if (ses.Open() != ReturnCode.Success)
                throw new InvalidOperationException("تعذّر تشغيل TWAIN. تأكد من تثبيت تعريف السكانر.");
            _session = ses;
        }

        public List<ScannerInfo> ListScanners()
        {
            EnsureSession();
            return _session.GetSources()
                .Select(d => new ScannerInfo { Name = d.Name, Id = d.Name, Kind = BackendKind.Twain })
                .ToList();
        }

        public void Scan(ScannerInfo scanner, ScanSettings s,
                         Action<Bitmap> onPage, Action onDone, Action<string> onError)
        {
            _onPage = onPage; _onDone = onDone; _onError = onError; _finished = false;
            try
            {
                EnsureSession();
                var ds = _session.GetSources().FirstOrDefault(d => d.Name == scanner.Id);
                if (ds == null) throw new InvalidOperationException("الجهاز غير موجود. اضغط تحديث.");
                if (ds.Open() != ReturnCode.Success) throw new InvalidOperationException("تعذّر فتح الجهاز.");

                Apply(ds, s);

                var mode = s.ShowDriverUi ? SourceEnableMode.ShowUI : SourceEnableMode.NoUI;
                if (ds.Enable(mode, s.ShowDriverUi, _hwnd) != ReturnCode.Success)
                    throw new InvalidOperationException("الجهاز رفض بدء المسح.");
            }
            catch (Exception ex) { Fail(ex.Message); }
        }

        static void Try(Action a) { try { a(); } catch { /* capability not supported by this model */ } }

        static void Apply(DataSource ds, ScanSettings s)
        {
            var c = ds.Capabilities;
            // B/W is scanned as gray; the threshold is applied in software for identical results on all models
            Try(() => c.ICapPixelType.SetValue(s.Mode == ColorMode.Color ? PixelType.RGB : PixelType.Gray));
            Try(() => c.ICapXResolution.SetValue((float)s.Dpi));
            Try(() => c.ICapYResolution.SetValue((float)s.Dpi));

            bool feeder = s.Source != ScanSource.Glass;
            Try(() => c.CapFeederEnabled.SetValue(feeder ? BoolType.True : BoolType.False));
            Try(() => c.CapDuplexEnabled.SetValue(s.Source == ScanSource.Duplex ? BoolType.True : BoolType.False));

            SupportedSize size;
            if (TryPaper(s.Paper, out size))
                Try(() => c.ICapSupportedSizes.SetValue(size));
        }

        static bool TryPaper(PaperSize p, out SupportedSize size)
        {
            string[] names;
            switch (p)
            {
                case PaperSize.A3: names = new[] { "A3", "ISOA3" }; break;
                case PaperSize.A5: names = new[] { "A5" }; break;
                case PaperSize.Legal: names = new[] { "USLegal" }; break;
                case PaperSize.Letter: names = new[] { "USLetter" }; break;
                default: names = new[] { "A4", "A4Letter" }; break;
            }
            foreach (var n in names)
                if (Enum.TryParse<SupportedSize>(n, out size)) return true;
            size = default(SupportedSize);
            return false;
        }

        void OnDataTransferred(object sender, DataTransferredEventArgs e)
        {
            try
            {
                using (var st = e.GetNativeImageStream())
                {
                    if (st == null) return;
                    using (var tmp = new Bitmap(st))
                        _onPage?.Invoke(new Bitmap(tmp));
                }
            }
            catch (Exception ex) { Fail(ex.Message); }
        }

        void Finish()
        {
            if (_finished) return;
            _finished = true;
            try { _session.CurrentSource?.Close(); } catch { }
            _onDone?.Invoke();
        }

        void Fail(string msg)
        {
            if (_finished) return;
            _finished = true;
            try { _session?.CurrentSource?.Close(); } catch { }
            _onError?.Invoke(msg);
        }

        public void Dispose()
        {
            try { _session?.Close(); } catch { }
        }
    }
}
