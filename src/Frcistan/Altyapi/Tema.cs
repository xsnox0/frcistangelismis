using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;

namespace Frcistan.Altyapi
{
    /// <summary>Koyu / açık tema geçişi</summary>
    public static class Tema
    {
        public static event Action Degisti;

        public static bool Koyu { get; private set; } = true;

        public static void Uygula(string ad)
        {
            Koyu = ad != "Acik";
            var kaynaklar = Application.Current.Resources.MergedDictionaries;
            var eski = kaynaklar.FirstOrDefault(d => d.Source?.OriginalString.Contains("Renkler.") == true);
            var yeni = new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/Temalar/Renkler.{(Koyu ? "Koyu" : "Acik")}.xaml")
            };
            if (eski != null)
                kaynaklar[kaynaklar.IndexOf(eski)] = yeni;
            else
                kaynaklar.Insert(0, yeni);

            foreach (Window w in Application.Current.Windows)
                PencereKoyuBaslik(w);

            Degisti?.Invoke();
        }

        public static Brush Firca(string anahtar) =>
            Application.Current?.TryFindResource(anahtar) as Brush ?? Brushes.Gray;

        public static Color Renk(string anahtar) => Firca(anahtar) is SolidColorBrush s ? s.Color : Colors.Gray;

        // ---- Windows 11: yuvarlak köşe ve koyu sistem çerçevesi ----
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public static void PencereHazirla(Window w)
        {
            w.SourceInitialized += (_, _) =>
            {
                var h = new WindowInteropHelper(w).Handle;
                int yuvarlak = 2; // DWMWCP_ROUND
                try { DwmSetWindowAttribute(h, 33, ref yuvarlak, sizeof(int)); } catch { }
                PencereKoyuBaslik(w);
            };
        }

        static void PencereKoyuBaslik(Window w)
        {
            try
            {
                var h = new WindowInteropHelper(w).Handle;
                if (h == IntPtr.Zero) return;
                int koyu = Koyu ? 1 : 0;
                DwmSetWindowAttribute(h, 20, ref koyu, sizeof(int)); // DWMWA_USE_IMMERSIVE_DARK_MODE
            }
            catch { }
        }
    }

    /// <summary>Kaynak anahtarını fırçaya çevirir: "Basari" → {DynamicResource Basari}. Parametre son ek ekler ("Yumusak").</summary>
    public class KaynakFircaConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) =>
            Tema.Firca((value as string ?? "Vurgu") + (p as string ?? ""));
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
    }
}
