using System.Windows.Input;
using Frcistan.Altyapi;
using Frcistan.Kontroller;
using Frcistan.Modeller;
using Frcistan.Servisler;
using Frcistan.Veri;

namespace Frcistan.ViewModels
{
    // =====================================================================
    // ÜRÜN EKLE / DÜZENLE
    // =====================================================================
    public class UrunDiyalogViewModel : DiyalogViewModel
    {
        readonly Urun _mevcut;
        string _urunAdi, _marka, _kod, _konum, _aciklama, _alis, _satis, _adet, _minStok;
        int? _kategoriId;
        string _urunAdiHatasi, _markaHatasi, _kategoriHatasi, _fiyatHatasi, _adetHatasi, _kodHatasi;
        string _kodIpucu = "Otomatik verilir";

        public UrunDiyalogViewModel(Urun mevcut = null, Urun kopyalanacak = null, int? varsayilanKategoriId = null)
        {
            _mevcut = mevcut;
            var kaynak = mevcut ?? kopyalanacak;
            Baslik = mevcut != null ? "Ürünü düzenle" : kopyalanacak != null ? "Ürünü çoğalt" : "Yeni ürün";
            AltBaslikMetni = mevcut != null ? $"{mevcut.Kod} · son güncelleme {GoreceliTarihConverter.Bicimle(mevcut.GuncellemeTarihi)}"
                : "Stoğa yeni bir parça ekleyin";

            if (kaynak != null)
            {
                _urunAdi = kaynak.UrunAdi + (kopyalanacak != null ? " (kopya)" : "");
                _marka = kaynak.Marka;
                _kategoriId = kaynak.KategoriId;
                _kod = mevcut != null ? kaynak.Kod : null;
                _konum = kaynak.Konum;
                _aciklama = kaynak.Aciklama;
                _alis = Para.Duzenle(kaynak.AlisFiyati);
                _satis = Para.Duzenle(kaynak.SatisFiyati);
                _adet = mevcut != null ? kaynak.Adet.ToString() : "0";
                _minStok = kaynak.MinStok.ToString();
            }
            else
            {
                _kategoriId = varsayilanKategoriId;
                _adet = "0";
                _minStok = FirmaAyarlari.VarsayilanMinStok.ToString();
            }

            KaydetCommand = new AsyncCommand(() => KaydetAsync(false), () => !Mesgul);
            KaydetVeYeniCommand = new AsyncCommand(() => KaydetAsync(true), () => !Mesgul);
            _ = YukleAsync();
        }

        public override double Genislik => 640;
        public bool YeniMi => _mevcut == null;
        public string AltBaslikMetni { get; }
        public string AdetEtiketi => YeniMi ? "Başlangıç stoğu" : "Stok adedi";

        public List<Kategori> Kategoriler { get; private set; } = new();
        public List<string> Markalar { get; private set; } = new();

        public Urun Kaydedilen { get; private set; }
        public int EklenenSayisi { get; private set; }

        public string UrunAdi { get => _urunAdi; set { Set(ref _urunAdi, value); UrunAdiHatasi = null; } }
        public string Marka { get => _marka; set { Set(ref _marka, value); MarkaHatasi = null; } }
        public int? KategoriId { get => _kategoriId; set { Set(ref _kategoriId, value); KategoriHatasi = null; } }
        public string Kod { get => _kod; set { Set(ref _kod, value); KodHatasi = null; } }
        public string Konum { get => _konum; set => Set(ref _konum, value); }
        public string Aciklama { get => _aciklama; set => Set(ref _aciklama, value); }
        public string AlisMetni { get => _alis; set { Set(ref _alis, value); FiyatHatasi = null; OnPropertyChanged(nameof(MarjMetni)); } }
        public string SatisMetni { get => _satis; set { Set(ref _satis, value); FiyatHatasi = null; OnPropertyChanged(nameof(MarjMetni)); } }
        public string AdetMetni { get => _adet; set { Set(ref _adet, value); AdetHatasi = null; OnPropertyChanged(nameof(AdetDegisti)); } }
        public string MinStokMetni { get => _minStok; set { Set(ref _minStok, value); AdetHatasi = null; } }
        public string KodIpucu { get => _kodIpucu; private set => Set(ref _kodIpucu, value); }
        public string ParaSembolu => Para.Sembol;

        public bool AdetDegisti => _mevcut != null && int.TryParse(AdetMetni, out int a) && a != _mevcut.Adet;

        public string MarjMetni
        {
            get
            {
                if (!Para.Coz(AlisMetni, out var a) || !Para.Coz(SatisMetni, out var s) || a <= 0 || s <= 0) return "";
                decimal kar = s - a;
                return $"Birim kâr {Para.Bicimle(kar)} · Marj %{(kar / a * 100).ToString("0.#", Ui.Tr)}";
            }
        }

        public string UrunAdiHatasi { get => _urunAdiHatasi; set => Set(ref _urunAdiHatasi, value); }
        public string MarkaHatasi { get => _markaHatasi; set => Set(ref _markaHatasi, value); }
        public string KategoriHatasi { get => _kategoriHatasi; set => Set(ref _kategoriHatasi, value); }
        public string FiyatHatasi { get => _fiyatHatasi; set => Set(ref _fiyatHatasi, value); }
        public string AdetHatasi { get => _adetHatasi; set => Set(ref _adetHatasi, value); }
        public string KodHatasi { get => _kodHatasi; set => Set(ref _kodHatasi, value); }

        public ICommand KaydetCommand { get; }
        public ICommand KaydetVeYeniCommand { get; }

        async Task YukleAsync()
        {
            try
            {
                Kategoriler = await KategoriServisi.GetirAsync();
                Markalar = await UrunServisi.MarkalarAsync();
                OnPropertyChanged(nameof(Kategoriler));
                OnPropertyChanged(nameof(Markalar));
                OnPropertyChanged(nameof(KategoriId));
                OnPropertyChanged(nameof(Marka));
                if (_mevcut == null) KodIpucu = "Otomatik: " + await UrunServisi.SiradakiKodAsync();
            }
            catch (Exception ex)
            {
                Hata = VeritabaniBaslatici.HatayiAcikla(ex);
            }
        }

        bool Dogrula(out Urun u)
        {
            u = null;
            bool tamam = true;
            if (string.IsNullOrWhiteSpace(UrunAdi)) { UrunAdiHatasi = "Ürün adını yazın."; tamam = false; }
            if (string.IsNullOrWhiteSpace(Marka)) { MarkaHatasi = "Markayı seçin ya da yazın."; tamam = false; }
            if (KategoriId == null) { KategoriHatasi = "Bir kategori seçin."; tamam = false; }
            if (!Para.Coz(AlisMetni, out var alis) || !Para.Coz(SatisMetni, out var satis)) { FiyatHatasi = "Fiyatı sayı olarak yazın (ör. 129,90)."; tamam = false; alis = satis = 0; }
            if (!int.TryParse(string.IsNullOrWhiteSpace(AdetMetni) ? "0" : AdetMetni, out int adet) || adet < 0) { AdetHatasi = "Stok adedi 0 ya da daha büyük bir tam sayı olmalı."; tamam = false; }
            if (!int.TryParse(string.IsNullOrWhiteSpace(MinStokMetni) ? "0" : MinStokMetni, out int min) || min < 0) { AdetHatasi = "Kritik seviye 0 ya da daha büyük bir tam sayı olmalı."; tamam = false; }
            if (!tamam) return false;

            u = new Urun
            {
                Id = _mevcut?.Id ?? 0, UrunAdi = UrunAdi, Marka = Marka, KategoriId = KategoriId.Value, Kod = Kod, Konum = Konum,
                Aciklama = Aciklama, AlisFiyati = alis, SatisFiyati = satis, Adet = adet, MinStok = min
            };
            return true;
        }

        async Task KaydetAsync(bool yeniAc)
        {
            if (!Dogrula(out var u)) return;
            bool kapat = false, sifirla = false;

            await CalistirAsync(async () =>
            {
                if (!string.IsNullOrWhiteSpace(u.Kod) && await UrunServisi.KodKullaniliyorMuAsync(u.Kod, u.Id))
                {
                    KodHatasi = "Bu ürün kodu başka bir üründe kullanılıyor.";
                    return;
                }

                var ayni = await UrunServisi.AyniUrunAsync(u.Marka, u.UrunAdi, u.Id);
                if (ayni != null)
                {
                    if (!YeniMi)
                    {
                        UrunAdiHatasi = $"Bu marka ve adda başka bir ürün var ({ayni.Kod}).";
                        return;
                    }
                    Mesgul = false;
                    bool birlestir = u.Adet > 0 && await Diyalog.Onayla("Bu ürün zaten kayıtlı",
                        $"\"{ayni.Marka} {ayni.UrunAdi}\" ({ayni.Kod}) zaten listede, stokta {ayni.Adet} adet var.\n\n" +
                        $"Yeni kayıt açmak yerine girdiğiniz {u.Adet} adet mevcut stoğa eklensin mi?", "Stoğa ekle", "Vazgeç");
                    if (u.Adet <= 0)
                        await Diyalog.Bilgi("Bu ürün zaten kayıtlı", $"\"{ayni.Marka} {ayni.UrunAdi}\" ({ayni.Kod}) zaten listede. Stok eklemek için adet girin.", MesajTuru.Uyari);
                    if (!birlestir) return;
                    Mesgul = true;
                    await StokServisi.HareketEkleAsync(ayni.Id, HareketTuru.Giris, u.Adet, u.AlisFiyati > 0 ? u.AlisFiyati : ayni.AlisFiyati, "Aynı ürün tekrar girildi, stoğa eklendi");
                    Kaydedilen = ayni;
                    EklenenSayisi++;
                    Bildirim.Basari("Stoğa eklendi", $"{ayni.UrunAdi}: +{u.Adet} adet");
                    kapat = !yeniAc;
                    sifirla = yeniAc;
                    return;
                }

                Kaydedilen = await UrunServisi.KaydetAsync(u);
                EklenenSayisi++;
                Bildirim.Basari(YeniMi ? "Ürün eklendi" : "Ürün güncellendi", $"{Kaydedilen.Kod} · {Kaydedilen.UrunAdi}");
                kapat = !yeniAc;
                sifirla = yeniAc;
            });

            if (kapat) Kapat(true);
            else if (sifirla)
            {
                // Hızlı giriş: kategori ve marka kalsın, diğer alanlar temizlensin
                UrunAdi = null; Kod = null; Konum = null; Aciklama = null; AlisMetni = null; SatisMetni = null; AdetMetni = "0";
                UrunAdiHatasi = null;
                KodIpucu = "Otomatik: " + await UrunServisi.SiradakiKodAsync();
                VeriOlayi.Bildir();
            }
        }
    }

    // =====================================================================
    // STOK GİRİŞ / ÇIKIŞ / SAYIM
    // =====================================================================
    public class StokDiyalogViewModel : DiyalogViewModel
    {
        Urun _urun;
        HareketTuru _tur;
        string _miktar = "1", _fiyat, _aciklama, _arama;
        List<UrunSatiri> _tumUrunler = new();
        List<UrunSatiri> _adaylar = new();
        UrunSatiri _seciliAday;
        string _miktarHatasi;

        public StokDiyalogViewModel(Urun urun, HareketTuru tur)
        {
            _urun = urun;
            _tur = tur;
            if (urun != null) FiyatVarsayilan();
            if (tur == HareketTuru.Duzeltme && urun != null) _miktar = urun.Adet.ToString();
            KaydetCommand = new AsyncCommand(KaydetAsync, () => !Mesgul && Urun != null);
            ArtirCommand = new RelayCommand(() => MiktarMetni = (Math.Max(0, MiktarSayi) + 1).ToString());
            AzaltCommand = new RelayCommand(() => MiktarMetni = Math.Max(Tur == HareketTuru.Duzeltme ? 0 : 1, MiktarSayi - 1).ToString());
            UrunDegistirCommand = new RelayCommand(() => { Urun = null; Arama = null; });
            SecCommand = new RelayCommand(p => { if (p is UrunSatiri s) UrunSec(s); });
            BaslikGuncelle();
            if (urun == null) _ = UrunleriYukleAsync();
        }

        public override double Genislik => 560;

        void BaslikGuncelle() => Baslik = Tur switch
        {
            HareketTuru.Giris => "Stok girişi",
            HareketTuru.Cikis => "Stok çıkışı (satış)",
            _ => "Sayım düzeltmesi"
        };

        public string AltBaslikMetni => Tur switch
        {
            HareketTuru.Giris => "Gelen ürünleri stoğa ekleyin",
            HareketTuru.Cikis => "Satılan ya da kullanılan ürünleri stoktan düşün",
            _ => "Sayımda bulunan gerçek adedi girin"
        };

        public string Ikon => Tur switch { HareketTuru.Giris => "", HareketTuru.Cikis => "", _ => "" };

        public Urun Urun
        {
            get => _urun;
            private set
            {
                Set(ref _urun, value);
                OnPropertyChanged(nameof(UrunSecili));
                OnPropertyChanged(nameof(UrunSatiri));
                Onizle();
            }
        }

        public UrunSatiri UrunSatiri => _urun == null ? null : new UrunSatiri(_urun);
        public bool UrunSecili => _urun != null;

        /// <summary>"Giris" / "Cikis" / "Duzeltme"</summary>
        public string TurMetni
        {
            get => _tur.ToString();
            set
            {
                if (!Enum.TryParse<HareketTuru>(value, out var t) || t == _tur) return;
                _tur = t;
                if (_urun != null)
                {
                    FiyatVarsayilan();
                    if (t == HareketTuru.Duzeltme) _miktar = _urun.Adet.ToString();
                    else if (MiktarSayi <= 0 || MiktarSayi == _urun.Adet) _miktar = "1";
                }
                BaslikGuncelle();
                OnPropertyChanged(string.Empty);
            }
        }

        public HareketTuru Tur => _tur;

        public string MiktarEtiketi => Tur switch
        {
            HareketTuru.Giris => "Gelen adet",
            HareketTuru.Cikis => "Çıkan adet",
            _ => "Sayılan (yeni) stok"
        };

        public string FiyatEtiketi => Tur == HareketTuru.Cikis ? $"Satış fiyatı ({Para.Sembol})" : $"Birim maliyet ({Para.Sembol})";

        public string MiktarMetni { get => _miktar; set { Set(ref _miktar, value); MiktarHatasi = null; Onizle(); } }
        public string FiyatMetni { get => _fiyat; set { Set(ref _fiyat, value); Onizle(); } }
        public string Aciklama { get => _aciklama; set => Set(ref _aciklama, value); }
        public string MiktarHatasi { get => _miktarHatasi; set => Set(ref _miktarHatasi, value); }

        int MiktarSayi => int.TryParse(MiktarMetni, out int m) ? m : 0;

        public int MevcutStok => _urun?.Adet ?? 0;
        public int YeniStok => Tur switch
        {
            HareketTuru.Giris => MevcutStok + Math.Max(0, MiktarSayi),
            HareketTuru.Cikis => MevcutStok - Math.Max(0, MiktarSayi),
            _ => Math.Max(0, MiktarSayi)
        };
        public bool YeniStokNegatif => YeniStok < 0;
        public bool YeniStokKritik => _urun != null && YeniStok >= 0 && YeniStok <= _urun.MinStok;
        public string TutarMetni
        {
            get
            {
                if (!Para.Coz(FiyatMetni, out var f)) return "";
                int adet = Tur == HareketTuru.Duzeltme ? Math.Abs(YeniStok - MevcutStok) : Math.Max(0, MiktarSayi);
                return Para.Bicimle(adet * f);
            }
        }

        // ---- Ürün seçici ----
        public string Arama
        {
            get => _arama;
            set
            {
                Set(ref _arama, value);
                Adaylar = _tumUrunler.Where(u => Metin.Eslesir(value, u.UrunAdi, u.Marka, u.Kod, u.KategoriAdi)).Take(30).ToList();
                SeciliAday = Adaylar.FirstOrDefault();
            }
        }

        public List<UrunSatiri> Adaylar { get => _adaylar; private set => Set(ref _adaylar, value); }
        public UrunSatiri SeciliAday { get => _seciliAday; set => Set(ref _seciliAday, value); }

        public ICommand KaydetCommand { get; }
        public ICommand ArtirCommand { get; }
        public ICommand AzaltCommand { get; }
        public ICommand UrunDegistirCommand { get; }
        public ICommand SecCommand { get; }

        public void UrunSec(UrunSatiri s)
        {
            if (s == null) return;
            Urun = s.Urun;
            FiyatVarsayilan();
            _miktar = Tur == HareketTuru.Duzeltme ? s.Adet.ToString() : "1";
            OnPropertyChanged(string.Empty);
        }

        async Task UrunleriYukleAsync()
        {
            try
            {
                _tumUrunler = (await UrunServisi.TumunuGetirAsync()).Select(u => new UrunSatiri(u)).ToList();
                Arama = "";
            }
            catch (Exception ex) { Hata = VeritabaniBaslatici.HatayiAcikla(ex); }
        }

        void FiyatVarsayilan()
        {
            if (_urun == null) return;
            _fiyat = Para.Duzenle(Tur == HareketTuru.Cikis ? _urun.SatisFiyati : _urun.AlisFiyati);
        }

        void Onizle()
        {
            foreach (var p in new[] { nameof(MevcutStok), nameof(YeniStok), nameof(YeniStokNegatif), nameof(YeniStokKritik), nameof(TutarMetni) })
                OnPropertyChanged(p);
        }

        async Task KaydetAsync()
        {
            if (_urun == null) return;
            if (!int.TryParse(MiktarMetni, out int m) || (Tur != HareketTuru.Duzeltme && m <= 0) || m < 0)
            {
                MiktarHatasi = Tur == HareketTuru.Duzeltme ? "Sayılan adedi yazın." : "Adet 1 ya da daha büyük olmalı.";
                return;
            }
            if (Tur == HareketTuru.Cikis && m > MevcutStok)
            {
                MiktarHatasi = $"Stokta yalnızca {MevcutStok} adet var.";
                return;
            }
            if (!Para.Coz(FiyatMetni, out var fiyat))
            {
                Hata = "Fiyatı sayı olarak yazın.";
                return;
            }

            if (await CalistirAsync(() => StokServisi.HareketEkleAsync(_urun.Id, Tur, m, fiyat, Aciklama)))
            {
                string ozet = Tur switch
                {
                    HareketTuru.Giris => $"+{m} adet · yeni stok {YeniStok}",
                    HareketTuru.Cikis => $"−{m} adet · kalan {YeniStok}",
                    _ => $"Stok {MevcutStok} → {m}"
                };
                Bildirim.Basari(Baslik + " kaydedildi", $"{_urun.UrunAdi}: {ozet}");
                if (Tur == HareketTuru.Cikis && YeniStok <= _urun.MinStok)
                    Bildirim.Uyari(YeniStok <= 0 ? "Ürün tükendi" : "Stok kritik seviyede", $"{_urun.UrunAdi}: {YeniStok} adet kaldı");
                Kapat(true);
            }
        }
    }

    // =====================================================================
    // KATEGORİ
    // =====================================================================
    public class KategoriDiyalogViewModel : DiyalogViewModel
    {
        readonly Kategori _mevcut;
        string _ad, _renk, _ikon, _adHatasi;

        public static readonly string[] RenkPaleti =
        {
            "#3D7EFF", "#06B6D4", "#10B981", "#84CC16", "#F59E0B", "#F97316",
            "#EF4444", "#EC4899", "#8B5CF6", "#6366F1", "#14B8A6", "#64748B"
        };

        public static readonly string[] IkonListesi =
        {
            "E9F5", "E9E9", "E895", "E9D9", "E950", "E7B3", "E945", "EE94", "EC7A", "E7B8",
            "E713", "E83F", "F22C", "E964", "EC05", "E99A", "EA86", "E8EC", "E719", "E7FC"
        };

        public KategoriDiyalogViewModel(Kategori mevcut)
        {
            _mevcut = mevcut;
            Baslik = mevcut == null ? "Yeni kategori" : "Kategoriyi düzenle";
            _ad = mevcut?.Ad;
            _renk = mevcut?.Renk ?? RenkPaleti[new Random().Next(RenkPaleti.Length)];
            _ikon = mevcut?.Ikon ?? "E7B8";
            KaydetCommand = new AsyncCommand(KaydetAsync, () => !Mesgul);
            RenkSecCommand = new RelayCommand(p => Renk = p as string);
            IkonSecCommand = new RelayCommand(p => Ikon = p as string);
        }

        public override double Genislik => 520;
        public string[] Renkler => RenkPaleti;
        public string[] Ikonlar => IkonListesi;

        public string Ad { get => _ad; set { Set(ref _ad, value); AdHatasi = null; } }
        public string Renk { get => _renk; set => Set(ref _renk, value); }
        public string Ikon { get => _ikon; set => Set(ref _ikon, value); }
        public string AdHatasi { get => _adHatasi; set => Set(ref _adHatasi, value); }

        public ICommand KaydetCommand { get; }
        public ICommand RenkSecCommand { get; }
        public ICommand IkonSecCommand { get; }

        async Task KaydetAsync()
        {
            if (string.IsNullOrWhiteSpace(Ad)) { AdHatasi = "Kategori adını yazın."; return; }
            var k = new Kategori { Id = _mevcut?.Id ?? 0, Ad = Ad, Renk = Renk, Ikon = Ikon };
            if (await CalistirAsync(() => KategoriServisi.KaydetAsync(k)))
            {
                Bildirim.Basari(_mevcut == null ? "Kategori eklendi" : "Kategori güncellendi", Ad.Trim());
                Kapat(true);
            }
        }
    }

    // =====================================================================
    // KULLANICI
    // =====================================================================
    public class KullaniciDiyalogViewModel : DiyalogViewModel
    {
        readonly Kullanici _mevcut;
        string _adSoyad, _kullaniciAdi, _rol, _sifre, _sifreTekrar;
        bool _aktif = true;

        public KullaniciDiyalogViewModel(Kullanici mevcut)
        {
            _mevcut = mevcut;
            Baslik = mevcut == null ? "Yeni kullanıcı" : "Kullanıcıyı düzenle";
            _adSoyad = mevcut?.AdSoyad;
            _kullaniciAdi = mevcut?.KullaniciAdi;
            _rol = mevcut?.Rol ?? Roller.Personel;
            _aktif = mevcut?.Aktif ?? true;
            KaydetCommand = new AsyncCommand(KaydetAsync, () => !Mesgul);
        }

        public override double Genislik => 540;
        public bool YeniMi => _mevcut == null;
        public bool Ben => _mevcut != null && _mevcut.Id == Oturum.Kullanici?.Id;
        public string SifreBasligi => YeniMi ? "Giriş şifresi" : "Şifreyi sıfırla (isteğe bağlı)";

        public string AdSoyad { get => _adSoyad; set => Set(ref _adSoyad, value); }
        public string KullaniciAdi { get => _kullaniciAdi; set => Set(ref _kullaniciAdi, value); }
        public string Rol { get => _rol; set => Set(ref _rol, value); }
        public bool Aktif { get => _aktif; set => Set(ref _aktif, value); }
        public string Sifre { get => _sifre; set { Set(ref _sifre, value); OnPropertyChanged(nameof(SifreGucu)); } }
        public string SifreTekrar { get => _sifreTekrar; set => Set(ref _sifreTekrar, value); }
        public int SifreGucu => SifreHasher.Guc(Sifre);

        public ICommand KaydetCommand { get; }

        async Task KaydetAsync()
        {
            Hata = null;
            if ((YeniMi || !string.IsNullOrEmpty(Sifre)) && Sifre != SifreTekrar)
            {
                Hata = "Şifreler birbirini tutmuyor.";
                return;
            }
            var k = new Kullanici { Id = _mevcut?.Id ?? 0, AdSoyad = AdSoyad, KullaniciAdi = KullaniciAdi, Rol = Rol, Aktif = Aktif };
            if (await CalistirAsync(() => KullaniciServisi.KaydetAsync(k, Sifre)))
            {
                Bildirim.Basari(YeniMi ? "Kullanıcı eklendi" : "Kullanıcı güncellendi", k.GorunenAd);
                Kapat(true);
            }
        }
    }

    // =====================================================================
    // ŞİFRE DEĞİŞTİR
    // =====================================================================
    public class SifreDiyalogViewModel : DiyalogViewModel
    {
        string _eski, _yeni, _tekrar;

        public SifreDiyalogViewModel()
        {
            Baslik = "Şifre değiştir";
            KaydetCommand = new AsyncCommand(KaydetAsync, () => !Mesgul);
        }

        public override double Genislik => 460;
        public string Eski { get => _eski; set => Set(ref _eski, value); }
        public string Yeni { get => _yeni; set { Set(ref _yeni, value); OnPropertyChanged(nameof(Guc)); } }
        public string Tekrar { get => _tekrar; set => Set(ref _tekrar, value); }
        public int Guc => SifreHasher.Guc(Yeni);
        public ICommand KaydetCommand { get; }

        async Task KaydetAsync()
        {
            Hata = null;
            if (string.IsNullOrEmpty(Eski)) { Hata = "Mevcut şifrenizi yazın."; return; }
            if (Yeni != Tekrar) { Hata = "Yeni şifreler birbirini tutmuyor."; return; }
            if (await CalistirAsync(() => KullaniciServisi.SifreDegistirAsync(Oturum.Kullanici.Id, Eski, Yeni)))
            {
                Bildirim.Basari("Şifreniz değiştirildi");
                Kapat(true);
            }
        }
    }

    // =====================================================================
    // VERİTABANI BAĞLANTISI
    // =====================================================================
    /// <summary>Kurulum sihirbazı ve ayarlarda ortak kullanılan veritabanı formu</summary>
    public class VeritabaniFormu : ObservableObject
    {
        string _tur, _sunucu, _port, _veritabani, _kullanici, _sifre, _testSonucu;
        bool _testBasarili, _testEdiliyor;

        public VeritabaniFormu(VeritabaniBilgisi b)
        {
            _tur = b.Tur.ToString();
            _sunucu = b.MySqlSunucu;
            _port = b.MySqlPort.ToString();
            _veritabani = b.MySqlVeritabani;
            _kullanici = b.MySqlKullanici;
            _sifre = b.MySqlSifre;
            TestCommand = new AsyncCommand(TestEtAsync, () => !TestEdiliyor);
        }

        public string Tur { get => _tur; set { if (Set(ref _tur, value)) { OnPropertyChanged(nameof(MySql)); TestSonucu = null; } } }
        public bool MySql => Tur == nameof(VeritabaniTuru.MySql);
        public string Sunucu { get => _sunucu; set { Set(ref _sunucu, value); TestSonucu = null; } }
        public string Port { get => _port; set { Set(ref _port, value); TestSonucu = null; } }
        public string Veritabani { get => _veritabani; set { Set(ref _veritabani, value); TestSonucu = null; } }
        public string Kullanici { get => _kullanici; set { Set(ref _kullanici, value); TestSonucu = null; } }
        public string Sifre { get => _sifre; set { Set(ref _sifre, value); TestSonucu = null; } }
        public string TestSonucu { get => _testSonucu; set => Set(ref _testSonucu, value); }
        public bool TestBasarili { get => _testBasarili; set => Set(ref _testBasarili, value); }
        public bool TestEdiliyor { get => _testEdiliyor; set => Set(ref _testEdiliyor, value); }
        public string SqliteYolu => new VeritabaniBilgisi().SqliteDosyasi();
        public ICommand TestCommand { get; }

        public VeritabaniBilgisi Bilgi()
        {
            var b = new VeritabaniBilgisi { Tur = MySql ? VeritabaniTuru.MySql : VeritabaniTuru.Sqlite };
            if (!MySql) return b;
            if (string.IsNullOrWhiteSpace(Sunucu)) throw new KuralHatasi("Sunucu adresini yazın (ör. localhost ya da 192.168.1.20).");
            if (!int.TryParse(Port, out int port) || port is < 1 or > 65535) throw new KuralHatasi("Port 1 ile 65535 arasında bir sayı olmalı (XAMPP'ta genelde 3306).");
            if (!VeritabaniBaslatici.VeritabaniAdiGecerli(Veritabani)) throw new KuralHatasi("Veritabanı adı yalnızca İngilizce harf, rakam ve alt çizgi içerebilir.");
            b.MySqlSunucu = Sunucu.Trim();
            b.MySqlPort = port;
            b.MySqlVeritabani = Veritabani.Trim();
            b.MySqlKullanici = string.IsNullOrWhiteSpace(Kullanici) ? "root" : Kullanici.Trim();
            b.MySqlSifre = Sifre ?? "";
            return b;
        }

        public async Task<bool> TestEtAsync()
        {
            TestEdiliyor = true;
            TestSonucu = null;
            try
            {
                var surum = await VeritabaniBaslatici.BaglantiyiTestEtAsync(Bilgi());
                TestBasarili = true;
                TestSonucu = MySql ? $"Bağlantı başarılı · sunucu sürümü {surum}" : "Hazır · veriler bu bilgisayarda saklanacak";
                return true;
            }
            catch (Exception ex)
            {
                TestBasarili = false;
                TestSonucu = ex is KuralHatasi ? ex.Message : VeritabaniBaslatici.HatayiAcikla(ex);
                return false;
            }
            finally
            {
                TestEdiliyor = false;
            }
        }
    }

    public class VeritabaniDiyalogViewModel : DiyalogViewModel
    {
        readonly bool _oturumAcik;
        bool _verileriTasi = true;

        public VeritabaniDiyalogViewModel(bool oturumAcik)
        {
            _oturumAcik = oturumAcik;
            Baslik = "Veritabanı bağlantısı";
            Form = new VeritabaniFormu(YerelAyarlar.Mevcut.Veritabani);
            KaydetCommand = new AsyncCommand(KaydetAsync, () => !Mesgul);
        }

        public override double Genislik => 600;
        public VeritabaniFormu Form { get; }
        public bool OturumAcik => _oturumAcik && Oturum.Admin;
        public bool VerileriTasi { get => _verileriTasi; set => Set(ref _verileriTasi, value); }
        public ICommand KaydetCommand { get; }

        async Task KaydetAsync()
        {
            Hata = null;
            VeritabaniBilgisi yeni;
            try { yeni = Form.Bilgi(); }
            catch (KuralHatasi ex) { Hata = ex.Message; return; }

            bool yeniden = false;
            await CalistirAsync(async () =>
            {
                await VeritabaniBaslatici.HazirlaAsync(yeni);
                bool hedefteKullaniciVar = await VeritabaniBaslatici.KullaniciVarMiAsync(yeni);

                if (OturumAcik && VerileriTasi && !hedefteKullaniciVar)
                {
                    // Mevcut verileri yeni veritabanına taşı
                    string gecici = Path.Combine(Path.GetTempPath(), $"frcistan-tasima-{Guid.NewGuid():N}{YedekServisi.Uzanti}");
                    await YedekServisi.YedekAlAsync(gecici);
                    var yedek = await YedekServisi.OkuAsync(gecici);
                    var eski = YerelAyarlar.Mevcut.Veritabani;
                    YerelAyarlar.Mevcut.Veritabani = yeni;
                    try { await YedekServisi.GeriYukleAsync(yedek); }
                    catch { YerelAyarlar.Mevcut.Veritabani = eski; throw; }
                    finally { try { File.Delete(gecici); } catch { } }
                }

                YerelAyarlar.Mevcut.Veritabani = yeni;
                YerelAyarlar.Kaydet();
                yeniden = true;
            });

            if (!yeniden) return;
            Kapat(true);
            await Diyalog.Bilgi("Bağlantı kaydedildi", "Yeni veritabanı ayarı kaydedildi. Program şimdi yeniden başlatılacak.", MesajTuru.Basari);
            App.YenidenBaslat();
        }
    }

    // =====================================================================
    // FRCİSTAN 0.1'DEN AKTARIM
    // =====================================================================
    public class AktarimDiyalogViewModel : DiyalogViewModel
    {
        string _sunucu = "localhost", _port = "3306", _veritabani = "frcistan01", _kullanici = "root", _sifre, _onizleme, _sonucMetni;

        public AktarimDiyalogViewModel()
        {
            Baslik = "Frcistan 0.1'den veri aktar";
            OnizleCommand = new AsyncCommand(OnizleAsync, () => !Mesgul);
            AktarCommand = new AsyncCommand(AktarAsync, () => !Mesgul && SonucMetni == null);
        }

        public override double Genislik => 580;
        public string Sunucu { get => _sunucu; set => Set(ref _sunucu, value); }
        public string Port { get => _port; set => Set(ref _port, value); }
        public string Veritabani { get => _veritabani; set => Set(ref _veritabani, value); }
        public string Kullanici { get => _kullanici; set => Set(ref _kullanici, value); }
        public string Sifre { get => _sifre; set => Set(ref _sifre, value); }
        public string Onizleme { get => _onizleme; set => Set(ref _onizleme, value); }
        public string SonucMetni { get => _sonucMetni; set => Set(ref _sonucMetni, value); }

        public ICommand OnizleCommand { get; }
        public ICommand AktarCommand { get; }

        string Baglanti()
        {
            if (!int.TryParse(Port, out int port)) throw new KuralHatasi("Port bir sayı olmalı.");
            return AktarimServisi.BaglantiMetni(Sunucu, port, Veritabani, Kullanici, Sifre);
        }

        async Task OnizleAsync()
        {
            Onizleme = null;
            await CalistirAsync(async () =>
            {
                var (u, k, ku) = await AktarimServisi.OnizleAsync(Baglanti());
                Onizleme = $"Bulundu: {u} ürün, {k} kategori, {ku} kullanıcı";
            });
        }

        async Task AktarAsync()
        {
            await CalistirAsync(async () =>
            {
                var s = await AktarimServisi.AktarAsync(Baglanti());
                SonucMetni = s.ToString();
                Bildirim.Basari("Aktarım tamamlandı", SonucMetni);
            });
            if (SonucMetni != null) Kapat(true);
        }
    }

    // =====================================================================
    // LİSANS
    // =====================================================================
    public class LisansDiyalogViewModel : DiyalogViewModel
    {
        string _anahtar;

        public LisansDiyalogViewModel()
        {
            Baslik = "Lisans";
            EtkinlestirCommand = new AsyncCommand(EtkinlestirAsync, () => !Mesgul);
        }

        public override double Genislik => 560;
        public LisansBilgisi Mevcut => LisansServisi.Mevcut;
        public bool Lisansli => Mevcut.Durum == LisansDurumu.Lisansli;
        public bool YoneticiMi => Oturum.Admin;
        public string Anahtar { get => _anahtar; set { Set(ref _anahtar, value); Hata = null; } }
        public ICommand EtkinlestirCommand { get; }

        async Task EtkinlestirAsync()
        {
            if (!Oturum.Admin) { Hata = "Lisansı yalnızca yönetici etkinleştirebilir."; return; }
            if (await CalistirAsync(() => LisansServisi.EtkinlestirAsync(Anahtar)))
            {
                Bildirim.Basari("Lisans etkinleştirildi", LisansServisi.Mevcut.Aciklama);
                Kapat(true);
            }
        }
    }

    // =====================================================================
    // SEÇİM (ör. kategori silerken ürünlerin taşınacağı yer)
    // =====================================================================
    public class SecimDiyalogViewModel : DiyalogViewModel
    {
        Secenek _secili;
        public string Mesaj { get; init; }
        public List<Secenek> Secenekler { get; init; }
        public Secenek Secili { get => _secili; set => Set(ref _secili, value); }
        public string OnayMetni { get; init; } = "Tamam";
        public bool Tehlikeli { get; init; }
        public override double Genislik => 460;
        public ICommand OnayCommand => new RelayCommand(() => { if (Secili != null) Kapat(true); });
    }

    // =====================================================================
    // ÜRÜN GEÇMİŞİ
    // =====================================================================
    public class UrunGecmisiViewModel : DiyalogViewModel
    {
        public UrunGecmisiViewModel(Urun urun)
        {
            Urun = urun;
            Baslik = urun.UrunAdi;
            GirisCommand = new AsyncCommand(async () => { Kapat(false); await AnaViewModel.Ornek.StokIslemi(Urun, HareketTuru.Giris); });
            CikisCommand = new AsyncCommand(async () => { Kapat(false); await AnaViewModel.Ornek.StokIslemi(Urun, HareketTuru.Cikis); });
            _ = YukleAsync();
        }

        public override double Genislik => 760;
        public Urun Urun { get; }
        public UrunSatiri Satir => new(Urun);
        public string AltBaslikMetni => $"{Urun.Marka} · {Urun.Kod}";

        public List<HareketSatiri> Hareketler { get; private set; } = new();
        public IList<GrafikSerisi> Seriler { get; private set; }
        public IList<string> Etiketler { get; private set; }
        public string ToplamGiris { get; private set; }
        public string ToplamCikis { get; private set; }
        public string SonHareket { get; private set; }
        public bool Bos => Hareketler.Count == 0;

        public ICommand GirisCommand { get; }
        public ICommand CikisCommand { get; }

        async Task YukleAsync()
        {
            try
            {
                var liste = await StokServisi.GetirAsync(urunId: Urun.Id);
                Hareketler = liste.Select(h => new HareketSatiri(h)).ToList();
                ToplamGiris = liste.Where(h => h.Tur == HareketTuru.Giris).Sum(h => h.Miktar).ToString("N0", Ui.Tr);
                ToplamCikis = liste.Where(h => h.Tur == HareketTuru.Cikis).Sum(h => -h.Miktar).ToString("N0", Ui.Tr);
                SonHareket = liste.Count > 0 ? GoreceliTarihConverter.Bicimle(liste[0].Tarih) : "—";

                // Son 60 günün gün sonu stok seviyeleri
                var gunler = Enumerable.Range(0, 60).Select(i => DateTime.Today.AddDays(i - 59)).ToList();
                var artan = liste.OrderBy(h => h.Tarih).ToList();
                var seviyeler = gunler.Select(g =>
                {
                    var son = artan.LastOrDefault(h => h.Tarih < g.AddDays(1));
                    return (double)(son?.SonrakiStok ?? 0);
                }).ToArray();
                Seriler = new List<GrafikSerisi> { new() { Ad = "Stok", Renk = "Vurgu", Degerler = seviyeler } };
                Etiketler = gunler.Select(g => g.ToString("d MMM", Ui.Tr)).ToList();
                OnPropertyChanged(string.Empty);
            }
            catch (Exception ex)
            {
                Hata = VeritabaniBaslatici.HatayiAcikla(ex);
            }
        }
    }
}
