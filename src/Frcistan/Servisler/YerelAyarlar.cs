using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Frcistan.Servisler
{
    public enum VeritabaniTuru
    {
        Sqlite,
        MySql
    }

    /// <summary>Veritabanı bağlantı bilgisi</summary>
    public class VeritabaniBilgisi
    {
        public VeritabaniTuru Tur { get; set; } = VeritabaniTuru.Sqlite;

        /// <summary>Boşsa varsayılan konum kullanılır</summary>
        public string SqliteYolu { get; set; }

        public string MySqlSunucu { get; set; } = "localhost";
        public int MySqlPort { get; set; } = 3306;
        public string MySqlVeritabani { get; set; } = "frcistan_stok";
        public string MySqlKullanici { get; set; } = "root";

        /// <summary>Windows DPAPI ile şifrelenmiş MySQL şifresi</summary>
        public string MySqlSifreKorumali { get; set; }

        [JsonIgnore]
        public string MySqlSifre
        {
            get => Coz(MySqlSifreKorumali);
            set => MySqlSifreKorumali = Sifrele(value);
        }

        public string SqliteDosyasi() =>
            string.IsNullOrWhiteSpace(SqliteYolu) ? Path.Combine(YerelAyarlar.Klasor, "Veri", "frcistan.db") : SqliteYolu;

        public string MySqlBaglantiMetni(bool veritabaniOlmadan = false)
        {
            var sb = new StringBuilder();
            sb.Append($"Server={MySqlSunucu};Port={MySqlPort};");
            if (!veritabaniOlmadan) sb.Append($"Database={MySqlVeritabani};");
            sb.Append($"Uid={MySqlKullanici};Pwd={MySqlSifre};CharSet=utf8mb4;Connection Timeout=8;");
            return sb.ToString();
        }

        public string Aciklama() => Tur == VeritabaniTuru.Sqlite
            ? "Bu bilgisayar (yerel veritabanı)"
            : $"MySQL · {MySqlSunucu}:{MySqlPort} / {MySqlVeritabani}";

        public VeritabaniBilgisi Kopya() => (VeritabaniBilgisi)MemberwiseClone();

        static string Sifrele(string duz)
        {
            if (string.IsNullOrEmpty(duz)) return null;
            try
            {
                var veri = ProtectedData.Protect(Encoding.UTF8.GetBytes(duz), null, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(veri);
            }
            catch { return null; }
        }

        static string Coz(string korumali)
        {
            if (string.IsNullOrEmpty(korumali)) return "";
            try
            {
                var veri = ProtectedData.Unprotect(Convert.FromBase64String(korumali), null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(veri);
            }
            catch { return ""; }
        }
    }

    /// <summary>
    /// Bu bilgisayara özel ayarlar. %LOCALAPPDATA%\Frcistan\ayarlar.json dosyasında saklanır.
    /// (Firma adı gibi ortak ayarlar veritabanında, bkz. FirmaAyarlari)
    /// </summary>
    public class YerelAyarlar
    {
        /// <summary>Veri klasörü. FRCISTAN_VERI_KLASORU ortam değişkeniyle değiştirilebilir (taşınabilir kullanım, test).</summary>
        public static string Klasor { get; set; } =
            Environment.GetEnvironmentVariable("FRCISTAN_VERI_KLASORU") is { Length: > 0 } ozel
                ? ozel
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FrcistanStok");

        static string Dosya => Path.Combine(Klasor, "ayarlar.json");

        public static YerelAyarlar Mevcut { get; private set; } = new();

        public static bool DosyaVarMi => File.Exists(Dosya);

        public VeritabaniBilgisi Veritabani { get; set; } = new();
        public string Tema { get; set; } = "Koyu";
        public string SonKullanici { get; set; }
        public bool BeniHatirla { get; set; } = true;
        public bool OtomatikYedek { get; set; } = true;
        public DateTime? SonOtomatikYedek { get; set; }
        public bool PencereTamEkran { get; set; } = true;
        public double PencereGenislik { get; set; } = 1400;
        public double PencereYukseklik { get; set; } = 880;

        static readonly JsonSerializerOptions Secenekler = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public static void Yukle()
        {
            try
            {
                if (File.Exists(Dosya))
                    Mevcut = JsonSerializer.Deserialize<YerelAyarlar>(File.ReadAllText(Dosya), Secenekler) ?? new YerelAyarlar();
            }
            catch
            {
                Mevcut = new YerelAyarlar();
            }
            Mevcut.Veritabani ??= new VeritabaniBilgisi();
        }

        public static void Kaydet()
        {
            try
            {
                Directory.CreateDirectory(Klasor);
                File.WriteAllText(Dosya, JsonSerializer.Serialize(Mevcut, Secenekler));
            }
            catch (Exception ex)
            {
                Gunluk.Yaz("Ayarlar kaydedilemedi", ex);
            }
        }

        public static void Sifirla() => Mevcut = new YerelAyarlar();
    }

    /// <summary>Hata günlüğü: %LOCALAPPDATA%\Frcistan\gunluk.txt</summary>
    public static class Gunluk
    {
        static readonly object Kilit = new();

        public static string Dosya => Path.Combine(YerelAyarlar.Klasor, "gunluk.txt");

        public static void Yaz(string mesaj, Exception ex = null)
        {
            try
            {
                lock (Kilit)
                {
                    Directory.CreateDirectory(YerelAyarlar.Klasor);
                    var fi = new FileInfo(Dosya);
                    if (fi.Exists && fi.Length > 2_000_000) fi.Delete();
                    File.AppendAllText(Dosya, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {mesaj}{(ex != null ? Environment.NewLine + ex : "")}{Environment.NewLine}");
                }
            }
            catch { }
        }
    }
}
