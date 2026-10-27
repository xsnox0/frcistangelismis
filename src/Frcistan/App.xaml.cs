using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using Frcistan.Altyapi;
using Frcistan.Pencereler;
using Frcistan.Servisler;

namespace Frcistan
{
    public partial class App : Application
    {
        static bool _cikisYapiliyor;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var tr = CultureInfo.GetCultureInfo("tr-TR");
            CultureInfo.DefaultThreadCurrentCulture = tr;
            CultureInfo.DefaultThreadCurrentUICulture = tr;
            Thread.CurrentThread.CurrentCulture = tr;
            Thread.CurrentThread.CurrentUICulture = tr;
            FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
                new FrameworkPropertyMetadata(XmlLanguage.GetLanguage("tr-TR")));

            DispatcherUnhandledException += BeklenmeyenHata;
            TaskScheduler.UnobservedTaskException += (_, a) => { Gunluk.Yaz("Gözlenmeyen görev hatası", a.Exception); a.SetObserved(); };

            YerelAyarlar.Yukle();

#if DEBUG
            if (e.Args.Length >= 2 && e.Args[0] == "--ekran-goruntusu")
            {
                await EkranGoruntusu.CalistirAsync(e.Args[1]);
                Shutdown();
                return;
            }
#endif
            Tema.Uygula(YerelAyarlar.Mevcut.Tema);

            if (!YerelAyarlar.DosyaVarMi)
            {
                var kurulum = new KurulumPenceresi();
                if (kurulum.ShowDialog() != true)
                {
                    Shutdown();
                    return;
                }
            }

            GirisGoster();
            await Task.CompletedTask;
        }

        void BeklenmeyenHata(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Gunluk.Yaz("Beklenmeyen hata", e.Exception);
            e.Handled = true;
            try
            {
                if (Diyalog.Acik.Count < 3)
                    _ = Diyalog.Hata(e.Exception, "Beklenmeyen bir hata oluştu");
            }
            catch { }
        }

        public static void GirisGoster()
        {
            _cikisYapiliyor = false;
            var g = new GirisPenceresi();
            Current.MainWindow = g;
            g.Show();
        }

        /// <summary>Başarılı girişten sonra ana pencereyi açar</summary>
        public static void AnaPencereyiAc(Window giris)
        {
            var ana = new AnaPencere();
            Current.MainWindow = ana;
            ana.Closed += (_, _) =>
            {
                if (_cikisYapiliyor) GirisGoster();
                else Current.Shutdown();
            };
            ana.Show();
            giris.Tag = "gecis";
            giris.Close();
        }

        /// <summary>Oturumu kapatır, giriş ekranına döner</summary>
        public static void OturumuKapat()
        {
            Oturum.Kullanici = null;
            _cikisYapiliyor = true;
            Diyalog.Acik.Clear();
            Current.MainWindow?.Close();
        }

        /// <summary>Programı yeniden başlatır (veritabanı değişikliği, geri yükleme sonrası)</summary>
        public static void YenidenBaslat()
        {
            try
            {
                var exe = Environment.ProcessPath;
                if (exe != null) System.Diagnostics.Process.Start(exe);
            }
            catch (Exception ex)
            {
                Gunluk.Yaz("Yeniden başlatılamadı", ex);
            }
            Current.Shutdown();
        }
    }
}
