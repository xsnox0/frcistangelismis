using System.Windows;
using System.Windows.Input;
using Frcistan.Altyapi;
using Frcistan.ViewModels;

namespace Frcistan.Pencereler
{
    public partial class KurulumPenceresi : Window
    {
        public KurulumViewModel Vm { get; } = new();

        public KurulumPenceresi()
        {
            InitializeComponent();
            DataContext = Vm;
            Tema.PencereHazirla(this);
            Vm.Tamamlandi += () =>
            {
                if (System.Windows.Interop.ComponentDispatcher.IsThreadModal) DialogResult = true;
                else Close();
            };
            PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape && Diyalog.EnUsttekiniKapat()) e.Handled = true;
            };
        }

        void Kucult_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        void Kapat_Click(object sender, RoutedEventArgs e) => Close();
    }
}
