using System.Windows;
using System.Windows.Controls;

namespace Frcistan.Kontroller
{
    /// <summary>Diyaloglar için ortak çerçeve: başlık, ikon, kapat düğmesi, içerik, hata alanı ve alt düğme çubuğu.</summary>
    public class DiyalogGovdesi : ContentControl
    {
        public static readonly DependencyProperty BaslikProperty = DependencyProperty.Register(nameof(Baslik), typeof(string), typeof(DiyalogGovdesi));
        public static readonly DependencyProperty AltBaslikProperty = DependencyProperty.Register(nameof(AltBaslik), typeof(string), typeof(DiyalogGovdesi));
        public static readonly DependencyProperty IkonProperty = DependencyProperty.Register(nameof(Ikon), typeof(string), typeof(DiyalogGovdesi));
        public static readonly DependencyProperty AltProperty = DependencyProperty.Register(nameof(Alt), typeof(object), typeof(DiyalogGovdesi));
        public static readonly DependencyProperty HataMetniProperty = DependencyProperty.Register(nameof(HataMetni), typeof(string), typeof(DiyalogGovdesi));
        public static readonly DependencyProperty MesgulProperty = DependencyProperty.Register(nameof(Mesgul), typeof(bool), typeof(DiyalogGovdesi));
        public static readonly DependencyProperty KaydirmaProperty = DependencyProperty.Register(nameof(Kaydirma), typeof(bool), typeof(DiyalogGovdesi), new PropertyMetadata(true));

        public string Baslik { get => (string)GetValue(BaslikProperty); set => SetValue(BaslikProperty, value); }
        public string AltBaslik { get => (string)GetValue(AltBaslikProperty); set => SetValue(AltBaslikProperty, value); }
        public string Ikon { get => (string)GetValue(IkonProperty); set => SetValue(IkonProperty, value); }
        public object Alt { get => GetValue(AltProperty); set => SetValue(AltProperty, value); }
        public string HataMetni { get => (string)GetValue(HataMetniProperty); set => SetValue(HataMetniProperty, value); }
        public bool Mesgul { get => (bool)GetValue(MesgulProperty); set => SetValue(MesgulProperty, value); }
        public bool Kaydirma { get => (bool)GetValue(KaydirmaProperty); set => SetValue(KaydirmaProperty, value); }
    }
}
