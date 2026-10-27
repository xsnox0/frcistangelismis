using System.Collections.ObjectModel;
using System.Windows.Input;
using Frcistan.Altyapi;
using Frcistan.Modeller;
using Frcistan.Servisler;

namespace Frcistan.ViewModels
{
    /// <summary>Ana pencere: sol menü, üst çubuk, sayfa geçişleri, kritik stok bildirimleri</summary>
    public class AnaViewModel : ObservableObject
    {
        public static AnaViewModel Ornek { get; private set; }

        MenuOgesi _seciliMenu;
        SayfaViewModel _aktifSayfa;
        List<UrunSatiri> _kritikUrunler = new();
        string _aramaMetni;
        readonly Action _veriDegisti, _lisansDegisti;

        /// <summary>Pencere kapanınca olay aboneliklerini bırakır</summary>
        public void Kapat()
        {
            VeriOlayi.Degisti -= _veriDegisti;
            LisansServisi.Degisti -= _lisansDegisti;
            if (Ornek == this) Ornek = null;
        }

        public AnaViewModel()
        {
            Ornek = this;
            Menu.Add(new MenuOgesi { Anahtar = "Panel", Baslik = "Genel Bakış", Ikon = "", Olustur = () => new PanelViewModel() });
            Menu.Add(new MenuOgesi { Anahtar = "Urunler", Baslik = "Ürünler", Ikon = "", Olustur = () => new UrunlerViewModel() });
            Menu.Add(new MenuOgesi { Anahtar = "Hareketler", Baslik = "Stok Hareketleri", Ikon = "", Olustur = () => new HareketlerViewModel() });
            Menu.Add(new MenuOgesi { Anahtar = "Kategoriler", Baslik = "Kategoriler", Ikon = "", Olustur = () => new KategorilerViewModel() });
            Menu.Add(new MenuOgesi { Anahtar = "Raporlar", Baslik = "Raporlar", Ikon = "", Olustur = () => new RaporlarViewModel() });
            if (Oturum.Admin)
                Menu.Add(new MenuOgesi { Anahtar = "Kullanicilar", Baslik = "Kullanıcılar", Ikon = "", Olustur = () => new KullanicilarViewModel() });
            Menu.Add(new MenuOgesi { Anahtar = "Ayarlar", Baslik = "Ayarlar", Ikon = "", Olustur = () => new AyarlarViewModel() });

            TemaDegistirCommand = new RelayCommand(TemaDegistir);
            CikisCommand = new AsyncCommand(CikisAsync);
            AraCommand = new RelayCommand(() => UrunlereGit(AramaMetni));
            KritikleriGorCommand = new RelayCommand(() => UrunlereGit(null, "Kritik"));
            KritikUrunCommand = new AsyncCommand(async p =>
            {
                if (p is UrunSatiri u) await StokIslemi(u.Urun, HareketTuru.Giris);
            });
            LisansCommand = new AsyncCommand(async () => await Diyalog.Goster(new LisansDiyalogViewModel()));
            SifreCommand = new AsyncCommand(async () => await Diyalog.Goster(new SifreDiyalogViewModel()));
            HizliSatisCommand = new AsyncCommand(async () => await StokIslemi(null, HareketTuru.Cikis));

            _veriDegisti = () => _ = KritikleriYenileAsync();
            _lisansDegisti = () => { OnPropertyChanged(nameof(Lisans)); OnPropertyChanged(nameof(LisansKartiGorunur)); OnPropertyChanged(nameof(LisansOrani)); };
            VeriOlayi.Degisti += _veriDegisti;
            LisansServisi.Degisti += _lisansDegisti;

            SeciliMenu = Menu[0];
            _ = KritikleriYenileAsync();
        }

        public ObservableCollection<MenuOgesi> Menu { get; } = new();

        public MenuOgesi SeciliMenu
        {
            get => _seciliMenu;
            set
            {
                if (value == null || !Set(ref _seciliMenu, value)) return;
                AktifSayfa = value.Sayfa;
                _ = AktifSayfa.YukleAsync();
            }
        }

        public SayfaViewModel AktifSayfa
        {
            get => _aktifSayfa;
            private set => Set(ref _aktifSayfa, value);
        }

        public string KullaniciAdi => Oturum.Kullanici?.GorunenAd;
        public string RolMetni => Roller.Goster(Oturum.Kullanici?.Rol);
        public string FirmaAdi => string.IsNullOrWhiteSpace(FirmaAyarlari.FirmaAdi) ? "Stok Yönetimi" : FirmaAyarlari.FirmaAdi;
        public string Surum => "Sürüm " + Altyapi.Surum.Kisa;

        public LisansBilgisi Lisans => LisansServisi.Mevcut;
        public bool LisansKartiGorunur => Lisans.Durum != LisansDurumu.Lisansli;
        public double LisansOrani => Lisans.Durum == LisansDurumu.Deneme ? Lisans.KalanGun / (double)LisansServisi.DenemeGunu : 0;

        public bool KoyuTema => Tema.Koyu;

        public List<UrunSatiri> KritikUrunler
        {
            get => _kritikUrunler;
            private set { Set(ref _kritikUrunler, value); OnPropertyChanged(nameof(KritikSayisi)); }
        }

        public int KritikSayisi => KritikUrunler.Count;

        public string AramaMetni { get => _aramaMetni; set => Set(ref _aramaMetni, value); }

        public ICommand TemaDegistirCommand { get; }
        public ICommand CikisCommand { get; }
        public ICommand AraCommand { get; }
        public ICommand KritikleriGorCommand { get; }
        public ICommand KritikUrunCommand { get; }
        public ICommand LisansCommand { get; }
        public ICommand SifreCommand { get; }
        public ICommand HizliSatisCommand { get; }

        public void TemaYenilendi() => OnPropertyChanged(nameof(KoyuTema));

        public void FirmaBilgisiniYenile()
        {
            OnPropertyChanged(nameof(FirmaAdi));
            OnPropertyChanged(nameof(KullaniciAdi));
        }

        public void Git(string anahtar)
        {
            var m = Menu.FirstOrDefault(x => x.Anahtar == anahtar);
            if (m == null) return;
            if (m == SeciliMenu) _ = m.Sayfa.YukleAsync();
            else SeciliMenu = m;
        }

        public void UrunlereGit(string arama = null, string durum = null, int? kategoriId = null)
        {
            var m = Menu.First(x => x.Anahtar == "Urunler");
            var vm = (UrunlerViewModel)m.Sayfa;
            vm.FiltreAyarla(arama, durum, kategoriId);
            AramaMetni = null;
            Git("Urunler");
        }

        public void Yenile() => _ = AktifSayfa?.YukleAsync();

        async Task KritikleriYenileAsync()
        {
            try
            {
                var urunler = await UrunServisi.TumunuGetirAsync();
                KritikUrunler = urunler.Where(u => u.Adet <= u.MinStok)
                    .OrderBy(u => u.Adet > 0).ThenBy(u => u.Adet - u.MinStok)
                    .Select(u => new UrunSatiri(u)).ToList();
                var m = Menu.FirstOrDefault(x => x.Anahtar == "Urunler");
                if (m != null) m.Rozet = KritikUrunler.Count(u => u.Adet <= 0);
            }
            catch (Exception ex)
            {
                Gunluk.Yaz("Kritik stoklar yüklenemedi", ex);
            }
        }

        /// <summary>Stok giriş/çıkış diyaloğunu açar; kayıttan sonra her şeyi yeniler.</summary>
        public async Task StokIslemi(Urun urun, HareketTuru tur)
        {
            if (!LisansServisi.Mevcut.YazmaIzni)
            {
                await Diyalog.Bilgi(Lisans.Baslik, Lisans.Aciklama, MesajTuru.Uyari);
                return;
            }
            var vm = new StokDiyalogViewModel(urun, tur);
            if (await Diyalog.Goster(vm))
            {
                VeriOlayi.Bildir();
                Yenile();
            }
        }

        void TemaDegistir()
        {
            Tema.Uygula(Tema.Koyu ? "Acik" : "Koyu");
            YerelAyarlar.Mevcut.Tema = Tema.Koyu ? "Koyu" : "Acik";
            YerelAyarlar.Kaydet();
            OnPropertyChanged(nameof(KoyuTema));
        }

        async Task CikisAsync()
        {
            if (await Diyalog.Onayla("Oturumu kapat", "Oturumunuz kapatılacak ve giriş ekranına döneceksiniz.", "Oturumu kapat", "Vazgeç"))
                App.OturumuKapat();
        }
    }
}
