using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Frcistan.ViewModels;

namespace Frcistan.Sayfalar
{
    public partial class HareketlerSayfasi : UserControl
    {
        public HareketlerSayfasi()
        {
            InitializeComponent();
        }

        void Tablo_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var kaynak = e.OriginalSource as DependencyObject;
            while (kaynak != null && kaynak is not DataGridRow)
                kaynak = VisualTreeHelper.GetParent(kaynak);
            if (kaynak is DataGridRow satir && satir.Item is HareketSatiri h && DataContext is HareketlerViewModel vm)
                vm.UrunGecmisiCommand.Execute(h);
        }
    }
}
