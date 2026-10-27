using Frcistan.Modeller;
using Frcistan.Servisler;
using Microsoft.EntityFrameworkCore;

namespace Frcistan.Veri
{
    /// <summary>
    /// Tanıtım ve deneme için örnek ürünler ve son 60 günün stok hareketleri.
    /// Fiyatlar örnek amaçlıdır.
    /// </summary>
    public static class DemoVeri
    {
        record Ornek(string Kategori, string Marka, string Ad, int Adet, int Min, decimal Alis, decimal Satis, string Konum);

        static readonly Ornek[] Urunler =
        {
            new("Motor", "REV Robotics", "NEO Brushless Motor V1.1", 18, 6, 38m, 52m, "A-01"),
            new("Motor", "REV Robotics", "NEO Vortex Brushless Motor", 12, 6, 48m, 65m, "A-01"),
            new("Motor", "REV Robotics", "NEO 550 Brushless Motor", 22, 8, 26m, 38m, "A-02"),
            new("Motor", "WCP", "Kraken X60 Brushless Motor", 8, 4, 165m, 210m, "A-03"),
            new("Motor", "WCP", "Kraken X44 Brushless Motor", 3, 4, 150m, 195m, "A-03"),
            new("Motor", "CTR Electronics", "Falcon 500 Brushless Motor", 0, 2, 140m, 185m, "A-04"),
            new("Motor", "AndyMark", "CIM Motor", 6, 4, 26m, 36m, "A-05"),
            new("Motor", "goBILDA", "5203 Yellow Jacket Motor (312 RPM)", 24, 10, 32m, 44m, "B-01"),
            new("Motor Sürücü", "REV Robotics", "SPARK MAX Motor Controller", 20, 8, 72m, 95m, "A-06"),
            new("Motor Sürücü", "REV Robotics", "SPARK Flex Motor Controller", 9, 6, 120m, 160m, "A-06"),
            new("Motor Sürücü", "CTR Electronics", "Talon SRX Motor Controller", 2, 4, 70m, 95m, "A-07"),
            new("Swerve Modülü", "REV Robotics", "MAXSwerve Module", 8, 4, 220m, 289m, "C-01"),
            new("Swerve Modülü", "Swerve Drive Specialties", "MK4i Swerve Module L2", 4, 4, 250m, 320m, "C-02"),
            new("Swerve Modülü", "Swerve Drive Specialties", "MK4n Swerve Module L2+", 6, 4, 260m, 335m, "C-02"),
            new("Swerve Modülü", "WCP", "Swerve X2i Module", 1, 4, 240m, 310m, "C-03"),
            new("Sensör", "CTR Electronics", "CANcoder", 14, 8, 50m, 68m, "D-01"),
            new("Sensör", "CTR Electronics", "Pigeon 2.0 IMU", 5, 3, 160m, 205m, "D-01"),
            new("Sensör", "Studica", "navX2-MXP Navigasyon Sensörü", 3, 2, 110m, 145m, "D-02"),
            new("Sensör", "REV Robotics", "Through Bore Encoder", 26, 10, 38m, 50m, "D-02"),
            new("Sensör", "REV Robotics", "2m Distance Sensor", 7, 4, 22m, 32m, "D-03"),
            new("Sensör", "Redux Robotics", "Canandmag Manyetik Encoder", 10, 4, 40m, 55m, "D-03"),
            new("Elektronik", "National Instruments", "roboRIO 2.0", 3, 2, 400m, 520m, "E-01"),
            new("Elektronik", "REV Robotics", "Power Distribution Hub", 4, 2, 150m, 195m, "E-02"),
            new("Elektronik", "CTR Electronics", "CANivore", 2, 2, 210m, 270m, "E-02"),
            new("Elektronik", "REV Robotics", "Control Hub (FTC)", 5, 2, 250m, 325m, "E-03"),
            new("Elektronik", "REV Robotics", "Expansion Hub (FTC)", 0, 2, 190m, 245m, "E-03"),
            new("Elektronik", "Vivid Hosting", "VH-109 Robot Radyosu", 6, 3, 60m, 80m, "E-04"),
            new("Görüntü İşleme", "Limelight", "Limelight 3G", 3, 2, 400m, 499m, "F-01"),
            new("Görüntü İşleme", "Limelight", "Limelight 4", 2, 2, 450m, 560m, "F-01"),
            new("Görüntü İşleme", "Arducam", "OV9281 Global Shutter Kamera", 9, 4, 35m, 52m, "F-02"),
            new("Batarya ve Güç", "MK Battery", "ES17-12 12V 18Ah Robot Aküsü", 12, 6, 95m, 125m, "G-01"),
            new("Batarya ve Güç", "AndyMark", "Akü Şarj Cihazı 12V 6A", 4, 2, 70m, 95m, "G-02"),
            new("Batarya ve Güç", "AndyMark", "SB50 Konnektör (Kırmızı)", 40, 20, 4.5m, 7.5m, "G-03"),
            new("Pnömatik", "REV Robotics", "Pneumatic Hub", 3, 2, 120m, 160m, "H-01"),
            new("Pnömatik", "VEX Robotics", "Viair 90C Kompresör", 4, 2, 70m, 95m, "H-02"),
            new("Pnömatik", "AndyMark", "Hava Tankı 574 ml", 7, 4, 18m, 26m, "H-02"),
            new("Mekanik Parça", "WCP", "1/2\" Hex Mil 36\" (Alüminyum)", 30, 10, 9m, 14m, "I-01"),
            new("Mekanik Parça", "ThriftyBot", "1/2\" Hex Rulman (10'lu)", 15, 6, 12m, 18m, "I-02"),
            new("Mekanik Parça", "WCP", "GT2 Triger Kayışı 3mm 120T", 18, 8, 8m, 12m, "I-03"),
            new("Mekanik Parça", "AndyMark", "4\" HiGrip Tekerlek", 16, 8, 14m, 20m, "I-04"),
            new("Mekanik Parça", "goBILDA", "U-Kanal 432 mm", 4, 6, 11m, 16m, "I-05"),
            new("Mekanik Parça", "REV Robotics", "MAXSpline Mil 300 mm", 10, 6, 7.5m, 11m, "I-05"),
            new("Diğer", "WCP", "Kablo Zinciri 10x15 mm (1 m)", 25, 10, 6m, 9.5m, "J-01"),
            new("Diğer", "AndyMark", "Dual Lock Cırt Bant (1 m)", 8, 6, 14m, 21m, "J-01"),
        };

        static readonly string[] Takimlar = { "6014 ARC", "7672 Kuzgun", "8092 G.O.A.T.", "9029 Sirius", "6838 ScienceBots", "7536 Atlas", "FTC 22058", "FTC 19735", "8159 Sphinx" };
        static readonly string[] Tedarikciler = { "REV Robotics siparişi", "WCP sevkiyatı", "AndyMark siparişi", "CTRE distribütör teslimatı", "Gümrükten çekilen koli" };

        public static async Task EkleAsync(string yoneticiAdi)
        {
            await using var db = new FrcistanContext();
            var kategoriler = await db.Kategoriler.ToDictionaryAsync(k => k.Ad, k => k.Id);
            var rnd = new Random(2026);
            var kisiler = new List<(int? Id, string Ad)> { (Oturum.Kullanici?.Id, yoneticiAdi) };

            if (!await db.Kullanicilar.AnyAsync(k => k.KullaniciAdi == "ayse.yilmaz"))
            {
                var a = new Kullanici { KullaniciAdi = "ayse.yilmaz", AdSoyad = "Ayşe Yılmaz", Rol = Roller.Personel, SifreHash = SifreHasher.Hashle("Demo1234"), SonGiris = DateTime.Now.AddHours(-3) };
                var m = new Kullanici { KullaniciAdi = "mehmet.kaya", AdSoyad = "Mehmet Kaya", Rol = Roller.Personel, SifreHash = SifreHasher.Hashle("Demo1234"), SonGiris = DateTime.Now.AddDays(-1) };
                db.Kullanicilar.AddRange(a, m);
                await db.SaveChangesAsync();
                kisiler.Add((a.Id, a.AdSoyad));
                kisiler.Add((m.Id, m.AdSoyad));
            }

            var simdi = DateTime.Now;
            var tumHareketler = new List<StokHareketi>();
            int sira = 0;

            foreach (var o in Urunler)
            {
                if (!kategoriler.TryGetValue(o.Kategori, out int katId)) katId = kategoriler.Values.First();
                var urun = new Urun
                {
                    KategoriId = katId, Marka = o.Marka, UrunAdi = o.Ad, Adet = o.Adet, MinStok = o.Min,
                    AlisFiyati = o.Alis, SatisFiyati = o.Satis, Konum = o.Konum,
                    OlusturmaTarihi = simdi.AddDays(-62), GuncellemeTarihi = simdi.AddDays(-rnd.Next(0, 5))
                };
                db.Urunler.Add(urun);
                await db.SaveChangesAsync();
                urun.Kod = $"{FirmaAyarlari.KodOnEki}-{urun.Id:D4}";
                sira++;

                // Bugünkü stoktan geriye doğru yürüyerek tutarlı bir geçmiş üret
                var gecmis = new List<(DateTime Tarih, HareketTuru Tur, int Miktar, string Not)>();
                int stok = o.Adet;
                double satisOlasiligi = 0.10 + Math.Min(0.22, o.Min / 40.0);
                for (int gun = 0; gun < 60; gun++)
                {
                    var t = simdi.Date.AddDays(-gun).AddHours(9 + rnd.Next(0, 9)).AddMinutes(rnd.Next(0, 60));
                    if (t > simdi) t = simdi.AddMinutes(-rnd.Next(5, 120));

                    if (rnd.NextDouble() < satisOlasiligi)
                    {
                        int q = 1 + rnd.Next(0, Math.Max(1, o.Min / 3));
                        gecmis.Add((t, HareketTuru.Cikis, q, $"{Takimlar[rnd.Next(Takimlar.Length)]} takımına satış"));
                        stok += q; // geriye doğru: satıştan önce stok daha fazlaydı
                    }
                    if (rnd.NextDouble() < 0.045 && stok > 0)
                    {
                        int q = Math.Min(stok, Math.Max(2, o.Min + rnd.Next(0, o.Min + 1)));
                        gecmis.Add((t.AddHours(-1), HareketTuru.Giris, q, Tedarikciler[rnd.Next(Tedarikciler.Length)]));
                        stok -= q;
                    }
                }

                // En eski durum: açılış stoğu
                gecmis.Add((simdi.Date.AddDays(-61).AddHours(10), HareketTuru.Giris, stok, "Açılış stoğu"));

                int anlik = 0;
                foreach (var g in gecmis.OrderBy(x => x.Tarih))
                {
                    if (g.Tur == HareketTuru.Giris && g.Miktar == 0) continue;
                    int fark = g.Tur == HareketTuru.Giris ? g.Miktar : -g.Miktar;
                    var kisi = kisiler[rnd.Next(kisiler.Count)];
                    tumHareketler.Add(new StokHareketi
                    {
                        UrunId = urun.Id, Tur = g.Tur, Miktar = fark, OncekiStok = anlik, SonrakiStok = anlik + fark,
                        BirimFiyat = g.Tur == HareketTuru.Giris ? o.Alis : o.Satis, Aciklama = g.Not,
                        KullaniciId = kisi.Id, KullaniciAdi = kisi.Ad, Tarih = g.Tarih
                    });
                    anlik += fark;
                }
            }

            db.StokHareketleri.AddRange(tumHareketler);
            await db.SaveChangesAsync();
        }
    }
}
