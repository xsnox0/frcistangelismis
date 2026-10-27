using Frcistan.Altyapi;
using Frcistan.Modeller;
using Frcistan.Servisler;
using Microsoft.Win32;

namespace Frcistan.ViewModels
{
    /// <summary>Veri değiştiğinde (ürün, stok, kategori) haber verir; menü rozetleri ve bildirimler yenilenir.</summary>
    public static class VeriOlayi
    {
        public static event Action Degisti;
        public static void Bildir() => Degisti?.Invoke();
    }

    public class MenuOgesi : ObservableObject
    {
        int _rozet;
        public string Anahtar { get; init; }
        public string Baslik { get; init; }
        public string Ikon { get; init; }
        public Func<SayfaViewModel> Olustur { get; init; }
        public int Rozet { get => _rozet; set => Set(ref _rozet, value); }

        SayfaViewModel _sayfa;
        public SayfaViewModel Sayfa => _sayfa ??= Olustur();
    }

    public class Secenek
    {
        public object Deger { get; init; }
        public string Ad { get; init; }
        public override string ToString() => Ad;
    }

    /// <summary>Çubuk listesi öğesi (ör. marka dağılımı)</summary>
    public class CubukOgesi
    {
        public string Ad { get; init; }
        public string AltMetin { get; init; }
        public string DegerMetni { get; init; }
        public double Oran { get; init; }
        public string Renk { get; init; } = "#3D7EFF";
    }

    /// <summary>Ürün listesindeki bir satır</summary>
    public class UrunSatiri
    {
        public UrunSatiri(Urun u) { Urun = u; }

        public Urun Urun { get; }
        public int Id => Urun.Id;
        public string Kod => Urun.Kod;
        public string UrunAdi => Urun.UrunAdi;
        public string Marka => Urun.Marka;
        public string Konum => string.IsNullOrWhiteSpace(Urun.Konum) ? "—" : Urun.Konum;
        public string Aciklama => Urun.Aciklama;
        public int KategoriId => Urun.KategoriId;
        public string KategoriAdi => Urun.Kategori?.Ad ?? "—";
        public string KategoriRengi => Urun.Kategori?.Renk ?? "#64748B";
        public string KategoriIkon => Urun.Kategori?.Ikon ?? "E7B8";
        public int Adet => Urun.Adet;
        public int MinStok => Urun.MinStok;
        public decimal AlisFiyati => Urun.AlisFiyati;
        public decimal SatisFiyati => Urun.SatisFiyati;
        public decimal StokDegeri => Urun.Adet * Urun.AlisFiyati;

        public string Durum => Urun.Adet <= 0 ? "Tukendi" : Urun.Adet <= Urun.MinStok ? "Kritik" : "Yeterli";
        public string DurumMetni => Durum switch { "Tukendi" => "Tükendi", "Kritik" => "Kritik", _ => "Yeterli" };
        public bool KritikMi => Urun.Adet <= Urun.MinStok;

        /// <summary>Stok çubuğu doluluğu: kritik seviyenin 3 katı = dolu</summary>
        public double StokOrani => Urun.Adet <= 0 ? 0 : Math.Min(1, Urun.Adet / (double)Math.Max(1, Urun.MinStok * 3));

        public string MarjMetni => Urun.AlisFiyati > 0 && Urun.SatisFiyati > 0
            ? "%" + ((Urun.SatisFiyati - Urun.AlisFiyati) / Urun.AlisFiyati * 100).ToString("0", Ui.Tr) + " kâr"
            : "";

        public string EksikMetni => Urun.Adet <= 0 ? "Stokta yok" : $"{Urun.Adet} / {Urun.MinStok} adet";
    }

    /// <summary>Stok hareketi satırı</summary>
    public class HareketSatiri
    {
        public HareketSatiri(StokHareketi h) { Hareket = h; }

        public StokHareketi Hareket { get; }
        public DateTime Tarih => Hareket.Tarih;
        public string TarihMetni => Hareket.Tarih.ToString("dd.MM.yyyy", Ui.Tr);
        public string SaatMetni => Hareket.Tarih.ToString("HH:mm", Ui.Tr);
        public string GoreceliTarih => GoreceliTarihConverter.Bicimle(Hareket.Tarih);
        public HareketTuru Tur => Hareket.Tur;
        public string TurMetni => RaporServisi.TurAdi(Hareket.Tur);
        public string TurIkon => Hareket.Tur switch { HareketTuru.Giris => "", HareketTuru.Cikis => "", _ => "" };
        public string UrunAdi => Hareket.Urun?.UrunAdi ?? "(silinmiş ürün)";
        public string Marka => Hareket.Urun?.Marka ?? "";
        public string Kod => Hareket.Urun?.Kod ?? "";
        public string KategoriRengi => Hareket.Urun?.Kategori?.Renk ?? "#64748B";
        public int Miktar => Hareket.Miktar;
        public string MiktarMetni => (Hareket.Miktar > 0 ? "+" : Hareket.Miktar < 0 ? "−" : "") + Math.Abs(Hareket.Miktar).ToString("N0", Ui.Tr);
        public bool Artis => Hareket.Miktar > 0;
        public string StokGecisi => $"{Hareket.OncekiStok:N0} → {Hareket.SonrakiStok:N0}";
        public decimal BirimFiyat => Hareket.BirimFiyat;
        public decimal Tutar => Math.Abs(Hareket.Miktar) * Hareket.BirimFiyat;
        public string KullaniciAdi => string.IsNullOrWhiteSpace(Hareket.KullaniciAdi) ? "—" : Hareket.KullaniciAdi;
        public string Aciklama => string.IsNullOrWhiteSpace(Hareket.Aciklama) ? "" : Hareket.Aciklama;
    }

    /// <summary>Excel'e kaydetme ve yazdırma</summary>
    public static class Disari
    {
        public static void Excel(Rapor r)
        {
            var dlg = new SaveFileDialog
            {
                Title = "Excel dosyası olarak kaydet",
                Filter = "Excel Çalışma Kitabı (*.xlsx)|*.xlsx",
                FileName = r.DosyaAdi + ".xlsx",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                ExcelYazici.Yaz(dlg.FileName, r);
                Bildirim.DosyaKaydedildi("Excel dosyası kaydedildi", dlg.FileName);
            }
            catch (IOException ex)
            {
                _ = Diyalog.Bilgi("Dosya kaydedilemedi", "Dosya başka bir programda (ör. Excel'de) açık olabilir. Kapatıp tekrar deneyin.\n\n" + ex.Message, MesajTuru.Uyari);
            }
        }

        public static void Yazdir(Rapor r)
        {
            if (Yazdirici.Yazdir(r))
                Bildirim.Basari("Yazdırma başlatıldı", r.Baslik);
        }
    }
}
