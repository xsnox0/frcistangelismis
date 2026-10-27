using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Frcistan.Servisler;
using Frcistan.Veri;

namespace Frcistan.Altyapi
{
    /// <summary>Pencere içinde açılan diyalogların temeli</summary>
    public abstract class DiyalogViewModel : ObservableObject
    {
        string _baslik;
        bool _mesgul;
        string _hata;

        internal readonly TaskCompletionSource<bool> Tamamlandi = new();

        public string Baslik { get => _baslik; set => Set(ref _baslik, value); }

        /// <summary>Kayıt sırasında düğmeleri kilitler, dönen göstergeyi açar</summary>
        public bool Mesgul { get => _mesgul; set => Set(ref _mesgul, value); }

        /// <summary>Diyalog altında gösterilen genel hata metni</summary>
        public string Hata { get => _hata; set => Set(ref _hata, value); }

        public virtual double Genislik => 520;

        public bool Sonuc { get; private set; }

        public ICommand IptalCommand => new RelayCommand(() => { if (!Mesgul) Kapat(false); });

        public void Kapat(bool sonuc)
        {
            Sonuc = sonuc;
            Tamamlandi.TrySetResult(sonuc);
        }

        /// <summary>Kaydetme gibi işlemleri meşgul durumu ve hata gösterimiyle çalıştırır.</summary>
        protected async Task<bool> CalistirAsync(Func<Task> is_)
        {
            Hata = null;
            Mesgul = true;
            try
            {
                await is_();
                return true;
            }
            catch (KuralHatasi ex)
            {
                Hata = ex.Message;
                return false;
            }
            catch (Exception ex)
            {
                Gunluk.Yaz("Diyalog işlemi", ex);
                Hata = VeritabaniBaslatici.HatayiAcikla(ex);
                return false;
            }
            finally
            {
                Mesgul = false;
            }
        }
    }

    public enum MesajTuru { Bilgi, Basari, Uyari, Hata, Soru }

    public class MesajViewModel : DiyalogViewModel
    {
        public string Mesaj { get; init; }
        public MesajTuru Tur { get; init; }
        public string OnayMetni { get; init; } = "Tamam";
        public string IptalMetni { get; init; }
        public bool Tehlikeli { get; init; }
        public string Ayrinti { get; init; }

        public override double Genislik => 440;

        public string Ikon => Tur switch
        {
            MesajTuru.Basari => "",
            MesajTuru.Uyari => "",
            MesajTuru.Hata => "",
            MesajTuru.Soru => Tehlikeli ? "" : "",
            _ => ""
        };

        public string Renk => Tur switch
        {
            MesajTuru.Basari => "Basari",
            MesajTuru.Uyari => "Uyari",
            MesajTuru.Hata => "Tehlike",
            MesajTuru.Soru => Tehlikeli ? "Tehlike" : "Vurgu",
            _ => "Vurgu"
        };

        public ICommand OnayCommand => new RelayCommand(() => Kapat(true));
    }

    public static class Diyalog
    {
        /// <summary>Açık diyaloglar. Her pencerede bulunan DiyalogKatmani bunları gösterir.</summary>
        public static ObservableCollection<DiyalogViewModel> Acik { get; } = new();

        public static async Task<bool> Goster(DiyalogViewModel vm)
        {
            Acik.Add(vm);
            try
            {
                return await vm.Tamamlandi.Task;
            }
            finally
            {
                Acik.Remove(vm);
            }
        }

        public static Task<bool> Onayla(string baslik, string mesaj, string onay = "Evet", string iptal = "Vazgeç", bool tehlikeli = false) =>
            Goster(new MesajViewModel
            {
                Baslik = baslik, Mesaj = mesaj, Tur = MesajTuru.Soru, OnayMetni = onay, IptalMetni = iptal, Tehlikeli = tehlikeli
            });

        public static Task Bilgi(string baslik, string mesaj, MesajTuru tur = MesajTuru.Bilgi) =>
            Goster(new MesajViewModel { Baslik = baslik, Mesaj = mesaj, Tur = tur });

        public static Task Hata(Exception ex, string baslik = null)
        {
            if (ex is KuralHatasi)
                return Bilgi(baslik ?? "Uyarı", ex.Message, MesajTuru.Uyari);

            Gunluk.Yaz(baslik ?? "Beklenmeyen hata", ex);
            return Goster(new MesajViewModel
            {
                Baslik = baslik ?? "İşlem tamamlanamadı",
                Mesaj = VeritabaniBaslatici.HatayiAcikla(ex),
                Tur = MesajTuru.Hata,
                Ayrinti = "Ayrıntılar günlük dosyasına yazıldı."
            });
        }

        /// <summary>Esc tuşu: en üstteki diyaloğu kapatır</summary>
        public static bool EnUsttekiniKapat()
        {
            var ust = Acik.LastOrDefault();
            if (ust == null || ust.Mesgul) return false;
            ust.Kapat(false);
            return true;
        }
    }

    public enum BildirimTuru { Bilgi, Basari, Uyari, Hata }

    public class BildirimOgesi : ObservableObject
    {
        public string Baslik { get; init; }
        public string Mesaj { get; init; }
        public BildirimTuru Tur { get; init; }
        public string EylemMetni { get; init; }
        public ICommand EylemCommand { get; init; }
        public ICommand KapatCommand { get; init; }

        public string Ikon => Tur switch
        {
            BildirimTuru.Basari => "",
            BildirimTuru.Uyari => "",
            BildirimTuru.Hata => "",
            _ => ""
        };

        public string Renk => Tur switch
        {
            BildirimTuru.Basari => "Basari",
            BildirimTuru.Uyari => "Uyari",
            BildirimTuru.Hata => "Tehlike",
            _ => "Vurgu"
        };
    }

    /// <summary>Sağ altta beliren kısa bildirimler</summary>
    public static class Bildirim
    {
        public static ObservableCollection<BildirimOgesi> Acik { get; } = new();

        public static void Goster(string baslik, string mesaj = null, BildirimTuru tur = BildirimTuru.Basari,
            string eylemMetni = null, Action eylem = null)
        {
            if (Application.Current?.Dispatcher is not { } d) return;
            d.Invoke(() =>
            {
                BildirimOgesi oge = null;
                void Kaldir() => Acik.Remove(oge);
                oge = new BildirimOgesi
                {
                    Baslik = baslik, Mesaj = mesaj, Tur = tur, EylemMetni = eylemMetni,
                    EylemCommand = eylem == null ? null : new RelayCommand(() => { Kaldir(); eylem(); }),
                    KapatCommand = new RelayCommand(Kaldir)
                };
                Acik.Add(oge);
                while (Acik.Count > 4) Acik.RemoveAt(0);

                var zamanlayici = new DispatcherTimer { Interval = TimeSpan.FromSeconds(eylem == null ? 4 : 7) };
                zamanlayici.Tick += (_, _) => { zamanlayici.Stop(); Kaldir(); };
                zamanlayici.Start();
            });
        }

        public static void Basari(string baslik, string mesaj = null) => Goster(baslik, mesaj, BildirimTuru.Basari);
        public static void Uyari(string baslik, string mesaj = null) => Goster(baslik, mesaj, BildirimTuru.Uyari);
        public static void Bilgi(string baslik, string mesaj = null) => Goster(baslik, mesaj, BildirimTuru.Bilgi);

        /// <summary>Kaydedilen dosya için "Aç" düğmeli bildirim</summary>
        public static void DosyaKaydedildi(string baslik, string dosya) =>
            Goster(baslik, Path.GetFileName(dosya), BildirimTuru.Basari, "Aç", () => Kabuk.Ac(dosya));
    }

    public static class Kabuk
    {
        public static void Ac(string yol)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(yol) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Bildirim.Goster("Açılamadı", ex.Message, BildirimTuru.Hata);
            }
        }

        public static void KlasordeGoster(string dosya)
        {
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{dosya}\"");
            }
            catch { }
        }
    }
}
