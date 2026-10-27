using System.IO.Compression;
using System.Text.Json;
using Frcistan.Modeller;
using Frcistan.Veri;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;

namespace Frcistan.Servisler
{
    public class YedekDosyasi
    {
        public string Uygulama { get; set; } = "Frcistan";
        public string Surum { get; set; } = Altyapi.Surum.Kisa;
        public DateTime Tarih { get; set; } = DateTime.Now;
        public string Firma { get; set; }
        public List<Kategori> Kategoriler { get; set; } = new();
        public List<Kullanici> Kullanicilar { get; set; } = new();
        public List<Urun> Urunler { get; set; } = new();
        public List<StokHareketi> Hareketler { get; set; } = new();
        public List<Ayar> Ayarlar { get; set; } = new();

        public string Ozet => $"{Urunler.Count} ürün, {Kategoriler.Count} kategori, {Hareketler.Count} stok hareketi, {Kullanicilar.Count} kullanıcı";
    }

    /// <summary>
    /// Yedekler, tüm verileri içeren sıkıştırılmış JSON dosyalarıdır (.frcyedek).
    /// SQLite ve MySQL arasında taşınabilir: bir bilgisayardaki yedek, MySQL sunucusuna geri yüklenebilir.
    /// </summary>
    public static class YedekServisi
    {
        public const string Uzanti = ".frcyedek";
        public static string OtomatikKlasor => Path.Combine(YerelAyarlar.Klasor, "Yedekler");

        static readonly JsonSerializerOptions Secenekler = new() { WriteIndented = false };

        public static async Task<YedekDosyasi> YedekAlAsync(string dosya)
        {
            await using var db = new FrcistanContext();
            var y = new YedekDosyasi
            {
                Firma = FirmaAyarlari.FirmaAdi,
                Kategoriler = await db.Kategoriler.AsNoTracking().ToListAsync(),
                Kullanicilar = await db.Kullanicilar.AsNoTracking().ToListAsync(),
                Urunler = await db.Urunler.AsNoTracking().ToListAsync(),
                Hareketler = await db.StokHareketleri.AsNoTracking().ToListAsync(),
                Ayarlar = await db.Ayarlar.AsNoTracking().ToListAsync()
            };

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(dosya)));
            string gecici = dosya + ".tmp";
            await using (var akis = File.Create(gecici))
            await using (var gz = new GZipStream(akis, CompressionLevel.Optimal))
                await JsonSerializer.SerializeAsync(gz, y, Secenekler);
            File.Move(gecici, dosya, overwrite: true);
            return y;
        }

        public static async Task<YedekDosyasi> OkuAsync(string dosya)
        {
            try
            {
                await using var akis = File.OpenRead(dosya);
                await using var gz = new GZipStream(akis, CompressionMode.Decompress);
                var y = await JsonSerializer.DeserializeAsync<YedekDosyasi>(gz, Secenekler);
                if (y == null || y.Uygulama != "Frcistan") throw new KuralHatasi("Bu dosya bir Frcistan yedeği değil.");
                return y;
            }
            catch (KuralHatasi) { throw; }
            catch (Exception ex)
            {
                throw new KuralHatasi("Yedek dosyası okunamadı. Dosya bozuk ya da bir Frcistan yedeği değil. (" + ex.Message + ")");
            }
        }

        /// <summary>Mevcut tüm verileri siler ve yedeği yükler.</summary>
        public static async Task GeriYukleAsync(YedekDosyasi y)
        {
            if (!y.Kullanicilar.Any(k => k.Rol == Roller.Admin && k.Aktif))
                throw new KuralHatasi("Yedekte aktif bir yönetici hesabı yok; geri yüklenemez.");

            await using var db = new FrcistanContext();
            await using var islem = await db.Database.BeginTransactionAsync();

            foreach (var tablo in new[] { "StokHareketleri", "Urunler", "Kategoriler", "Kullanicilar", "Ayarlar" })
                await db.TumunuSilAsync(tablo);

            db.Kategoriler.AddRange(y.Kategoriler);
            db.Kullanicilar.AddRange(y.Kullanicilar);
            await db.SaveChangesAsync();
            db.Urunler.AddRange(y.Urunler);
            await db.SaveChangesAsync();
            db.StokHareketleri.AddRange(y.Hareketler);
            db.Ayarlar.AddRange(y.Ayarlar);
            await db.SaveChangesAsync();

            await islem.CommitAsync();
        }

        /// <summary>Günde bir kez otomatik yedek alır, son 14 yedeği saklar.</summary>
        public static async Task OtomatikYedekAsync()
        {
            var a = YerelAyarlar.Mevcut;
            if (!a.OtomatikYedek) return;
            if (a.SonOtomatikYedek.HasValue && a.SonOtomatikYedek.Value.Date == DateTime.Today) return;
            try
            {
                Directory.CreateDirectory(OtomatikKlasor);
                await YedekAlAsync(Path.Combine(OtomatikKlasor, $"otomatik-{DateTime.Now:yyyyMMdd-HHmmss}{Uzanti}"));
                a.SonOtomatikYedek = DateTime.Now;
                YerelAyarlar.Kaydet();

                foreach (var eski in new DirectoryInfo(OtomatikKlasor).GetFiles("otomatik-*" + Uzanti)
                             .OrderByDescending(f => f.CreationTime).Skip(14))
                    eski.Delete();
            }
            catch (Exception ex)
            {
                Gunluk.Yaz("Otomatik yedek alınamadı", ex);
            }
        }
    }

    /// <summary>Frcistan 0.1 (MySQL frcistan01 veritabanı) verilerini içeri aktarır.</summary>
    public static class AktarimServisi
    {
        public class Sonuc
        {
            public int Kategori, Urun, BirlesenUrun, Kullanici, AtlananKullanici;
            public override string ToString() =>
                $"{Urun} yeni ürün eklendi, {BirlesenUrun} ürünün stoğu birleştirildi, {Kategori} yeni kategori, {Kullanici} kullanıcı aktarıldı" +
                (AtlananKullanici > 0 ? $" ({AtlananKullanici} kullanıcı zaten vardı)." : ".");
        }

        public static string BaglantiMetni(string sunucu, int port, string veritabani, string kullanici, string sifre) =>
            $"Server={sunucu};Port={port};Database={veritabani};Uid={kullanici};Pwd={sifre};CharSet=utf8mb4;Connection Timeout=8;";

        public static async Task<(int Urun, int Kategori, int Kullanici)> OnizleAsync(string baglantiMetni)
        {
            await using var c = new MySqlConnection(baglantiMetni);
            await c.OpenAsync();
            return (await Say(c, "Urunler"), await Say(c, "Kategoriler"), await Say(c, "Kullanicilar"));
        }

        static async Task<int> Say(MySqlConnection c, string tablo)
        {
            await using var k = new MySqlCommand($"SELECT COUNT(*) FROM `{tablo}`", c);
            return Convert.ToInt32(await k.ExecuteScalarAsync());
        }

        public static async Task<Sonuc> AktarAsync(string baglantiMetni)
        {
            var sonuc = new Sonuc();
            var eskiKategoriler = new Dictionary<int, string>();
            var eskiUrunler = new List<(int KategoriId, string Marka, string Ad, int Adet, decimal Fiyat)>();
            var eskiKullanicilar = new List<(string Ad, string Sifre, string Rol)>();

            await using (var c = new MySqlConnection(baglantiMetni))
            {
                await c.OpenAsync();
                await using (var k = new MySqlCommand("SELECT Id, Ad FROM Kategoriler", c))
                await using (var r = await k.ExecuteReaderAsync())
                    while (await r.ReadAsync()) eskiKategoriler[r.GetInt32(0)] = r.GetString(1);

                await using (var k = new MySqlCommand("SELECT KategoriId, Marka, UrunAdi, Adet, Fiyat FROM Urunler", c))
                await using (var r = await k.ExecuteReaderAsync())
                    while (await r.ReadAsync())
                        eskiUrunler.Add((r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetInt32(3), r.GetDecimal(4)));

                await using (var k = new MySqlCommand("SELECT KullaniciAdi, Sifre, Rol FROM Kullanicilar", c))
                await using (var r = await k.ExecuteReaderAsync())
                    while (await r.ReadAsync()) eskiKullanicilar.Add((r.GetString(0), r.GetString(1), r.GetString(2)));
            }

            await using var db = new FrcistanContext();
            await using var islem = await db.Database.BeginTransactionAsync();

            var kategoriler = await db.Kategoriler.ToListAsync();
            var eslesme = new Dictionary<int, int>();
            foreach (var (eskiId, ad) in eskiKategoriler)
            {
                var k = kategoriler.FirstOrDefault(x => Metin.Ayni(x.Ad, ad));
                if (k == null)
                {
                    k = new Kategori { Ad = ad.Trim(), Renk = "#64748B", Ikon = "E7B8", Sira = kategoriler.Count };
                    db.Kategoriler.Add(k);
                    kategoriler.Add(k);
                    sonuc.Kategori++;
                }
                await db.SaveChangesAsync();
                eslesme[eskiId] = k.Id;
            }

            var urunler = await db.Urunler.ToListAsync();
            foreach (var e in eskiUrunler)
            {
                var u = urunler.FirstOrDefault(x => Metin.Ayni(x.Marka, e.Marka) && Metin.Ayni(x.UrunAdi, e.Ad));
                if (u != null)
                {
                    if (e.Adet > 0)
                    {
                        int onceki = u.Adet;
                        u.Adet += e.Adet;
                        db.StokHareketleri.Add(UrunServisi.Hareket(u.Id, HareketTuru.Giris, e.Adet, onceki, u.Adet, e.Fiyat, "Frcistan 0.1'den aktarıldı"));
                    }
                    sonuc.BirlesenUrun++;
                    continue;
                }

                int katId = eslesme.TryGetValue(e.KategoriId, out var kid) ? kid : kategoriler.First().Id;
                u = new Urun
                {
                    KategoriId = katId, Marka = e.Marka.Trim(), UrunAdi = e.Ad.Trim(), Adet = e.Adet,
                    MinStok = FirmaAyarlari.VarsayilanMinStok, AlisFiyati = e.Fiyat, SatisFiyati = e.Fiyat
                };
                db.Urunler.Add(u);
                await db.SaveChangesAsync();
                u.Kod = $"{FirmaAyarlari.KodOnEki}-{u.Id:D4}";
                if (u.Adet > 0)
                    db.StokHareketleri.Add(UrunServisi.Hareket(u.Id, HareketTuru.Giris, u.Adet, 0, u.Adet, u.AlisFiyati, "Frcistan 0.1'den aktarıldı"));
                urunler.Add(u);
                sonuc.Urun++;
            }

            var kullanicilar = await db.Kullanicilar.ToListAsync();
            foreach (var (ad, sifre, rol) in eskiKullanicilar)
            {
                if (kullanicilar.Any(x => string.Equals(x.KullaniciAdi, ad, StringComparison.OrdinalIgnoreCase)))
                {
                    sonuc.AtlananKullanici++;
                    continue;
                }
                var k = new Kullanici
                {
                    KullaniciAdi = ad.Trim(), SifreHash = SifreHasher.Hashle(sifre),
                    Rol = rol == Roller.Admin ? Roller.Admin : Roller.Personel, Aktif = true
                };
                db.Kullanicilar.Add(k);
                kullanicilar.Add(k);
                sonuc.Kullanici++;
            }

            await db.SaveChangesAsync();
            await islem.CommitAsync();
            return sonuc;
        }
    }
}
