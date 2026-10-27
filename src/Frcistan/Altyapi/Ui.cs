using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Frcistan.Altyapi
{
    /// <summary>
    /// XAML'de kontrollere eklenen yardımcı özellikler:
    /// ikon, ipucu metni (placeholder), hata durumu, şifre bağlama, sayı girişi ve ilk odak.
    /// </summary>
    public static class Ui
    {
        // ---- İkon (Segoe Fluent Icons karakteri) ----
        public static readonly DependencyProperty IkonProperty = DependencyProperty.RegisterAttached(
            "Ikon", typeof(string), typeof(Ui), new PropertyMetadata(null));
        public static string GetIkon(DependencyObject o) => (string)o.GetValue(IkonProperty);
        public static void SetIkon(DependencyObject o, string v) => o.SetValue(IkonProperty, v);

        // ---- İpucu metni ----
        public static readonly DependencyProperty IpucuProperty = DependencyProperty.RegisterAttached(
            "Ipucu", typeof(string), typeof(Ui), new PropertyMetadata(null));
        public static string GetIpucu(DependencyObject o) => (string)o.GetValue(IpucuProperty);
        public static void SetIpucu(DependencyObject o, string v) => o.SetValue(IpucuProperty, v);

        // ---- Hata durumu (kırmızı kenarlık) ----
        public static readonly DependencyProperty HataVarProperty = DependencyProperty.RegisterAttached(
            "HataVar", typeof(bool), typeof(Ui), new PropertyMetadata(false));
        public static bool GetHataVar(DependencyObject o) => (bool)o.GetValue(HataVarProperty);
        public static void SetHataVar(DependencyObject o, bool v) => o.SetValue(HataVarProperty, v);

        // ---- Köşe yarıçapı ----
        public static readonly DependencyProperty KoseProperty = DependencyProperty.RegisterAttached(
            "Kose", typeof(CornerRadius), typeof(Ui), new PropertyMetadata(new CornerRadius(8)));
        public static CornerRadius GetKose(DependencyObject o) => (CornerRadius)o.GetValue(KoseProperty);
        public static void SetKose(DependencyObject o, CornerRadius v) => o.SetValue(KoseProperty, v);

        // ---- PasswordBox: dolu mu (ipucu metnini gizlemek için) ----
        public static readonly DependencyProperty DoluProperty = DependencyProperty.RegisterAttached(
            "Dolu", typeof(bool), typeof(Ui), new PropertyMetadata(false));
        public static bool GetDolu(DependencyObject o) => (bool)o.GetValue(DoluProperty);
        public static void SetDolu(DependencyObject o, bool v) => o.SetValue(DoluProperty, v);

        // ---- PasswordBox: bağlanabilir şifre ----
        public static readonly DependencyProperty SifreProperty = DependencyProperty.RegisterAttached(
            "Sifre", typeof(string), typeof(Ui),
            new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, SifreDegisti));
        public static string GetSifre(DependencyObject o) => (string)o.GetValue(SifreProperty);
        public static void SetSifre(DependencyObject o, string v) => o.SetValue(SifreProperty, v);

        static readonly DependencyProperty GuncelleniyorProperty = DependencyProperty.RegisterAttached(
            "Guncelleniyor", typeof(bool), typeof(Ui), new PropertyMetadata(false));

        static void SifreDegisti(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not PasswordBox kutu) return;
            kutu.PasswordChanged -= Kutu_PasswordChanged;
            if (!(bool)kutu.GetValue(GuncelleniyorProperty))
                kutu.Password = (string)e.NewValue ?? "";
            SetDolu(kutu, kutu.Password.Length > 0);
            kutu.PasswordChanged += Kutu_PasswordChanged;
        }

        static void Kutu_PasswordChanged(object sender, RoutedEventArgs e)
        {
            var kutu = (PasswordBox)sender;
            kutu.SetValue(GuncelleniyorProperty, true);
            SetSifre(kutu, kutu.Password);
            kutu.SetValue(GuncelleniyorProperty, false);
            SetDolu(kutu, kutu.Password.Length > 0);
        }

        // ---- Sayı girişi: "Tam" veya "Ondalik" ----
        public static readonly DependencyProperty SayiProperty = DependencyProperty.RegisterAttached(
            "Sayi", typeof(string), typeof(Ui), new PropertyMetadata(null, SayiDegisti));
        public static string GetSayi(DependencyObject o) => (string)o.GetValue(SayiProperty);
        public static void SetSayi(DependencyObject o, string v) => o.SetValue(SayiProperty, v);

        static void SayiDegisti(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TextBox kutu) return;
            kutu.PreviewTextInput -= Sayi_PreviewTextInput;
            DataObject.RemovePastingHandler(kutu, Sayi_Yapistir);
            if (e.NewValue != null)
            {
                kutu.PreviewTextInput += Sayi_PreviewTextInput;
                DataObject.AddPastingHandler(kutu, Sayi_Yapistir);
                InputMethod.SetIsInputMethodEnabled(kutu, false);
            }
        }

        static bool SayiGecerli(TextBox kutu, string eklenecek)
        {
            bool ondalik = GetSayi(kutu) == "Ondalik";
            string yeni = kutu.Text.Remove(kutu.SelectionStart, kutu.SelectionLength).Insert(kutu.SelectionStart, eklenecek);
            foreach (char c in yeni)
            {
                if (char.IsDigit(c)) continue;
                if (ondalik && (c == ',' || c == '.')) continue;
                return false;
            }
            return true;
        }

        static void Sayi_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !SayiGecerli((TextBox)sender, e.Text);
        }

        static void Sayi_Yapistir(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetData(DataFormats.Text) is string metin && SayiGecerli((TextBox)sender, metin.Trim()))
                return;
            e.CancelCommand();
        }

        // ---- Pencere/kontrol açılınca odak ----
        public static readonly DependencyProperty IlkOdakProperty = DependencyProperty.RegisterAttached(
            "IlkOdak", typeof(bool), typeof(Ui), new PropertyMetadata(false, IlkOdakDegisti));
        public static bool GetIlkOdak(DependencyObject o) => (bool)o.GetValue(IlkOdakProperty);
        public static void SetIlkOdak(DependencyObject o, bool v) => o.SetValue(IlkOdakProperty, v);

        static void IlkOdakDegisti(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement el && (bool)e.NewValue)
            {
                el.Loaded += (s, _) =>
                {
                    el.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        el.Focus();
                        Keyboard.Focus(el);
                        if (el is TextBox t) t.SelectAll();
                    }), System.Windows.Threading.DispatcherPriority.Input);
                };
            }
        }

        public static CultureInfo Tr { get; } = CultureInfo.GetCultureInfo("tr-TR");
    }
}
