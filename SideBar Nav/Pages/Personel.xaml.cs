using SideBar_Nav.Windows;
﻿using SideBar_Nav.Models;
using Supabase.Gotrue;
using Supabase.Postgrest;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static Supabase.Postgrest.Constants;
using Microsoft.Win32;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using static Supabase.Postgrest.Constants;

namespace SideBar_Nav.Pages
{
    public partial class Personel : Page
    {
        private void AvatarDragEnter(object sender, DragEventArgs e) => e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        private void AvatarDragOver(object sender, DragEventArgs e) => e.Handled = true;
        public Personel()
        {
            InitializeComponent();
            Loaded += Personel_Loaded;
        }

        private async void Personel_Loaded(object sender, RoutedEventArgs e)
        {
            // Sayfaya erişim zaten Nav guard ile kontrol edildi,
            // yine de Me null ise engelle:
            var me = GetMe();
            if (me == null)
            {
                lblStatus.Text = "Oturum bulunamadı.";
                SetBusy(false);
                return;
            }

            // Formu doldur
            FillForm(me);

            // İsteğe bağlı: açılışta taze veri çekmek istersen
            await RefreshFromServerAsync();
        }

        private Kullanicilar? GetMe()
        {
            if (Application.Current.MainWindow is MainWindow mw)
                return mw?.Vm?.Me;
            return null;
        }

        private void FillForm(Kullanicilar me)
        {
            txtEmail.Text = me.Email ?? "";
            txtAd.Text = me.Ad ?? "";
            txtPozisyon.Text = me.Pozisyon ?? "";
            txtPhone.Text = me.Phone ?? "";

            txtCreated.Text = me.CreatedAt.ToLocalTime().ToString("g");
            txtUpdated.Text = me.UpdatedAt.ToLocalTime().ToString("g");
            txtLastSignIn.Text = me.LastSignInAt?.ToLocalTime().ToString("g") ?? "—";

            // PersonelAllow kapalıysa (teoride bu sayfaya gelemez ama yine de):
            var canEdit = me.PersonelAllow;
            btnSave.IsEnabled = canEdit;

            // --- EK: avatar'ı göster ---
            if (!string.IsNullOrWhiteSpace(me.AvatarUrl))
                SetAvatarFromUrl(me.AvatarUrl!);
            else
                imgAvatar.Source = null;
        }
        private void SetAvatarFromUrl(string url)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                // Dosyayı kilitlemeden ve cache'i baypas ederek yükle
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;

                // Cache-busting (aynı dosya adıyla güncellediğinde de yenilensin)
                var bust = (url.Contains("?") ? "&" : "?") + "t=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                bmp.UriSource = new Uri(url + bust, UriKind.Absolute);

                bmp.EndInit();
                imgAvatar.Source = bmp;
            }
            catch
            {
                imgAvatar.Source = null;
            }
        }

        private async Task RefreshFromServerAsync()
        {
            try
            {
                SetBusy(true);

                if (App.SupabaseClient == null)
                    await App.InitializeSupabase();

                var meAuth = App.SupabaseClient.Auth.CurrentUser;
                if (meAuth == null)
                    throw new Exception("Oturum süresi dolmuş olabilir.");

                var meRow = await App.SupabaseClient
                    .From<Kullanicilar>()
                    .Filter("id", Operator.Equals, meAuth.Id)
                    .Single();

                // VM'ye geri yaz ve formu güncelle
                if (Application.Current.MainWindow is MainWindow mw && mw.Vm != null)
                {
                    mw.Vm.SetMe(meRow);
                }
                FillForm(meRow);

                lblStatus.Foreground = new SolidColorBrush(Colors.ForestGreen);
                lblStatus.Text = "Bilgiler güncellendi.";
            }
            catch (Exception ex)
            {
                lblStatus.Foreground = new SolidColorBrush(Colors.IndianRed);
                lblStatus.Text = $"Yenileme hatası: {ex.Message}";
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void btnRefresh_Click(object sender, RoutedEventArgs e)
            => await RefreshFromServerAsync();
        
        private async void btnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                lblStatus.Text = "";
                SetBusy(true);

                if (App.SupabaseClient == null)
                    await App.InitializeSupabase();

                var authUser = App.SupabaseClient.Auth.CurrentUser;
                if (authUser == null)
                    throw new Exception("Oturum bulunamadı.");

                // Basit doğrulamalar
                var ad = txtAd.Text?.Trim();
                var phone = txtPhone.Text?.Trim();

                // UPDATE: sadece değişen alanları göndermek istersen önce mevcut Me’yi al
                var meCurrent = await App.SupabaseClient
                    .From<Kullanicilar>()
                    .Filter("id", Operator.Equals, authUser.Id)
                    .Single();

                meCurrent.Ad = ad;
                meCurrent.Phone = phone;

                await App.SupabaseClient.From<Kullanicilar>().Update(meCurrent);

                // VM’yi ve formu tazele
                if (Application.Current.MainWindow is MainWindow mw && mw.Vm != null)
                {
                    mw.Vm.SetMe(meCurrent);
                }
                FillForm(meCurrent);

                lblStatus.Foreground = new SolidColorBrush(Colors.ForestGreen);
                lblStatus.Text = "Bilgiler kaydedildi.";
            }
            catch (Exception ex)
            {
                lblStatus.Foreground = new SolidColorBrush(Colors.IndianRed);
                lblStatus.Text = $"Kaydetme hatası: {ex.Message}";
            }
            finally
            {
                SetBusy(false);
            }
        }
        private void btnPassword_Click(object sender, RoutedEventArgs e)
        {
            if (App.SupabaseClient?.Auth.CurrentUser == null)
            {
                MessageBox.Show("Bu işlemi yapmak için önce giriş yapmalısınız.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var wnd = new Windows.PasswordWindow { Owner = Window.GetWindow(this) };
            wnd.ShowDialog();
        }

        private async void AvatarDrop(object sender, DragEventArgs e)
        {
            try
            {
                if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files is null || files.Length == 0) return;

                var file = files[0];
                var ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
                if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp"))
                    throw new Exception("Lütfen JPG, PNG veya WEBP yükleyin.");

                SetBusy(true);

                if (App.SupabaseClient == null) await App.InitializeSupabase();
                var meAuth = App.SupabaseClient.Auth.CurrentUser ?? throw new Exception("Oturum bulunamadı.");

                // 1) Storage'a yükle
                byte[] fileBytes = await File.ReadAllBytesAsync(file);

                // 2) Storage'a yükle
                var bucket = App.SupabaseClient.Storage.From("avatars");
                var path = $"users/{meAuth.Id}/avatar{ext}";

                await bucket.Upload(fileBytes, path, new Supabase.Storage.FileOptions
                {
                    Upsert = true,
                    ContentType = ext switch
                    {
                        ".png" => "image/png",
                        ".webp" => "image/webp",
                        _ => "image/jpeg"
                    }
                });

                // Public bucket ise:
                var publicUrl = bucket.GetPublicUrl(path);

                // Private bucket isen şuna geç (örn. 1 saatlik):
                // var publicUrl = await bucket.CreateSignedUrl(path, 3600);

                // 2) DB'de kendi satırını güncelle
                var meRow = await App.SupabaseClient
                    .From<Kullanicilar>()
                    .Filter("id", Operator.Equals, meAuth.Id)
                    .Single();

                meRow.AvatarUrl = publicUrl;

                await App.SupabaseClient.From<Kullanicilar>().Update(meRow);

                // 3) UI'da göster
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(publicUrl);
                bmp.EndInit();
                imgAvatar.Source = bmp;

                lblStatus.Text = "Fotoğraf güncellendi.";
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Yükleme hatası: " + ex.Message;
            }
            finally
            {
                SetBusy(false);
            }
        }




        private void SetBusy(bool busy)
        {
            busyOverlay.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            btnSave.IsEnabled = !busy && (GetMe()?.PersonelAllow ?? false);
            btnRefresh.IsEnabled = !busy;
        }
    }
}
