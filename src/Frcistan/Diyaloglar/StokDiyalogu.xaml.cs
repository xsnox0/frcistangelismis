using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Frcistan.ViewModels;

namespace Frcistan.Diyaloglar
{
    public partial class StokDiyalogu : UserControl
    {
        public StokDiyalogu()
        {
            InitializeComponent();
            DataContextChanged += (_, e) =>
            {
                if (e.OldValue is INotifyPropertyChanged eski) eski.PropertyChanged -= VmDegisti;
                if (e.NewValue is INotifyPropertyChanged yeni) yeni.PropertyChanged += VmDegisti;
            };
            Loaded += (_, _) => { if (Vm?.UrunSecili == true) MiktaraOdaklan(); };
        }

        StokDiyalogViewModel Vm => DataContext as StokDiyalogViewModel;

        void VmDegisti(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(StokDiyalogViewModel.Urun) or "")
            {
                if (Vm?.UrunSecili == true) MiktaraOdaklan();
                else Dispatcher.BeginInvoke(new Action(() => aramaKutusu.Focus()), System.Windows.Threading.DispatcherPriority.Input);
            }
        }

        void MiktaraOdaklan() => Dispatcher.BeginInvoke(new Action(() =>
        {
            miktarKutusu.Focus();
            miktarKutusu.SelectAll();
        }), System.Windows.Threading.DispatcherPriority.Input);

        void Arama_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Vm == null) return;
            var liste = Vm.Adaylar;
            int i = Vm.SeciliAday == null ? -1 : liste.IndexOf(Vm.SeciliAday);
            if (e.Key == Key.Down && liste.Count > 0)
            {
                Vm.SeciliAday = liste[Math.Min(liste.Count - 1, i + 1)];
                adayListesi.ScrollIntoView(Vm.SeciliAday);
                e.Handled = true;
            }
            else if (e.Key == Key.Up && liste.Count > 0)
            {
                Vm.SeciliAday = liste[Math.Max(0, i - 1)];
                adayListesi.ScrollIntoView(Vm.SeciliAday);
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && Vm.SeciliAday != null)
            {
                Vm.UrunSec(Vm.SeciliAday);
                e.Handled = true;
            }
        }

        void Aday_Click(object sender, MouseButtonEventArgs e)
        {
            var kaynak = e.OriginalSource as DependencyObject;
            while (kaynak != null && kaynak is not ListBoxItem) kaynak = VisualTreeHelper.GetParent(kaynak);
            if (kaynak is ListBoxItem oge && oge.DataContext is UrunSatiri u) Vm?.UrunSec(u);
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);
            if (e.Key == Key.Enter && Vm?.UrunSecili == true && !e.Handled && Vm.KaydetCommand.CanExecute(null))
            {
                Vm.KaydetCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}
