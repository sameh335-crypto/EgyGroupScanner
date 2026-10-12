using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace EgyGroupScanner
{
    public class MainForm : Form
    {
        static readonly Color Brand = Color.FromArgb(0, 114, 188);

        readonly Dictionary<BackendKind, IScanBackend> _backends = new Dictionary<BackendKind, IScanBackend>();
        readonly List<Bitmap> _pages = new List<Bitmap>();
        string _lastDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        ComboBox cmbScanner, cmbMode, cmbDpi, cmbSource, cmbPaper;
        TrackBar tbBright, tbContrast, tbThreshold;
        Label lblBright, lblContrast, lblThreshold, lblStatus;
        CheckBox chkUi;
        Button btnScan, btnRefresh;
        ListView lvPages;
        ImageList thumbs;
        PictureBox preview;
        NumericUpDown numJpeg;

        public MainForm()
        {
            Text = "ايجي جروب اسكانر - Egy Group Scanner (" + (Environment.Is64BitProcess ? "64-bit" : "32-bit") + ")";
            Font = new Font("Tahoma", 9.5f);
            RightToLeft = RightToLeft.Yes;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1120, 740);
            MinimumSize = new Size(940, 620);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            BuildUi();
            Load += (s, e) => RefreshScanners();
            FormClosing += (s, e) => Cleanup();
        }

        // ---------- UI ----------
        void BuildUi()
        {
            // center: preview
            preview = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(60, 64, 70) };

            // thumbnails
            thumbs = new ImageList { ImageSize = new Size(96, 128), ColorDepth = ColorDepth.Depth32Bit };
            lvPages = new ListView { Dock = DockStyle.Left, Width = 140, View = View.LargeIcon, LargeImageList = thumbs, MultiSelect = false, HideSelection = false };
            lvPages.SelectedIndexChanged += (s, e) => ShowPreview();

            // settings panel
            var side = new FlowLayoutPanel
            {
                Dock = DockStyle.Right, Width = 320, FlowDirection = FlowDirection.TopDown,
                WrapContents = false, AutoScroll = true, Padding = new Padding(12, 6, 12, 6),
                BackColor = Color.FromArgb(245, 247, 250)
            };
            cmbScanner = Combo();
            btnRefresh = Btn("تحديث قائمة الأجهزة", 285, 30);
            btnRefresh.Click += (s, e) => RefreshScanners();
            cmbMode = Combo("ألوان", "رمادي", "أبيض وأسود");
            cmbDpi = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Width = 285, Text = "300" };
            cmbDpi.Items.AddRange(new object[] { "100", "150", "200", "300", "400", "600" });
            cmbSource = Combo("زجاج (Flatbed)", "تغذية تلقائية ADF", "ADF وجهين (Duplex)");
            cmbPaper = Combo("A4", "A3", "A5", "Legal", "Letter");

            lblBright = Cap("السطوع: 0");
            tbBright = Track(lblBright, "السطوع", -100, 100, 0);
            lblContrast = Cap("التباين: 0");
            tbContrast = Track(lblContrast, "التباين", -100, 100, 0);
            lblThreshold = Cap("حساسية الأبيض/الأسود: 128");
            tbThreshold = Track(lblThreshold, "حساسية الأبيض/الأسود", 0, 255, 128);

            chkUi = new CheckBox { Text = "إعدادات متقدمة (شاشة تعريف الجهاز)", AutoSize = true, Margin = new Padding(3, 10, 3, 3) };
            btnScan = new Button
            {
                Text = "ابدأ المسح", Width = 285, Height = 46, FlatStyle = FlatStyle.Flat,
                BackColor = Brand, ForeColor = Color.White, Margin = new Padding(3, 14, 3, 3),
                Font = new Font("Tahoma", 12f, FontStyle.Bold), Cursor = Cursors.Hand
            };
            btnScan.FlatAppearance.BorderSize = 0;
            btnScan.Click += (s, e) => DoScan();

            side.Controls.AddRange(new Control[]
            {
                Cap("الجهاز"), cmbScanner, btnRefresh,
                Cap("نمط اللون"), cmbMode,
                Cap("الجودة (DPI)"), cmbDpi,
                Cap("مصدر الورق"), cmbSource,
                Cap("حجم الورق"), cmbPaper,
                lblBright, tbBright, lblContrast, tbContrast, lblThreshold, tbThreshold,
                chkUi, btnScan
            });

            // bottom buttons
            var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 52, Padding = new Padding(8, 8, 8, 4), BackColor = Color.White };
            Button bPdf = Btn("حفظ PDF", 100, 34), bJpg = Btn("حفظ JPG", 100, 34), bPng = Btn("حفظ PNG", 100, 34), bTif = Btn("حفظ TIFF", 100, 34);
            Button bDel = Btn("حذف الصفحة", 100, 34), bClr = Btn("مسح الكل", 100, 34);
            bPdf.BackColor = Brand; bPdf.ForeColor = Color.White; bPdf.FlatStyle = FlatStyle.Flat; bPdf.FlatAppearance.BorderSize = 0;
            bPdf.Click += (s, e) => SaveAs("pdf");
            bJpg.Click += (s, e) => SaveAs("jpg");
            bPng.Click += (s, e) => SaveAs("png");
            bTif.Click += (s, e) => SaveAs("tiff");
            bDel.Click += (s, e) => DeleteSelected();
            bClr.Click += (s, e) => ClearAll();
            numJpeg = new NumericUpDown { Minimum = 10, Maximum = 100, Value = 85, Width = 55, Margin = new Padding(3, 8, 3, 3) };
            var lblQ = new Label { Text = "جودة JPG:", AutoSize = true, Margin = new Padding(12, 11, 3, 3) };
            bottom.Controls.AddRange(new Control[] { bPdf, bJpg, bPng, bTif, bDel, bClr, lblQ, numJpeg });

            lblStatus = new Label { Dock = DockStyle.Bottom, Height = 26, TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(8, 0, 8, 0), Text = "جاهز" };

            // header
            var line = new Panel { Dock = DockStyle.Top, Height = 3, BackColor = Brand };
            var header = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = Color.White };
            header.Controls.Add(new PictureBox { Dock = DockStyle.Right, Width = 150, SizeMode = PictureBoxSizeMode.Zoom, Image = LoadLogo() });

            // order matters for docking: Fill first, outer edges last
            Controls.Add(preview);
            Controls.Add(lvPages);
            Controls.Add(side);
            Controls.Add(bottom);
            Controls.Add(lblStatus);
            Controls.Add(line);
            Controls.Add(header);
        }

        ComboBox Combo(params string[] items)
        {
            var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 285 };
            c.Items.AddRange(items);
            if (items.Length > 0) c.SelectedIndex = 0;
            return c;
        }

        Label Cap(string text)
        {
            return new Label { Text = text, AutoSize = true, Margin = new Padding(3, 10, 3, 2), Font = new Font(Font, FontStyle.Bold) };
        }

        Button Btn(string text, int w, int h)
        {
            return new Button { Text = text, Width = w, Height = h, Margin = new Padding(3, 0, 3, 0) };
        }

        TrackBar Track(Label lbl, string name, int min, int max, int val)
        {
            var t = new TrackBar { Minimum = min, Maximum = max, Value = val, TickFrequency = (max - min) / 8, Width = 285, Height = 40 };
            t.Scroll += (s, e) => lbl.Text = name + ": " + t.Value;
            return t;
        }

        static Image LoadLogo()
        {
            using (var st = Assembly.GetExecutingAssembly().GetManifestResourceStream("EgyGroupScanner.logo.png"))
            {
                if (st == null) return null;
                using (var tmp = Image.FromStream(st)) return new Bitmap(tmp);
            }
        }

        // ---------- scanners ----------
        IScanBackend GetBackend(BackendKind k)
        {
            IScanBackend b;
            if (_backends.TryGetValue(k, out b)) return b;
            b = k == BackendKind.Twain ? (IScanBackend)new TwainBackend(Handle) : new WiaBackend();
            _backends[k] = b;
            return b;
        }

        void RefreshScanners()
        {
            cmbScanner.Items.Clear();
            var errors = new List<string>();
            foreach (BackendKind k in Enum.GetValues(typeof(BackendKind)))
            {
                try { foreach (var sc in GetBackend(k).ListScanners()) cmbScanner.Items.Add(sc); }
                catch (Exception ex) { errors.Add(k + ": " + ex.Message); }
            }
            if (cmbScanner.Items.Count > 0)
            {
                cmbScanner.SelectedIndex = 0;
                lblStatus.Text = "تم العثور على " + cmbScanner.Items.Count + " جهاز";
            }
            else
            {
                lblStatus.Text = "مفيش سكانر. وصّل الماكينة وشغّلها ثم اضغط تحديث."
                    + (errors.Count > 0 ? "  [" + string.Join(" | ", errors) + "]" : "");
            }
        }

        ScanSettings ReadSettings()
        {
            int dpi;
            if (!int.TryParse(cmbDpi.Text, out dpi)) dpi = 300;
            dpi = Math.Max(50, Math.Min(1200, dpi));
            return new ScanSettings
            {
                Mode = (ColorMode)cmbMode.SelectedIndex,
                Dpi = dpi,
                Source = (ScanSource)cmbSource.SelectedIndex,
                Paper = (PaperSize)cmbPaper.SelectedIndex,
                Brightness = tbBright.Value,
                Contrast = tbContrast.Value,
                Threshold = tbThreshold.Value,
                ShowDriverUi = chkUi.Checked
            };
        }

        // ---------- scanning ----------
        void OnUi(Action a)
        {
            if (IsDisposed) return;
            if (InvokeRequired) BeginInvoke(a); else a();
        }

        void SetBusy(bool busy, string msg)
        {
            btnScan.Enabled = !busy;
            btnRefresh.Enabled = !busy;
            lblStatus.Text = msg;
        }

        void DoScan()
        {
            var sc = cmbScanner.SelectedItem as ScannerInfo;
            if (sc == null) { Msg("اختار الجهاز الأول (أو اضغط تحديث).", MessageBoxIcon.Information); return; }

            var s = ReadSettings();
            SetBusy(true, "جاري المسح...");
            try
            {
                GetBackend(sc.Kind).Scan(sc, s,
                    bmp => OnUi(() => AddPage(bmp, s)),
                    () => OnUi(() => SetBusy(false, "تم المسح. عدد الصفحات: " + _pages.Count)),
                    err => OnUi(() => { SetBusy(false, "فشل المسح"); Msg(err, MessageBoxIcon.Warning); }));
            }
            catch (Exception ex) { SetBusy(false, "فشل المسح"); Msg(ex.Message, MessageBoxIcon.Warning); }
        }

        void AddPage(Bitmap raw, ScanSettings s)
        {
            Bitmap processed;
            try { processed = ImageProcessor.Process(raw, s); }
            finally { raw.Dispose(); }
            _pages.Add(processed);
            RebuildList(_pages.Count - 1);
        }

        // ---------- pages list ----------
        Bitmap Thumb(Bitmap src)
        {
            var t = new Bitmap(96, 128);
            using (var g = Graphics.FromImage(t))
            {
                g.Clear(Color.White);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                float r = Math.Min(96f / src.Width, 128f / src.Height);
                int w = Math.Max(1, (int)(src.Width * r)), h = Math.Max(1, (int)(src.Height * r));
                g.DrawImage(src, (96 - w) / 2, (128 - h) / 2, w, h);
            }
            return t;
        }

        void RebuildList(int select)
        {
            lvPages.BeginUpdate();
            lvPages.Items.Clear();
            foreach (Image im in thumbs.Images) im.Dispose();
            thumbs.Images.Clear();
            for (int i = 0; i < _pages.Count; i++)
            {
                thumbs.Images.Add(Thumb(_pages[i]));
                lvPages.Items.Add(new ListViewItem("صفحة " + (i + 1), i));
            }
            lvPages.EndUpdate();
            if (_pages.Count == 0) { preview.Image = null; return; }
            select = Math.Max(0, Math.Min(select, _pages.Count - 1));
            lvPages.Items[select].Selected = true;
            lvPages.Items[select].EnsureVisible();
        }

        void ShowPreview()
        {
            if (lvPages.SelectedIndices.Count == 0) return;
            preview.Image = _pages[lvPages.SelectedIndices[0]];
        }

        void DeleteSelected()
        {
            if (lvPages.SelectedIndices.Count == 0) return;
            int i = lvPages.SelectedIndices[0];
            preview.Image = null;
            _pages[i].Dispose();
            _pages.RemoveAt(i);
            RebuildList(i);
            lblStatus.Text = "عدد الصفحات: " + _pages.Count;
        }

        void ClearAll()
        {
            if (_pages.Count == 0) return;
            if (MessageBox.Show(this, "مسح كل الصفحات؟", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign) != DialogResult.Yes) return;
            preview.Image = null;
            foreach (var p in _pages) p.Dispose();
            _pages.Clear();
            RebuildList(0);
            lblStatus.Text = "جاهز";
        }

        // ---------- saving ----------
        void SaveAs(string kind)
        {
            if (_pages.Count == 0) { Msg("مفيش صفحات للحفظ. اعمل مسح الأول.", MessageBoxIcon.Information); return; }

            string filter = kind == "pdf" ? "PDF|*.pdf" : kind == "jpg" ? "JPEG|*.jpg" : kind == "png" ? "PNG|*.png" : "TIFF|*.tif";
            using (var dlg = new SaveFileDialog
            {
                Filter = filter,
                DefaultExt = kind == "tiff" ? "tif" : kind,
                FileName = "Scan_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"),
                InitialDirectory = _lastDir,
                OverwritePrompt = true
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                _lastDir = Path.GetDirectoryName(dlg.FileName);
                try
                {
                    UseWaitCursor = true;
                    int q = (int)numJpeg.Value;
                    string first;
                    switch (kind)
                    {
                        case "pdf": Exporter.SavePdf(_pages, dlg.FileName, q); first = dlg.FileName; break;
                        case "jpg": first = Exporter.SaveImages(_pages, dlg.FileName, ImageFormat.Jpeg, q); break;
                        case "png": first = Exporter.SaveImages(_pages, dlg.FileName, ImageFormat.Png, q); break;
                        default: first = Exporter.SaveTiff(_pages, dlg.FileName); break;
                    }
                    lblStatus.Text = "تم الحفظ: " + first;
                    Process.Start("explorer.exe", "/select,\"" + first + "\"");
                }
                catch (Exception ex) { Msg("تعذّر الحفظ:\n" + ex.Message, MessageBoxIcon.Error); }
                finally { UseWaitCursor = false; }
            }
        }

        void Msg(string text, MessageBoxIcon icon)
        {
            MessageBox.Show(this, text, Text, MessageBoxButtons.OK, icon, MessageBoxDefaultButton.Button1,
                MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
        }

        void Cleanup()
        {
            preview.Image = null;
            foreach (var b in _backends.Values) b.Dispose();
            foreach (var p in _pages) p.Dispose();
        }
    }
}
