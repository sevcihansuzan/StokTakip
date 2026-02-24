using SideBar_Nav.Models;
using SideBar_Nav.Services;
using Supabase.Postgrest;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static Supabase.Postgrest.Constants;

namespace SideBar_Nav.Pages
{
    public partial class KullaniciYonetimi : System.Windows.Controls.Page, INotifyPropertyChanged
    {
        // UI Binding için
        private ObservableCollection<Kullanicilar> _users = new();
        public ObservableCollection<Kullanicilar> Users
        {
            get => _users;
            set { _users = value; OnPropertyChanged(); }
        }

        private ObservableCollection<LogEylem> _logEylemleri = new();
        public ObservableCollection<LogEylem> LogEylemleri
        {
            get => _logEylemleri;
            set { _logEylemleri = value; OnPropertyChanged(); }
        }

        private Kullanicilar _selectedUser;
        public Kullanicilar SelectedUser
        {
            get => _selectedUser;
            set { _selectedUser = value; OnPropertyChanged(); LoadSelectedToForm(value); }
        }

        // Admin yetki kontrolü
        private bool CurrentIsAdmin =>
            (Application.Current.MainWindow as MainWindow)?.Vm?.Me?.AdminAllow ?? false;



        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

        public KullaniciYonetimi()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += KullaniciYonetimi_Loaded;
        }

        private async void KullaniciYonetimi_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Admin guard
                if (!CurrentIsAdmin)
                {
                    MessageBox.Show("Bu sayfa için yönetici yetkisi gerekir.", "Yetki Yok",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    IsEnabled = false;
                    return;
                }

                await RefreshUsersAsync();
                await LoadEylemlerAsync();

                dpLogDate.SelectedDate = DateTime.Today;
                await RefreshLogsAsync();

                dpLogDate.SelectedDateChanged += async (_, __) => await RefreshLogsAsync();
                cbDateRange.SelectionChanged += async (_, __) => await RefreshLogsAsync();
                cbPersonelFilter.SelectionChanged += async (_, __) => await RefreshLogsAsync();
                cbEylemFilter.SelectionChanged += async (_, __) => await RefreshLogsAsync();
                txtDetayFilter.TextChanged += async (_, __) => await RefreshLogsAsync();


            }
            catch (Exception ex)
            {
                ShowError($"Sayfa yüklenemedi: {ex.Message}");
            }
        }

        // ===================== DATA LOADING =====================

        private async Task RefreshUsersAsync(string? search = null)
        {
            try
            {
                SetBusy(true);

                if (App.SupabaseClient == null)
                    await App.InitializeSupabase();

                // Arama (email/ad/pozisyon içinde ILIKE)
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = $"%{search.Trim()}%";

                    var byEmail = await App.SupabaseClient
                        .From<Kullanicilar>()
                        .Filter("email", Operator.ILike, s)
                        .Get();

                    var byAd = await App.SupabaseClient
                        .From<Kullanicilar>()
                        .Filter("ad", Operator.ILike, s)
                        .Get();

                    var byPozisyon = await App.SupabaseClient
                        .From<Kullanicilar>()
                        .Filter("pozisyon", Operator.ILike, s)
                        .Get();
                    var byPhone = await App.SupabaseClient
                        .From<Kullanicilar>()
                        .Filter("phone", Operator.ILike, s)
                        .Get();

                    var all = (byEmail.Models ?? new())
                        .Concat(byAd.Models ?? new())
                          .Concat(byPozisyon.Models ?? new())
                        .Concat(byPhone.Models ?? new())   // <-- eklendi
                        .GroupBy(u => u.Id)
                        .Select(g => g.First())
                          .OrderBy(u => u.Ad).ThenBy(u => u.Pozisyon).ThenBy(u => u.Email)
                        .ToList();


                    Users = new ObservableCollection<Kullanicilar>(all);
                }
                else
                {
                    // Tüm kullanıcılar
                    var res = await App.SupabaseClient
                        .From<Kullanicilar>()
                        .Get();

                    var list = (res.Models ?? new List<Kullanicilar>())
                        .OrderBy(u => u.Ad)
                          .ThenBy(u => u.Pozisyon)
                        .ThenBy(u => u.Email)
                        .ToList();

                    Users = new ObservableCollection<Kullanicilar>(list);
                }

                // İlk satır seçili olsun
                if (Users.Count > 0)
                    SelectedUser = Users[0];
                else
                    ClearForm();

                ShowOk($"Toplam {Users.Count} kullanıcı yüklendi.");
            }
            catch (Exception ex)
            {
                ShowError($"Kullanıcılar getirilemedi: {ex.Message}");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async Task LoadEylemlerAsync()
        {
            try
            {
                SetBusy(true);

                if (App.SupabaseClient == null)
                    await App.InitializeSupabase();

                var res = await App.SupabaseClient.From<LogEylem>().Get();

                LogEylemleri.Clear();

                // UI-only "Hepsi" satırı (DB'ye gitmez, sadece filtre için)
                //LogEylemleri.Add(new LogEylem { Eylem = "Hepsi" }); // <-- EKLENDİ

                foreach (var e in (res.Models ?? Enumerable.Empty<LogEylem>()).OrderBy(x => x.Eylem))
                    LogEylemleri.Add(e);

                //ShowOk($"Toplam {LogEylemleri.Count - 1} eylem yüklendi."); // -1 çünkü Hepsi UI satırı
            }
            catch (Exception ex)
            {
                ShowError($"Eylemler getirilemedi: {ex.Message}");
            }
            finally
            {
                SetBusy(false);
            }
        }


        private async Task RefreshLogsAsync()
        {
            try
            {
                SetBusy(true);

                var date = dpLogDate.SelectedDate ?? DateTime.Today;
                DateTime start, end;
                switch ((cbDateRange.SelectedItem as ComboBoxItem)?.Tag?.ToString())
                {
                    case "Monthly":
                        start = new DateTime(date.Year, date.Month, 1);
                        end = start.AddMonths(1);
                        break;
                    case "Yearly":
                        start = new DateTime(date.Year, 1, 1);
                        end = start.AddYears(1);
                        break;
                    default:
                        start = date.Date;
                        end = start.AddDays(1);
                        break;
                }

                Guid? userId = null;
                if (cbPersonelFilter.SelectedItem is Kullanicilar u)
                    userId = u.Id;

                string? eylem = (cbEylemFilter.SelectedItem as LogEylem)?.Eylem;

                // <-- KRİTİK: "Hepsi" veya boş ise filtre uygulama
                if (string.IsNullOrWhiteSpace(eylem) ||
                    string.Equals(eylem, "Hepsi", StringComparison.OrdinalIgnoreCase))
                {
                    eylem = null;
                }

                string? detay = string.IsNullOrWhiteSpace(txtDetayFilter.Text) ? null : txtDetayFilter.Text;

                await ActivityLogger.LoadAsync(start, end, userId, eylem, detay);
            }
            catch (Exception ex)
            {
                ShowError($"Loglar getirilemedi: {ex.Message}");
            }
            finally
            {
                SetBusy(false);
            }
        }



        // ===================== FORM BINDING =====================

        private void LoadSelectedToForm(Kullanicilar? u)
        {
            if (u == null)
            {
                ClearForm();
                return;
            }

            txtEmail.Text = u.Email ?? "";
            txtAd.Text = u.Ad ?? "";
            txtPozisyon.Text = u.Pozisyon ?? "";
            txtPhone.Text = u.Phone ?? "";

            txtCreated.Text = u.CreatedAt.ToLocalTime().ToString("g");
            txtUpdated.Text = u.UpdatedAt.ToLocalTime().ToString("g");
            txtLastSignIn.Text = u.LastSignInAt?.ToLocalTime().ToString("g") ?? "—";

            // Yetkiler
            chkAdmin.IsChecked = u.AdminAllow || u.KullaniciYonetimiAllow;
            chkGpioOps.IsChecked = u.GpioOpsAllow;
            chkIptal.IsChecked = u.IptalAllow;
            chkKalite.IsChecked = u.KaliteKontrolAllow;
            chkPersonel.IsChecked = u.PersonelAllow;
            chkReading.IsChecked = u.ReadingAllow;
            chkRezOlustur.IsChecked = u.RezOlusturAllow;
            chkSatisYonetimi.IsChecked = u.SatisYonetimiAllow;
            chkSevkiyat.IsChecked = u.SevkiyatAllow;
            chkStokYonetimi.IsChecked = u.StokYonetimiAllow;
            chkUrunTakip.IsChecked = u.UrunTakipAllow;
            chkUserSettings.IsChecked = u.UserSettingsAllow;
            chkUretim.IsChecked = u.UretimAllow;

            // Avatar
            SetSelectedAvatar(u.AvatarUrl);

            lblStatus.Text = "";
        }
        private void SetSelectedAvatar(string? url)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url))
                {
                    imgSelectedAvatar.Source = null; // placeholder kullanmak istersen burada ata
                    return;
                }

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad; // dosya kilitlenmesin
                bmp.UriSource = new Uri(url, UriKind.Absolute);
                bmp.EndInit();
                imgSelectedAvatar.Source = bmp;
            }
            catch
            {
                imgSelectedAvatar.Source = null;
            }
        }
        private void ClearForm()
        {
            txtEmail.Text = "";
            txtAd.Text = "";
            txtPozisyon.Text = "";
            txtPhone.Text = "";
            txtCreated.Text = "";
            txtUpdated.Text = "";
            txtLastSignIn.Text = "";

            foreach (var cb in new[]
                     { chkAdmin, chkGpioOps, chkIptal, chkKalite,
                       chkPersonel, chkReading, chkRezOlustur, chkSatisYonetimi, chkSevkiyat,
                       chkStokYonetimi, chkUrunTakip, chkUserSettings,chkUretim })
            {
                if (cb != null) cb.IsChecked = false;
            }
        }

        // ===================== SAVE =====================

        private async Task SaveSelectedAsync()
        {
            if (SelectedUser == null)
            {
                ShowError("Önce bir kullanıcı seçiniz.");
                return;
            }

            try
            {
                SetBusy(true);

                if (App.SupabaseClient == null)
                    await App.InitializeSupabase();

                // Formdan modele
                SelectedUser.Email = txtEmail.Text?.Trim();
                SelectedUser.Ad = txtAd.Text?.Trim();
                SelectedUser.Pozisyon = txtPozisyon.Text?.Trim();
                SelectedUser.Phone = txtPhone.Text?.Trim();

                SelectedUser.AdminAllow = chkAdmin.IsChecked == true;
                SelectedUser.KullaniciYonetimiAllow = SelectedUser.AdminAllow;
                SelectedUser.GpioOpsAllow = chkGpioOps.IsChecked == true;
                SelectedUser.IptalAllow = chkIptal.IsChecked == true;
                SelectedUser.KaliteKontrolAllow = chkKalite.IsChecked == true;
                SelectedUser.PersonelAllow = chkPersonel.IsChecked == true;
                SelectedUser.ReadingAllow = chkReading.IsChecked == true;
                SelectedUser.RezOlusturAllow = chkRezOlustur.IsChecked == true;
                SelectedUser.SatisYonetimiAllow = chkSatisYonetimi.IsChecked == true;
                SelectedUser.SevkiyatAllow = chkSevkiyat.IsChecked == true;
                SelectedUser.StokYonetimiAllow = chkStokYonetimi.IsChecked == true;
                SelectedUser.UrunTakipAllow = chkUrunTakip.IsChecked == true;
                SelectedUser.UserSettingsAllow = chkUserSettings.IsChecked == true;
                SelectedUser.UretimAllow = chkUretim.IsChecked == true;

                await App.SupabaseClient.From<Kullanicilar>().Update(SelectedUser);

                // Grid'i ve VM.Me’yi tazelemek için sunucudan tekrar çekelim
                var refreshed = await App.SupabaseClient
                    .From<Kullanicilar>()
                    .Filter("id", Operator.Equals, SelectedUser.Id.ToString())
                    .Single();

                // Koleksiyonda güncelle
                var idx = Users.IndexOf(Users.First(u => u.Id == refreshed.Id));
                Users[idx] = refreshed;
                SelectedUser = refreshed;

                // Eğer admin kendi kaydını güncellediyse MainWindow VM’yi de güncelle
                /*
                var myId = (Application.Current.MainWindow as MainWindow)?.Vm?.Me?.Id ?? Guid.Empty;
                if (myId == refreshed.Id && Application.Current.MainWindow is MainWindow mw)
                    mw.Vm.SetMe(refreshed);
                    mw.GuncelleAktifKullanici();
                */
                var mw = Application.Current.MainWindow as MainWindow;
                var myId = mw?.Vm?.Me?.Id ?? Guid.Empty;

                if (myId == refreshed.Id && mw != null)
                {
                    mw.Vm.SetMe(refreshed);
                    mw.GuncelleAktifKullanici(); // Text="{Binding DisplayName}" yaptıysan bu satır opsiyonel
                }

                ShowOk("Kullanıcı güncellendi.");
            }
            catch (Exception ex)
            {
                ShowError($"Kaydetme hatası: {ex.Message}");
            }
            finally
            {
                SetBusy(false);
            }
        }

        // ===================== BUTTON HANDLERS =====================

        private async void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            var keyword = txtSearch?.Text;
            await RefreshUsersAsync(keyword);
        }

        private async void btnLogFilter_Click(object sender, RoutedEventArgs e)
            => await RefreshLogsAsync();

        private async void btnSave_Click(object sender, RoutedEventArgs e)
            => await SaveSelectedAsync();

        private void btnGrantAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var cb in new[]
                     { chkAdmin, chkGpioOps, chkIptal, chkKalite,
                       chkPersonel, chkReading, chkRezOlustur, chkSatisYonetimi, chkSevkiyat,
                       chkStokYonetimi, chkUrunTakip, chkUserSettings,chkUretim })
            {
                if (cb != null) cb.IsChecked = true;
            }
        }

        private void btnRevokeAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var cb in new[]
                     { chkAdmin, chkGpioOps, chkIptal, chkKalite,
                       chkPersonel, chkReading, chkRezOlustur, chkSatisYonetimi, chkSevkiyat,
                       chkStokYonetimi, chkUrunTakip, chkUserSettings,chkUretim })
            {
                if (cb != null) cb.IsChecked = false;
            }
        }




        private string GetDisplayName(Kullanicilar u)
        {
            if (string.IsNullOrWhiteSpace(u.Ad) && string.IsNullOrWhiteSpace(u.Pozisyon))
                return u.Email ?? string.Empty;

            var ad = u.Ad ?? string.Empty;
            var pozisyon = u.Pozisyon ?? string.Empty;
            var full = (ad + " " + pozisyon).Trim();
            return string.IsNullOrEmpty(full) ? (u.Email ?? string.Empty) : full;
        }

        private void dgUsers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgUsers.SelectedItem is Kullanicilar u)
                SelectedUser = u;
        }

        // ===================== UI HELPERS =====================

        private void SetBusy(bool on)
        {
            if (busyOverlay != null)
                busyOverlay.Visibility = on ? Visibility.Visible : Visibility.Collapsed;

            bool enable = !on;
            if (btnRefresh != null) btnRefresh.IsEnabled = enable;
            if (btnSave != null) btnSave.IsEnabled = enable;
            if (btnGrantAll != null) btnGrantAll.IsEnabled = enable;
            if (btnRevokeAll != null) btnRevokeAll.IsEnabled = enable;
            if (dgUsers != null) dgUsers.IsEnabled = enable;
        }

        private void ShowOk(string msg)
        {
            if (lblStatus == null) return;
            lblStatus.Foreground = new SolidColorBrush(Colors.ForestGreen);
            lblStatus.Text = msg;
        }

        private void ShowError(string msg)
        {
            if (lblStatus == null) return;
            lblStatus.Foreground = new SolidColorBrush(Colors.IndianRed);
            lblStatus.Text = msg;
            System.Diagnostics.Debug.WriteLine($"[HATA] {msg}");
        }

    }
}
