using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace SideBar_Nav.Windows
{
    public partial class PasswordWindow : Window
    {
        private bool _hasPassword;

        public PasswordWindow()
        {
            InitializeComponent();
            Loaded += PasswordWindow_Loaded;
        }

        private bool TryGetMetaHasPassword(User u)
        {
            try
            {
                if (u?.UserMetadata is Dictionary<string, object> meta &&
                    meta.TryGetValue("has_password", out var v))
                {
                    // metadata "true"/true/"True"/1 tarzlarını normalize et
                    return v is bool b ? b :
                           v is string s ? bool.TryParse(s, out var sb) && sb :
                           v is int i ? i != 0 :
                           false;
                }
            }
            catch { }
            return false;
        }

        private async void PasswordWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var client = App.SupabaseClient;
            var user = client?.Auth.CurrentUser;
            var session = client?.Auth.CurrentSession;

            if (client == null || user == null || session == null)
            {
                MessageBox.Show("Oturum bulunamadı.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }

            txtEmail.Text = user.Email ?? "";

            try
            {
                // En güncel kullanıcıyı çek
                var refreshed = await client.Auth.GetUser(session.AccessToken);
                // 1) Öncelik: metadata’daki kalıcı bayrak
                _hasPassword = TryGetMetaHasPassword(refreshed);

                // (İsteğe bağlı yedek kontrol: provider kontrolü; başarısız ise görmezden gelinir)
                if (!_hasPassword && refreshed?.Identities != null)
                {
                    var hasEmailProvider = refreshed.Identities.Any(i =>
                        string.Equals(i.Provider, "email", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(i.Provider, "password", StringComparison.OrdinalIgnoreCase));
                    if (hasEmailProvider) _hasPassword = true;
                }
            }
            catch
            {
                // sessiz geç: metadata okunamadıysa _hasPassword varsayılan false kalabilir
            }
        }

        private async void btnSave_Click(object sender, RoutedEventArgs e)
        {
            lblStatus.Text = "";

            var client = App.SupabaseClient;
            var email = txtEmail.Text.Trim();
            var current = txtCurrentPassword.Password;
            var newPass = txtNewPassword.Password;
            var confirm = txtConfirmPassword.Password;

            if (string.IsNullOrWhiteSpace(newPass) || newPass != confirm)
            {
                lblStatus.Text = "Yeni şifre geçersiz veya eşleşmiyor.";
                return;
            }

            try
            {
                if (client == null)
                {
                    await App.InitializeSupabase();
                    client = App.SupabaseClient;
                }

                var session = client!.Auth.CurrentSession;
                if (session == null)
                {
                    lblStatus.Text = "Oturum süresi dolmuş. Lütfen tekrar giriş yapın.";
                    return;
                }

                // HER DENEMEDE state’i tazele ve metadata’dan oku
                try
                {
                    var refreshed = await client.Auth.GetUser(session.AccessToken);
                    _hasPassword = TryGetMetaHasPassword(refreshed) || _hasPassword;
                }
                catch { /* yok say */ }

                // Eğer kullanıcıda daha önce şifre VAR ise → mevcut şifreyle yeniden doğrulama zorunlu
                if (_hasPassword)
                {
                    if (string.IsNullOrWhiteSpace(current))
                    {
                        lblStatus.Text = "Mevcut şifre gerekli.";
                        return;
                    }

                    try
                    {
                        await client.Auth.SignInWithPassword(email, current);
                    }
                    catch (GotrueException)
                    {
                        lblStatus.Text = "Mevcut şifre hatalı.";
                        return;
                    }
                }

                // Şifreyi güncelle
                await client.Auth.Update(new UserAttributes { Password = newPass });

                // Kalıcı güvenlik bayrağını Auth metadata’ya yaz
                // (NOT: mevcut metadata üzerine yazmak yerine merge mantığıyla gönderiyoruz)
                await client.Auth.Update(new UserAttributes
                {
                    Data = new Dictionary<string, object>
                    {
                        ["has_password"] = true
                    }
                });

                // Yeni şifreyle yeniden giriş → session tazele
                await client.Auth.SignInWithPassword(email, newPass);

                // Lokal state’i kilitle
                _hasPassword = true;
                txtCurrentPassword.Clear();

                MessageBox.Show("Şifre güncellendi.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (GotrueException gex)
            {
                lblStatus.Text = gex.Message;
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
