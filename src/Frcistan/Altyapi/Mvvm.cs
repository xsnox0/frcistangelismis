using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Frcistan.Servisler;

namespace Frcistan.Altyapi
{
    public abstract class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string ad = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(ad));

        protected bool Set<T>(ref T alan, T deger, [CallerMemberName] string ad = null)
        {
            if (EqualityComparer<T>.Default.Equals(alan, deger)) return false;
            alan = deger;
            OnPropertyChanged(ad);
            return true;
        }
    }

    public class RelayCommand : ICommand
    {
        readonly Action<object> _calistir;
        readonly Func<object, bool> _kosul;

        public RelayCommand(Action calistir, Func<bool> kosul = null)
        {
            _calistir = _ => calistir();
            _kosul = kosul == null ? null : _ => kosul();
        }

        public RelayCommand(Action<object> calistir, Func<object, bool> kosul = null)
        {
            _calistir = calistir;
            _kosul = kosul;
        }

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object p) => _kosul?.Invoke(p) ?? true;
        public void Execute(object p) => _calistir(p);
    }

    /// <summary>Async komut: çalışırken tekrar tetiklenmez, hataları kullanıcıya düzgün gösterir.</summary>
    public class AsyncCommand : ICommand
    {
        readonly Func<object, Task> _calistir;
        readonly Func<object, bool> _kosul;
        bool _calisiyor;

        public AsyncCommand(Func<Task> calistir, Func<bool> kosul = null)
        {
            _calistir = _ => calistir();
            _kosul = kosul == null ? null : _ => kosul();
        }

        public AsyncCommand(Func<object, Task> calistir, Func<object, bool> kosul = null)
        {
            _calistir = calistir;
            _kosul = kosul;
        }

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object p) => !_calisiyor && (_kosul?.Invoke(p) ?? true);

        public async void Execute(object p)
        {
            if (_calisiyor) return;
            _calisiyor = true;
            CommandManager.InvalidateRequerySuggested();
            try
            {
                await _calistir(p);
            }
            catch (Exception ex)
            {
                await Diyalog.Hata(ex);
            }
            finally
            {
                _calisiyor = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    /// <summary>Sol menüdeki bir sayfa</summary>
    public interface ISayfa
    {
        string Baslik { get; }
        string AltBaslik { get; }
        Task YukleAsync();
    }

    /// <summary>Sayfa görünüm modellerinin temeli</summary>
    public abstract class SayfaViewModel : ObservableObject, ISayfa
    {
        string _altBaslik;
        bool _yukleniyor;

        public abstract string Baslik { get; }

        public string AltBaslik
        {
            get => _altBaslik;
            protected set => Set(ref _altBaslik, value);
        }

        public bool Yukleniyor
        {
            get => _yukleniyor;
            protected set => Set(ref _yukleniyor, value);
        }

        public bool Admin => Oturum.Admin;

        public async Task YukleAsync()
        {
            Yukleniyor = true;
            try { await YukleIcAsync(); }
            catch (Exception ex) { await Diyalog.Hata(ex, "Veriler yüklenemedi"); }
            finally { Yukleniyor = false; }
        }

        protected abstract Task YukleIcAsync();

        /// <summary>Lisans (deneme süresi) yazmaya izin vermiyorsa uyarı gösterir.</summary>
        protected static async Task<bool> YazabilirMi()
        {
            if (LisansServisi.Mevcut.YazmaIzni) return true;
            await Diyalog.Bilgi(LisansServisi.Mevcut.Baslik,
                LisansServisi.Mevcut.Aciklama + "\n\nLisans anahtarını Ayarlar › Lisans bölümünden girebilirsiniz.", MesajTuru.Uyari);
            return false;
        }
    }
}
