using System.Windows.Input;
using Frcistan.Altyapi;
using Frcistan.Kontroller;
using Frcistan.Modeller;
using Frcistan.Servisler;

namespace Frcistan.ViewModels
{
    /// <summary>Genel bakış: özet kartlar, 30 günlük hareket grafiği, kategori dağılımı, kritik stoklar, son hareketler</summary>
    public class PanelViewModel : SayfaViewModel
    {
        public override string Baslik => "Genel Bakış";

        public PanelViewModel()
        {
            StokGirisiCommand = new AsyncCommand(async p => { if (p is UrunSatiri u) await AnaViewModel.Ornek.StokIslemi(u.Urun, HareketTuru.Giris); });
            KritikleriGorCommand = new RelayCommand(() => AnaViewModel.Ornek.UrunlereGit(null, "Kritik"));
            HareketleriGorCommand = new RelayCommand(() => AnaViewModel.Ornek.Git("Hareketler"));
            SatisCommand = new AsyncCommand(() => AnaViewModel.Ornek.StokIslemi(null, HareketTuru.Cikis));
            GirisCommand = new AsyncCommand(() => AnaViewModel.Ornek.StokIslemi(null, HareketTuru.Giris));
            YeniUrunCommand = new AsyncCommand(YeniUrunAsync);
        }

        int _donem = 30;
        public int Donem
        {
            get => _donem;
            set { if (Set(ref _donem, value)) _ = YukleAsync(); }
        }

        public string DonemMetni => $"Son {Donem} gün";

        public string Selamlama
        {
            get
            {
                int s = DateTime.Now.Hour;
                string sel = s < 6 ? "İyi geceler" : s < 12 ? "Günaydın" : s < 18 ? "İyi günler" : "İyi akşamlar";
                string ad = Oturum.Kullanici?.GorunenAd?.Split(' ')[0] ?? "";
                return $"{sel}, {ad}";
            }
        }

        // ---- Özet kartlar ----
        public int UrunCesidi { get; private set; }
        public int ToplamAdet { get; private set; }
        public string StokDegeri { get; private set; }
        public string SatisDegeri { get; private set; }
        public string PotansiyelKar { get; private set; }
        public int KritikSayisi { get; private set; }
        public int TukenenSayisi { get; private set; }
        public string DonemSatis { get; private set; }
        public int DonemSatisAdet { get; private set; }
        public string DonemDegisim { get; private set; }
        public bool DonemArtis { get; private set; }
        public bool DonemDegisimVar { get; private set; }

        // ---- Grafikler ----
        public IList<GrafikSerisi> Seriler { get; private set; }
        public IList<string> Etiketler { get; private set; }
        public IList<GrafikDilimi> Dilimler { get; private set; }
        public List<CubukOgesi> KategoriListesi { get; private set; } = new();
        public List<CubukOgesi> EnCokSatanlar { get; private set; } = new();

        // ---- Listeler ----
        public List<UrunSatiri> KritikUrunler { get; private set; } = new();
        public List<HareketSatiri> SonHareketler { get; private set; } = new();

        public ICommand StokGirisiCommand { get; }
        public ICommand KritikleriGorCommand { get; }
        public ICommand HareketleriGorCommand { get; }
        public ICommand SatisCommand { get; }
        public ICommand GirisCommand { get; }
        public ICommand YeniUrunCommand { get; }

        protected override async Task YukleIcAsync()
        {
            var urunler = await UrunServisi.TumunuGetirAsync();
            var kategoriler = await KategoriServisi.GetirAsync();
            var bugun = DateTime.Today;
            var hareketler = await StokServisi.GetirAsync(bugun.AddDays(-(Donem * 2 - 1)));

            UrunCesidi = urunler.Count;
            ToplamAdet = urunler.Sum(u => u.Adet);
            decimal alis = urunler.Sum(u => u.Adet * u.AlisFiyati);
            decimal satis = urunler.Sum(u => u.Adet * u.SatisFiyati);
            StokDegeri = Para.Bicimle(alis);
            SatisDegeri = Para.Bicimle(satis);
            PotansiyelKar = Para.Bicimle(satis - alis);
            KritikSayisi = urunler.Count(u => u.Adet > 0 && u.Adet <= u.MinStok);
            TukenenSayisi = urunler.Count(u => u.Adet <= 0);

            // Dönem satışları ve önceki dönemle karşılaştırma
            var donemBasi = bugun.AddDays(-(Donem - 1));
            var buDonem = hareketler.Where(h => h.Tur == HareketTuru.Cikis && h.Tarih >= donemBasi).ToList();
            var oncekiDonem = hareketler.Where(h => h.Tur == HareketTuru.Cikis && h.Tarih < donemBasi).ToList();
            decimal buTutar = buDonem.Sum(h => -h.Miktar * h.BirimFiyat);
            decimal oncekiTutar = oncekiDonem.Sum(h => -h.Miktar * h.BirimFiyat);
            DonemSatis = Para.Bicimle(buTutar);
            DonemSatisAdet = buDonem.Sum(h => -h.Miktar);
            DonemDegisimVar = oncekiTutar > 0;
            if (oncekiTutar > 0)
            {
                decimal oran = (buTutar - oncekiTutar) / oncekiTutar * 100;
                DonemArtis = oran >= 0;
                DonemDegisim = (oran >= 0 ? "+" : "−") + "%" + Math.Abs(oran).ToString("0.#", Ui.Tr);
            }

            // Günlük giriş / çıkış grafiği
            var gunler = Enumerable.Range(0, Donem).Select(i => donemBasi.AddDays(i)).ToList();
            var girisler = gunler.Select(g => (double)hareketler.Where(h => h.Tur == HareketTuru.Giris && h.Tarih.Date == g).Sum(h => h.Miktar)).ToArray();
            var cikislar = gunler.Select(g => (double)hareketler.Where(h => h.Tur == HareketTuru.Cikis && h.Tarih.Date == g).Sum(h => -h.Miktar)).ToArray();
            Seriler = new List<GrafikSerisi>
            {
                new() { Ad = "Giriş", Renk = "Vurgu", Degerler = girisler },
                new() { Ad = "Çıkış", Renk = "#F59E0B", Degerler = cikislar }
            };
            Etiketler = gunler.Select(g => g.ToString("d MMM", Ui.Tr)).ToList();

            // Kategoriye göre stok değeri
            var dagilim = kategoriler
                .Select(k => new { K = k, Deger = urunler.Where(u => u.KategoriId == k.Id).Sum(u => u.Adet * u.AlisFiyati) })
                .Where(x => x.Deger > 0).OrderByDescending(x => x.Deger).ToList();
            Dilimler = dagilim.Select(x => new GrafikDilimi { Ad = x.K.Ad, Deger = (double)x.Deger, Renk = x.K.Renk, DegerMetni = Para.Kisa(x.Deger) }).ToList();
            KategoriListesi = dagilim.Take(5).Select(x => new CubukOgesi
            {
                Ad = x.K.Ad,
                Renk = x.K.Renk,
                DegerMetni = Para.Kisa(x.Deger),
                Oran = alis == 0 ? 0 : (double)(x.Deger / alis),
                AltMetin = "%" + (alis == 0 ? 0 : x.Deger / alis * 100).ToString("0", Ui.Tr)
            }).ToList();

            // Dönemde en çok satanlar
            var satanlar = buDonem.GroupBy(h => h.UrunId)
                .Select(g => new { Urun = g.First().Urun, Adet = g.Sum(h => -h.Miktar), Tutar = g.Sum(h => -h.Miktar * h.BirimFiyat) })
                .OrderByDescending(x => x.Tutar).Take(5).ToList();
            decimal enBuyuk = satanlar.FirstOrDefault()?.Tutar ?? 1;
            EnCokSatanlar = satanlar.Select(x => new CubukOgesi
            {
                Ad = x.Urun?.UrunAdi ?? "?",
                AltMetin = $"{x.Adet} adet",
                DegerMetni = Para.Kisa(x.Tutar),
                Oran = enBuyuk == 0 ? 0 : (double)(x.Tutar / enBuyuk),
                Renk = x.Urun?.Kategori?.Renk ?? "#3D7EFF"
            }).ToList();

            KritikUrunler = urunler.Where(u => u.Adet <= u.MinStok)
                .OrderBy(u => u.Adet > 0).ThenBy(u => u.Adet - u.MinStok).Take(6).Select(u => new UrunSatiri(u)).ToList();
            SonHareketler = hareketler.Take(7).Select(h => new HareketSatiri(h)).ToList();

            AltBaslik = DateTime.Now.ToString("d MMMM yyyy, dddd", Ui.Tr);
            OnPropertyChanged(string.Empty);
        }

        async Task YeniUrunAsync()
        {
            if (!await YazabilirMi()) return;
            if (await Diyalog.Goster(new UrunDiyalogViewModel()))
            {
                VeriOlayi.Bildir();
                await YukleAsync();
            }
        }
    }
}
