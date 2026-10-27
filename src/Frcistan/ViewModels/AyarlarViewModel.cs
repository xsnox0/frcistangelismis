using System.Windows.Input;
using Frcistan.Altyapi;
using Frcistan.Modeller;
using Frcistan.Servisler;
using Frcistan.Veri;
using Microsoft.Win32;

namespace Frcistan.ViewModels
{
    public class AyarlarViewModel : SayfaViewModel
    {
        public override string Baslik => "Ayarlar";

        string _firmaAdi, _paraBirimi, _minStok, _kodOnEki, _bolum = "Genel";

        public AyarlarViewModel()
        {
            FirmaKaydetCommand = new AsyncCommand(FirmaKaydetAsync);
            SifreCommand = new AsyncCommand(async () => await Diyalog.Goster(new SifreDiyalogViewModel()));
            VeritabaniCommand = new AsyncCommand(async () => await Diyalog.Goster(new VeritabaniDiyalogViewModel(oturumAcik: true)));
            YedekAlCommand = new AsyncCommand(YedekAlAsync);
            GeriYukleCommand = new AsyncCommand(GeriYukleAsync);
            AktarimCommand = new AsyncCommand(async () =>
            {
                if (!await YazabilirMi()) return;
                if (await Diyalog.Goster(new AktarimDiyalogViewModel())) VeriOlayi.Bildir();
            });
            LisansCommand = new AsyncCommand(async () => { await Diyalog.Goster(new LisansDiyalogViewModel()); OnPropertyChanged(string.Empty); });
            KlasorAcCommand = new RelayCommand(p => Kabuk.Ac(p as string ?? YerelAyarlar.Klasor));
            GunlukAcCommand = new RelayCommand(() => { if (File.Exists(Gunluk.Dosya)) Kabuk.Ac(Gunluk.Dosya); else Bildirim.Bilgi("Günlük boş", "Henüz kaydedilmiş bir hata yok."); });
            BolumCommand = new RelayCommand(p => Bolum = p as string);
        }

        /// <summary>Sol taraftaki bölüm seçimi</summary>
        public string Bolum { get => _bolum; set => Set(ref _bolum, value); }

        // ---- Firma ----
        public string FirmaAdi { get => _firmaAdi; set => Set(ref _firmaAdi, value); }
        public string ParaBirimi { get => _paraBirimi; set => Set(ref _paraBirimi, value); }
        public string MinStok { get => _minStok; set => Set(ref _minStok, value); }
        public string KodOnEki { get => _kodOnEki; set => Set(ref _kodOnEki, value); }

        // ---- Görünüm ----
        public bool KoyuTema
        {
            get => Tema.Koyu;
            set
            {
                if (value == Tema.Koyu) return;
                Tema.Uygula(value ? "Koyu" : "Acik");
                YerelAyarlar.Mevcut.Tema = value ? "Koyu" : "Acik";
                YerelAyarlar.Kaydet();
                OnPropertyChanged();
                AnaViewModel.Ornek?.TemaYenilendi();
            }
        }

        public bool PencereTamEkran
        {
            get => YerelAyarlar.Mevcut.PencereTamEkran;
            set { YerelAyarlar.Mevcut.PencereTamEkran = value; YerelAyarlar.Kaydet(); OnPropertyChanged(); }
        }

        // ---- Hesap ----
        public string KullaniciAdi => Oturum.Kullanici?.KullaniciAdi;
        public string GorunenAd => Oturum.Kullanici?.GorunenAd;
        public string RolMetni => Roller.Goster(Oturum.Kullanici?.Rol);

        // ---- Veritabanı ----
        public string VeritabaniAciklama => YerelAyarlar.Mevcut.Veritabani.Aciklama();
        public string VeritabaniYolu => YerelAyarlar.Mevcut.Veritabani.Tur == VeritabaniTuru.Sqlite
            ? YerelAyarlar.Mevcut.Veritabani.SqliteDosyasi() : YerelAyarlar.Mevcut.Veritabani.MySqlBaglantiMetni().Split("Pwd=")[0].TrimEnd(';');
        public bool Sqlite => YerelAyarlar.Mevcut.Veritabani.Tur == VeritabaniTuru.Sqlite;
        public string VeriKlasoru => Path.GetDirectoryName(YerelAyarlar.Mevcut.Veritabani.SqliteDosyasi());

        // ---- Yedekleme ----
        public bool OtomatikYedek
        {
            get => YerelAyarlar.Mevcut.OtomatikYedek;
            set { YerelAyarlar.Mevcut.OtomatikYedek = value; YerelAyarlar.Kaydet(); OnPropertyChanged(); }
        }
        public string SonYedek => YerelAyarlar.Mevcut.SonOtomatikYedek.HasValue
            ? "Son otomatik yedek: " + GoreceliTarihConverter.Bicimle(YerelAyarlar.Mevcut.SonOtomatikYedek.Value)
            : "Henüz otomatik yedek alınmadı";
        public string YedekKlasoru => YedekServisi.OtomatikKlasor;

        // ---- Lisans ----
        public LisansBilgisi Lisans => LisansServisi.Mevcut;
        public bool Lisansli => Lisans.Durum == LisansDurumu.Lisansli;

        // ---- Hakkında ----
        public string Surum => Altyapi.Surum.Tam.ToString(3);
        public string Gelistirici => Altyapi.Surum.Gelistirici;
        public string AyarKlasoru => YerelAyarlar.Klasor;

        public ICommand FirmaKaydetCommand { get; }
        public ICommand SifreCommand { get; }
        public ICommand VeritabaniCommand { get; }
        public ICommand YedekAlCommand { get; }
        public ICommand GeriYukleCommand { get; }
        public ICommand AktarimCommand { get; }
        public ICommand LisansCommand { get; }
        public ICommand KlasorAcCommand { get; }
        public ICommand GunlukAcCommand { get; }
        public ICommand BolumCommand { get; }

        protected override async Task YukleIcAsync()
        {
            await FirmaAyarlari.YukleAsync();
            FirmaAdi = FirmaAyarlari.FirmaAdi;
            ParaBirimi = FirmaAyarlari.ParaBirimi;
            MinStok = FirmaAyarlari.VarsayilanMinStok.ToString();
            KodOnEki = FirmaAyarlari.KodOnEki;
            AltBaslik = "Firma bilgileri, görünüm, veritabanı, yedekleme ve lisans";
            OnPropertyChanged(string.Empty);
        }

        async Task FirmaKaydetAsync()
        {
            if (!Admin || !await YazabilirMi()) return;
            if (!int.TryParse(MinStok, out int min) || min < 0)
                throw new KuralHatasi("Varsayılan kritik stok seviyesi 0 ya da daha büyük bir sayı olmalı.");
            string onEk = (KodOnEki ?? "").Trim().ToUpperInvariant();
            if (onEk.Length is < 1 or > 6 || !onEk.All(char.IsLetterOrDigit))
                throw new KuralHatasi("Ürün kodu ön eki 1-6 harf ya da rakamdan oluşmalı (ör. FRC).");

            await FirmaAyarlari.KaydetAsync(new Dictionary<string, string>
            {
                [FirmaAyarlari.Anahtarlar.FirmaAdi] = FirmaAdi?.Trim(),
                [FirmaAyarlari.Anahtarlar.ParaBirimi] = ParaBirimi ?? "USD",
                [FirmaAyarlari.Anahtarlar.VarsayilanMinStok] = min.ToString(),
                [FirmaAyarlari.Anahtarlar.KodOnEki] = onEk
            });
            KodOnEki = onEk;
            AnaViewModel.Ornek?.FirmaBilgisiniYenile();
            Bildirim.Basari("Ayarlar kaydedildi");
        }

        async Task YedekAlAsync()
        {
            var dlg = new SaveFileDialog
            {
                Title = "Yedeği kaydet",
                Filter = "Frcistan yedeği (*.frcyedek)|*.frcyedek",
                FileName = $"Frcistan-Yedek-{DateTime.Now:yyyy-MM-dd-HHmm}{YedekServisi.Uzanti}",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            if (dlg.ShowDialog() != true) return;
            var y = await YedekServisi.YedekAlAsync(dlg.FileName);
            Bildirim.Goster("Yedek alındı", y.Ozet, BildirimTuru.Basari, "Klasörde göster", () => Kabuk.KlasordeGoster(dlg.FileName));
        }

        async Task GeriYukleAsync()
        {
            if (!Admin || !await YazabilirMi()) return;
            var dlg = new OpenFileDialog
            {
                Title = "Yedek dosyası seçin",
                Filter = "Frcistan yedeği (*.frcyedek)|*.frcyedek",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            if (dlg.ShowDialog() != true) return;
            var y = await YedekServisi.OkuAsync(dlg.FileName);
            bool onay = await Diyalog.Onayla("Yedekten geri yükle",
                $"{y.Tarih:dd.MM.yyyy HH:mm} tarihli yedek ({y.Ozet}) yüklenecek.\n\n" +
                "Şu anki TÜM VERİLER silinip yerine yedektekiler gelecek. Güvenlik için önce mevcut verilerin otomatik bir yedeği alınacak.\n\n" +
                "İşlemden sonra program yeniden başlar.", "Geri yükle", "Vazgeç", tehlikeli: true);
            if (!onay) return;

            Directory.CreateDirectory(YedekServisi.OtomatikKlasor);
            await YedekServisi.YedekAlAsync(Path.Combine(YedekServisi.OtomatikKlasor, $"geri-yukleme-oncesi-{DateTime.Now:yyyyMMdd-HHmmss}{YedekServisi.Uzanti}"));
            await YedekServisi.GeriYukleAsync(y);
            await Diyalog.Bilgi("Geri yükleme tamamlandı", "Veriler yedekten yüklendi. Program şimdi yeniden başlatılacak.", MesajTuru.Basari);
            App.YenidenBaslat();
        }
    }
}
