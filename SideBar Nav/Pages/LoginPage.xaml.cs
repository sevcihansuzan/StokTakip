using SideBar_Nav.Models;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;
using Supabase.Postgrest;
using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static Supabase.Postgrest.Constants;
using System.Linq;

namespace SideBar_Nav.Pages
{
    public partial class LoginPage : Page
    {
        public event Action LoginSuccess;

        // Google OAuth için callback portu
        private const int GoogleCallbackPort = 51789;
        private string GoogleRedirectUrl => $"http://127.0.0.1:{GoogleCallbackPort}/auth/callback";

        // İptal kontrolü için
        private CancellationTokenSource? _listenerCts;

        public LoginPage()
        {
            InitializeComponent();

            phEmail.Visibility = string.IsNullOrWhiteSpace(txtEmail.Text) ? Visibility.Visible : Visibility.Collapsed;
            phPassword.Visibility = string.IsNullOrWhiteSpace(txtPassword.Password) ? Visibility.Visible : Visibility.Collapsed;
        }

        #region Placeholder Eventleri
        private void txtEmail_TextChanged(object sender, TextChangedEventArgs e)
        {
            phEmail.Visibility = string.IsNullOrWhiteSpace(txtEmail.Text) ? Visibility.Visible : Visibility.Collapsed;
            lblStatus.Text = "";
        }

        private void txtPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            phPassword.Visibility = string.IsNullOrWhiteSpace(txtPassword.Password) ? Visibility.Visible : Visibility.Collapsed;
            lblStatus.Text = "";
        }
        #endregion

        // =========================================================
        //  GOOGLE OAUTH GİRİŞ
        // =========================================================
        private async void btnGoogle_Click(object sender, RoutedEventArgs e)
        {
            lblStatus.Text = "";
            lblStatus.Foreground = new SolidColorBrush(Colors.IndianRed);
            var existingSession = App.SupabaseClient?.Auth?.CurrentSession;
            if (App.AktifKullanici != null || existingSession != null)
            {
                lblStatus.Text = "Yeni bir hesapla giriş yapmak için önce mevcut oturumdan çıkış yapın.";
                return;
            }

            SetBusy(true);
            try
            {
                if (App.SupabaseClient == null)
                    await App.InitializeSupabase();

                var auth = App.SupabaseClient.Auth;

                // 1) Google için OAuth isteği oluştur (PKCE)
                var state = await auth.SignIn(Supabase.Gotrue.Constants.Provider.Google, new SignInOptions
                {
                    FlowType = Supabase.Gotrue.Constants.OAuthFlowType.PKCE,
                    RedirectTo = GoogleRedirectUrl,
                    Scopes = "email profile"
                });

                if (state == null || state.Uri == null || string.IsNullOrWhiteSpace(state.Uri.AbsoluteUri))
                    throw new Exception("Google giriş URL'si oluşturulamadı.");

                // 1.a) Hesap seçtir: prompt=select_account parametresi ekle
                var authUrl = AddQueryParams(state.Uri.AbsoluteUri, ("prompt", "select_account"));

                // 2) Callback'i dinle ve tarayıcıyı aç
                _listenerCts = new CancellationTokenSource();
                var authCode = await ListenForAuthCodeOnce(GoogleCallbackPort, authUrl, _listenerCts.Token);
                if (string.IsNullOrWhiteSpace(authCode))
                    throw new Exception("Yetkilendirme kodu alınamadı.");

                // 3) code + PKCE verifier ile session al
                var session = await auth.ExchangeCodeForSession(state.PKCEVerifier, authCode);
                if (session == null || session.User == null)
                    throw new Exception("Oturum oluşturulamadı.");

                // Aktif kullanıcı (Supabase Auth user)
                App.AktifKullanici = session.User;

                // >>> Auth -> kullanicilar eşitle + Me'yi yükle + MainWindow'a aktar
                await SyncUserAndLoadMeAsync(session.User);

                lblStatus.Foreground = new SolidColorBrush(Colors.LightGreen);
                lblStatus.Text = "Google ile giriş başarılı!";
                LoginSuccess?.Invoke();
            }
            catch (TaskCanceledException)
            {
                lblStatus.Text = "Google ile giriş iptal edildi.";
            }
            catch (GotrueException gex)
            {
                lblStatus.Text = FriendlyAuthMessage(gex);
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Google ile giriş başarısız: {ex.Message}";
            }
            finally
            {
                SetBusy(false);
                _listenerCts = null;
            }
        }

        // Tek seferlik, güvenli callback dinleyici
        private async Task<string?> ListenForAuthCodeOnce(int port, string authUrl, CancellationToken token)
        {
            using var listener = new HttpListener();

            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            listener.Prefixes.Add($"http://localhost:{port}/");
            listener.Start();

            try
            {
                // Tarayıcıyı aç
                Process.Start(new ProcessStartInfo
                {
                    FileName = authUrl,
                    UseShellExecute = true
                });

                string? code = null;

                while (true)
                {
                    var ctxTask = listener.GetContextAsync();
                    var completed = await Task.WhenAny(ctxTask, Task.Delay(-1, token));
                    if (completed != ctxTask)
                        throw new TaskCanceledException();

                    var ctx = ctxTask.Result;
                    var req = ctx.Request;
                    var path = req.Url.AbsolutePath;

                    if (!string.Equals(path, "/auth/callback", StringComparison.OrdinalIgnoreCase))
                    {
                        ctx.Response.StatusCode = 204;
                        ctx.Response.Close();
                        continue;
                    }

                    code = req.QueryString["code"];

                    var html = "<html><body style='font-family:sans-serif'>Giriş tamamlandı, uygulamaya dönebilirsiniz.</body></html>";
                    var bytes = System.Text.Encoding.UTF8.GetBytes(html);
                    ctx.Response.ContentType = "text/html; charset=utf-8";
                    ctx.Response.ContentLength64 = bytes.LongLength;
                    await ctx.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
                    ctx.Response.Close();

                    break;
                }

                return code;
            }
            finally
            {
                listener.Stop();
            }
        }

        private void btnCancelGoogle_Click(object sender, RoutedEventArgs e)
        {
            _listenerCts?.Cancel();
            lblStatus.Foreground = new SolidColorBrush(Colors.OrangeRed);
            lblStatus.Text = "Google ile giriş iptal edildi.";
            SetBusy(false);
        }

        // =========================================================
        //  EMAIL/PASSWORD GİRİŞ
        // =========================================================
        private async void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            lblStatus.Text = "";
            lblStatus.Foreground = new SolidColorBrush(Colors.IndianRed);

            var existingSession = App.SupabaseClient?.Auth?.CurrentSession;
            if (App.AktifKullanici != null || existingSession != null)
            {
                lblStatus.Text = "Yeni bir hesapla giriş yapmak için önce mevcut oturumdan çıkış yapın.";
                return;
            }

            var email = txtEmail.Text?.Trim();
            var password = txtPassword.Password;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                lblStatus.Text = "Email ve parola zorunludur.";
                return;
            }

            SetBusy(true);

            try
            {
                if (App.SupabaseClient == null)
                    await App.InitializeSupabase();

                var auth = App.SupabaseClient.Auth;
                var session = await auth.SignInWithPassword(email, password);
                if (session == null || session.User == null)
                    throw new Exception("Giriş başarısız.");

                App.AktifKullanici = session.User;

                // >>> Auth -> kullanicilar eşitle + Me'yi yükle + MainWindow'a aktar
                await SyncUserAndLoadMeAsync(session.User);

                lblStatus.Foreground = new SolidColorBrush(Colors.LightGreen);
                lblStatus.Text = "Giriş başarılı!";
                LoginSuccess?.Invoke();
            }
            catch (GotrueException gex)
            {
                lblStatus.Text = FriendlyAuthMessage(gex);
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Hata: {ex.Message}";
            }
            finally
            {
                SetBusy(false);
            }
        }

        // =========================================================
        //  ÇIKIŞ
        // =========================================================
        private async void btnLogout_Click(object sender, RoutedEventArgs e)
        {
            lblStatus.Text = "";
            lblStatus.Foreground = new SolidColorBrush(Colors.IndianRed);

            SetBusy(true);
            try
            {
                if (App.SupabaseClient == null)
                    await App.InitializeSupabase();

                await App.SupabaseClient.Auth.SignOut();
                App.AktifKullanici = null;

                if (Application.Current.MainWindow is MainWindow mw)
                {
                    mw.GuncelleAktifKullanici();
                    // ayrıca Me'yi boşaltmak istersen:
                    mw.Vm?.SetMe(null);
                    if (mw.FindName("txtAktifKullanici") is TextBlock tb) tb.Text = "—";
                }

                lblStatus.Foreground = new SolidColorBrush(Colors.LightGreen);
                lblStatus.Text = "Çıkış yapıldı.";
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Çıkış yapılamadı: {ex.Message}";
            }
            finally
            {
                SetBusy(false);
            }
        }

        // =========================================================
        //  ŞİFRE SIFIRLAMA
        // =========================================================
        private async void btnForgot_Click(object sender, RoutedEventArgs e)
        {
            lblStatus.Text = "";
            var email = txtEmail.Text?.Trim();
            if (string.IsNullOrWhiteSpace(email))
            {
                lblStatus.Text = "Şifre sıfırlama için email giriniz.";
                return;
            }

            SetBusy(true);
            try
            {
                if (App.SupabaseClient == null)
                    await App.InitializeSupabase();

                await App.SupabaseClient.Auth.ResetPasswordForEmail(email);

                MessageBox.Show(
                    "Şifre sıfırlama bağlantısı email adresine gönderildi.",
                    "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (GotrueException gex)
            {
                lblStatus.Text = $"Şifre sıfırlama başarısız: {FriendlyAuthMessage(gex)}";
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Şifre sıfırlama başarısız: {ex.Message}";
            }
            finally
            {
                SetBusy(false);
            }
        }

        // =========================================================
        //  KAYIT (AYRI PENCERE)
        // =========================================================
        private void btnRegister_Click(object sender, RoutedEventArgs e)
        {
            var wnd = new Windows.RegisterWindow { Owner = Window.GetWindow(this) };
            if (wnd.ShowDialog() == true && !string.IsNullOrWhiteSpace(wnd.RegisteredEmail))
            {
                txtEmail.Text = wnd.RegisteredEmail;
                txtPassword.Password = "";
                phEmail.Visibility = string.IsNullOrWhiteSpace(txtEmail.Text) ? Visibility.Visible : Visibility.Collapsed;
                phPassword.Visibility = Visibility.Visible;

                lblStatus.Foreground = new SolidColorBrush(Colors.LightGreen);
                lblStatus.Text = "Hesabınız oluşturuldu. Lütfen giriş yapın.";
            }
        }

        // =========================================================
        //  YARDIMCI: Auth -> kullanicilar eşitle + Me'yi yükle + MainWindow'a aktar
        // =========================================================
        private async Task<Kullanicilar> SyncUserAndLoadMeAsync(User authUser)
        {
            if (App.SupabaseClient == null)
                await App.InitializeSupabase();

            var uid = Guid.Parse(authUser.Id);
            var now = DateTimeOffset.UtcNow;
            var lastSignIn = authUser.LastSignInAt ?? now;

            string? Meta(string key)
            {
                try
                {
                    if (authUser.UserMetadata != null &&
                        authUser.UserMetadata.TryGetValue(key, out var v) && v != null)
                        return v.ToString();
                }
                catch { }
                return null;
            }

            var ad = Meta("ad") ?? Meta("first_name") ?? Meta("name");
            //var tel = Meta("phone") ?? authUser.Phone;

            // Var mı?
            var exist = await App.SupabaseClient
                .From<Kullanicilar>()
                .Filter("id", Operator.Equals, authUser.Id)
                .Get();

            if (!exist.Models.Any())
            {
                // İlk kez giriş: insert (izinler DB default)
                var rec = new Kullanicilar
                {
                    Id = uid,
                    Email = authUser.Email,
                    Ad = ad,
                    //Phone = tel,
                    CreatedAt = now,
                    UpdatedAt = now,
                    LastSignInAt = lastSignIn,

                    // İlk kurulum: sadece personel & ayarlar açık kalsın (istersen kaldır)
                    PersonelAllow = true,
                    UserSettingsAllow = true
                };

                await App.SupabaseClient.From<Kullanicilar>().Insert(rec);
            }
            else
            {
                // Güncelle (izinlere dokunma; admin panelinden değişecek)
                var rec = exist.Models[0];
                rec.Email = authUser.Email ?? rec.Email;
                rec.Ad = ad ?? rec.Ad;
                //rec.Phone = tel ?? rec.Phone;
                rec.LastSignInAt = lastSignIn;

                await App.SupabaseClient.From<Kullanicilar>().Update(rec);
            }

            // Güncel Me'yi çek
            var me = await App.SupabaseClient
                .From<Kullanicilar>()
                .Filter("id", Operator.Equals, authUser.Id)
                .Single();

            UpdateMainWindowMe(me);
            return me;
        }

        // MainWindow ViewModel'e Me'yi ver (NavButton binding’leri için)
        private void UpdateMainWindowMe(Kullanicilar me)
        {
            if (Application.Current.MainWindow is MainWindow mw && mw.Vm != null)
            {
                mw.Vm.SetMe(me);
                if (mw.FindName("txtAktifKullanici") is TextBlock tb)
                    tb.Text = string.IsNullOrWhiteSpace(me.Ad)
                        ? (me.Email ?? "—")
                        : me.Ad.Trim();
            }
        }

        // =========================================================
        //  UI YARDIMCILAR
        // =========================================================
        private string FriendlyAuthMessage(GotrueException ex)
        {
            var msg = ex.Message?.ToLowerInvariant() ?? "";
            if (msg.Contains("invalid login credentials"))
                return "Email veya parola hatalı.";
            if (msg.Contains("email not confirmed"))
                return "Email adresi doğrulanmamış. Lütfen emailini doğrula.";
            if (msg.Contains("unexpectedly"))
                return "Bağlantı hatası oluştu. Biraz sonra tekrar deneyin.";
            return $"Giriş yapılamadı: {ex.Message}";
        }

        private void SetBusy(bool busy)
        {
            busyOverlay.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;

            btnLogin.IsEnabled = !busy;
            btnRegister.IsEnabled = !busy;
            btnForgot.IsEnabled = !busy;
            btnGoogle.IsEnabled = !busy;

            // İptal butonu busy sırasında görünür ve aktif kalmalı
            btnCancelGoogle.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            btnCancelGoogle.IsEnabled = busy;

            if (FindName("btnLogout") is Button btnLogout)
                btnLogout.IsEnabled = !busy;
        }

        // Basit query param ekleyici
        private static string AddQueryParams(string url, params (string key, string? value)[] kvs)
        {
            var firstSep = url.Contains("?") ? "&" : "?";
            var parts = new System.Collections.Generic.List<string>();
            foreach (var (key, value) in kvs)
            {
                if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value)) continue;
                parts.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");
            }
            var extra = string.Join("&", parts);
            return parts.Count == 0 ? url : (url + firstSep + extra);
        }
    }
}