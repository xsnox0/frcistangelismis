using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using Frcistan.Altyapi;
using Frcistan.Kontroller;
using Frcistan.Modeller;
using Frcistan.Servisler;
using Microsoft.Win32;

namespace Frcistan.ViewModels
{
    // =====================================================================
    // STOK HAREKETLERİ
    // =====================================================================
    public class HareketlerViewModel : SayfaViewModel
    {
        public override string Baslik => "Stok Hareketleri";

        readonly ObservableCollection<HareketSatiri> _satirlar = new();
        string _donem = "30";
        string _tur = "Tumu";
        string _arama;

        public HareketlerViewModel()
        {
            Gorunum = CollectionViewSource.GetDefaultView(_satirlar);
            Gorunum.Filter = o => Filtre((HareketSatiri)o);
            SatisCommand = new AsyncCommand(() => AnaViewModel.Ornek.StokIslemi(null, HareketTuru.Cikis));
            GirisCommand = new AsyncCommand(() => AnaViewModel.Ornek.StokIslemi(null, HareketTuru.Giris));
            ExcelCommand = new RelayCommand(() => Disari.Excel(RaporServisi.Hareketler(Gorunen.Select(s => s.Hareket), DonemAciklamasi)));
            YazdirCommand = new RelayCommand(() => Disari.Yazdir(RaporServisi.Hareketler(Gorunen.Select(s => s.Hareket), DonemAciklamasi)));
            UrunGecmisiCommand = new AsyncCommand(async p =>
            {
                if (p is HareketSatiri h && h.Hareket.Urun != null) await Diyalog.Goster(new UrunGecmisiViewModel(h.Hareket.Urun));
            });
        }

        public ICollectionView Gorunum { get; }
        IEnumerable<HareketSatiri> Gorunen => Gorunum.Cast<HareketSatiri>();

        /// <summary>"1", "7", "30", "90", "365", "Tumu"</summary>
        public string Donem
        {
            get => _donem;
            set { if (Set(ref _donem, value)) _ = YukleAsync(); }
        }

        public string Tur
        {
            get => _tur;
            set { if (Set(ref _tur, value)) Yenile(); }
        }

        public string Arama
        {
            get => _arama;
            set { if (Set(ref _arama, value)) Yenile(); }
        }

        string DonemAciklamasi => Donem switch
        {
            "1" => "Bugün",
            "Tumu" => "Tüm zamanlar",
            _ => $"Son {Donem} gün"
        };

        public string GirisAdet { get; private set; }
        public string GirisTutar { get; private set; }
        public string CikisAdet { get; private set; }
        public string CikisTutar { get; private set; }
        public string DuzeltmeAdet { get; private set; }
        public string HareketSayisi { get; private set; }
        public bool BosMu => !Gorunen.Any();

        public ICommand SatisCommand { get; }
        public ICommand GirisCommand { get; }
        public ICommand ExcelCommand { get; }
        public ICommand YazdirCommand { get; }
        public ICommand UrunGecmisiCommand { get; }

        bool Filtre(HareketSatiri h)
        {
            if (Tur != "Tumu" && h.Tur.ToString() != Tur) return false;
            return Metin.Eslesir(Arama, h.UrunAdi, h.Marka, h.Kod, h.KullaniciAdi, h.Aciklama);
        }

        void Yenile()
        {
            Gorunum.Refresh();
            var g = Gorunen.ToList();
            var giris = g.Where(h => h.Tur == HareketTuru.Giris).ToList();
            var cikis = g.Where(h => h.Tur == HareketTuru.Cikis).ToList();
            GirisAdet = giris.Sum(h => h.Miktar).ToString("N0", Ui.Tr);
            GirisTutar = Para.Bicimle(giris.Sum(h => h.Tutar));
            CikisAdet = cikis.Sum(h => -h.Miktar).ToString("N0", Ui.Tr);
            CikisTutar = Para.Bicimle(cikis.Sum(h => h.Tutar));
            DuzeltmeAdet = g.Count(h => h.Tur == HareketTuru.Duzeltme).ToString("N0", Ui.Tr);
            HareketSayisi = g.Count.ToString("N0", Ui.Tr);
            foreach (var p in new[] { nameof(GirisAdet), nameof(GirisTutar), nameof(CikisAdet), nameof(CikisTutar), nameof(DuzeltmeAdet), nameof(HareketSayisi), nameof(BosMu) })
                OnPropertyChanged(p);
        }

        protected override async Task YukleIcAsync()
        {
            DateTime? bas = Donem == "Tumu" ? null : DateTime.Today.AddDays(1 - int.Parse(Donem));
            var liste = await StokServisi.GetirAsync(bas);
            _satirlar.Clear();
            foreach (var h in liste) _satirlar.Add(new HareketSatiri(h));
            AltBaslik = $"{DonemAciklamasi} · {liste.Count:N0} hareket";
            Yenile();
        }
    }

    // =====================================================================
    // KATEGORİLER
    // =====================================================================
    public class KategoriKarti
    {
        public Kategori Kategori { get; init; }
        public string Ad => Kategori.Ad;
        public string Renk => Kategori.Renk;
        public string Ikon => Kategori.Ikon;
        public int UrunSayisi { get; init; }
        public int ToplamAdet { get; init; }
        public decimal StokDegeri { get; init; }
        public int KritikSayisi { get; init; }
        public double Pay { get; init; }
        public string PayMetni => "%" + (Pay * 100).ToString("0.#", Ui.Tr);
    }

    public class KategorilerViewModel : SayfaViewModel
    {
        public override string Baslik => "Kategoriler";

        public KategorilerViewModel()
        {
            YeniCommand = new AsyncCommand(YeniAsync);
            DuzenleCommand = new AsyncCommand(p => DuzenleAsync(p as KategoriKarti));
            SilCommand = new AsyncCommand(p => SilAsync(p as KategoriKarti));
            UrunleriGorCommand = new RelayCommand(p => { if (p is KategoriKarti k) AnaViewModel.Ornek.UrunlereGit(null, null, k.Kategori.Id); });
            ExcelCommand = new AsyncCommand(async () =>
            {
                Disari.Excel(RaporServisi.KategoriDegerleme(await UrunServisi.TumunuGetirAsync(), Kartlar.Select(k => k.Kategori)));
            });
        }

        public List<KategoriKarti> Kartlar { get; private set; } = new();

        public ICommand YeniCommand { get; }
        public ICommand DuzenleCommand { get; }
        public ICommand SilCommand { get; }
        public ICommand UrunleriGorCommand { get; }
        public ICommand ExcelCommand { get; }

        protected override async Task YukleIcAsync()
        {
            var kategoriler = await KategoriServisi.GetirAsync();
            var urunler = await UrunServisi.TumunuGetirAsync();
            decimal toplam = urunler.Sum(u => u.Adet * u.AlisFiyati);
            Kartlar = kategoriler.Select(k =>
            {
                var ku = urunler.Where(u => u.KategoriId == k.Id).ToList();
                decimal deger = ku.Sum(u => u.Adet * u.AlisFiyati);
                return new KategoriKarti
                {
                    Kategori = k,
                    UrunSayisi = ku.Count,
                    ToplamAdet = ku.Sum(u => u.Adet),
                    StokDegeri = deger,
                    KritikSayisi = ku.Count(u => u.Adet <= u.MinStok),
                    Pay = toplam == 0 ? 0 : (double)(deger / toplam)
                };
            }).ToList();
            AltBaslik = $"{Kartlar.Count} kategori · {urunler.Count:N0} ürün";
            OnPropertyChanged(nameof(Kartlar));
        }

        async Task YeniAsync()
        {
            if (!Admin || !await YazabilirMi()) return;
            if (await Diyalog.Goster(new KategoriDiyalogViewModel(null)))
            {
                VeriOlayi.Bildir();
                await YukleAsync();
            }
        }

        async Task DuzenleAsync(KategoriKarti k)
        {
            if (k == null || !Admin || !await YazabilirMi()) return;
            if (await Diyalog.Goster(new KategoriDiyalogViewModel(k.Kategori)))
            {
                VeriOlayi.Bildir();
                await YukleAsync();
            }
        }

        async Task SilAsync(KategoriKarti k)
        {
            if (k == null || !Admin || !await YazabilirMi()) return;
            int? hedef = null;
            if (k.UrunSayisi > 0)
            {
                var digerleri = Kartlar.Where(x => x.Kategori.Id != k.Kategori.Id)
                    .Select(x => new Secenek { Deger = x.Kategori.Id, Ad = x.Ad }).ToList();
                if (digerleri.Count == 0)
                {
                    await Diyalog.Bilgi("Kategori silinemez", "Bu kategoride ürünler var ve taşınabilecek başka bir kategori yok.", MesajTuru.Uyari);
                    return;
                }
                var secim = new SecimDiyalogViewModel
                {
                    Baslik = "Kategoriyi sil",
                    Mesaj = $"\"{k.Ad}\" kategorisinde {k.UrunSayisi} ürün var. Silmeden önce bu ürünler hangi kategoriye taşınsın?",
                    Secenekler = digerleri,
                    Secili = digerleri.FirstOrDefault(d => d.Ad == "Diğer") ?? digerleri[0],
                    OnayMetni = "Taşı ve sil",
                    Tehlikeli = true
                };
                if (!await Diyalog.Goster(secim)) return;
                hedef = (int)secim.Secili.Deger;
            }
            else if (!await Diyalog.Onayla("Kategoriyi sil", $"\"{k.Ad}\" kategorisi silinecek.", "Sil", "Vazgeç", true))
                return;

            await KategoriServisi.SilAsync(k.Kategori.Id, hedef);
            Bildirim.Basari("Kategori silindi", k.Ad);
            VeriOlayi.Bildir();
            await YukleAsync();
        }
    }

    // =====================================================================
    // RAPORLAR
    // =====================================================================
    public class RaporKarti
    {
        public string Baslik { get; init; }
        public string Aciklama { get; init; }
        public string Ikon { get; init; }
        public string Renk { get; init; }
        public ICommand ExcelCommand { get; init; }
        public ICommand YazdirCommand { get; init; }
    }

    public class RaporlarViewModel : SayfaViewModel
    {
        public override string Baslik => "Raporlar";

        string _donem = "30";
        List<Urun> _urunler = new();
        List<Kategori> _kategoriler = new();
        List<StokHareketi> _hareketler = new();

        public RaporlarViewModel()
        {
            Kartlar = new List<RaporKarti>
            {
                new()
                {
                    Baslik = "Stok listesi", Ikon = "", Renk = "#3D7EFF",
                    Aciklama = "Tüm ürünler; stok, kritik seviye, alış-satış fiyatı ve stok değeri ile.",
                    ExcelCommand = new RelayCommand(() => Disari.Excel(RaporServisi.StokListesi(_urunler))),
                    YazdirCommand = new RelayCommand(() => Disari.Yazdir(RaporServisi.StokListesi(_urunler)))
                },
                new()
                {
                    Baslik = "Sipariş listesi", Ikon = "", Renk = "#F59E0B",
                    Aciklama = "Kritik seviyedeki ve tükenen ürünler, önerilen sipariş adedi ve tahmini tutar.",
                    ExcelCommand = new RelayCommand(() => Disari.Excel(RaporServisi.KritikStok(_urunler))),
                    YazdirCommand = new RelayCommand(() => Disari.Yazdir(RaporServisi.KritikStok(_urunler)))
                },
                new()
                {
                    Baslik = "Stok hareketleri", Ikon = "", Renk = "#10B981",
                    Aciklama = "Seçili dönemdeki tüm giriş, çıkış ve düzeltmeler; kim, ne zaman, kaç adet.",
                    ExcelCommand = new RelayCommand(() => Disari.Excel(RaporServisi.Hareketler(_hareketler, DonemMetni))),
                    YazdirCommand = new RelayCommand(() => Disari.Yazdir(RaporServisi.Hareketler(_hareketler, DonemMetni)))
                },
                new()
                {
                    Baslik = "Kategori değerlemesi", Ikon = "", Renk = "#8B5CF6",
                    Aciklama = "Kategori bazında ürün sayısı, alış ve satış değeri, potansiyel kâr ve pay.",
                    ExcelCommand = new RelayCommand(() => Disari.Excel(RaporServisi.KategoriDegerleme(_urunler, _kategoriler))),
                    YazdirCommand = new RelayCommand(() => Disari.Yazdir(RaporServisi.KategoriDegerleme(_urunler, _kategoriler)))
                },
                new()
                {
                    Baslik = "Sayım formu", Ikon = "", Renk = "#06B6D4",
                    Aciklama = "Raf sırasına göre yazdırılabilir sayım formu; sayılan adet ve fark için boş sütunlar.",
                    ExcelCommand = new RelayCommand(() => Disari.Excel(RaporServisi.SayimFormu(_urunler))),
                    YazdirCommand = new RelayCommand(() => Disari.Yazdir(RaporServisi.SayimFormu(_urunler)))
                },
                new()
                {
                    Baslik = "Tam dışa aktarım", Ikon = "", Renk = "#EC4899",
                    Aciklama = "Stok listesi, hareketler ve kategori raporu tek Excel dosyasında, ayrı sayfalarda.",
                    ExcelCommand = new RelayCommand(TamAktarim),
                    YazdirCommand = null
                }
            };
        }

        public List<RaporKarti> Kartlar { get; }

        public string Donem
        {
            get => _donem;
            set { if (Set(ref _donem, value)) _ = YukleAsync(); }
        }

        string DonemMetni => Donem == "Tumu" ? "Tüm zamanlar" : $"Son {Donem} gün";

        public string SatisTutari { get; private set; }
        public string SatisAdedi { get; private set; }
        public string AlimTutari { get; private set; }
        public string BrutKar { get; private set; }
        public List<CubukOgesi> EnCokSatanlar { get; private set; } = new();
        public IList<GrafikDilimi> KategoriSatislari { get; private set; }
        public string KategoriToplam { get; private set; }
        public bool SatisYok { get; private set; }

        void TamAktarim()
        {
            var dlg = new SaveFileDialog
            {
                Filter = "Excel Çalışma Kitabı (*.xlsx)|*.xlsx",
                FileName = $"Frcistan-Tam-Rapor-{DateTime.Now:yyyy-MM-dd}.xlsx",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            if (dlg.ShowDialog() != true) return;
            ExcelYazici.Yaz(dlg.FileName,
                RaporServisi.StokListesi(_urunler),
                RaporServisi.Hareketler(_hareketler, DonemMetni),
                RaporServisi.KategoriDegerleme(_urunler, _kategoriler),
                RaporServisi.KritikStok(_urunler));
            Bildirim.DosyaKaydedildi("Excel dosyası kaydedildi", dlg.FileName);
        }

        protected override async Task YukleIcAsync()
        {
            _urunler = await UrunServisi.TumunuGetirAsync();
            _kategoriler = await KategoriServisi.GetirAsync();
            DateTime? bas = Donem == "Tumu" ? null : DateTime.Today.AddDays(1 - int.Parse(Donem));
            _hareketler = await StokServisi.GetirAsync(bas);

            var cikis = _hareketler.Where(h => h.Tur == HareketTuru.Cikis).ToList();
            var giris = _hareketler.Where(h => h.Tur == HareketTuru.Giris && h.Aciklama != "Açılış stoğu").ToList();
            decimal satis = cikis.Sum(h => -h.Miktar * h.BirimFiyat);
            decimal maliyet = cikis.Sum(h => -h.Miktar * (h.Urun?.AlisFiyati ?? 0));
            SatisTutari = Para.Bicimle(satis);
            SatisAdedi = cikis.Sum(h => -h.Miktar).ToString("N0", Ui.Tr) + " adet";
            AlimTutari = Para.Bicimle(giris.Sum(h => h.Miktar * h.BirimFiyat));
            BrutKar = Para.Bicimle(satis - maliyet);
            SatisYok = cikis.Count == 0;

            var satanlar = cikis.GroupBy(h => h.UrunId)
                .Select(g => new { U = g.First().Urun, Adet = g.Sum(h => -h.Miktar), Tutar = g.Sum(h => -h.Miktar * h.BirimFiyat) })
                .OrderByDescending(x => x.Tutar).Take(8).ToList();
            decimal enBuyuk = satanlar.FirstOrDefault()?.Tutar ?? 1;
            EnCokSatanlar = satanlar.Select(x => new CubukOgesi
            {
                Ad = x.U?.UrunAdi ?? "?",
                AltMetin = $"{x.U?.Marka} · {x.Adet} adet",
                DegerMetni = Para.Bicimle(x.Tutar),
                Oran = enBuyuk == 0 ? 0 : (double)(x.Tutar / enBuyuk),
                Renk = x.U?.Kategori?.Renk ?? "#3D7EFF"
            }).ToList();

            KategoriSatislari = cikis.Where(h => h.Urun?.Kategori != null).GroupBy(h => h.Urun.Kategori.Id)
                .Select(g => new GrafikDilimi
                {
                    Ad = g.First().Urun.Kategori.Ad,
                    Renk = g.First().Urun.Kategori.Renk,
                    Deger = (double)g.Sum(h => -h.Miktar * h.BirimFiyat),
                    DegerMetni = Para.Bicimle(g.Sum(h => -h.Miktar * h.BirimFiyat))
                }).OrderByDescending(d => d.Deger).ToList();
            KategoriToplam = Para.Bicimle(satis);

            AltBaslik = $"{DonemMetni} · {cikis.Count:N0} satış hareketi";
            OnPropertyChanged(string.Empty);
        }
    }

    // =====================================================================
    // KULLANICILAR
    // =====================================================================
    public class KullaniciSatiri
    {
        public Kullanici Kullanici { get; init; }
        public string GorunenAd => Kullanici.GorunenAd;
        public string KullaniciAdi => Kullanici.KullaniciAdi;
        public string RolMetni => Roller.Goster(Kullanici.Rol);
        public bool Yonetici => Kullanici.Rol == Roller.Admin;
        public bool Aktif => Kullanici.Aktif;
        public bool Ben => Kullanici.Id == Oturum.Kullanici?.Id;
        public string SonGirisMetni => Kullanici.SonGiris.HasValue ? GoreceliTarihConverter.Bicimle(Kullanici.SonGiris.Value) : "Hiç giriş yapmadı";
        public string Renk => Yonetici ? "#3D7EFF" : "#10B981";
    }

    public class KullanicilarViewModel : SayfaViewModel
    {
        public override string Baslik => "Kullanıcılar";

        public KullanicilarViewModel()
        {
            YeniCommand = new AsyncCommand(async () =>
            {
                if (!await YazabilirMi()) return;
                if (await Diyalog.Goster(new KullaniciDiyalogViewModel(null))) await YukleAsync();
            });
            DuzenleCommand = new AsyncCommand(async p =>
            {
                if (p is not KullaniciSatiri s || !await YazabilirMi()) return;
                if (await Diyalog.Goster(new KullaniciDiyalogViewModel(s.Kullanici))) await YukleAsync();
            });
            SilCommand = new AsyncCommand(async p =>
            {
                if (p is not KullaniciSatiri s || !await YazabilirMi()) return;
                if (!await Diyalog.Onayla("Kullanıcıyı sil", $"{s.GorunenAd} ({s.KullaniciAdi}) silinecek. Geçmiş stok hareketlerinde adı görünmeye devam eder.", "Sil", "Vazgeç", true)) return;
                await KullaniciServisi.SilAsync(s.Kullanici.Id);
                Bildirim.Basari("Kullanıcı silindi", s.GorunenAd);
                await YukleAsync();
            });
        }

        public List<KullaniciSatiri> Kullanicilar { get; private set; } = new();
        public ICommand YeniCommand { get; }
        public ICommand DuzenleCommand { get; }
        public ICommand SilCommand { get; }

        protected override async Task YukleIcAsync()
        {
            Kullanicilar = (await KullaniciServisi.GetirAsync()).Select(k => new KullaniciSatiri { Kullanici = k }).ToList();
            AltBaslik = $"{Kullanicilar.Count} kullanıcı · {Kullanicilar.Count(k => k.Yonetici)} yönetici";
            OnPropertyChanged(nameof(Kullanicilar));
        }
    }
}
