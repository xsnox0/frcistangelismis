#if DEBUG
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Frcistan.Modeller;
using Frcistan.Pencereler;
using Frcistan.Servisler;
using Frcistan.Veri;
using Frcistan.ViewModels;

namespace Frcistan.Altyapi
{
    /// <summary>
    /// Yalnızca geliştirme için: "Frcistan.exe --ekran-goruntusu klasor" geçici bir veritabanıyla
    /// tüm ekranların görüntüsünü PNG olarak kaydeder (tanıtım broşürü ve tasarım kontrolü için).
    /// </summary>
    public static class EkranGoruntusu
    {
        public static async Task CalistirAsync(string klasor)
        {
            try
            {
                await CalistirIcAsync(klasor);
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(klasor, "hata.txt"), ex.ToString());
            }
        }

        static async Task CalistirIcAsync(string klasor)
        {
            Directory.CreateDirectory(klasor);
            YerelAyarlar.Klasor = Path.Combine(Path.GetTempPath(), "FrcistanEkranGoruntusu");
            if (Directory.Exists(YerelAyarlar.Klasor)) Directory.Delete(YerelAyarlar.Klasor, true);
            YerelAyarlar.Sifirla();
            YerelAyarlar.Mevcut.PencereTamEkran = false;
            YerelAyarlar.Mevcut.OtomatikYedek = false;

            await VeritabaniBaslatici.HazirlaAsync();
            await FirmaAyarlari.KaydetAsync(new Dictionary<string, string>
            {
                [FirmaAyarlari.Anahtarlar.FirmaAdi] = "Robotistan Robotik",
                [FirmaAyarlari.Anahtarlar.ParaBirimi] = "USD"
            });
            var yonetici = await KullaniciServisi.KaydetAsync(new Kullanici
            {
                AdSoyad = "İlyas Enes Sepetci", KullaniciAdi = "ilyas", Rol = Roller.Admin, Aktif = true
            }, "Frcistan2026");
            Oturum.Kullanici = yonetici;
            await DemoVeri.EkleAsync(yonetici.GorunenAd);
            await FirmaAyarlari.YukleAsync();
            LisansServisi.Hesapla();
            YerelAyarlar.Mevcut.SonKullanici = "ilyas";
            YerelAyarlar.Kaydet();

            var urunler = await UrunServisi.TumunuGetirAsync();
            var kraken = urunler.First(u => u.UrunAdi.Contains("Kraken X60"));

            // Örnek Excel ve yazdırma çıktısı
            var hareketler = await StokServisi.GetirAsync(DateTime.Today.AddDays(-30));
            ExcelYazici.Yaz(Path.Combine(klasor, "ornek-rapor.xlsx"),
                RaporServisi.StokListesi(urunler), RaporServisi.Hareketler(hareketler, "Son 30 gün"),
                RaporServisi.KategoriDegerleme(urunler, await KategoriServisi.GetirAsync()), RaporServisi.KritikStok(urunler));
            BaskiOnizleme(RaporServisi.StokListesi(urunler), Path.Combine(klasor, "baski-stok-listesi.png"), true);
            BaskiOnizleme(RaporServisi.KritikStok(urunler), Path.Combine(klasor, "baski-siparis-listesi.png"), false);

            foreach (var tema in new[] { "Koyu", "Acik" })
            {
                Tema.Uygula(tema);

                // Giriş
                Oturum.Kullanici = null;
                var g = new GirisPenceresi();
                g.Show();
                await Bekle(1500);
                Kaydet(g, klasor, $"{tema}-01-giris.png");
                g.Tag = "gecis";
                g.Close();

                // Kurulum sihirbazı
                var k = new KurulumPenceresi();
                k.Show();
                k.Vm.FirmaAdi = "Robotistan Robotik";
                k.Vm.AdSoyad = "İlyas Enes Sepetci";
                for (int i = 0; i < 5; i++)
                {
                    k.Vm.Adim = i;
                    if (i == 2) k.Vm.Form.Tur = "MySql";
                    await Bekle(450);
                    Kaydet(k, klasor, $"{tema}-02-kurulum-{i}.png");
                }
                k.Close();

                // Ana pencere ve sayfalar
                Oturum.Kullanici = yonetici;
                var a = new AnaPencere { WindowState = WindowState.Normal, Width = 1440, Height = 900, Left = 0, Top = 0 };
                a.Show();
                int no = 10;
                foreach (var m in a.Vm.Menu.ToList())
                {
                    a.Vm.SeciliMenu = m;
                    await Bekle(1600);
                    Kaydet(a, klasor, $"{tema}-{no++}-{m.Anahtar}.png");
                }

                a.Vm.Git("Urunler");
                await Bekle(900);
                await DiyalogCek(a, klasor, $"{tema}-30-urun-duzenle.png", new UrunDiyalogViewModel(kraken));
                await DiyalogCek(a, klasor, $"{tema}-31-urun-yeni.png", new UrunDiyalogViewModel());
                await DiyalogCek(a, klasor, $"{tema}-32-stok-cikis.png", new StokDiyalogViewModel(kraken, HareketTuru.Cikis));
                await DiyalogCek(a, klasor, $"{tema}-33-stok-secici.png", new StokDiyalogViewModel(null, HareketTuru.Giris));
                await DiyalogCek(a, klasor, $"{tema}-34-urun-gecmisi.png", new UrunGecmisiViewModel(kraken));
                await DiyalogCek(a, klasor, $"{tema}-35-kategori.png", new KategoriDiyalogViewModel(null) { Ad = "Pnömatik" });
                await DiyalogCek(a, klasor, $"{tema}-36-kullanici.png", new KullaniciDiyalogViewModel(null));
                await DiyalogCek(a, klasor, $"{tema}-37-lisans.png", new LisansDiyalogViewModel());
                await DiyalogCek(a, klasor, $"{tema}-38-onay.png", new MesajViewModel
                {
                    Baslik = "Ürünü sil", Mesaj = "\"WCP Kraken X60 Brushless Motor\" kalıcı olarak silinecek.\n\nBu ürüne ait 14 stok hareketi de silinecek.",
                    Tur = MesajTuru.Soru, OnayMetni = "Sil", IptalMetni = "Vazgeç", Tehlikeli = true
                });
                await DiyalogCek(a, klasor, $"{tema}-39-veritabani.png", new VeritabaniDiyalogViewModel(true));

                a.Vm.Git("Panel");
                await Bekle(1200);
                Bildirim.Basari("Stok çıkışı (satış) kaydedildi", "Kraken X60 Brushless Motor: −2 adet · kalan 6");
                Bildirim.Uyari("Stok kritik seviyede", "Talon SRX Motor Controller: 2 adet kaldı");
                await Bekle(700);
                Kaydet(a, klasor, $"{tema}-40-bildirim.png");
                Bildirim.Acik.Clear();
                a.Close();
            }
        }

        static async Task DiyalogCek(Window w, string klasor, string ad, DiyalogViewModel vm)
        {
            _ = Diyalog.Goster(vm);
            await Bekle(1000);
            Kaydet(w, klasor, ad);
            vm.Kapat(false);
            await Bekle(150);
        }

        static async Task Bekle(int ms)
        {
            await Task.Delay(ms);
            await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        }

        static void Kaydet(Window w, string klasor, string ad)
        {
            var el = (FrameworkElement)w.Content;
            el.UpdateLayout();
            int gen = (int)Math.Ceiling(el.ActualWidth), yuk = (int)Math.Ceiling(el.ActualHeight);
            var rtb = new RenderTargetBitmap(gen, yuk, 96, 96, PixelFormats.Pbgra32);
            var arka = new DrawingVisual();
            using (var dc = arka.RenderOpen())
                dc.DrawRectangle(w.Background ?? Brushes.Black, null, new Rect(0, 0, gen, yuk));
            rtb.Render(arka);
            rtb.Render(el);
            PngKaydet(rtb, Path.Combine(klasor, ad));
        }

        static void BaskiOnizleme(Rapor r, string dosya, bool yatay)
        {
            var boyut = yatay ? new Size(1122.5, 793.7) : new Size(793.7, 1122.5);
            var belge = Yazdirici.Belge(r, boyut);
            var sayfalayici = ((IDocumentPaginatorSource)belge).DocumentPaginator;
            sayfalayici.PageSize = boyut;
            var sayfa = sayfalayici.GetPage(0);
            var rtb = new RenderTargetBitmap((int)boyut.Width, (int)boyut.Height, 96, 96, PixelFormats.Pbgra32);
            var arka = new DrawingVisual();
            using (var dc = arka.RenderOpen()) dc.DrawRectangle(Brushes.White, null, new Rect(boyut));
            rtb.Render(arka);
            rtb.Render(sayfa.Visual);
            PngKaydet(rtb, dosya);
        }

        static void PngKaydet(BitmapSource b, string dosya)
        {
            var e = new PngBitmapEncoder();
            e.Frames.Add(BitmapFrame.Create(b));
            using var f = File.Create(dosya);
            e.Save(f);
        }
    }
}
#endif
