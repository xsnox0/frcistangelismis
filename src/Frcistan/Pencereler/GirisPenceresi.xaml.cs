using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Frcistan.Altyapi;
using Frcistan.ViewModels;

namespace Frcistan.Pencereler
{
    public partial class GirisPenceresi : Window
    {
        readonly GirisViewModel _vm = new();

        public GirisPenceresi()
        {
            InitializeComponent();
            DataContext = _vm;
            Tema.PencereHazirla(this);

            _vm.GirisBasarili += () => App.AnaPencereyiAc(this);
            _vm.PropertyChanged += VmDegisti;
            Loaded += async (_, _) => await _vm.HazirlaAsync();
            Closed += (_, _) =>
            {
                _vm.PropertyChanged -= VmDegisti;
                if (Tag as string != "gecis") Application.Current.Shutdown();
            };
            PreviewKeyDown += TusBasildi;
            Activated += (_, _) => CapsKontrol();
        }

        void VmDegisti(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GirisViewModel.Durum) && _vm.Durum == "Giris")
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (string.IsNullOrWhiteSpace(_vm.KullaniciAdi)) kullaniciKutusu.Focus();
                    else sifreKutusu.Focus();
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
            else if (e.PropertyName == nameof(GirisViewModel.Hata) && !string.IsNullOrEmpty(_vm.Hata))
            {
                Salla();
                if (_vm.Durum == "Giris" && !_vm.SifreGoster) sifreKutusu.Focus();
            }
        }

        void TusBasildi(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && Diyalog.EnUsttekiniKapat())
            {
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Enter && Diyalog.Acik.Count == 0)
            {
                var komut = _vm.Durum switch
                {
                    "Giris" => _vm.GirisCommand,
                    "IlkKullanici" => _vm.IlkKullaniciCommand,
                    "Hata" => _vm.TekrarDeneCommand,
                    _ => null
                };
                if (komut?.CanExecute(null) == true)
                {
                    komut.Execute(null);
                    e.Handled = true;
                }
            }
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            CapsKontrol();
        }

        void CapsKontrol() =>
            capsUyari.Visibility = Keyboard.IsKeyToggled(Key.CapsLock) ? Visibility.Visible : Visibility.Collapsed;

        void Salla()
        {
            var a = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(360) };
            foreach (var (t, x) in new[] { (0, 0.0), (60, -9.0), (120, 8.0), (180, -6.0), (240, 4.0), (300, -2.0), (360, 0.0) })
                a.KeyFrames.Add(new EasingDoubleKeyFrame(x, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(t))));
            sallama.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, a);
        }

        void Kucult_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        void Kapat_Click(object sender, RoutedEventArgs e) => Close();
    }
}
