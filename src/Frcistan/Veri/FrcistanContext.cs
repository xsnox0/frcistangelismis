using Frcistan.Modeller;
using Frcistan.Servisler;
using Microsoft.EntityFrameworkCore;

namespace Frcistan.Veri
{
    /// <summary>
    /// Entity Framework bağlamı. Ayarlara göre SQLite (varsayılan, kurulum gerektirmez)
    /// ya da MySQL / MariaDB (çok kullanıcılı, ağ üzerinden) kullanır.
    /// </summary>
    public class FrcistanContext : DbContext
    {
        readonly VeritabaniBilgisi _bilgi;

        public FrcistanContext() : this(null) { }

        public FrcistanContext(VeritabaniBilgisi bilgi)
        {
            _bilgi = bilgi ?? YerelAyarlar.Mevcut.Veritabani;
        }

        public DbSet<Urun> Urunler { get; set; }
        public DbSet<Kategori> Kategoriler { get; set; }
        public DbSet<Kullanici> Kullanicilar { get; set; }
        public DbSet<StokHareketi> StokHareketleri { get; set; }
        public DbSet<Ayar> Ayarlar { get; set; }

        public bool MySqlMi => _bilgi.Tur == VeritabaniTuru.MySql;

        // Not: MariaDB 10.4, EF'in toplu silme (ExecuteDelete) komutundaki tablo takma adını desteklemiyor.
        // Bu yüzden silmeler her iki veritabanında da çalışan düz SQL ile yapılır.
        static readonly HashSet<string> Tablolar = new() { "Urunler", "Kategoriler", "Kullanicilar", "StokHareketleri", "Ayarlar" };
        static readonly HashSet<string> Sutunlar = new() { "Id", "UrunId", "KategoriId" };

#pragma warning disable EF1002 // tablo ve sütun adları yukarıdaki sabit listelerden gelir, kullanıcı girdisi değildir
        public Task<int> SilAsync(string tablo, string sutun, int deger)
        {
            if (!Tablolar.Contains(tablo) || !Sutunlar.Contains(sutun)) throw new ArgumentException("Geçersiz tablo/sütun");
            return Database.ExecuteSqlRawAsync($"DELETE FROM `{tablo}` WHERE `{sutun}` = {{0}}", deger);
        }

        public Task<int> TumunuSilAsync(string tablo)
        {
            if (!Tablolar.Contains(tablo)) throw new ArgumentException("Geçersiz tablo");
            return Database.ExecuteSqlRawAsync($"DELETE FROM `{tablo}`");
        }
#pragma warning restore EF1002

        protected override void OnConfiguring(DbContextOptionsBuilder o)
        {
            if (_bilgi.Tur == VeritabaniTuru.MySql)
                o.UseMySQL(_bilgi.MySqlBaglantiMetni());
            else
                o.UseSqlite($"Data Source={_bilgi.SqliteDosyasi()}");
        }

        protected override void OnModelCreating(ModelBuilder m)
        {
            m.Entity<Kategori>(e =>
            {
                e.HasIndex(k => k.Ad).IsUnique();
            });

            m.Entity<Urun>(e =>
            {
                e.HasIndex(u => u.Kod).IsUnique();
                e.HasIndex(u => new { u.Marka, u.UrunAdi });
                e.Property(u => u.AlisFiyati).HasPrecision(12, 2);
                e.Property(u => u.SatisFiyati).HasPrecision(12, 2);
                e.HasOne(u => u.Kategori).WithMany(k => k.Urunler)
                 .HasForeignKey(u => u.KategoriId).OnDelete(DeleteBehavior.Restrict);
            });

            m.Entity<StokHareketi>(e =>
            {
                e.HasIndex(h => h.Tarih);
                e.Property(h => h.Tur).HasConversion<int>();
                e.Property(h => h.BirimFiyat).HasPrecision(12, 2);
                e.HasOne(h => h.Urun).WithMany()
                 .HasForeignKey(h => h.UrunId).OnDelete(DeleteBehavior.Cascade);
            });

            m.Entity<Kullanici>(e =>
            {
                e.HasIndex(k => k.KullaniciAdi).IsUnique();
                e.Ignore(k => k.GorunenAd);
            });
        }
    }
}
