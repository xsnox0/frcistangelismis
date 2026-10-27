using Frcistan.Altyapi;
using Frcistan.Modeller;

namespace Frcistan.Servisler
{
    /// <summary>Hazır raporlar: stok listesi, kritik stok, hareketler, kategori değerlemesi, sayım formu</summary>
    public static class RaporServisi
    {
        static string Firma => string.IsNullOrWhiteSpace(FirmaAyarlari.FirmaAdi) ? "Frcistan" : FirmaAyarlari.FirmaAdi;
        static string Bugun => DateTime.Now.ToString("dd.MM.yyyy HH:mm", Ui.Tr);
        static string DosyaTarihi => DateTime.Now.ToString("yyyy-MM-dd");

        public static string Durum(Urun u) => u.Adet <= 0 ? "Tükendi" : u.Adet <= u.MinStok ? "Kritik" : "Yeterli";

        public static Rapor StokListesi(IEnumerable<Urun> urunler, string filtreAciklamasi = null)
        {
            var liste = urunler.ToList();
            var r = new Rapor
            {
                Baslik = "Stok Listesi",
                AltBaslik = $"{Firma} · {liste.Count} ürün · {Bugun}" + (string.IsNullOrWhiteSpace(filtreAciklamasi) ? "" : " · " + filtreAciklamasi),
                SayfaAdi = "Stok Listesi",
                DosyaAdi = $"Frcistan-Stok-Listesi-{DosyaTarihi}",
                Yatay = true
            };
            r.Sutun("Kod", genislik: 12, oran: 0.9)
             .Sutun("Ürün", genislik: 40, oran: 3)
             .Sutun("Marka", genislik: 22, oran: 1.5)
             .Sutun("Kategori", genislik: 18, oran: 1.3)
             .Sutun("Konum", genislik: 9, oran: 0.7)
             .Sutun("Stok", SutunTuru.Tamsayi, 9, 0.6)
             .Sutun("Kritik", SutunTuru.Tamsayi, 9, 0.6)
             .Sutun("Alış", SutunTuru.Para, 12, 0.9)
             .Sutun("Satış", SutunTuru.Para, 12, 0.9)
             .Sutun("Stok Değeri", SutunTuru.Para, 14, 1.1)
             .Sutun("Durum", genislik: 10, oran: 0.8);

            foreach (var u in liste)
                r.Satir(u.Kod, u.UrunAdi, u.Marka, u.Kategori?.Ad, u.Konum, u.Adet, u.MinStok, u.AlisFiyati, u.SatisFiyati, u.Adet * u.AlisFiyati, Durum(u));

            r.Ozet.Add(("Ürün çeşidi", liste.Count.ToString("N0", Ui.Tr)));
            r.Ozet.Add(("Toplam stok", liste.Sum(u => u.Adet).ToString("N0", Ui.Tr) + " adet"));
            r.Ozet.Add(("Stok değeri (alış)", Para.Bicimle(liste.Sum(u => u.Adet * u.AlisFiyati))));
            r.Ozet.Add(("Stok değeri (satış)", Para.Bicimle(liste.Sum(u => u.Adet * u.SatisFiyati))));
            return r;
        }

        public static Rapor KritikStok(IEnumerable<Urun> urunler)
        {
            var liste = urunler.Where(u => u.Adet <= u.MinStok)
                .OrderBy(u => u.Adet > 0).ThenBy(u => u.Adet - u.MinStok).ToList();
            var r = new Rapor
            {
                Baslik = "Kritik Stok ve Sipariş Listesi",
                AltBaslik = $"{Firma} · {liste.Count} ürün kritik seviyede ya da tükendi · {Bugun}",
                SayfaAdi = "Sipariş Listesi",
                DosyaAdi = $"Frcistan-Siparis-Listesi-{DosyaTarihi}"
            };
            r.Sutun("Kod", genislik: 12, oran: 0.9)
             .Sutun("Ürün", genislik: 40, oran: 3)
             .Sutun("Marka", genislik: 22, oran: 1.5)
             .Sutun("Stok", SutunTuru.Tamsayi, 9, 0.6)
             .Sutun("Kritik", SutunTuru.Tamsayi, 9, 0.6)
             .Sutun("Önerilen Sipariş", SutunTuru.Tamsayi, 16, 1)
             .Sutun("Birim Alış", SutunTuru.Para, 12, 0.9)
             .Sutun("Tahmini Tutar", SutunTuru.Para, 14, 1);

            decimal toplam = 0;
            foreach (var u in liste)
            {
                int oneri = Math.Max(1, u.MinStok * 2 - u.Adet);
                toplam += oneri * u.AlisFiyati;
                r.Satir(u.Kod, u.UrunAdi, u.Marka, u.Adet, u.MinStok, oneri, u.AlisFiyati, oneri * u.AlisFiyati);
            }
            r.Ozet.Add(("Tükenen ürün", liste.Count(u => u.Adet <= 0).ToString()));
            r.Ozet.Add(("Kritik seviyedeki ürün", liste.Count(u => u.Adet > 0).ToString()));
            r.Ozet.Add(("Tahmini sipariş tutarı", Para.Bicimle(toplam)));
            return r;
        }

        public static Rapor Hareketler(IEnumerable<StokHareketi> hareketler, string donemAciklamasi)
        {
            var liste = hareketler.ToList();
            var r = new Rapor
            {
                Baslik = "Stok Hareketleri",
                AltBaslik = $"{Firma} · {donemAciklamasi} · {liste.Count} hareket · {Bugun}",
                SayfaAdi = "Hareketler",
                DosyaAdi = $"Frcistan-Stok-Hareketleri-{DosyaTarihi}",
                Yatay = true
            };
            r.Sutun("Tarih", SutunTuru.Tarih, 17, 1.2)
             .Sutun("Tür", genislik: 11, oran: 0.8)
             .Sutun("Kod", genislik: 12, oran: 0.9)
             .Sutun("Ürün", genislik: 38, oran: 2.6)
             .Sutun("Miktar", SutunTuru.Tamsayi, 9, 0.6)
             .Sutun("Önceki", SutunTuru.Tamsayi, 9, 0.6)
             .Sutun("Sonraki", SutunTuru.Tamsayi, 9, 0.6)
             .Sutun("Birim Fiyat", SutunTuru.Para, 12, 0.9)
             .Sutun("Tutar", SutunTuru.Para, 13, 0.9)
             .Sutun("Kullanıcı", genislik: 18, oran: 1.1)
             .Sutun("Açıklama", genislik: 34, oran: 1.8);

            foreach (var h in liste)
                r.Satir(h.Tarih, TurAdi(h.Tur), h.Urun?.Kod, h.Urun == null ? "" : $"{h.Urun.Marka} {h.Urun.UrunAdi}", h.Miktar, h.OncekiStok, h.SonrakiStok,
                    h.BirimFiyat, Math.Abs(h.Miktar) * h.BirimFiyat, h.KullaniciAdi, h.Aciklama);

            var giris = liste.Where(h => h.Tur == HareketTuru.Giris).ToList();
            var cikis = liste.Where(h => h.Tur == HareketTuru.Cikis).ToList();
            r.Ozet.Add(("Toplam giriş", $"{giris.Sum(h => h.Miktar):N0} adet · {Para.Bicimle(giris.Sum(h => h.Miktar * h.BirimFiyat))}"));
            r.Ozet.Add(("Toplam çıkış (satış)", $"{cikis.Sum(h => -h.Miktar):N0} adet · {Para.Bicimle(cikis.Sum(h => -h.Miktar * h.BirimFiyat))}"));
            return r;
        }

        public static Rapor KategoriDegerleme(IEnumerable<Urun> urunler, IEnumerable<Kategori> kategoriler)
        {
            var u = urunler.ToList();
            var r = new Rapor
            {
                Baslik = "Kategori Değerleme Raporu",
                AltBaslik = $"{Firma} · {Bugun}",
                SayfaAdi = "Kategoriler",
                DosyaAdi = $"Frcistan-Kategori-Degerleme-{DosyaTarihi}"
            };
            r.Sutun("Kategori", genislik: 24, oran: 2)
             .Sutun("Ürün Çeşidi", SutunTuru.Tamsayi, 12, 1)
             .Sutun("Toplam Stok", SutunTuru.Tamsayi, 12, 1)
             .Sutun("Kritik / Tükenen", SutunTuru.Tamsayi, 16, 1.1)
             .Sutun("Alış Değeri", SutunTuru.Para, 15, 1.2)
             .Sutun("Satış Değeri", SutunTuru.Para, 15, 1.2)
             .Sutun("Potansiyel Kâr", SutunTuru.Para, 15, 1.2)
             .Sutun("Pay", SutunTuru.Yuzde, 9, 0.7);

            decimal genelToplam = u.Sum(x => x.Adet * x.AlisFiyati);
            foreach (var k in kategoriler)
            {
                var ku = u.Where(x => x.KategoriId == k.Id).ToList();
                decimal alis = ku.Sum(x => x.Adet * x.AlisFiyati);
                decimal satis = ku.Sum(x => x.Adet * x.SatisFiyati);
                r.Satir(k.Ad, ku.Count, ku.Sum(x => x.Adet), ku.Count(x => x.Adet <= x.MinStok), alis, satis, satis - alis,
                    genelToplam == 0 ? 0m : alis / genelToplam);
            }
            r.Ozet.Add(("Toplam alış değeri", Para.Bicimle(genelToplam)));
            r.Ozet.Add(("Toplam satış değeri", Para.Bicimle(u.Sum(x => x.Adet * x.SatisFiyati))));
            return r;
        }

        public static Rapor SayimFormu(IEnumerable<Urun> urunler)
        {
            var liste = urunler.OrderBy(x => x.Konum ?? "~").ThenBy(x => x.UrunAdi).ToList();
            var r = new Rapor
            {
                Baslik = "Stok Sayım Formu",
                AltBaslik = $"{Firma} · Raf sırasına göre · {liste.Count} ürün · Sayımı yapan: ................................",
                SayfaAdi = "Sayım",
                DosyaAdi = $"Frcistan-Sayim-Formu-{DosyaTarihi}"
            };
            r.Sutun("Konum", genislik: 9, oran: 0.7)
             .Sutun("Kod", genislik: 12, oran: 0.9)
             .Sutun("Ürün", genislik: 44, oran: 3.2)
             .Sutun("Sistem", SutunTuru.Tamsayi, 9, 0.7)
             .Sutun("Sayılan", genislik: 12, oran: 0.9)
             .Sutun("Fark", genislik: 10, oran: 0.7);
            foreach (var u in liste)
                r.Satir(u.Konum, u.Kod, $"{u.Marka} · {u.UrunAdi}", u.Adet, "", "");
            return r;
        }

        public static string TurAdi(HareketTuru t) => t switch
        {
            HareketTuru.Giris => "Giriş",
            HareketTuru.Cikis => "Çıkış",
            _ => "Düzeltme"
        };
    }
}
