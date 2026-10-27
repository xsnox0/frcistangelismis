using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using Frcistan.Altyapi;
using Frcistan.Modeller;
using Frcistan.Servisler;

namespace Frcistan.ViewModels
{
    public class UrunlerViewModel : SayfaViewModel
    {
        public override string Baslik => "Ürünler";

        readonly ObservableCollection<UrunSatiri> _satirlar = new();
        string _arama;
        Secenek _seciliKategori;
        Secenek _seciliMarka;
        string _durum = "Tumu";
        UrunSatiri _secili;
        bool _filtreYukleniyor;

        public UrunlerViewModel()
        {
            Gorunum = CollectionViewSource.GetDefaultView(_satirlar);
            Gorunum.Filter = o => Filtre((UrunSatiri)o);

            YeniCommand = new AsyncCommand(YeniAsync);
            DuzenleCommand = new AsyncCommand(p => DuzenleAsync(p as UrunSatiri ?? Secili));
            KopyalaCommand = new AsyncCommand(p => KopyalaAsync(p as UrunSatiri ?? Secili));
            SilCommand = new AsyncCommand(p => SilAsync(p as UrunSatiri ?? Secili));
            GirisCommand = new AsyncCommand(p => StokAsync(p as UrunSatiri ?? Secili, HareketTuru.Giris));
            CikisCommand = new AsyncCommand(p => StokAsync(p as UrunSatiri ?? Secili, HareketTuru.Cikis));
            SayimCommand = new AsyncCommand(p => StokAsync(p as UrunSatiri ?? Secili, HareketTuru.Duzeltme));
            GecmisCommand = new AsyncCommand(async p =>
            {
                var s = p as UrunSatiri ?? Secili;
                if (s != null) await Diyalog.Goster(new UrunGecmisiViewModel(s.Urun));
            });
            AcCommand = new AsyncCommand(p => Admin ? DuzenleAsync(p as UrunSatiri ?? Secili) : StokAsync(p as UrunSatiri ?? Secili, HareketTuru.Cikis));
            ExcelCommand = new RelayCommand(() => Disari.Excel(RaporServisi.StokListesi(Gorunen.Select(s => s.Urun), FiltreAciklamasi)));
            YazdirCommand = new RelayCommand(() => Disari.Yazdir(RaporServisi.StokListesi(Gorunen.Select(s => s.Urun), FiltreAciklamasi)));
            FiltreTemizleCommand = new RelayCommand(() => FiltreAyarla(null, "Tumu", null));
        }

        public ICollectionView Gorunum { get; }
        IEnumerable<UrunSatiri> Gorunen => Gorunum.Cast<UrunSatiri>();

        public List<Secenek> Kategoriler { get; private set; } = new();
        public List<Secenek> Markalar { get; private set; } = new();

        public string Arama
        {
            get => _arama;
            set { if (Set(ref _arama, value)) Yenile(); }
        }

        public Secenek SeciliKategori
        {
            get => _seciliKategori;
            set { if (Set(ref _seciliKategori, value)) Yenile(); }
        }

        public Secenek SeciliMarka
        {
            get => _seciliMarka;
            set { if (Set(ref _seciliMarka, value)) Yenile(); }
        }

        /// <summary>Tumu / Kritik / Tukendi / Yeterli</summary>
        public string Durum
        {
            get => _durum;
            set { if (Set(ref _durum, value ?? "Tumu")) Yenile(); }
        }

        public UrunSatiri Secili { get => _secili; set => Set(ref _secili, value); }

        public int ToplamSayi => _satirlar.Count;
        public int KritikSayi => _satirlar.Count(s => s.Durum == "Kritik");
        public int TukenenSayi => _satirlar.Count(s => s.Durum == "Tukendi");
        public int GorunenSayi => Gorunen.Count();
        public string OzetMetni
        {
            get
            {
                var g = Gorunen.ToList();
                return $"{g.Count:N0} ürün  ·  {g.Sum(s => s.Adet):N0} adet  ·  Stok değeri {Para.Bicimle(g.Sum(s => s.StokDegeri))}";
            }
        }
        public bool FiltreVar => !string.IsNullOrWhiteSpace(Arama) || Durum != "Tumu" || SeciliKategori?.Deger != null || SeciliMarka?.Deger != null;
        public bool BosMu => _satirlar.Count == 0 && !Yukleniyor;
        public bool SonucYok => _satirlar.Count > 0 && GorunenSayi == 0;

        string FiltreAciklamasi
        {
            get
            {
                var p = new List<string>();
                if (SeciliKategori?.Deger != null) p.Add(SeciliKategori.Ad);
                if (SeciliMarka?.Deger != null) p.Add(SeciliMarka.Ad);
                if (Durum == "Kritik") p.Add("Kritik stok");
                if (Durum == "Tukendi") p.Add("Tükenenler");
                if (!string.IsNullOrWhiteSpace(Arama)) p.Add($"Arama: \"{Arama}\"");
                return string.Join(", ", p);
            }
        }

        public ICommand YeniCommand { get; }
        public ICommand DuzenleCommand { get; }
        public ICommand KopyalaCommand { get; }
        public ICommand SilCommand { get; }
        public ICommand GirisCommand { get; }
        public ICommand CikisCommand { get; }
        public ICommand SayimCommand { get; }
        public ICommand GecmisCommand { get; }
        public ICommand AcCommand { get; }
        public ICommand ExcelCommand { get; }
        public ICommand YazdirCommand { get; }
        public ICommand FiltreTemizleCommand { get; }

        int? _bekleyenKategori;

        /// <summary>Başka sayfalardan filtreli açmak için</summary>
        public void FiltreAyarla(string arama, string durum, int? kategoriId)
        {
            _filtreYukleniyor = true;
            Arama = arama;
            Durum = durum ?? "Tumu";
            SeciliMarka = Markalar.FirstOrDefault();
            _bekleyenKategori = kategoriId;
            SeciliKategori = Kategoriler.FirstOrDefault(k => Equals(k.Deger, kategoriId)) ?? Kategoriler.FirstOrDefault();
            _filtreYukleniyor = false;
            Yenile();
        }

        bool Filtre(UrunSatiri s)
        {
            if (Durum == "Kritik" && !s.KritikMi) return false;
            if (Durum == "Tukendi" && s.Adet > 0) return false;
            if (Durum == "Yeterli" && s.KritikMi) return false;
            if (SeciliKategori?.Deger is int k && s.KategoriId != k) return false;
            if (SeciliMarka?.Deger is string m && !Metin.Ayni(s.Marka, m)) return false;
            return Metin.Eslesir(Arama, s.UrunAdi, s.Marka, s.Kod, s.KategoriAdi, s.Konum, s.Aciklama);
        }

        void Yenile()
        {
            if (_filtreYukleniyor) return;
            Gorunum.Refresh();
            Ozetler();
        }

        void Ozetler()
        {
            foreach (var p in new[] { nameof(ToplamSayi), nameof(KritikSayi), nameof(TukenenSayi), nameof(GorunenSayi), nameof(OzetMetni), nameof(FiltreVar), nameof(BosMu), nameof(SonucYok) })
                OnPropertyChanged(p);
        }

        protected override async Task YukleIcAsync()
        {
            var urunler = await UrunServisi.TumunuGetirAsync();
            var kategoriler = await KategoriServisi.GetirAsync();
            int? seciliId = Secili?.Id;

            _filtreYukleniyor = true;
            var oncekiKategori = _bekleyenKategori ?? SeciliKategori?.Deger as int?;
            var oncekiMarka = SeciliMarka?.Deger as string;
            _bekleyenKategori = null;

            Kategoriler = new List<Secenek> { new() { Deger = null, Ad = "Tüm kategoriler" } }
                .Concat(kategoriler.Select(k => new Secenek { Deger = k.Id, Ad = k.Ad })).ToList();
            Markalar = new List<Secenek> { new() { Deger = null, Ad = "Tüm markalar" } }
                .Concat(urunler.Select(u => u.Marka).Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(m => m, StringComparer.Create(Ui.Tr, true)).Select(m => new Secenek { Deger = m, Ad = m })).ToList();
            OnPropertyChanged(nameof(Kategoriler));
            OnPropertyChanged(nameof(Markalar));
            SeciliKategori = Kategoriler.FirstOrDefault(k => Equals(k.Deger, oncekiKategori)) ?? Kategoriler[0];
            SeciliMarka = Markalar.FirstOrDefault(m => Equals(m.Deger, oncekiMarka)) ?? Markalar[0];

            _satirlar.Clear();
            foreach (var u in urunler) _satirlar.Add(new UrunSatiri(u));
            _filtreYukleniyor = false;

            Gorunum.Refresh();
            Secili = seciliId.HasValue ? _satirlar.FirstOrDefault(s => s.Id == seciliId) : null;
            AltBaslik = $"{urunler.Count:N0} ürün · {urunler.Sum(u => u.Adet):N0} adet stok";
            Ozetler();
        }

        async Task YeniAsync()
        {
            if (!Admin || !await YazabilirMi()) return;
            var vm = new UrunDiyalogViewModel(varsayilanKategoriId: SeciliKategori?.Deger as int?);
            if (await Diyalog.Goster(vm) || vm.EklenenSayisi > 0)
            {
                VeriOlayi.Bildir();
                await YukleAsync();
                if (vm.Kaydedilen != null) Secili = _satirlar.FirstOrDefault(s => s.Id == vm.Kaydedilen.Id);
            }
        }

        async Task DuzenleAsync(UrunSatiri s)
        {
            if (s == null || !Admin || !await YazabilirMi()) return;
            if (await Diyalog.Goster(new UrunDiyalogViewModel(s.Urun)))
            {
                VeriOlayi.Bildir();
                await YukleAsync();
            }
        }

        async Task KopyalaAsync(UrunSatiri s)
        {
            if (s == null || !Admin || !await YazabilirMi()) return;
            var vm = new UrunDiyalogViewModel(kopyalanacak: s.Urun);
            if (await Diyalog.Goster(vm))
            {
                VeriOlayi.Bildir();
                await YukleAsync();
            }
        }

        async Task SilAsync(UrunSatiri s)
        {
            if (s == null || !Admin || !await YazabilirMi()) return;
            int hareket = await UrunServisi.HareketSayisiAsync(s.Id);
            string mesaj = $"\"{s.Marka} {s.UrunAdi}\" kalıcı olarak silinecek.";
            if (hareket > 0) mesaj += $"\n\nBu ürüne ait {hareket} stok hareketi de silinecek. Bu işlem geri alınamaz.";
            if (!await Diyalog.Onayla("Ürünü sil", mesaj, "Sil", "Vazgeç", tehlikeli: true)) return;
            await UrunServisi.SilAsync(s.Id);
            Bildirim.Basari("Ürün silindi", s.UrunAdi);
            VeriOlayi.Bildir();
            await YukleAsync();
        }

        async Task StokAsync(UrunSatiri s, HareketTuru tur)
        {
            if (s == null) return;
            await AnaViewModel.Ornek.StokIslemi(s.Urun, tur);
        }
    }
}
