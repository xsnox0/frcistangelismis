using System.Security.Cryptography;
using System.Text.Json;

namespace Frcistan.Servisler
{
    public enum LisansDurumu
    {
        Lisansli,
        Deneme,
        DenemeBitti,
        SuresiDoldu
    }

    public class LisansBilgisi
    {
        public LisansDurumu Durum { get; init; }
        public string Sahip { get; init; }
        public string Tur { get; init; }
        public DateTime? Bitis { get; init; }
        public string No { get; init; }
        public int KalanGun { get; init; }

        public bool YazmaIzni => Durum is LisansDurumu.Lisansli or LisansDurumu.Deneme;

        public string Baslik => Durum switch
        {
            LisansDurumu.Lisansli => $"Frcistan {Tur}",
            LisansDurumu.Deneme => "Deneme sürümü",
            LisansDurumu.SuresiDoldu => "Lisans süresi doldu",
            _ => "Deneme süresi doldu"
        };

        public string Aciklama => Durum switch
        {
            LisansDurumu.Lisansli => Bitis.HasValue
                ? $"{Sahip} adına lisanslı · {Bitis:dd.MM.yyyy} tarihine kadar geçerli"
                : $"{Sahip} adına süresiz lisanslı",
            LisansDurumu.Deneme => $"Tüm özellikler açık · {KalanGun} gün kaldı",
            LisansDurumu.SuresiDoldu => $"{Sahip} lisansının süresi {Bitis:dd.MM.yyyy} tarihinde doldu. Kayıtları görüntüleyebilirsiniz; değişiklik için lisansı yenileyin.",
            _ => "Kayıtlarınızı görüntüleyebilirsiniz. Ekleme ve değişiklik yapmak için lisans anahtarı girin."
        };
    }

    /// <summary>
    /// Lisans anahtarları ECDSA (P-256) ile imzalanır. Program yalnızca açık anahtarı bilir;
    /// yeni anahtar üretmek için araclar/LisansUretici ve gizli anahtar gerekir.
    /// </summary>
    public static class LisansServisi
    {
        public const int DenemeGunu = 30;

        const string AcikAnahtar =
            "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEtm3172fvjMjSyU5l8a713S0qgFadh2FEundBmesVA3WSxmixIsap5VUidJ15chuOL40WMLCiLTa5f9W8dIze3A==";

        public static LisansBilgisi Mevcut { get; private set; } = new() { Durum = LisansDurumu.Deneme, KalanGun = DenemeGunu };

        public static event Action Degisti;

        public static LisansBilgisi Hesapla()
        {
            var anahtar = FirmaAyarlari.LisansAnahtari;
            if (!string.IsNullOrWhiteSpace(anahtar) && Coz(anahtar, out var y))
            {
                bool doldu = y.Bitis.HasValue && y.Bitis.Value.Date < DateTime.Today;
                Mevcut = new LisansBilgisi
                {
                    Durum = doldu ? LisansDurumu.SuresiDoldu : LisansDurumu.Lisansli,
                    Sahip = y.Sahip,
                    Tur = y.Tur,
                    Bitis = y.Bitis,
                    No = y.No,
                    KalanGun = y.Bitis.HasValue ? Math.Max(0, (y.Bitis.Value.Date - DateTime.Today).Days) : int.MaxValue
                };
            }
            else
            {
                int gecen = (DateTime.Today - FirmaAyarlari.KurulumTarihi.Date).Days;
                int kalan = DenemeGunu - Math.Max(0, gecen);
                Mevcut = new LisansBilgisi
                {
                    Durum = kalan > 0 ? LisansDurumu.Deneme : LisansDurumu.DenemeBitti,
                    KalanGun = Math.Max(0, kalan)
                };
            }
            Degisti?.Invoke();
            return Mevcut;
        }

        class Yuk
        {
            public string Sahip, Tur, No;
            public DateTime? Bitis;
        }

        static bool Coz(string anahtar, out Yuk yuk)
        {
            yuk = null;
            try
            {
                var p = anahtar.Trim().Replace("\r", "").Replace("\n", "").Replace(" ", "").Split('.');
                if (p.Length != 3 || p[0] != "FRC1") return false;
                byte[] veri = B64Coz(p[1]);
                byte[] imza = B64Coz(p[2]);

                using var ec = ECDsa.Create();
                ec.ImportSubjectPublicKeyInfo(Convert.FromBase64String(AcikAnahtar), out _);
                if (!ec.VerifyData(veri, imza, HashAlgorithmName.SHA256)) return false;

                var d = JsonSerializer.Deserialize<Dictionary<string, string>>(veri);
                yuk = new Yuk
                {
                    Sahip = d.GetValueOrDefault("ad") ?? "",
                    Tur = d.GetValueOrDefault("tur") ?? "Pro",
                    No = d.GetValueOrDefault("no"),
                    Bitis = DateTime.TryParse(d.GetValueOrDefault("bitis"), out var b) ? b : null
                };
                return true;
            }
            catch
            {
                return false;
            }
        }

        static byte[] B64Coz(string s)
        {
            s = s.Replace('-', '+').Replace('_', '/');
            return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
        }

        public static async Task<LisansBilgisi> EtkinlestirAsync(string anahtar)
        {
            if (string.IsNullOrWhiteSpace(anahtar) || !Coz(anahtar, out var y))
                throw new KuralHatasi("Lisans anahtarı geçersiz. Anahtarı eksiksiz kopyaladığınızdan emin olun.");
            if (y.Bitis.HasValue && y.Bitis.Value.Date < DateTime.Today)
                throw new KuralHatasi($"Bu lisansın süresi {y.Bitis:dd.MM.yyyy} tarihinde dolmuş.");

            await FirmaAyarlari.KaydetAsync(new Dictionary<string, string>
            {
                [FirmaAyarlari.Anahtarlar.LisansAnahtari] = anahtar.Trim()
            });
            return Hesapla();
        }

        public static async Task KaldirAsync()
        {
            await FirmaAyarlari.KaydetAsync(new Dictionary<string, string> { [FirmaAyarlari.Anahtarlar.LisansAnahtari] = null });
            Hesapla();
        }
    }
}
