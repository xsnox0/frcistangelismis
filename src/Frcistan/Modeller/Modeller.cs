using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Frcistan.Modeller
{
    public class Kategori
    {
        public int Id { get; set; }

        [Required, MaxLength(60)]
        public string Ad { get; set; }

        /// <summary>Kart ve etiketlerde kullanılan renk (#RRGGBB)</summary>
        [MaxLength(9)]
        public string Renk { get; set; } = "#3D7EFF";

        /// <summary>Segoe Fluent Icons karakter kodu (ör. "E7B8")</summary>
        [MaxLength(8)]
        public string Ikon { get; set; } = "E7B8";

        public int Sira { get; set; }

        [JsonIgnore]
        public List<Urun> Urunler { get; set; } = new();
    }

    public class Urun
    {
        public int Id { get; set; }

        /// <summary>Ürün kodu (stok kodu). Boş bırakılırsa FRC-0001 biçiminde otomatik verilir.</summary>
        [MaxLength(30)]
        public string Kod { get; set; }

        public int KategoriId { get; set; }
        [JsonIgnore]
        public Kategori Kategori { get; set; }

        [Required, MaxLength(60)]
        public string Marka { get; set; }

        [Required, MaxLength(150)]
        public string UrunAdi { get; set; }

        [MaxLength(500)]
        public string Aciklama { get; set; }

        /// <summary>Raf / depo konumu (ör. A-03)</summary>
        [MaxLength(40)]
        public string Konum { get; set; }

        public int Adet { get; set; }

        /// <summary>Bu seviyenin altına düşünce "kritik stok" sayılır</summary>
        public int MinStok { get; set; }

        public decimal AlisFiyati { get; set; }
        public decimal SatisFiyati { get; set; }

        public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;
        public DateTime GuncellemeTarihi { get; set; } = DateTime.Now;
    }

    public enum HareketTuru
    {
        Giris = 1,
        Cikis = 2,
        Duzeltme = 3
    }

    public class StokHareketi
    {
        public int Id { get; set; }

        public int UrunId { get; set; }
        [JsonIgnore]
        public Urun Urun { get; set; }

        public HareketTuru Tur { get; set; }

        /// <summary>Stoğa etkisi: giriş +, çıkış -, düzeltmede fark (±)</summary>
        public int Miktar { get; set; }

        public int OncekiStok { get; set; }
        public int SonrakiStok { get; set; }

        public decimal BirimFiyat { get; set; }

        [MaxLength(300)]
        public string Aciklama { get; set; }

        public int? KullaniciId { get; set; }

        [MaxLength(100)]
        public string KullaniciAdi { get; set; }

        public DateTime Tarih { get; set; } = DateTime.Now;
    }

    public class Kullanici
    {
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string KullaniciAdi { get; set; }

        [MaxLength(100)]
        public string AdSoyad { get; set; }

        /// <summary>PBKDF2 ile özetlenmiş şifre (düz şifre asla saklanmaz)</summary>
        [Required, MaxLength(200)]
        public string SifreHash { get; set; }

        [Required, MaxLength(20)]
        public string Rol { get; set; } = Roller.Personel;

        public bool Aktif { get; set; } = true;

        public DateTime? SonGiris { get; set; }
        public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;

        [JsonIgnore]
        public string GorunenAd => string.IsNullOrWhiteSpace(AdSoyad) ? KullaniciAdi : AdSoyad;
    }

    public static class Roller
    {
        public const string Admin = "Admin";
        public const string Personel = "Personel";

        public static string Goster(string rol) => rol == Admin ? "Yönetici" : "Personel";
    }

    /// <summary>Veritabanında saklanan ortak ayarlar (firma adı, para birimi, lisans...)</summary>
    public class Ayar
    {
        [Key, MaxLength(60)]
        public string Anahtar { get; set; }

        [MaxLength(4000)]
        public string Deger { get; set; }
    }
}
