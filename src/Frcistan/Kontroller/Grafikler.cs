using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Frcistan.Altyapi;
using Frcistan.Servisler;

namespace Frcistan.Kontroller
{
    public class GrafikSerisi
    {
        public string Ad { get; init; }
        public string Renk { get; init; }
        public double[] Degerler { get; init; } = Array.Empty<double>();
    }

    public class GrafikDilimi
    {
        public string Ad { get; init; }
        public double Deger { get; init; }
        public string Renk { get; init; }
        public string DegerMetni { get; init; }
    }

    /// <summary>Tema değişince yeniden çizilen, açılışta animasyonlu grafik temeli</summary>
    public abstract class GrafikTemeli : FrameworkElement
    {
        public static readonly DependencyProperty IlerlemeProperty = DependencyProperty.Register(
            nameof(Ilerleme), typeof(double), typeof(GrafikTemeli), new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public double Ilerleme
        {
            get => (double)GetValue(IlerlemeProperty);
            set => SetValue(IlerlemeProperty, value);
        }

        protected GrafikTemeli()
        {
            Loaded += (_, _) => Tema.Degisti += InvalidateVisual;
            Unloaded += (_, _) => Tema.Degisti -= InvalidateVisual;
            SnapsToDevicePixels = true;
        }

        protected void Canlandir()
        {
            var a = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(700)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            BeginAnimation(IlerlemeProperty, a);
        }

        protected static Typeface Yazi(FontWeight? kalinlik = null) =>
            new(new FontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal, kalinlik ?? FontWeights.Normal, FontStretches.Normal);

        protected FormattedText Metin(string s, double boyut, Brush firca, FontWeight? kalinlik = null) =>
            new(s ?? "", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Yazi(kalinlik), boyut, firca,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

        protected static Color RenkCoz(string renk)
        {
            if (string.IsNullOrEmpty(renk)) return Colors.SteelBlue;
            if (renk.StartsWith('#'))
            {
                try { return (Color)ColorConverter.ConvertFromString(renk); } catch { return Colors.SteelBlue; }
            }
            return Tema.Renk(renk);
        }

        protected static SolidColorBrush Firca(Color c, double opaklik = 1)
        {
            var b = new SolidColorBrush(Color.FromArgb((byte)(c.A * opaklik), c.R, c.G, c.B));
            b.Freeze();
            return b;
        }
    }

    /// <summary>Zaman serisi alan grafiği (ör. son 30 gün giriş / çıkış)</summary>
    public class AlanGrafik : GrafikTemeli
    {
        public static readonly DependencyProperty SerilerProperty = DependencyProperty.Register(
            nameof(Seriler), typeof(IList<GrafikSerisi>), typeof(AlanGrafik), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((AlanGrafik)d).Canlandir()));

        public static readonly DependencyProperty EtiketlerProperty = DependencyProperty.Register(
            nameof(Etiketler), typeof(IList<string>), typeof(AlanGrafik), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BirimProperty = DependencyProperty.Register(
            nameof(Birim), typeof(string), typeof(AlanGrafik), new FrameworkPropertyMetadata("adet", FrameworkPropertyMetadataOptions.AffectsRender));

        public IList<GrafikSerisi> Seriler { get => (IList<GrafikSerisi>)GetValue(SerilerProperty); set => SetValue(SerilerProperty, value); }
        public IList<string> Etiketler { get => (IList<string>)GetValue(EtiketlerProperty); set => SetValue(EtiketlerProperty, value); }
        public string Birim { get => (string)GetValue(BirimProperty); set => SetValue(BirimProperty, value); }

        int _imlec = -1;
        const double Sol = 40, Alt = 26, Ust = 12, Sag = 8;

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int n = Seriler?.FirstOrDefault()?.Degerler.Length ?? 0;
            if (n < 2) return;
            double x = e.GetPosition(this).X;
            double w = ActualWidth - Sol - Sag;
            int i = (int)Math.Round((x - Sol) / w * (n - 1));
            i = Math.Clamp(i, 0, n - 1);
            if (i != _imlec) { _imlec = i; InvalidateVisual(); }
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            _imlec = -1;
            InvalidateVisual();
        }

        /// <summary>Y ekseni üst sınırı: 4 eşit, yuvarlak aralık (1, 2, 5 × 10ⁿ)</summary>
        static double GuzelUst(double enBuyuk)
        {
            if (enBuyuk <= 0) return 4;
            double ham = enBuyuk / 4;
            double us = Math.Pow(10, Math.Floor(Math.Log10(ham)));
            double adim = new[] { 1, 2, 3, 4, 5, 6, 8, 10 }.Select(k => k * us).First(k => k >= ham);
            if (adim < 1) adim = 1;
            return adim * 4;
        }

        protected override void OnRender(DrawingContext dc)
        {
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
            var seriler = Seriler;
            if (seriler == null || seriler.Count == 0 || ActualWidth < 60 || ActualHeight < 60) return;
            int n = seriler.Max(s => s.Degerler.Length);
            if (n < 2) return;

            var izgara = Tema.Firca("Izgara");
            var metin3 = Tema.Firca("Metin3");
            double w = ActualWidth - Sol - Sag, h = ActualHeight - Ust - Alt;
            double ust = GuzelUst(seriler.SelectMany(s => s.Degerler).DefaultIfEmpty(0).Max() * 1.08);
            var cizgiKalem = new Pen(izgara, 1);
            cizgiKalem.Freeze();

            // Yatay ızgara ve y ekseni etiketleri
            for (int k = 0; k <= 4; k++)
            {
                double y = Math.Round(Ust + h - h * k / 4) + 0.5;
                dc.DrawLine(cizgiKalem, new Point(Sol, y), new Point(Sol + w, y));
                var t = Metin((ust * k / 4).ToString(ust >= 10 ? "N0" : "0.#", Ui.Tr), 11, metin3);
                dc.DrawText(t, new Point(Sol - 10 - t.Width, y - t.Height / 2));
            }

            // X ekseni etiketleri
            var etiketler = Etiketler;
            if (etiketler != null && etiketler.Count == n)
            {
                int adim = Math.Max(1, (int)Math.Ceiling(n / Math.Max(1, w / 70)));
                for (int i = 0; i < n; i += adim)
                {
                    var t = Metin(etiketler[i], 11, metin3);
                    double x = Sol + w * i / (n - 1) - t.Width / 2;
                    x = Math.Clamp(x, Sol - 4, Sol + w - t.Width + 4);
                    dc.DrawText(t, new Point(x, Ust + h + 8));
                }
            }

            double ilerleme = Ilerleme;
            Point Nokta(double[] d, int i) => new(Sol + w * i / (n - 1), Ust + h - h * (i < d.Length ? d[i] : 0) / ust * ilerleme);

            foreach (var s in seriler)
            {
                var renk = RenkCoz(s.Renk);
                var noktalar = Enumerable.Range(0, n).Select(i => Nokta(s.Degerler, i)).ToArray();
                var egri = Egri(noktalar);

                // Dolgu
                var dolgu = new StreamGeometry();
                using (var g = dolgu.Open())
                {
                    g.BeginFigure(new Point(noktalar[0].X, Ust + h), true, true);
                    g.LineTo(noktalar[0], false, false);
                    EgriCiz(g, noktalar, egri);
                    g.LineTo(new Point(noktalar[^1].X, Ust + h), false, false);
                }
                dolgu.Freeze();
                var gradyan = new LinearGradientBrush(Color.FromArgb(Tema.Koyu ? (byte)80 : (byte)60, renk.R, renk.G, renk.B),
                    Color.FromArgb(0, renk.R, renk.G, renk.B), 90);
                gradyan.Freeze();
                dc.DrawGeometry(gradyan, null, dolgu);

                // Çizgi
                var cizgi = new StreamGeometry();
                using (var g = cizgi.Open())
                {
                    g.BeginFigure(noktalar[0], false, false);
                    EgriCiz(g, noktalar, egri);
                }
                cizgi.Freeze();
                var kalem = new Pen(Firca(renk), 2.2) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                kalem.Freeze();
                dc.DrawGeometry(null, kalem, cizgi);
            }

            // Fare ile işaretlenen gün
            if (_imlec >= 0 && _imlec < n)
            {
                double x = Math.Round(Sol + w * _imlec / (n - 1)) + 0.5;
                var dikey = new Pen(Tema.Firca("Cizgi2"), 1) { DashStyle = new DashStyle(new double[] { 3, 3 }, 0) };
                dc.DrawLine(dikey, new Point(x, Ust), new Point(x, Ust + h));

                var yuzey = Tema.Firca("Yuzey");
                foreach (var s in seriler)
                {
                    var p = Nokta(s.Degerler, _imlec);
                    dc.DrawEllipse(yuzey, new Pen(Firca(RenkCoz(s.Renk)), 2.5), p, 4.5, 4.5);
                }

                // Bilgi kutusu
                var metin = Tema.Firca("Metin");
                var baslik = Metin(etiketler != null && _imlec < etiketler.Count ? etiketler[_imlec] : "", 11.5, Tema.Firca("Metin2"), FontWeights.SemiBold);
                var satirlar = seriler.Select(s => Metin($"{s.Ad}: {(s.Degerler.Length > _imlec ? s.Degerler[_imlec] : 0).ToString("N0", Ui.Tr)} {Birim}", 12, metin)).ToList();
                double kw = Math.Max(baslik.Width, satirlar.Max(t => t.Width) + 16) + 24;
                double kh = 14 + baslik.Height + satirlar.Sum(t => t.Height + 3) + 10;
                double kx = x + 14;
                if (kx + kw > ActualWidth) kx = x - 14 - kw;
                double ky = Ust + 4;
                var kutu = new Rect(kx, ky, kw, kh);
                dc.DrawRoundedRectangle(Tema.Firca("Kabarik"), new Pen(Tema.Firca("Cizgi2"), 1), kutu, 9, 9);
                dc.DrawText(baslik, new Point(kx + 12, ky + 9));
                double sy = ky + 12 + baslik.Height;
                for (int i = 0; i < satirlar.Count; i++)
                {
                    dc.DrawEllipse(Firca(RenkCoz(seriler[i].Renk)), null, new Point(kx + 16, sy + satirlar[i].Height / 2), 4, 4);
                    dc.DrawText(satirlar[i], new Point(kx + 28, sy));
                    sy += satirlar[i].Height + 3;
                }
            }
        }

        // Monoton kübik eğri (Fritsch–Carlson): değerler sıfırın altına taşmaz
        static (Point C1, Point C2)[] Egri(Point[] p)
        {
            int n = p.Length;
            var m = new double[n];
            var d = new double[n - 1];
            for (int i = 0; i < n - 1; i++)
            {
                double dx = p[i + 1].X - p[i].X;
                d[i] = dx == 0 ? 0 : (p[i + 1].Y - p[i].Y) / dx;
            }
            m[0] = d[0];
            m[n - 1] = d[n - 2];
            for (int i = 1; i < n - 1; i++)
                m[i] = d[i - 1] * d[i] <= 0 ? 0 : (d[i - 1] + d[i]) / 2;
            for (int i = 0; i < n - 1; i++)
            {
                if (d[i] == 0) { m[i] = 0; m[i + 1] = 0; continue; }
                double a = m[i] / d[i], b = m[i + 1] / d[i];
                double s = a * a + b * b;
                if (s > 9) { double t = 3 / Math.Sqrt(s); m[i] = t * a * d[i]; m[i + 1] = t * b * d[i]; }
            }
            var sonuc = new (Point, Point)[n - 1];
            for (int i = 0; i < n - 1; i++)
            {
                double dx = (p[i + 1].X - p[i].X) / 3;
                sonuc[i] = (new Point(p[i].X + dx, p[i].Y + m[i] * dx), new Point(p[i + 1].X - dx, p[i + 1].Y - m[i + 1] * dx));
            }
            return sonuc;
        }

        static void EgriCiz(StreamGeometryContext g, Point[] p, (Point C1, Point C2)[] e)
        {
            for (int i = 0; i < e.Length; i++)
                g.BezierTo(e[i].C1, e[i].C2, p[i + 1], true, true);
        }
    }

    /// <summary>Halka (donut) grafik: kategoriye göre stok değeri gibi dağılımlar</summary>
    public class HalkaGrafik : GrafikTemeli
    {
        public static readonly DependencyProperty DilimlerProperty = DependencyProperty.Register(
            nameof(Dilimler), typeof(IList<GrafikDilimi>), typeof(HalkaGrafik), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((HalkaGrafik)d).Canlandir()));

        public static readonly DependencyProperty MerkezBaslikProperty = DependencyProperty.Register(
            nameof(MerkezBaslik), typeof(string), typeof(HalkaGrafik), new FrameworkPropertyMetadata("Toplam", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty MerkezDegerProperty = DependencyProperty.Register(
            nameof(MerkezDeger), typeof(string), typeof(HalkaGrafik), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty KalinlikProperty = DependencyProperty.Register(
            nameof(Kalinlik), typeof(double), typeof(HalkaGrafik), new FrameworkPropertyMetadata(20.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public IList<GrafikDilimi> Dilimler { get => (IList<GrafikDilimi>)GetValue(DilimlerProperty); set => SetValue(DilimlerProperty, value); }
        public string MerkezBaslik { get => (string)GetValue(MerkezBaslikProperty); set => SetValue(MerkezBaslikProperty, value); }
        public string MerkezDeger { get => (string)GetValue(MerkezDegerProperty); set => SetValue(MerkezDegerProperty, value); }
        public double Kalinlik { get => (double)GetValue(KalinlikProperty); set => SetValue(KalinlikProperty, value); }

        int _secili = -1;

        (Point Merkez, double Yaricap) Olcu()
        {
            double r = Math.Min(ActualWidth, ActualHeight) / 2 - 6;
            return (new Point(ActualWidth / 2, ActualHeight / 2), r);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            var dilimler = Dilimler;
            if (dilimler == null || dilimler.Count == 0) return;
            var (m, r) = Olcu();
            var p = e.GetPosition(this);
            double dx = p.X - m.X, dy = p.Y - m.Y;
            double uzaklik = Math.Sqrt(dx * dx + dy * dy);
            int yeni = -1;
            if (uzaklik <= r + 4 && uzaklik >= r - Kalinlik - 6)
            {
                double aci = (Math.Atan2(dy, dx) * 180 / Math.PI + 90 + 360) % 360;
                double toplam = dilimler.Sum(x => x.Deger);
                double bas = 0;
                for (int i = 0; i < dilimler.Count; i++)
                {
                    double son = bas + dilimler[i].Deger / toplam * 360;
                    if (aci >= bas && aci < son) { yeni = i; break; }
                    bas = son;
                }
            }
            if (yeni != _secili) { _secili = yeni; InvalidateVisual(); }
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            _secili = -1;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
            var (m, r) = Olcu();
            if (r < 20) return;
            double kal = Kalinlik;
            double orta = r - kal / 2;
            var dilimler = Dilimler?.Where(d => d.Deger > 0).ToList() ?? new List<GrafikDilimi>();
            double toplam = dilimler.Sum(d => d.Deger);

            // Arka halka
            dc.DrawEllipse(null, new Pen(Tema.Firca("Yuzey3"), kal), m, orta, orta);

            if (toplam > 0)
            {
                double bas = 0;
                double bosluk = dilimler.Count > 1 ? 1.6 : 0;
                double ilerleme = Ilerleme;
                for (int i = 0; i < dilimler.Count; i++)
                {
                    double tarama = dilimler[i].Deger / toplam * 360 * ilerleme;
                    var renk = RenkCoz(dilimler[i].Renk);
                    bool secili = i == _secili;
                    double k = secili ? kal + 6 : kal;
                    double opak = _secili >= 0 && !secili ? 0.45 : 1;
                    if (tarama >= 359.5)
                        dc.DrawEllipse(null, new Pen(Firca(renk, opak), k), m, orta, orta);
                    else if (tarama - bosluk > 0.2)
                        dc.DrawGeometry(null, new Pen(Firca(renk, opak), k), Yay(m, orta, bas + bosluk / 2, bas + tarama - bosluk / 2));
                    bas += tarama;
                }
            }

            // Merkez metni
            string ust = MerkezBaslik, alt = MerkezDeger;
            if (_secili >= 0 && _secili < dilimler.Count)
            {
                var d = dilimler[_secili];
                ust = $"{d.Ad} · %{(d.Deger / toplam * 100).ToString("0.#", Ui.Tr)}";
                alt = d.DegerMetni ?? d.Deger.ToString("N0", Ui.Tr);
            }
            double maxGen = (r - kal) * 1.6;
            var t1 = Metin(ust, 12, Tema.Firca("Metin3"));
            t1.MaxTextWidth = Math.Max(20, maxGen);
            t1.TextAlignment = TextAlignment.Center;
            t1.MaxLineCount = 1;
            t1.Trimming = TextTrimming.CharacterEllipsis;
            double boyut = Math.Clamp((r - kal) / 3.4, 13, 24);
            var t2 = Metin(alt, boyut, Tema.Firca("Metin"), FontWeights.SemiBold);
            t2.MaxTextWidth = Math.Max(20, maxGen);
            t2.TextAlignment = TextAlignment.Center;
            t2.MaxLineCount = 1;
            t2.Trimming = TextTrimming.CharacterEllipsis;
            double toplamY = t1.Height + t2.Height + 2;
            dc.DrawText(t1, new Point(m.X - t1.MaxTextWidth / 2, m.Y - toplamY / 2));
            dc.DrawText(t2, new Point(m.X - t2.MaxTextWidth / 2, m.Y - toplamY / 2 + t1.Height + 2));
        }

        static Geometry Yay(Point m, double r, double basAci, double sonAci)
        {
            Point P(double a)
            {
                double rad = (a - 90) * Math.PI / 180;
                return new Point(m.X + r * Math.Cos(rad), m.Y + r * Math.Sin(rad));
            }
            var g = new StreamGeometry();
            using (var c = g.Open())
            {
                c.BeginFigure(P(basAci), false, false);
                c.ArcTo(P(sonAci), new Size(r, r), 0, sonAci - basAci > 180, SweepDirection.Clockwise, true, false);
            }
            g.Freeze();
            return g;
        }
    }

    /// <summary>Giriş ve kurulum ekranlarındaki yıldızlı uzay arka planı</summary>
    public class YildizliArkaPlan : FrameworkElement
    {
        static readonly (double X, double Y, double R, double O)[] Yildizlar;

        static YildizliArkaPlan()
        {
            var rnd = new Random(7);
            Yildizlar = Enumerable.Range(0, 260).Select(_ =>
            {
                double r = rnd.NextDouble();
                return (rnd.NextDouble(), rnd.NextDouble(), 0.35 + r * r * r * 1.5, 0.25 + rnd.NextDouble() * 0.7);
            }).ToArray();
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo info)
        {
            base.OnRenderSizeChanged(info);
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            if (w <= 0 || h <= 0) return;
            var alan = new Rect(0, 0, w, h);

            var taban = new LinearGradientBrush(Color.FromRgb(0x07, 0x0A, 0x14), Color.FromRgb(0x03, 0x04, 0x08), new Point(0, 0), new Point(0.4, 1));
            dc.DrawRectangle(taban, null, alan);

            void Bulut(double x, double y, double rx, double ry, Color c, byte alfa)
            {
                var b = new RadialGradientBrush(Color.FromArgb(alfa, c.R, c.G, c.B), Color.FromArgb(0, c.R, c.G, c.B));
                dc.DrawEllipse(b, null, new Point(w * x, h * y), w * rx, h * ry);
            }
            Bulut(0.25, 0.30, 0.75, 0.42, Color.FromRgb(0x2A, 0x4C, 0xB8), 70);
            Bulut(0.80, 0.70, 0.60, 0.45, Color.FromRgb(0x5B, 0x2C, 0x9E), 46);
            Bulut(0.55, 0.45, 0.55, 0.18, Color.FromRgb(0x9A, 0xA8, 0xC8), 26);
            Bulut(0.10, 0.95, 0.45, 0.30, Color.FromRgb(0x0E, 0x5A, 0x8A), 40);

            foreach (var (x, y, r, o) in Yildizlar)
            {
                var p = new Point(x * w, y * h);
                if (r > 1.4)
                {
                    var parilti = new RadialGradientBrush(Color.FromArgb((byte)(o * 120), 200, 215, 255), Color.FromArgb(0, 200, 215, 255));
                    dc.DrawEllipse(parilti, null, p, r * 4, r * 4);
                }
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(o * 255), 235, 240, 255)), null, p, r, r);
            }
        }
    }
    /// <summary>Şifre gücü göstergesi: 4 çubuk ve etiket</summary>
    public class SifreGucu : GrafikTemeli
    {
        public static readonly DependencyProperty GucProperty = DependencyProperty.Register(
            nameof(Guc), typeof(int), typeof(SifreGucu), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

        public int Guc { get => (int)GetValue(GucProperty); set => SetValue(GucProperty, value); }

        protected override Size MeasureOverride(Size s) => new(double.IsInfinity(s.Width) ? 200 : s.Width, 18);

        protected override void OnRender(DrawingContext dc)
        {
            int g = Guc;
            string anahtar = g <= 1 ? "Tehlike" : g == 2 ? "Uyari" : "Basari";
            string etiket = g == 0 ? "" : g == 1 ? "Zayıf" : g == 2 ? "Orta" : g == 3 ? "İyi" : "Güçlü";
            var et = Metin(etiket, 11.5, Tema.Firca(anahtar + "Metin"), FontWeights.SemiBold);
            double etGen = 52;
            double w = Math.Max(0, ActualWidth - etGen);
            double bosluk = 5, cw = (w - bosluk * 3) / 4;
            for (int i = 0; i < 4; i++)
            {
                var r = new Rect(i * (cw + bosluk), 7, Math.Max(0, cw), 4);
                dc.DrawRoundedRectangle(i < g ? Tema.Firca(anahtar) : Tema.Firca("Yuzey3"), null, r, 2, 2);
            }
            dc.DrawText(et, new Point(ActualWidth - et.Width, 9 - et.Height / 2));
        }
    }
}
