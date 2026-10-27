using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Frcistan.Altyapi;
using Frcistan.Modeller;
using Frcistan.Servisler;
using Frcistan.ViewModels;

namespace Frcistan.Pencereler
{
    public partial class AnaPencere : Window
    {
        public AnaViewModel Vm { get; }

        public AnaPencere()
        {
            InitializeComponent();
            Vm = new AnaViewModel();
            DataContext = Vm;
            Tema.PencereHazirla(this);

            var a = YerelAyarlar.Mevcut;
            Width = Math.Max(MinWidth, a.PencereGenislik);
            Height = Math.Max(MinHeight, a.PencereYukseklik);
            if (a.PencereTamEkran) WindowState = WindowState.Maximized;

            Vm.PropertyChanged += VmDegisti;
            StateChanged += (_, _) => DurumDegisti();
            Loaded += (_, _) =>
            {
                DurumDegisti();
                _ = YedekServisi.OtomatikYedekAsync();
            };
            Closing += Kapaniyor;
            Closed += (_, _) => { Vm.PropertyChanged -= VmDegisti; Vm.Kapat(); };
            PreviewKeyDown += TusBasildi;
        }

        void VmDegisti(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(AnaViewModel.AktifSayfa)) return;
            var sure = TimeSpan.FromMilliseconds(260);
            var kolay = new CubicEase { EasingMode = EasingMode.EaseOut };
            sayfaKabi.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, sure) { EasingFunction = kolay });
            sayfaKaydirma.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, new DoubleAnimation(12, 0, sure) { EasingFunction = kolay });
        }

        void DurumDegisti()
        {
            bool buyuk = WindowState == WindowState.Maximized;
            kok.Margin = buyuk ? new Thickness(7) : new Thickness(0);
            buyutDugmesi.Content = buyuk ? "" : "";
            buyutDugmesi.ToolTip = buyuk ? "Önceki boyut" : "Ekranı kapla";
        }

        void Kapaniyor(object sender, CancelEventArgs e)
        {
            var a = YerelAyarlar.Mevcut;
            a.PencereTamEkran = WindowState == WindowState.Maximized;
            if (WindowState == WindowState.Normal)
            {
                a.PencereGenislik = ActualWidth;
                a.PencereYukseklik = ActualHeight;
            }
            YerelAyarlar.Kaydet();
        }

        void TusBasildi(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (Diyalog.EnUsttekiniKapat()) e.Handled = true;
                return;
            }
            if (Diyalog.Acik.Count > 0) return;

            if (e.Key == Key.F5)
            {
                Vm.Yenile();
                e.Handled = true;
                return;
            }

            bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
            if (ctrl && e.Key == Key.K)
            {
                genelArama.Focus();
                genelArama.SelectAll();
                e.Handled = true;
            }
            else if (ctrl && e.Key == Key.S)
            {
                Vm.HizliSatisCommand.Execute(null);
                e.Handled = true;
            }
            else if (ctrl && e.Key == Key.N && Vm.AktifSayfa is UrunlerViewModel u)
            {
                u.YeniCommand.Execute(null);
                e.Handled = true;
            }
            else if (ctrl && e.Key >= Key.D1 && e.Key <= Key.D9)
            {
                int i = e.Key - Key.D1;
                if (i < Vm.Menu.Count) Vm.SeciliMenu = Vm.Menu[i];
                e.Handled = true;
            }
        }

        void KullaniciMenusu_Click(object sender, RoutedEventArgs e)
        {
            var menu = kullaniciMenuDugmesi.ContextMenu;
            menu.PlacementTarget = kullaniciMenuDugmesi;
            menu.DataContext = Vm;
            menu.IsOpen = true;
        }

        void ZilOgesi_Click(object sender, RoutedEventArgs e) => zilDugmesi.IsChecked = false;
        void Zil_Closed(object sender, EventArgs e) => zilDugmesi.IsChecked = false;

        void Kucult_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        void Buyut_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        void Kapat_Click(object sender, RoutedEventArgs e) => Close();
    }
}
