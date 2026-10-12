using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace EgyGroupScanner
{
    // WIA: built into Windows (no NuGet package, late-bound COM). Used for models
    // whose driver exposes WIA instead of / in addition to TWAIN.
    public class WiaBackend : IScanBackend
    {
        const string FmtBmp = "{B96B3CAB-0728-11D3-9D7B-0000F81EF32E}";
        const uint PaperEmpty = 0x80210003;

        static dynamic NewManager()
        {
            var t = Type.GetTypeFromProgID("WIA.DeviceManager");
            if (t == null) throw new InvalidOperationException("خدمة WIA غير متاحة على هذا الجهاز.");
            return Activator.CreateInstance(t);
        }

        public List<ScannerInfo> ListScanners()
        {
            var res = new List<ScannerInfo>();
            dynamic mgr = NewManager();
            int n = mgr.DeviceInfos.Count;
            for (int i = 1; i <= n; i++)
            {
                dynamic info = mgr.DeviceInfos[i];
                if ((int)info.Type != 1) continue; // 1 = scanner
                res.Add(new ScannerInfo
                {
                    Name = (string)info.Properties["Name"].Value,
                    Id = (string)info.DeviceID,
                    Kind = BackendKind.Wia
                });
            }
            return res;
        }

        static void TrySet(dynamic props, int id, int value)
        {
            try { props[id].Value = value; } catch { }
        }

        static int MaxOf(dynamic props, int id, int fallback)
        {
            try { return (int)props[id].SubTypeMax; } catch { return fallback; }
        }

        public void Scan(ScannerInfo sc, ScanSettings s,
                         Action<Bitmap> onPage, Action onDone, Action<string> onError)
        {
            var th = new Thread(() =>
            {
                try
                {
                    dynamic mgr = NewManager();
                    dynamic dev = null;
                    int n = mgr.DeviceInfos.Count;
                    for (int i = 1; i <= n; i++)
                    {
                        dynamic info = mgr.DeviceInfos[i];
                        if ((string)info.DeviceID == sc.Id) { dev = info.Connect(); break; }
                    }
                    if (dev == null) throw new InvalidOperationException("الجهاز غير متصل.");

                    bool feeder = s.Source != ScanSource.Glass;
                    // Document handling: 1 = feeder, 2 = flatbed, 5 = feeder + duplex
                    TrySet(dev.Properties, 3088, s.Source == ScanSource.Glass ? 2 : s.Source == ScanSource.Adf ? 1 : 5);
                    if (feeder) TrySet(dev.Properties, 3096, 0); // all pages

                    dynamic item = dev.Items[1];
                    dynamic p = item.Properties;
                    TrySet(p, 6146, s.Mode == ColorMode.Color ? 1 : 2); // intent: color / gray (B/W via software)
                    TrySet(p, 6147, s.Dpi);
                    TrySet(p, 6148, s.Dpi);

                    var inch = s.PaperInches();
                    int wpx = Math.Min((int)(inch.Width * s.Dpi), MaxOf(p, 6151, int.MaxValue));
                    int hpx = Math.Min((int)(inch.Height * s.Dpi), MaxOf(p, 6152, int.MaxValue));
                    TrySet(p, 6149, 0);
                    TrySet(p, 6150, 0);
                    TrySet(p, 6151, wpx);
                    TrySet(p, 6152, hpx);

                    int count = 0;
                    while (true)
                    {
                        dynamic img;
                        try { img = item.Transfer(FmtBmp); }
                        catch (COMException ex) when ((uint)ex.ErrorCode == PaperEmpty) { break; }

                        string tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".bmp");
                        img.SaveFile(tmp);
                        Bitmap bmp;
                        using (var fs = new FileStream(tmp, FileMode.Open, FileAccess.Read))
                        using (var t = new Bitmap(fs))
                            bmp = new Bitmap(t);
                        File.Delete(tmp);

                        count++;
                        onPage(bmp);
                        if (!feeder) break;
                    }

                    if (count == 0) throw new InvalidOperationException("مفيش ورق في الـ ADF أو الجهاز مش جاهز.");
                    onDone();
                }
                catch (Exception ex) { onError(ex.Message); }
            });
            th.SetApartmentState(ApartmentState.STA);
            th.IsBackground = true;
            th.Start();
        }

        public void Dispose() { }
    }
}
