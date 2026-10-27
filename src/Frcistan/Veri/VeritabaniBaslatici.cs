using System.Text.RegularExpressions;
using Frcistan.Modeller;
using Frcistan.Servisler;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using MySql.Data.MySqlClient;

namespace Frcistan.Veri
{
    /// <summary>Seçilen veritabanı Frcistan'a değil başka bir programa ait olduğunda fırlatılır.</summary>
    public class YabanciVeritabaniException : Exception
    {
        public YabanciVeritabaniException() : base(
            "Bu veritabanında başka bir programa (ya da Frcistan 0.1'e) ait tablolar var. " +
            "Lütfen farklı bir veritabanı adı seçin. Frcistan 0.1 verilerinizi daha sonra " +
            "Ayarlar › Veri aktarımı bölümünden içeri alabilirsiniz.")
        { }
    }

    public static class VeritabaniBaslatici
    {
        public const int SemaSurumu = 1;

        public static readonly (string Ad, string Renk, string Ikon)[] VarsayilanKategoriler =
        {
            ("Motor", "#3D7EFF", "E9F5"),
            ("Motor Sürücü", "#8B5CF6", "E9E9"),
            ("Swerve Modülü", "#06B6D4", "E895"),
            ("Sensör", "#10B981", "E9D9"),
            ("Elektronik", "#F59E0B", "E950"),
            ("Görüntü İşleme", "#EC4899", "E7B3"),
            ("Batarya ve Güç", "#EF4444", "E945"),
            ("Pnömatik", "#14B8A6", "EE94"),
            ("Mekanik Parça", "#F97316", "EC7A"),
            ("Diğer", "#64748B", "E7B8"),
        };

        /// <summary>Veritabanını ve tabloları (yoksa) oluşturur, ilk kayıtları ekler.</summary>
        public static async Task HazirlaAsync(VeritabaniBilgisi bilgi = null)
        {
            bilgi ??= YerelAyarlar.Mevcut.Veritabani;

            if (bilgi.Tur == VeritabaniTuru.MySql)
                await MySqlVeritabaniOlusturAsync(bilgi);
            else
                Directory.CreateDirectory(Path.GetDirectoryName(bilgi.SqliteDosyasi()));

            await using var db = new FrcistanContext(bilgi);
            var olusturucu = db.GetService<IRelationalDatabaseCreator>();

            if (!await olusturucu.ExistsAsync())
                await olusturucu.CreateAsync();

            if (!await olusturucu.HasTablesAsync())
                await olusturucu.CreateTablesAsync();
            else if (!await BizimTablolarVarMiAsync(db))
                throw new YabanciVeritabaniException();

            await GuncelleAsync(db);

            if (!await db.Kategoriler.AnyAsync())
            {
                int sira = 0;
                foreach (var (ad, renk, ikon) in VarsayilanKategoriler)
                    db.Kategoriler.Add(new Kategori { Ad = ad, Renk = renk, Ikon = ikon, Sira = sira++ });
            }

            await AyarYoksaEkleAsync(db, FirmaAyarlari.Anahtarlar.SemaSurumu, SemaSurumu.ToString());
            await AyarYoksaEkleAsync(db, FirmaAyarlari.Anahtarlar.KurulumTarihi, DateTime.Now.ToString("o"));
            await db.SaveChangesAsync();
        }

        /// <summary>İleride şema değişirse sürüm yükseltmeleri burada yapılır.</summary>
        static Task GuncelleAsync(FrcistanContext db) => Task.CompletedTask;

        static async Task<bool> BizimTablolarVarMiAsync(FrcistanContext db)
        {
            try
            {
                await db.Ayarlar.AnyAsync();
                await db.StokHareketleri.AnyAsync();
                await db.Urunler.Select(u => u.MinStok).FirstOrDefaultAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        static async Task AyarYoksaEkleAsync(FrcistanContext db, string anahtar, string deger)
        {
            if (!await db.Ayarlar.AnyAsync(a => a.Anahtar == anahtar))
                db.Ayarlar.Add(new Ayar { Anahtar = anahtar, Deger = deger });
        }

        public static bool VeritabaniAdiGecerli(string ad) => !string.IsNullOrWhiteSpace(ad) && Regex.IsMatch(ad, "^[A-Za-z0-9_]{1,64}$");

        static async Task MySqlVeritabaniOlusturAsync(VeritabaniBilgisi b)
        {
            if (!VeritabaniAdiGecerli(b.MySqlVeritabani))
                throw new InvalidOperationException("Veritabanı adı yalnızca İngilizce harf, rakam ve alt çizgi (_) içerebilir.");

            await using var baglanti = new MySqlConnection(b.MySqlBaglantiMetni(veritabaniOlmadan: true));
            await baglanti.OpenAsync();
            await using var komut = baglanti.CreateCommand();
            komut.CommandText = $"CREATE DATABASE IF NOT EXISTS `{b.MySqlVeritabani}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci";
            await komut.ExecuteNonQueryAsync();
        }

        /// <summary>Bağlantıyı dener, başarılıysa sunucu sürümünü döndürür.</summary>
        public static async Task<string> BaglantiyiTestEtAsync(VeritabaniBilgisi b)
        {
            if (b.Tur == VeritabaniTuru.Sqlite)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(b.SqliteDosyasi()));
                return "SQLite";
            }

            await using var baglanti = new MySqlConnection(b.MySqlBaglantiMetni(veritabaniOlmadan: true));
            await baglanti.OpenAsync();
            return baglanti.ServerVersion;
        }

        public static async Task<bool> KullaniciVarMiAsync(VeritabaniBilgisi b)
        {
            await using var db = new FrcistanContext(b);
            return await db.Kullanicilar.AnyAsync();
        }

        /// <summary>Veritabanı hatalarını kullanıcının anlayacağı dile çevirir.</summary>
        public static string HatayiAcikla(Exception ex)
        {
            var ic = ex;
            while (ic.InnerException != null && ic is not MySqlException && ic is not YabanciVeritabaniException)
                ic = ic.InnerException;

            if (ic is YabanciVeritabaniException) return ic.Message;
            if (ic is MySqlException my)
            {
                return my.Number switch
                {
                    1045 => "MySQL kullanıcı adı veya şifresi hatalı.",
                    1044 => "Bu MySQL kullanıcısının veritabanına erişim izni yok.",
                    1049 => "Veritabanı bulunamadı.",
                    1042 or 0 when my.Message.Contains("connect", StringComparison.OrdinalIgnoreCase) =>
                        "MySQL sunucusuna bağlanılamadı. Sunucu açık mı? (XAMPP kullanıyorsanız MySQL'i başlatın.) Adres ve port numarası doğru mu?",
                    _ => "MySQL hatası: " + my.Message
                };
            }
            if (ic is TimeoutException || ic.Message.Contains("Unable to connect", StringComparison.OrdinalIgnoreCase))
                return "MySQL sunucusuna bağlanılamadı. Sunucu açık mı? Adres ve port numarası doğru mu?";
            if (ic is UnauthorizedAccessException || ic is IOException)
                return "Veritabanı dosyasına erişilemedi: " + ic.Message;
            return ic.Message;
        }
    }
}
