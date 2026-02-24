using SideBar_Nav.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SideBar_Nav.Windows
{
    public partial class YuzeyIslemiEkleWindow : Window
    {
        private List<YuzeyIslemleri> yuzeyIslemListesi;

        public bool DegisiklikYapildi { get; private set; } = false;
        public string SonEklenenYuzeyIslemi { get; private set; }

        public YuzeyIslemiEkleWindow()
        {
            InitializeComponent();
            _ = YukleYuzeyIslemleri();
        }

        private async Task YukleYuzeyIslemleri()
        {
            try
            {
                var response = await App.SupabaseClient.From<YuzeyIslemleri>().Get();
                yuzeyIslemListesi = response.Models;
                dataGridYuzeyIslemleri.ItemsSource = yuzeyIslemListesi;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Yüzey işlemleri yüklenemedi: " + ex.Message);
            }
        }

        private async void BtnEkle_Click(object sender, RoutedEventArgs e)
        {
            string islem = txtYuzeyIslemi.Text.Trim();

            if (string.IsNullOrWhiteSpace(islem))
            {
                MessageBox.Show("Lütfen bir yüzey işlemi girin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (yuzeyIslemListesi.Any(x => x.YuzeyIslemi.Equals(islem, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("Bu yüzey işlemi zaten mevcut!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var yeniKayit = new YuzeyIslemleri { YuzeyIslemi = islem };
                await App.SupabaseClient.From<YuzeyIslemleri>().Insert(yeniKayit);

                DegisiklikYapildi = true;
                SonEklenenYuzeyIslemi = islem;

                txtYuzeyIslemi.Clear();
                await YukleYuzeyIslemleri();
                MessageBox.Show("Yüzey işlemi eklendi.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ekleme hatası: " + ex.Message);
            }
        }

        private async void BtnSil_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridYuzeyIslemleri.SelectedItem is not YuzeyIslemleri secili)
            {
                MessageBox.Show("Lütfen silinecek bir satır seçin.");
                return;
            }

            var sonuc = MessageBox.Show($"'{secili.YuzeyIslemi}' işlemini silmek istiyor musunuz?", "Onay", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (sonuc == MessageBoxResult.Yes)
            {
                try
                {
                    await App.SupabaseClient.From<YuzeyIslemleri>().Delete(secili);
                    DegisiklikYapildi = true;
                    await YukleYuzeyIslemleri();
                    MessageBox.Show("Silme başarılı.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Silme hatası: " + ex.Message);
                }
            }
        }

        private async void BtnYenile_Click(object sender, RoutedEventArgs e)
        {
            await YukleYuzeyIslemleri();
        }

        private async void DataGridYuzeyIslemleri_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit)
                return;

            await Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    dataGridYuzeyIslemleri.CommitEdit();
                    if (dataGridYuzeyIslemleri.SelectedItem is YuzeyIslemleri guncellenen)
                    {
                        await App.SupabaseClient.From<YuzeyIslemleri>().Update(guncellenen);
                        DegisiklikYapildi = true;
                        MessageBox.Show("Güncelleme başarılı.");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Güncelleme hatası: " + ex.Message);
                }
            }, System.Windows.Threading.DispatcherPriority.Background);
        }
    }
}
