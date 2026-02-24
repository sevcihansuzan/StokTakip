using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using SideBar_Nav.Models; // UrunTurleri modeli buradan gelmeli

namespace SideBar_Nav.Windows
{
    public partial class UrunTuruEkleWindow : Window
    {
        private List<UrunTurleri> urunTurleriList;

        public bool DegisiklikYapildi { get; private set; } = false;
        public string SonEklenenUrunTuru { get; private set; }

        public UrunTuruEkleWindow()
        {
            InitializeComponent();
            _ = YukleUrunTurleri();
        }

        private async Task YukleUrunTurleri()
        {
            try
            {
                var response = await App.SupabaseClient.From<UrunTurleri>().Get();
                urunTurleriList = response.Models;
                dataGridUrunTurleri.ItemsSource = urunTurleriList;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Veri yüklenemedi: " + ex.Message);
            }
        }

        private async void BtnEkle_Click(object sender, RoutedEventArgs e)
        {
            string tur = txtUrunTuru.Text.Trim();
            if (!double.TryParse(txtYogunluk.Text.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double katsayi))
            {
                MessageBox.Show("Geçerli bir yoğunluk katsayısı girin (örnek: 2.65)", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var yeniTur = new UrunTurleri
                {
                    UrunTuru = tur,
                    YogunlukKatsayisi = katsayi
                };

                var response = await App.SupabaseClient.From<UrunTurleri>().Insert(yeniTur);
                SonEklenenUrunTuru = tur;
                DegisiklikYapildi = true;

                txtUrunTuru.Clear();
                txtYogunluk.Clear();
                await YukleUrunTurleri();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ekleme hatası: " + ex.Message);
            }
        }

        private async void BtnSil_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridUrunTurleri.SelectedItem is UrunTurleri selectedItem)
            {
                var result = MessageBox.Show($"'{selectedItem.UrunTuru}' ürün türünü silmek istiyor musunuz?", "Silme Onayı", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        await App.SupabaseClient.From<UrunTurleri>().Delete(selectedItem);
                        DegisiklikYapildi = true;
                        await YukleUrunTurleri();
                        MessageBox.Show("Silme başarılı.");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Silme hatası: " + ex.Message);
                    }
                }
            }
            else
            {
                MessageBox.Show("Lütfen silinecek satırı seçin.");
            }
        }

        private async void BtnYenile_Click(object sender, RoutedEventArgs e)
        {
            await YukleUrunTurleri();
        }

        private async void DataGridUrunTurleri_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit)
                return;

            await Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    dataGridUrunTurleri.CommitEdit();

                    if (dataGridUrunTurleri.SelectedItem is UrunTurleri updatedItem)
                    {
                        await App.SupabaseClient.From<UrunTurleri>().Update(updatedItem);
                        DegisiklikYapildi = true;
                        await YukleUrunTurleri();
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
