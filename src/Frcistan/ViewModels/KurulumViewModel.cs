using System.Windows.Input;
using Frcistan.Altyapi;
using Frcistan.Modeller;
using Frcistan.Servisler;
using Frcistan.Veri;

namespace Frcistan.ViewModels
{
    public class KurulumAdimi : ObservableObject
    {
        string _durum = "Bekliyor";
        public int No { get; init; }
        public string Baslik { get; init; }
        public string Aciklama { get; init; }
        /// <summary>Bekliyor / Aktif / Tamam</summary>
        public string Durum { get => _durum; set => Set(ref _durum, value); }
    }

    /// <summary>İlk açılış sihirbazı: işletme → veritabanı → yönetici hesabı → hazır</summary>
    public class KurulumViewModel : ObservableObject
    {
        int _adim;
        bool _mesgul, _demoVeri = true, _mevcutKurulum;
        string _hata, _firmaAdi, _paraBirimi = "USD", _minStok = "5";
        string _adSoyad, _kullaniciAdi = "admin", _sifre, _sifreTekrar;

        public event Action Tamamlandi;

        public KurulumViewModel()
        {
            Form = new VeritabaniFormu(new VeritabaniBilgisi());
            Adimlar = new List<KurulumAdimi>
            {
                new() { No = 1, Baslik = "Hoş geldiniz", Aciklama = "Frcistan'ı tanıyın" },
                new() { No = 2, Baslik = "İşletme", Aciklama = "Firma adı ve para birimi" },
                new() { No = 3, Baslik = "Veritabanı", Aciklama = "Verilerin saklanacağı yer" },
                new() { No = 4, Baslik = "Yönetici hesabı", Aciklama = "İlk kullanıcı" },
                new() { No = 5, Baslik = "Hazır", Aciklama = "Kullanmaya başlayın" }
            };
            IleriCommand = new AsyncCommand(IleriAsync, () => !Mesgul);
            GeriCommand = new RelayCommand(() => { Hata = null; Adim--; }, () => !Mesgul && Adim > 0 && Adim < 4);
            AdimlariGuncelle();
        }

        public List<KurulumAdimi> Adimlar { get; }
        public VeritabaniFormu Form { get; }

        public int Adim
        {
            get => _adim;
            set
            {
                if (!Set(ref _adim, value)) return;
                AdimlariGuncelle();
                OnPropertyChanged(nameof(IleriMetni));
                OnPropertyChanged(nameof(AdimMetni));
            }
        }

        public string AdimMetni => $"Adım {Adim + 1} / {Adimlar.Count}";
        public string IleriMetni => Adim switch
        {
            0 => "Başlayalım",
            3 => "Kurulumu tamamla",
            4 => "Frcistan'ı başlat",
            _ => "Devam"
        };

        public bool Mesgul { get => _mesgul; set => Set(ref _mesgul, value); }
        public string Hata { get => _hata; set => Set(ref _hata, value); }

        public string FirmaAdi { get => _firmaAdi; set => Set(ref _firmaAdi, value); }
        public string ParaBirimi { get => _paraBirimi; set => Set(ref _paraBirimi, value); }
        public string MinStok { get => _minStok; set => Set(ref _minStok, value); }

        public string AdSoyad { get => _adSoyad; set => Set(ref _adSoyad, value); }
        public string KullaniciAdi { get => _kullaniciAdi; set => Set(ref _kullaniciAdi, value); }
        public string Sifre { get => _sifre; set { Set(ref _sifre, value); OnPropertyChanged(nameof(SifreGucu)); } }
        public string SifreTekrar { get => _sifreTekrar; set => Set(ref _sifreTekrar, value); }
        public int SifreGucu => SifreHasher.Guc(Sifre);
        public bool DemoVeri { get => _demoVeri; set => Set(ref _demoVeri, value); }

        /// <summary>Seçilen MySQL veritabanında zaten kullanıcılar varsa (ikinci bilgisayar kurulumu)</summary>
        public bool MevcutKurulum { get => _mevcutKurulum; set => Set(ref _mevcutKurulum, value); }

        public string OzetVeritabani => Form.MySql ? $"MySQL · {Form.Sunucu}:{Form.Port} / {Form.Veritabani}" : "Bu bilgisayar (yerel veritabanı)";

        public ICommand IleriCommand { get; }
        public ICommand GeriCommand { get; }

        void AdimlariGuncelle()
        {
            for (int i = 0; i < Adimlar.Count; i++)
                Adimlar[i].Durum = i < Adim ? "Tamam" : i == Adim ? "Aktif" : "Bekliyor";
        }

        async Task IleriAsync()
        {
            Hata = null;
            switch (Adim)
            {
                case 0:
                    Adim = 1;
                    break;

                case 1:
                    if (!int.TryParse(MinStok, out int m) || m < 0) { Hata = "Kritik stok seviyesi 0 ya da daha büyük bir sayı olmalı."; return; }
                    Adim = 2;
                    break;

                case 2:
                    await VeritabaniAdimiAsync();
                    break;

                case 3:
                    await TamamlaAsync();
                    break;

                case 4:
                    Tamamlandi?.Invoke();
                    break;
            }
        }

        async Task VeritabaniAdimiAsync()
        {
            Mesgul = true;
            try
            {
                var bilgi = Form.Bilgi();
                if (!await Form.TestEtAsync()) { Hata = Form.TestSonucu; return; }
                await VeritabaniBaslatici.HazirlaAsync(bilgi);
                MevcutKurulum = await VeritabaniBaslatici.KullaniciVarMiAsync(bilgi);
                OnPropertyChanged(nameof(OzetVeritabani));
                Adim = 3;
            }
            catch (KuralHatasi ex) { Hata = ex.Message; }
            catch (Exception ex) { Hata = VeritabaniBaslatici.HatayiAcikla(ex); }
            finally { Mesgul = false; }
        }

        async Task TamamlaAsync()
        {
            if (!MevcutKurulum)
            {
                if (string.IsNullOrWhiteSpace(KullaniciAdi) || KullaniciAdi.Trim().Length < 3) { Hata = "Kullanıcı adı en az 3 karakter olmalı."; return; }
                var kural = SifreHasher.KuralHatasi(Sifre);
                if (kural != null) { Hata = kural; return; }
                if (Sifre != SifreTekrar) { Hata = "Şifreler birbirini tutmuyor."; return; }
            }

            Mesgul = true;
            try
            {
                var bilgi = Form.Bilgi();
                YerelAyarlar.Mevcut.Veritabani = bilgi;
                await VeritabaniBaslatici.HazirlaAsync(bilgi);

                if (!MevcutKurulum)
                {
                    await FirmaAyarlari.KaydetAsync(new Dictionary<string, string>
                    {
                        [FirmaAyarlari.Anahtarlar.FirmaAdi] = FirmaAdi?.Trim(),
                        [FirmaAyarlari.Anahtarlar.ParaBirimi] = ParaBirimi,
                        [FirmaAyarlari.Anahtarlar.VarsayilanMinStok] = MinStok,
                        [FirmaAyarlari.Anahtarlar.KurulumTarihi] = DateTime.Now.ToString("o")
                    });

                    var yonetici = await KullaniciServisi.KaydetAsync(new Kullanici
                    {
                        AdSoyad = AdSoyad, KullaniciAdi = KullaniciAdi, Rol = Roller.Admin, Aktif = true
                    }, Sifre);

                    if (DemoVeri)
                    {
                        Oturum.Kullanici = yonetici;
                        try { await Veri.DemoVeri.EkleAsync(yonetici.GorunenAd); }
                        finally { Oturum.Kullanici = null; }
                    }
                    YerelAyarlar.Mevcut.SonKullanici = yonetici.KullaniciAdi;
                }
                else
                {
                    await FirmaAyarlari.YukleAsync();
                }

                YerelAyarlar.Mevcut.BeniHatirla = true;
                YerelAyarlar.Kaydet();
                Adim = 4;
            }
            catch (KuralHatasi ex) { Hata = ex.Message; }
            catch (Exception ex)
            {
                Gunluk.Yaz("Kurulum tamamlanamadı", ex);
                Hata = VeritabaniBaslatici.HatayiAcikla(ex);
            }
            finally { Mesgul = false; }
        }
    }
}
