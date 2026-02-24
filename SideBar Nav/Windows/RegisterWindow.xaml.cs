using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;
using System;
using System.Collections.Generic;
using System.Windows;

namespace SideBar_Nav.Windows
{
    public partial class RegisterWindow : Window
    {
        public string RegisteredEmail { get; private set; } = null;

        public RegisterWindow()
        {
            InitializeComponent();
        }

        private void SetBusy(bool on)
        {
            busyOverlay.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            btnRegister.IsEnabled = !on;
            btnCancel.IsEnabled = !on;
        }

        private async void btnRegister_Click(object sender, RoutedEventArgs e)
        {
            lblStatus.Text = "";
            lblStatus.Foreground = System.Windows.Media.Brushes.IndianRed;

            var email = txtEmail.Text?.Trim();
            var password = txtPassword.Password;
            var fullName = txtFullName.Text?.Trim();
            var phone = txtPhone.Text?.Trim();

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

                // Metadata ile kayıt (fullname ve phone’u ekliyoruz)
                var options = new Supabase.Gotrue.SignUpOptions
                {
                    Data = new Dictionary<string, object>
                    {
                        ["full_name"] = fullName,
                        ["phone"] = phone
                    }
                };

                await auth.SignUp(email, password, options);

                lblStatus.Foreground = System.Windows.Media.Brushes.LightGreen;
                lblStatus.Text = "Kayıt oluşturuldu. Lütfen emailinizi doğrulayın.";

                RegisteredEmail = email;
                DialogResult = true;
                Close();
            }
            catch (Supabase.Gotrue.Exceptions.GotrueException gex)
            {
                lblStatus.Text = FriendlyAuthMessage(gex);
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Kayıt başarısız: {ex.Message}";
            }
            finally
            {
                SetBusy(false);
            }
        }


        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private string FriendlyAuthMessage(GotrueException ex)
        {
            var msg = ex.Message?.ToLowerInvariant() ?? "";
            if (msg.Contains("user already registered"))
                return "Bu email ile zaten bir hesap var.";
            if (msg.Contains("password"))
                return "Parola politikası sağlanamadı.";
            return $"İşlem başarısız: {ex.Message}";
        }
    }
}
