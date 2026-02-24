using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SideBar_Nav.Models;

namespace SideBar_Nav
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        // Dışarıdan null gelirse bile içerde hep non-null tutuyoruz.
        private Kullanicilar _me = new Kullanicilar
        {
            // Varsayılan: tüm izinler false, sadece UI kırılmasın
            PersonelAllow = false,
            UserSettingsAllow = false
        };

        public Kullanicilar Me
        {
            get => _me;
            private set
            {
                if (!ReferenceEquals(_me, value) && value != null)
                {
                    _me = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayName));
                    // İzinlere bağlı UI elemanlarını tetiklemek için:
                    RaiseAllPermissionChanges();
                }
            }
        }

        // Logout vb. durumlarda null gelebilir.
        public void SetMe(Kullanicilar? me)
        {
            Me = me ?? new Kullanicilar(); // boş model
        }

        public string DisplayName =>
            string.IsNullOrWhiteSpace(Me?.Ad)
                ? (Me?.Email ?? "—")
                : Me.Ad.Trim();
        // İzinler değiştiğinde tek tek OnPropertyChanged atmak istersen:
        private void RaiseAllPermissionChanges()
        {
            OnPropertyChanged(nameof(Me.AdminAllow));
            OnPropertyChanged(nameof(Me.GpioOpsAllow));
            OnPropertyChanged(nameof(Me.IptalAllow));
            OnPropertyChanged(nameof(Me.KaliteKontrolAllow));
            OnPropertyChanged(nameof(Me.KullaniciYonetimiAllow));
            OnPropertyChanged(nameof(Me.PersonelAllow));
            OnPropertyChanged(nameof(Me.ReadingAllow));
            OnPropertyChanged(nameof(Me.RezOlusturAllow));
            OnPropertyChanged(nameof(Me.SatisYonetimiAllow));
            OnPropertyChanged(nameof(Me.SevkiyatAllow));
            OnPropertyChanged(nameof(Me.StokYonetimiAllow));
            OnPropertyChanged(nameof(Me.UrunTakipAllow));
            OnPropertyChanged(nameof(Me.UserSettingsAllow));
            OnPropertyChanged(nameof(Me.UretimAllow));
        }

        // ===== INotifyPropertyChanged =====
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
