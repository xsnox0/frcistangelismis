using System.Windows.Input;
using System.Windows.Threading;
using Frcistan.Altyapi;
using Frcistan.Modeller;
using Frcistan.Servisler;
using Frcistan.Veri;

namespace Frcistan.ViewModels
{
    /// <summary>Giriş ekranı. Durumlar: Hazirlaniyor, Giris, Hata, IlkKullanici</summary>
    public class GirisViewModel : ObservableObject
    {
        string _durum = "Hazirlaniyor";
        string _kullaniciAdi, _sifre, _hata, _baglantiHatasi;
        bool _beniHatirla, _mesgul, _sifreGoster;
        int _hataliDeneme;
        DateTime? _kilitBitis;
        readonly DispatcherTimer _kilitSayaci = new() { Interval = TimeSpan.FromSeconds(1) };

        string _yAdSoyad, _yKullanici = "admin", _ySifre, _ySifreTekrar;

        public event Action GirisBasarili;

        public GirisViewModel()
        {
            _kullaniciAdi = YerelAyarlar.Mevcut.BeniHatirla ? YerelAyarlar.Mevcut.SonKullanici : null;
            _beniHatirla = YerelAyarlar.Mevcut.BeniHatirla;
            GirisCommand = new AsyncCommand(GirisAsync, () => !Mesgul && !Kilitli);
            TekrarDeneCommand = new AsyncCommand(HazirlaAsync);
            BaglantiAyarlariCommand = new AsyncCommand(async () => await Diyalog.Goster(new VeritabaniDiyalogViewModel(oturumAcik: false)));
            IlkKullaniciCommand = new AsyncCommand(IlkKullaniciAsync, () => !Mesgul);
            SifreGosterCommand = new RelayCommand(() => SifreGoster = !SifreGoster);
            _kilitSayaci.Tick += (_, _) =>
            {
                if (!Kilitli) { _kilitSayaci.Stop(); Hata = null; CommandManager.InvalidateRequerySuggested(); }
                else Hata = $"Çok fazla hatalı deneme. {KilitKalan} saniye sonra tekrar deneyin.";
            };
        }

        public string Durum { get => _durum; set => Set(ref _durum, value); }
        public string KullaniciAdi { get => _kullaniciAdi; set { Set(ref _kullaniciAdi, value); if (!Kilitli) Hata = null; } }
        public string Sifre { get => _sifre; set { Set(ref _sifre, value); if (!Kilitli) Hata = null; } }
        public bool BeniHatirla { get => _beniHatirla; set => Set(ref _beniHatirla, value); }
        public bool SifreGoster { get => _sifreGoster; set => Set(ref _sifreGoster, value); }
        public string Hata { get => _hata; set => Set(ref _hata, value); }
        public string BaglantiHatasi { get => _baglantiHatasi; set => Set(ref _baglantiHatasi, value); }
        public bool Mesgul { get => _mesgul; set => Set(ref _mesgul, value); }

        public string YAdSoyad { get => _yAdSoyad; set => Set(ref _yAdSoyad, value); }
        public string YKullanici { get => _yKullanici; set => Set(ref _yKullanici, value); }
        public string YSifre { get => _ySifre; set => Set(ref _ySifre, value); }
        public string YSifreTekrar { get => _ySifreTekrar; set => Set(ref _ySifreTekrar, value); }

        bool Kilitli => _kilitBitis.HasValue && _kilitBitis > DateTime.Now;
        int KilitKalan => _kilitBitis.HasValue ? Math.Max(0, (int)Math.Ceiling((_kilitBitis.Value - DateTime.Now).TotalSeconds)) : 0;

        public string FirmaAdi => string.IsNullOrWhiteSpace(FirmaAyarlari.FirmaAdi) ? null : FirmaAyarlari.FirmaAdi;
        public string VeritabaniAciklama => YerelAyarlar.Mevcut.Veritabani.Aciklama();
        public string Surum => "Sürüm " + Altyapi.Surum.Kisa;
        public string Yil => DateTime.Now.Year.ToString();

        public ICommand GirisCommand { get; }
        public ICommand TekrarDeneCommand { get; }
        public ICommand BaglantiAyarlariCommand { get; }
        public ICommand IlkKullaniciCommand { get; }
        public ICommand SifreGosterCommand { get; }

        public async Task HazirlaAsync()
        {
            Durum = "Hazirlaniyor";
            BaglantiHatasi = null;
            try
            {
                await Task.Delay(250);
                await VeritabaniBaslatici.HazirlaAsync();
                await FirmaAyarlari.YukleAsync();
                LisansServisi.Hesapla();
                OnPropertyChanged(nameof(FirmaAdi));
                OnPropertyChanged(nameof(VeritabaniAciklama));
                Durum = await VeritabaniBaslatici.KullaniciVarMiAsync(YerelAyarlar.Mevcut.Veritabani) ? "Giris" : "IlkKullanici";
            }
            catch (Exception ex)
            {
                Gunluk.Yaz("Veritabanı hazırlanamadı", ex);
                BaglantiHatasi = VeritabaniBaslatici.HatayiAcikla(ex);
                Durum = "Hata";
            }
        }

        async Task GirisAsync()
        {
            if (string.IsNullOrWhiteSpace(KullaniciAdi) || string.IsNullOrEmpty(Sifre))
            {
                Hata = "Kullanıcı adı ve şifrenizi yazın.";
                return;
            }

            Mesgul = true;
            Hata = null;
            try
            {
                var k = await KullaniciServisi.GirisYapAsync(KullaniciAdi, Sifre);
                if (k == null)
                {
                    _hataliDeneme++;
                    Sifre = "";
                    if (_hataliDeneme >= 5)
                    {
                        _hataliDeneme = 0;
                        _kilitBitis = DateTime.Now.AddSeconds(30);
                        Hata = "Çok fazla hatalı deneme. 30 saniye sonra tekrar deneyin.";
                        _kilitSayaci.Start();
                    }
                    else
                        Hata = "Kullanıcı adı veya şifre hatalı.";
                    return;
                }
                Basarili(k);
            }
            catch (KuralHatasi ex)
            {
                Hata = ex.Message;
            }
            catch (Exception ex)
            {
                Gunluk.Yaz("Giriş", ex);
                Hata = VeritabaniBaslatici.HatayiAcikla(ex);
            }
            finally
            {
                Mesgul = false;
            }
        }

        async Task IlkKullaniciAsync()
        {
            Hata = null;
            if (YSifre != YSifreTekrar) { Hata = "Şifreler birbirini tutmuyor."; return; }
            Mesgul = true;
            try
            {
                var k = await KullaniciServisi.KaydetAsync(new Kullanici
                {
                    AdSoyad = YAdSoyad, KullaniciAdi = YKullanici, Rol = Roller.Admin, Aktif = true
                }, YSifre);
                k.SonGiris = DateTime.Now;
                Basarili(k);
            }
            catch (KuralHatasi ex) { Hata = ex.Message; }
            catch (Exception ex) { Hata = VeritabaniBaslatici.HatayiAcikla(ex); }
            finally { Mesgul = false; }
        }

        void Basarili(Kullanici k)
        {
            Oturum.Kullanici = k;
            YerelAyarlar.Mevcut.BeniHatirla = BeniHatirla;
            YerelAyarlar.Mevcut.SonKullanici = BeniHatirla ? k.KullaniciAdi : null;
            YerelAyarlar.Kaydet();
            GirisBasarili?.Invoke();
        }
    }
}
