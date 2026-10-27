using System.Globalization;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Frcistan.Servisler
{
    /// <summary>
    /// Raporları yazıcıya gönderir. PDF için yazıcı olarak "Microsoft Print to PDF" seçilebilir.
    /// </summary>
    public static class Yazdirici
    {
        static readonly SolidColorBrush Koyu = Dondur(new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A)));
        static readonly SolidColorBrush Gri = Dondur(new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)));
        static readonly SolidColorBrush Cizgi = Dondur(new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)));
        static readonly SolidColorBrush BaslikArka = Dondur(new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)));
        static readonly SolidColorBrush Zebra = Dondur(new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC)));
        static readonly SolidColorBrush Vurgu = Dondur(new SolidColorBrush(Color.FromRgb(0x2F, 0x6B, 0xFF)));

        static SolidColorBrush Dondur(SolidColorBrush b) { b.Freeze(); return b; }

        public static bool Yazdir(Rapor r)
        {
            var dlg = new PrintDialog { UserPageRangeEnabled = true };
            try
            {
                dlg.PrintTicket.PageOrientation = r.Yatay ? PageOrientation.Landscape : PageOrientation.Portrait;
                dlg.PrintTicket.PageMediaSize = new PageMediaSize(PageMediaSizeName.ISOA4);
            }
            catch { }
            if (dlg.ShowDialog() != true) return false;

            var boyut = new Size(dlg.PrintableAreaWidth, dlg.PrintableAreaHeight);
            var belge = Belge(r, boyut);
            var sayfalayici = new AltBilgiliSayfalayici(((IDocumentPaginatorSource)belge).DocumentPaginator, r.Baslik, boyut);
            dlg.PrintDocument(sayfalayici, "Frcistan - " + r.Baslik);
            return true;
        }

        public static FlowDocument Belge(Rapor r, Size boyut)
        {
            var doc = new FlowDocument
            {
                PageWidth = boyut.Width,
                PageHeight = boyut.Height,
                PagePadding = new Thickness(44, 40, 44, 64),
                ColumnWidth = double.PositiveInfinity,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 10,
                Foreground = Koyu,
                Background = Brushes.White,
                TextAlignment = TextAlignment.Left
            };

            doc.Blocks.Add(new BlockUIContainer(Baslik(r)) { Margin = new Thickness(0, 0, 0, 14) });

            var tablo = new Table { CellSpacing = 0, BorderBrush = Cizgi, BorderThickness = new Thickness(0.75) };
            double toplamOran = r.Sutunlar.Sum(s => s.Oran);
            foreach (var s in r.Sutunlar)
                tablo.Columns.Add(new TableColumn { Width = new GridLength(s.Oran / toplamOran, GridUnitType.Star) });

            var baslikGrubu = new TableRowGroup();
            var baslikSatiri = new TableRow { Background = BaslikArka };
            foreach (var s in r.Sutunlar)
                baslikSatiri.Cells.Add(Hucre(s.Baslik, s.SagaYasli, true));
            baslikGrubu.Rows.Add(baslikSatiri);
            tablo.RowGroups.Add(baslikGrubu);

            var govde = new TableRowGroup();
            int no = 0;
            foreach (var satir in r.Satirlar)
            {
                var tr = new TableRow();
                if (no++ % 2 == 1) tr.Background = Zebra;
                for (int i = 0; i < r.Sutunlar.Count; i++)
                {
                    var deger = i < satir.Length ? satir[i] : null;
                    tr.Cells.Add(Hucre(r.Sutunlar[i].Bicimle(deger), r.Sutunlar[i].SagaYasli, false));
                }
                govde.Rows.Add(tr);
            }
            if (r.Satirlar.Count == 0)
            {
                var tr = new TableRow();
                var c = Hucre("Kayıt yok", false, false);
                c.ColumnSpan = Math.Max(1, r.Sutunlar.Count);
                tr.Cells.Add(c);
                govde.Rows.Add(tr);
            }
            tablo.RowGroups.Add(govde);
            doc.Blocks.Add(tablo);

            if (r.Ozet.Count > 0)
                doc.Blocks.Add(new BlockUIContainer(Ozet(r)) { Margin = new Thickness(0, 14, 0, 0) });

            return doc;
        }

        static TableCell Hucre(string metin, bool sag, bool baslik)
        {
            var p = new Paragraph(new Run(metin ?? ""))
            {
                Margin = new Thickness(0),
                TextAlignment = sag ? TextAlignment.Right : TextAlignment.Left,
                FontWeight = baslik ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = baslik ? Gri : Koyu,
                FontSize = baslik ? 9 : 9.75
            };
            return new TableCell(p)
            {
                Padding = new Thickness(7, baslik ? 6 : 5, 7, baslik ? 6 : 5),
                BorderBrush = Cizgi,
                BorderThickness = new Thickness(0, 0, 0, 0.75)
            };
        }

        static UIElement Baslik(Rapor r)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition());
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var logo = new Border
            {
                Width = 42, Height = 42, CornerRadius = new CornerRadius(9),
                Background = new SolidColorBrush(Color.FromRgb(0x0B, 0x10, 0x20)),
                Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center,
                Child = new Image
                {
                    Source = new BitmapImage(new Uri("pack://application:,,,/Assets/logo-beyaz.png")),
                    Margin = new Thickness(7), Stretch = Stretch.Uniform
                }
            };
            g.Children.Add(logo);

            var sol = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            sol.Children.Add(new TextBlock { Text = r.Baslik, FontSize = 18, FontWeight = FontWeights.Bold, Foreground = Koyu });
            if (!string.IsNullOrWhiteSpace(r.AltBaslik))
                sol.Children.Add(new TextBlock { Text = r.AltBaslik, FontSize = 10, Foreground = Gri, Margin = new Thickness(0, 2, 0, 0), TextWrapping = TextWrapping.Wrap });
            Grid.SetColumn(sol, 1);
            g.Children.Add(sol);

            var sag = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            string firma = string.IsNullOrWhiteSpace(FirmaAyarlari.FirmaAdi) ? "Frcistan" : FirmaAyarlari.FirmaAdi;
            sag.Children.Add(new TextBlock { Text = firma, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = Koyu, HorizontalAlignment = HorizontalAlignment.Right });
            sag.Children.Add(new TextBlock
            {
                Text = DateTime.Now.ToString("dd MMMM yyyy, HH:mm", CultureInfo.GetCultureInfo("tr-TR")),
                FontSize = 9.5, Foreground = Gri, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 2, 0, 0)
            });
            if (Oturum.Kullanici != null)
                sag.Children.Add(new TextBlock { Text = "Hazırlayan: " + Oturum.Kullanici.GorunenAd, FontSize = 9.5, Foreground = Gri, HorizontalAlignment = HorizontalAlignment.Right });
            Grid.SetColumn(sag, 2);
            g.Children.Add(sag);

            var cizgi = new Border { Height = 2.5, Background = Vurgu, Margin = new Thickness(0, 12, 0, 0), CornerRadius = new CornerRadius(1) };
            Grid.SetRow(cizgi, 1);
            Grid.SetColumnSpan(cizgi, 3);
            g.Children.Add(cizgi);
            return g;
        }

        static UIElement Ozet(Rapor r)
        {
            var kap = new Border
            {
                Background = BaslikArka, CornerRadius = new CornerRadius(6), Padding = new Thickness(14, 10, 14, 10),
                HorizontalAlignment = HorizontalAlignment.Right, MinWidth = 260
            };
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            int i = 0;
            foreach (var (etiket, deger) in r.Ozet)
            {
                g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var e = new TextBlock { Text = etiket, Foreground = Gri, FontSize = 10, Margin = new Thickness(0, 2, 24, 2) };
                var d = new TextBlock { Text = deger, Foreground = Koyu, FontSize = 10.5, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 2, 0, 2) };
                Grid.SetRow(e, i); Grid.SetRow(d, i); Grid.SetColumn(d, 1);
                g.Children.Add(e); g.Children.Add(d);
                i++;
            }
            kap.Child = g;
            return kap;
        }

        /// <summary>Her sayfanın altına "Başlık · Sayfa 1 / 3" ekler.</summary>
        sealed class AltBilgiliSayfalayici : DocumentPaginator
        {
            readonly DocumentPaginator _ic;
            readonly string _baslik;

            public AltBilgiliSayfalayici(DocumentPaginator ic, string baslik, Size boyut)
            {
                _ic = ic;
                _baslik = baslik;
                _ic.PageSize = boyut;
                _ic.ComputePageCount();
            }

            public override DocumentPage GetPage(int no)
            {
                var sayfa = _ic.GetPage(no);
                var kap = new ContainerVisual();
                kap.Children.Add(sayfa.Visual);

                var alt = new DrawingVisual();
                using (var dc = alt.RenderOpen())
                {
                    var yazi = new Typeface("Segoe UI");
                    double dpi = 1.0;
                    var solMetin = new FormattedText($"Frcistan · {_baslik}", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, yazi, 8.5, Gri, dpi);
                    var sagMetin = new FormattedText($"Sayfa {no + 1} / {_ic.PageCount}", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, yazi, 8.5, Gri, dpi);
                    double y = PageSize.Height - 40;
                    dc.DrawRectangle(Cizgi, null, new Rect(44, y - 8, PageSize.Width - 88, 0.75));
                    dc.DrawText(solMetin, new Point(44, y));
                    dc.DrawText(sagMetin, new Point(PageSize.Width - 44 - sagMetin.Width, y));
                }
                kap.Children.Add(alt);
                return new DocumentPage(kap, PageSize, sayfa.BleedBox, sayfa.ContentBox);
            }

            public override bool IsPageCountValid => _ic.IsPageCountValid;
            public override int PageCount => _ic.PageCount;
            public override Size PageSize { get => _ic.PageSize; set => _ic.PageSize = value; }
            public override IDocumentPaginatorSource Source => _ic.Source;
        }
    }
}
