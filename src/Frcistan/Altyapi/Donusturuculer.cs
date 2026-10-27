using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Frcistan.Servisler;

namespace Frcistan.Altyapi
{
    /// <summary>bool → Visibility. Parametre "Ters" ise tersine çevirir.</summary>
    public class GorunurConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            bool b = value is bool v && v;
            if (p as string == "Ters") b = !b;
            return b ? Visibility.Visible : Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => value is Visibility v && v == Visibility.Visible;
    }

    /// <summary>Değer dolu mu (null değil, boş metin değil, sıfır değil, boş liste değil)? Görünürlük döndürür.</summary>
    public class DoluGorunurConverter : IValueConverter
    {
        public static bool DoluMu(object value) => value switch
        {
            null => false,
            string s => !string.IsNullOrWhiteSpace(s),
            int i => i != 0,
            decimal d => d != 0,
            double d => d != 0,
            bool b => b,
            ICollection col => col.Count > 0,
            _ => true
        };

        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            bool b = DoluMu(value);
            if (p as string == "Ters") b = !b;
            return b ? Visibility.Visible : Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
    }

    /// <summary>Değer dolu mu? bool döndürür.</summary>
    public class DoluMuConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            bool b = DoluGorunurConverter.DoluMu(value);
            return p as string == "Ters" ? !b : b;
        }
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
    }

    public class TersBoolConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) => !(value is bool b && b);
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => !(value is bool b && b);
    }

    /// <summary>value.ToString() == parametre → true. RadioButton grupları için çift yönlü çalışır.</summary>
    public class EsitMiConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) => string.Equals(value?.ToString(), p?.ToString(), StringComparison.Ordinal);
        public object ConvertBack(object value, Type t, object p, CultureInfo c)
        {
            if (value is bool b && b)
            {
                if (t.IsEnum) return Enum.Parse(t, p.ToString());
                if (t == typeof(int)) return int.Parse(p.ToString());
                return p;
            }
            return Binding.DoNothing;
        }
    }

    public class EsitGorunurConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            var parcalar = (p?.ToString() ?? "").Split('|');
            return parcalar.Contains(value?.ToString()) ? Visibility.Visible : Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
    }

    /// <summary>decimal → "$1.234,56"</summary>
    public class ParaConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) => value switch
        {
            decimal d => Para.Bicimle(d),
            double d => Para.Bicimle((decimal)d),
            int i => Para.Bicimle(i),
            _ => ""
        };
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
    }

    /// <summary>int → "1.234"</summary>
    public class SayiConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) => value switch
        {
            int i => i.ToString("N0", Ui.Tr),
            long l => l.ToString("N0", Ui.Tr),
            decimal d => d.ToString("N0", Ui.Tr),
            double d => d.ToString("N0", Ui.Tr),
            _ => value?.ToString() ?? ""
        };
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
    }

    /// <summary>"#3B82F6" → fırça. Parametre "Yumusak" ise %14 opak fırça döndürür.</summary>
    public class HexFircaConverter : IValueConverter
    {
        static readonly Dictionary<string, SolidColorBrush> Onbellek = new();

        public static SolidColorBrush Firca(string hex, byte? alfa = null)
        {
            hex ??= "#64748B";
            string anahtar = hex + "|" + alfa;
            if (Onbellek.TryGetValue(anahtar, out var f)) return f;
            Color renk;
            try { renk = (Color)ColorConverter.ConvertFromString(hex); }
            catch { renk = Color.FromRgb(0x64, 0x74, 0x8B); }
            if (alfa.HasValue) renk.A = alfa.Value;
            f = new SolidColorBrush(renk);
            f.Freeze();
            Onbellek[anahtar] = f;
            return f;
        }

        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            byte? alfa = (p as string) switch { "Yumusak" => (byte)0x26, "Cok" => (byte)0x55, _ => null };
            return Firca(value as string, alfa);
        }
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
    }

    /// <summary>"E7B8" → ikon karakteri</summary>
    public class IkonKoduConverter : IValueConverter
    {
        public static string Karakter(string kod)
        {
            if (string.IsNullOrWhiteSpace(kod)) return "";
            try { return ((char)System.Convert.ToInt32(kod, 16)).ToString(); }
            catch { return ""; }
        }
        public object Convert(object value, Type t, object p, CultureInfo c) => Karakter(value as string);
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
    }

    /// <summary>"Ayşe Yılmaz" → "AY"</summary>
    public class BasHarfConverter : IValueConverter
    {
        public static string BasHarf(string ad)
        {
            if (string.IsNullOrWhiteSpace(ad)) return "?";
            var parca = ad.Split(new[] { ' ', '.', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
            string s = parca.Length >= 2 ? $"{parca[0][0]}{parca[^1][0]}" : parca[0].Substring(0, Math.Min(2, parca[0].Length));
            // WPF, "İ" ile başlayan baş harfleri küçük harf gibi çizebiliyor; avatarda noktasız I kullanılır
            return s.ToUpper(Ui.Tr).Replace('İ', 'I');
        }
        public object Convert(object value, Type t, object p, CultureInfo c) => BasHarf(value as string);
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
    }

    /// <summary>Tarih → "3 dk önce", "Dün 14:20", "12.10.2026"</summary>
    public class GoreceliTarihConverter : IValueConverter
    {
        public static string Bicimle(DateTime t)
        {
            var simdi = DateTime.Now;
            var fark = simdi - t;
            if (fark.TotalSeconds < 60) return "Az önce";
            if (fark.TotalMinutes < 60) return $"{(int)fark.TotalMinutes} dk önce";
            if (t.Date == simdi.Date) return $"Bugün {t:HH:mm}";
            if (t.Date == simdi.Date.AddDays(-1)) return $"Dün {t:HH:mm}";
            if (fark.TotalDays < 7) return t.ToString("dddd HH:mm", Ui.Tr);
            return t.ToString("dd.MM.yyyy", Ui.Tr);
        }
        public object Convert(object value, Type t, object p, CultureInfo c) => value switch
        {
            DateTime d => Bicimle(d),
            _ => "—"
        };
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
    }

    /// <summary>0..1 arası oranı genişliğe çevirir: değerler [oran, toplamGenislik]</summary>
    public class OranGenislikConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type t, object p, CultureInfo c)
        {
            if (values.Length < 2 || values[0] is not double oran || values[1] is not double genislik || double.IsNaN(genislik)) return 0d;
            return Math.Max(0, Math.Min(1, oran)) * genislik;
        }
        public object[] ConvertBack(object value, Type[] t, object p, CultureInfo c) => throw new NotSupportedException();
    }
}

namespace Frcistan.Altyapi
{
    /// <summary>İki değer eşit mi? (MultiBinding ile seçili renk/ikon vurgusu)</summary>
    public class Esitlik : System.Windows.Data.IMultiValueConverter
    {
        public static Esitlik Ornek { get; } = new();
        public object Convert(object[] v, Type t, object p, System.Globalization.CultureInfo c) =>
            v.Length == 2 && string.Equals(v[0]?.ToString(), v[1]?.ToString(), StringComparison.OrdinalIgnoreCase);
        public object[] ConvertBack(object v, Type[] t, object p, System.Globalization.CultureInfo c) => throw new NotSupportedException();
    }
}

namespace Frcistan.Altyapi
{
    /// <summary>Genişliği kart sütun sayısına çevirir (parametre: en küçük kart genişliği)</summary>
    public class SutunSayisiConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type t, object p, System.Globalization.CultureInfo c)
        {
            double kart = double.TryParse(p?.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var k) ? k : 320;
            return value is double w && w > 0 ? Math.Max(1, (int)(w / kart)) : 3;
        }
        public object ConvertBack(object value, Type t, object p, System.Globalization.CultureInfo c) => throw new NotSupportedException();
    }
}
