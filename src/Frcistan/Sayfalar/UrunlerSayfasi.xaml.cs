using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Frcistan.ViewModels;

namespace Frcistan.Sayfalar
{
    public partial class UrunlerSayfasi : UserControl
    {
        public UrunlerSayfasi()
        {
            InitializeComponent();
            // "Diğer işlemler" düğmesinden açılan menü kapanınca sağ tık davranışına geri dön
            tablo.ContextMenu.Closed += (_, _) =>
            {
                tablo.ContextMenu.Placement = PlacementMode.MousePoint;
                tablo.ContextMenu.ClearValue(ContextMenu.PlacementTargetProperty);
            };
        }

        UrunlerViewModel Vm => DataContext as UrunlerViewModel;

        void Tablo_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Yalnızca satıra çift tıklanınca (başlık ya da kaydırma çubuğu değil)
            var kaynak = e.OriginalSource as DependencyObject;
            while (kaynak != null && kaynak is not DataGridRow)
            {
                if (kaynak is ButtonBase || kaynak is DataGridColumnHeader) return;
                kaynak = VisualTreeHelper.GetParent(kaynak);
            }
            if (kaynak is DataGridRow satir && satir.Item is UrunSatiri u)
                Vm?.AcCommand.Execute(u);
        }

        void Diger_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement b && b.DataContext is UrunSatiri u && Vm != null)
            {
                Vm.Secili = u;
                var menu = tablo.ContextMenu;
                menu.PlacementTarget = b;
                menu.Placement = PlacementMode.Bottom;
                menu.DataContext = Vm;
                menu.IsOpen = true;
            }
        }
    }
}
