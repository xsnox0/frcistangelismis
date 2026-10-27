using Frcistan.Modeller;
using Frcistan.Veri;
using Microsoft.EntityFrameworkCore;

namespace Frcistan.Servisler
{
    public static class UrunServisi
    {
        public static readonly string[] BilinenMarkalar =
        {
            "AndyMark", "Arducam", "CTR Electronics", "goBILDA", "Kauai Labs", "Limelight", "MK Battery",
            "National Instruments", "Playing With Fusion", "Redux Robotics", "REV Robotics", "Studica",
            "Swerve Drive Specialties", "ThriftyBot", "VEX Robotics", "Vivid Hosting", "WCP"
        };

        public static async Task<List<Urun>> TumunuGetirAsync()
        {
            await using var db = new FrcistanContext();
            var liste = await db.Urunler.AsNoTracking().Include(u => u.Kategori).ToListAsync();
            return liste.OrderBy(u => u.UrunAdi, StringComparer.Create(Altyapi.Ui.Tr, true)).ToList();
        }

        public static async Task<Urun> GetirAsync(int id)
        {
            await using var db = new FrcistanContext();
            return await db.Urunler.AsNoTracking().Include(u => u.Kategori).FirstOrDefaultAsync(u => u.Id == id);
        }

        public static async Task<List<string>> MarkalarAsync()
        {
            await using var db = new FrcistanContext();
            var dbMarkalar = await db.Urunler.Select(u => u.Marka).Distinct().ToListAsync();
            return dbMarkalar.Concat(BilinenMarkalar)
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .GroupBy(Metin.Normallestir).Select(g => g.First())
                .OrderBy(m => m, StringComparer.Create(Altyapi.Ui.Tr, true)).ToList();
        }

        /// <summary>Aynı marka + ürün adında başka ürün var mı? (Türkçe karakter ve büyük/küçük harf duyarsız)</summary>
        public static async Task<Urun> AyniUrunAsync(string marka, string urunAdi, int haricId = 0)
        {
            await using var db = new FrcistanContext();
            var adaylar = await db.Urunler.AsNoTracking().Where(u => u.Id != haricId).ToListAsync();
            return adaylar.FirstOrDefault(u => Metin.Ayni(u.Marka, marka) && Metin.Ayni(u.UrunAdi, urunAdi));
        }

        public static async Task<bool> KodKullaniliyorMuAsync(string kod, int haricId = 0)
        {
            if (string.IsNullOrWhiteSpace(kod)) return false;
            await using var db = new FrcistanContext();
            var kodlar = await db.Urunler.Where(u => u.Id != haricId && u.Kod != null).Select(u => u.Kod).ToListAsync();
            return kodlar.Any(k => string.Equals(k.Trim(), kod.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public static async Task<string> SiradakiKodAsync()
        {
            await using var db = new FrcistanContext();
            int sonId = await db.Urunler.MaxAsync(u => (int?)u.Id) ?? 0;
            return $"{FirmaAyarlari.KodOnEki}-{sonId + 1:D4}";
        }

        /// <summary>Yeni ürünü ekler ya da var olanı günceller. Stok farkı hareket olarak kaydedilir.</summary>
        public static async Task<Urun> KaydetAsync(Urun veri)
        {
            if (string.IsNullOrWhiteSpace(veri.UrunAdi)) throw new KuralHatasi("Ürün adı boş bırakılamaz.");
            if (string.IsNullOrWhiteSpace(veri.Marka)) throw new KuralHatasi("Marka boş bırakılamaz.");
            if (veri.Adet < 0) throw new KuralHatasi("Stok adedi negatif olamaz.");
            if (veri.AlisFiyati < 0 || veri.SatisFiyati < 0) throw new KuralHatasi("Fiyat negatif olamaz.");

            await using var db = new FrcistanContext();
            await using var islem = await db.Database.BeginTransactionAsync();
            var simdi = DateTime.Now;
            Urun u;

            if (veri.Id == 0)
            {
                u = new Urun { OlusturmaTarihi = simdi };
                Kopyala(veri, u);
                u.GuncellemeTarihi = simdi;
                db.Urunler.Add(u);
                await db.SaveChangesAsync();

                if (string.IsNullOrWhiteSpace(u.Kod))
                {
                    string kod = $"{FirmaAyarlari.KodOnEki}-{u.Id:D4}";
                    while (await db.Urunler.AnyAsync(x => x.Kod == kod && x.Id != u.Id)) kod += "A";
                    u.Kod = kod;
                }

                if (u.Adet > 0)
                    db.StokHareketleri.Add(Hareket(u.Id, HareketTuru.Giris, u.Adet, 0, u.Adet, u.AlisFiyati, "Açılış stoğu"));
            }
            else
            {
                u = await db.Urunler.FirstOrDefaultAsync(x => x.Id == veri.Id)
                    ?? throw new KuralHatasi("Ürün bulunamadı. Başka bir kullanıcı silmiş olabilir.");
                int eskiAdet = u.Adet;
                Kopyala(veri, u);
                u.GuncellemeTarihi = simdi;
                if (string.IsNullOrWhiteSpace(u.Kod)) u.Kod = $"{FirmaAyarlari.KodOnEki}-{u.Id:D4}";

                if (eskiAdet != u.Adet)
                    db.StokHareketleri.Add(Hareket(u.Id, HareketTuru.Duzeltme, u.Adet - eskiAdet, eskiAdet, u.Adet, u.AlisFiyati, "Ürün kartından stok düzeltmesi"));
            }

            await db.SaveChangesAsync();
            await islem.CommitAsync();
            return await GetirAsync(u.Id);
        }

        static void Kopyala(Urun k, Urun h)
        {
            h.Kod = string.IsNullOrWhiteSpace(k.Kod) ? null : k.Kod.Trim();
            h.KategoriId = k.KategoriId;
            h.Marka = k.Marka.Trim();
            h.UrunAdi = k.UrunAdi.Trim();
            h.Aciklama = string.IsNullOrWhiteSpace(k.Aciklama) ? null : k.Aciklama.Trim();
            h.Konum = string.IsNullOrWhiteSpace(k.Konum) ? null : k.Konum.Trim();
            h.Adet = k.Adet;
            h.MinStok = Math.Max(0, k.MinStok);
            h.AlisFiyati = k.AlisFiyati;
            h.SatisFiyati = k.SatisFiyati;
        }

        internal static StokHareketi Hareket(int urunId, HareketTuru tur, int miktar, int onceki, int sonraki, decimal fiyat, string aciklama) => new()
        {
            UrunId = urunId,
            Tur = tur,
            Miktar = miktar,
            OncekiStok = onceki,
            SonrakiStok = sonraki,
            BirimFiyat = fiyat,
            Aciklama = aciklama,
            KullaniciId = Oturum.Kullanici?.Id,
            KullaniciAdi = Oturum.Kullanici?.GorunenAd,
            Tarih = DateTime.Now
        };

        public static async Task<int> HareketSayisiAsync(int urunId)
        {
            await using var db = new FrcistanContext();
            return await db.StokHareketleri.CountAsync(h => h.UrunId == urunId);
        }

        public static async Task SilAsync(int id)
        {
            await using var db = new FrcistanContext();
            await using var islem = await db.Database.BeginTransactionAsync();
            await db.SilAsync("StokHareketleri", "UrunId", id);
            await db.SilAsync("Urunler", "Id", id);
            await islem.CommitAsync();
        }
    }

    public static class StokServisi
    {
        /// <summary>
        /// Stok hareketi kaydeder. Giriş/çıkışta miktar pozitif girilir; düzeltmede yeni stok adedi verilir.
        /// Stok, veritabanında tek bir atomik komutla güncellenir; aynı anda işlem yapan kullanıcılar stoğu bozamaz.
        /// </summary>
        public static async Task<StokHareketi> HareketEkleAsync(int urunId, HareketTuru tur, int miktar, decimal birimFiyat, string aciklama)
        {
            if (tur != HareketTuru.Duzeltme && miktar <= 0) throw new KuralHatasi("Miktar sıfırdan büyük olmalı.");
            if (tur == HareketTuru.Duzeltme && miktar < 0) throw new KuralHatasi("Yeni stok adedi negatif olamaz.");

            await using var db = new FrcistanContext();
            await using var islem = await db.Database.BeginTransactionAsync();
            var simdi = DateTime.Now;

            var urun = await db.Urunler.AsNoTracking().FirstOrDefaultAsync(u => u.Id == urunId)
                ?? throw new KuralHatasi("Ürün bulunamadı. Başka bir kullanıcı silmiş olabilir.");

            int fark;
            if (tur == HareketTuru.Duzeltme)
            {
                fark = miktar - urun.Adet;
                if (fark == 0) throw new KuralHatasi("Yeni stok adedi mevcut stokla aynı.");
                await db.Urunler.Where(u => u.Id == urunId)
                    .ExecuteUpdateAsync(s => s.SetProperty(u => u.Adet, miktar).SetProperty(u => u.GuncellemeTarihi, simdi));
            }
            else
            {
                fark = tur == HareketTuru.Giris ? miktar : -miktar;
                int etkilenen = await db.Urunler.Where(u => u.Id == urunId && u.Adet + fark >= 0)
                    .ExecuteUpdateAsync(s => s.SetProperty(u => u.Adet, u => u.Adet + fark).SetProperty(u => u.GuncellemeTarihi, simdi));
                if (etkilenen == 0)
                    throw new KuralHatasi($"Stokta yeterli ürün yok. Mevcut stok: {urun.Adet} adet.");
            }

            int sonraki = await db.Urunler.Where(u => u.Id == urunId).Select(u => u.Adet).FirstAsync();
            var h = UrunServisi.Hareket(urunId, tur, fark, sonraki - fark, sonraki, birimFiyat,
                string.IsNullOrWhiteSpace(aciklama) ? null : aciklama.Trim());
            db.StokHareketleri.Add(h);
            await db.SaveChangesAsync();
            await islem.CommitAsync();
            return h;
        }

        public static async Task<List<StokHareketi>> GetirAsync(DateTime? baslangic = null, int? urunId = null, int? enFazla = null)
        {
            await using var db = new FrcistanContext();
            var q = db.StokHareketleri.AsNoTracking().Include(h => h.Urun).ThenInclude(u => u.Kategori).AsQueryable();
            if (baslangic.HasValue) q = q.Where(h => h.Tarih >= baslangic.Value);
            if (urunId.HasValue) q = q.Where(h => h.UrunId == urunId.Value);
            q = q.OrderByDescending(h => h.Tarih).ThenByDescending(h => h.Id);
            if (enFazla.HasValue) q = q.Take(enFazla.Value);
            return await q.ToListAsync();
        }
    }

    public static class KategoriServisi
    {
        public static async Task<List<Kategori>> GetirAsync()
        {
            await using var db = new FrcistanContext();
            var liste = await db.Kategoriler.AsNoTracking().ToListAsync();
            return liste.OrderBy(k => k.Sira).ThenBy(k => k.Ad, StringComparer.Create(Altyapi.Ui.Tr, true)).ToList();
        }

        public static async Task<Kategori> KaydetAsync(Kategori veri)
        {
            if (string.IsNullOrWhiteSpace(veri.Ad)) throw new KuralHatasi("Kategori adı boş bırakılamaz.");
            await using var db = new FrcistanContext();
            var digerleri = await db.Kategoriler.AsNoTracking().Where(k => k.Id != veri.Id).ToListAsync();
            if (digerleri.Any(k => Metin.Ayni(k.Ad, veri.Ad)))
                throw new KuralHatasi($"\"{veri.Ad.Trim()}\" adında bir kategori zaten var.");

            Kategori k;
            if (veri.Id == 0)
            {
                k = new Kategori { Sira = digerleri.Count == 0 ? 0 : digerleri.Max(x => x.Sira) + 1 };
                db.Kategoriler.Add(k);
            }
            else
            {
                k = await db.Kategoriler.FindAsync(veri.Id) ?? throw new KuralHatasi("Kategori bulunamadı.");
            }
            k.Ad = veri.Ad.Trim();
            k.Renk = veri.Renk;
            k.Ikon = veri.Ikon;
            await db.SaveChangesAsync();
            return k;
        }

        public static async Task<int> UrunSayisiAsync(int id)
        {
            await using var db = new FrcistanContext();
            return await db.Urunler.CountAsync(u => u.KategoriId == id);
        }

        /// <summary>Kategoriyi siler; içinde ürün varsa önce hedef kategoriye taşır.</summary>
        public static async Task SilAsync(int id, int? tasinacakKategoriId)
        {
            await using var db = new FrcistanContext();
            await using var islem = await db.Database.BeginTransactionAsync();
            if (tasinacakKategoriId.HasValue)
                await db.Urunler.Where(u => u.KategoriId == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(u => u.KategoriId, tasinacakKategoriId.Value));
            else if (await db.Urunler.AnyAsync(u => u.KategoriId == id))
                throw new KuralHatasi("Bu kategoride ürünler var. Önce ürünleri başka bir kategoriye taşıyın.");
            await db.SilAsync("Kategoriler", "Id", id);
            await islem.CommitAsync();
        }
    }

    public static class KullaniciServisi
    {
        public static async Task<Kullanici> GirisYapAsync(string kullaniciAdi, string sifre)
        {
            await using var db = new FrcistanContext();
            var adlar = await db.Kullanicilar.ToListAsync();
            var k = adlar.FirstOrDefault(x => string.Equals(x.KullaniciAdi, kullaniciAdi?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (k == null || !SifreHasher.Dogrula(sifre, k.SifreHash)) return null;
            if (!k.Aktif) throw new KuralHatasi("Bu hesap devre dışı bırakılmış. Yöneticinizle görüşün.");
            k.SonGiris = DateTime.Now;
            await db.SaveChangesAsync();
            return k;
        }

        public static async Task<List<Kullanici>> GetirAsync()
        {
            await using var db = new FrcistanContext();
            var liste = await db.Kullanicilar.AsNoTracking().ToListAsync();
            return liste.OrderByDescending(k => k.Rol == Roller.Admin).ThenBy(k => k.GorunenAd, StringComparer.Create(Altyapi.Ui.Tr, true)).ToList();
        }

        public static async Task<Kullanici> KaydetAsync(Kullanici veri, string yeniSifre)
        {
            string ad = veri.KullaniciAdi?.Trim() ?? "";
            if (ad.Length < 3) throw new KuralHatasi("Kullanıcı adı en az 3 karakter olmalı.");
            if (ad.Any(char.IsWhiteSpace)) throw new KuralHatasi("Kullanıcı adında boşluk olamaz.");
            if (veri.Id == 0 || !string.IsNullOrEmpty(yeniSifre))
            {
                var h = SifreHasher.KuralHatasi(yeniSifre);
                if (h != null) throw new KuralHatasi(h);
            }

            await using var db = new FrcistanContext();
            var tumu = await db.Kullanicilar.ToListAsync();
            if (tumu.Any(k => k.Id != veri.Id && string.Equals(k.KullaniciAdi, ad, StringComparison.OrdinalIgnoreCase)))
                throw new KuralHatasi($"\"{ad}\" kullanıcı adı zaten kullanılıyor.");

            Kullanici x;
            if (veri.Id == 0)
            {
                x = new Kullanici { OlusturmaTarihi = DateTime.Now };
                db.Kullanicilar.Add(x);
            }
            else
            {
                x = tumu.FirstOrDefault(k => k.Id == veri.Id) ?? throw new KuralHatasi("Kullanıcı bulunamadı.");
                bool yoneticiliktenCikiyor = x.Rol == Roller.Admin && x.Aktif && (veri.Rol != Roller.Admin || !veri.Aktif);
                if (yoneticiliktenCikiyor && tumu.Count(k => k.Rol == Roller.Admin && k.Aktif) <= 1)
                    throw new KuralHatasi("Sistemde en az bir aktif yönetici kalmalı.");
                if (x.Id == Oturum.Kullanici?.Id && !veri.Aktif)
                    throw new KuralHatasi("Kendi hesabınızı devre dışı bırakamazsınız.");
            }

            x.KullaniciAdi = ad;
            x.AdSoyad = string.IsNullOrWhiteSpace(veri.AdSoyad) ? null : veri.AdSoyad.Trim();
            x.Rol = veri.Rol == Roller.Admin ? Roller.Admin : Roller.Personel;
            x.Aktif = veri.Aktif;
            if (!string.IsNullOrEmpty(yeniSifre)) x.SifreHash = SifreHasher.Hashle(yeniSifre);
            await db.SaveChangesAsync();
            return x;
        }

        public static async Task SifreDegistirAsync(int id, string eskiSifre, string yeniSifre)
        {
            var h = SifreHasher.KuralHatasi(yeniSifre);
            if (h != null) throw new KuralHatasi(h);
            await using var db = new FrcistanContext();
            var k = await db.Kullanicilar.FindAsync(id) ?? throw new KuralHatasi("Kullanıcı bulunamadı.");
            if (eskiSifre != null && !SifreHasher.Dogrula(eskiSifre, k.SifreHash))
                throw new KuralHatasi("Mevcut şifreniz hatalı.");
            k.SifreHash = SifreHasher.Hashle(yeniSifre);
            await db.SaveChangesAsync();
        }

        public static async Task SilAsync(int id)
        {
            if (id == Oturum.Kullanici?.Id) throw new KuralHatasi("Kendi hesabınızı silemezsiniz.");
            await using var db = new FrcistanContext();
            var tumu = await db.Kullanicilar.ToListAsync();
            var k = tumu.FirstOrDefault(x => x.Id == id);
            if (k == null) return;
            if (k.Rol == Roller.Admin && k.Aktif && tumu.Count(x => x.Rol == Roller.Admin && x.Aktif) <= 1)
                throw new KuralHatasi("Sistemdeki son yönetici silinemez.");
            db.Kullanicilar.Remove(k);
            await db.SaveChangesAsync();
        }
    }
}
