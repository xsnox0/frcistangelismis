using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Frcistan.Altyapi;
using Frcistan.Modeller;
using Frcistan.Veri;
using Microsoft.EntityFrameworkCore;

namespace Frcistan.Servisler
{
    /// <summary>Şifreleri PBKDF2-SHA256 ile özetler. Veritabanında düz şifre tutulmaz.</summary>
    public static class SifreHasher
    {
        const int Tekrar = 120_000;

        public static string Hashle(string sifre)
        {
            byte[] tuz = RandomNumberGenerator.GetBytes(16);
            byte[] ozet = Rfc2898DeriveBytes.Pbkdf2(sifre, tuz, Tekrar, HashAlgorithmName.SHA256, 32);
            return $"PBKDF2${Tekrar}${Convert.ToBase64String(tuz)}${Convert.ToBase64String(ozet)}";
        }

        public static bool Dogrula(string sifre, string kayit)
        {
            if (string.IsNullOrEmpty(kayit) || sifre == null) return false;
            var p = kayit.Split('$');
            if (p.Length != 4 || p[0] != "PBKDF2" || !int.TryParse(p[1], out int tekrar)) return false;
            try
            {
                byte[] tuz = Convert.FromBase64String(p[2]);
                byte[] beklenen = Convert.FromBase64String(p[3]);
                byte[] ozet = Rfc2898DeriveBytes.Pbkdf2(sifre, tuz, tekrar, HashAlgorithmName.SHA256, beklenen.Length);
                return CryptographicOperations.FixedTimeEquals(ozet, beklenen);
            }
            catch { return false; }
        }

        /// <summary>Şifre kuralı: en az 6 karakter. Hata yoksa null döner.</summary>
        public static string KuralHatasi(string sifre)
        {
            if (string.IsNullOrEmpty(sifre) || sifre.Length < 6) return "Şifre en az 6 karakter olmalı.";
            return null;
        }

        /// <summary>0-4 arası şifre gücü</summary>
        public static int Guc(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            int g = 0;
            if (s.Length >= 6) g++;
            if (s.Length >= 10) g++;
            if (s.Any(char.IsDigit) && s.Any(char.IsLetter)) g++;
            if (s.Any(char.IsUpper) && s.Any(char.IsLower) || s.Any(c => !char.IsLetterOrDigit(c))) g++;
            return Math.Min(4, g);
        }
    }

    /// <summary>Giriş yapmış kullanıcı</summary>
    public static class Oturum
    {
        public static Kullanici Kullanici { get; set; }
        public static bool Admin => Kullanici?.Rol == Roller.Admin;
    }

    /// <summary>Para birimi biçimlendirme</summary>
    public static class Para
    {
        public static string Kod { get; set; } = "USD";

        public static string Sembol => Sembolu(Kod);

        public static string Sembolu(string kod) => kod switch
        {
            "TRY" => "₺",
            "EUR" => "€",
            _ => "$"
        };

        public static string Bicimle(decimal deger) =>
            (deger < 0 ? "-" : "") + Sembol + Math.Abs(deger).ToString("N2", Ui.Tr);

        /// <summary>Büyük tutarlar için kısa gösterim: $12,4 B</summary>
        public static string Kisa(decimal deger)
        {
            decimal m = Math.Abs(deger);
            string s = m >= 1_000_000 ? (m / 1_000_000).ToString("0.#", Ui.Tr) + " Mn"
                     : m >= 10_000 ? (m / 1_000).ToString("0.#", Ui.Tr) + " B"
                     : m.ToString("N0", Ui.Tr);
            return (deger < 0 ? "-" : "") + Sembol + s;
        }

        public static bool Coz(string metin, out decimal deger)
        {
            deger = 0;
            if (string.IsNullOrWhiteSpace(metin)) return true;
            metin = metin.Trim().Replace(" ", "");
            // "1.234,56" ve "1234.56" biçimlerini de kabul et
            if (metin.Contains(',') && metin.Contains('.'))
                metin = metin.LastIndexOf(',') > metin.LastIndexOf('.') ? metin.Replace(".", "").Replace(',', '.') : metin.Replace(",", "");
            else
                metin = metin.Replace(',', '.');
            return decimal.TryParse(metin, NumberStyles.Number, CultureInfo.InvariantCulture, out deger) && deger >= 0;
        }

        public static string Duzenle(decimal d) => d == 0 ? "" : d.ToString("0.##", Ui.Tr);
    }

    /// <summary>Türkçe karakter ve büyük/küçük harf duyarsız arama</summary>
    public static class Metin
    {
        public static string Normallestir(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char c in s.ToLower(Ui.Tr))
            {
                sb.Append(c switch
                {
                    'ç' => 'c', 'ğ' => 'g', 'ı' => 'i', 'ö' => 'o', 'ş' => 's', 'ü' => 'u', 'â' => 'a', 'î' => 'i', 'û' => 'u',
                    _ => c
                });
            }
            return sb.ToString();
        }

        /// <summary>Aranan metnin tüm kelimeleri, alanlardan herhangi birinde geçiyor mu?</summary>
        public static bool Eslesir(string aranan, params string[] alanlar)
        {
            if (string.IsNullOrWhiteSpace(aranan)) return true;
            string hepsi = Normallestir(string.Join(" ", alanlar.Where(a => a != null)));
            foreach (var kelime in Normallestir(aranan).Split(' ', StringSplitOptions.RemoveEmptyEntries))
                if (!hepsi.Contains(kelime)) return false;
            return true;
        }

        public static bool Ayni(string a, string b) => Normallestir(a?.Trim()) == Normallestir(b?.Trim());
    }

    /// <summary>Veritabanında tutulan ortak ayarlar (tüm bilgisayarlar için aynı)</summary>
    public static class FirmaAyarlari
    {
        public static class Anahtarlar
        {
            public const string FirmaAdi = "FirmaAdi";
            public const string ParaBirimi = "ParaBirimi";
            public const string VarsayilanMinStok = "VarsayilanMinStok";
            public const string KurulumTarihi = "KurulumTarihi";
            public const string LisansAnahtari = "LisansAnahtari";
            public const string SemaSurumu = "SemaSurumu";
            public const string KodOnEki = "KodOnEki";
        }

        public static string FirmaAdi { get; private set; } = "";
        public static string ParaBirimi { get; private set; } = "USD";
        public static int VarsayilanMinStok { get; private set; } = 5;
        public static DateTime KurulumTarihi { get; private set; } = DateTime.Now;
        public static string LisansAnahtari { get; private set; }
        public static string KodOnEki { get; private set; } = "FRC";

        public static async Task YukleAsync()
        {
            await using var db = new FrcistanContext();
            var s = await db.Ayarlar.AsNoTracking().ToDictionaryAsync(a => a.Anahtar, a => a.Deger);
            FirmaAdi = s.GetValueOrDefault(Anahtarlar.FirmaAdi) ?? "";
            ParaBirimi = s.GetValueOrDefault(Anahtarlar.ParaBirimi) ?? "USD";
            VarsayilanMinStok = int.TryParse(s.GetValueOrDefault(Anahtarlar.VarsayilanMinStok), out int m) ? m : 5;
            KurulumTarihi = DateTime.TryParse(s.GetValueOrDefault(Anahtarlar.KurulumTarihi), null, DateTimeStyles.RoundtripKind, out var t) ? t : DateTime.Now;
            LisansAnahtari = s.GetValueOrDefault(Anahtarlar.LisansAnahtari);
            KodOnEki = s.GetValueOrDefault(Anahtarlar.KodOnEki) ?? "FRC";
            Para.Kod = ParaBirimi;
        }

        public static async Task KaydetAsync(IDictionary<string, string> degerler)
        {
            await using var db = new FrcistanContext();
            foreach (var (anahtar, deger) in degerler)
            {
                var a = await db.Ayarlar.FindAsync(anahtar);
                if (a == null) db.Ayarlar.Add(new Ayar { Anahtar = anahtar, Deger = deger });
                else a.Deger = deger;
            }
            await db.SaveChangesAsync();
            await YukleAsync();
        }
    }

    /// <summary>Kullanıcıya gösterilecek, beklenen iş kuralı hataları</summary>
    public class KuralHatasi : Exception
    {
        public KuralHatasi(string mesaj) : base(mesaj) { }
    }
}
