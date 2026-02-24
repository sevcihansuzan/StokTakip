using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Data;
using SideBar_Nav.Models;

namespace SideBar_Nav.Windows
{
    public partial class BoyutGuncelleWindow : Window
    {
        public string SatisEn => txtSatisEn.Text.Replace('.', ',');
        public string SatisBoy => txtSatisBoy.Text.Replace('.', ',');
        public string SatisAlan => txtSatisAlan.Text;
        public string SatisTonaj => txtSatisTonaj.Text;

        private double kalinlik;
        private int plakaAdedi;
        private string urunTuru;
        private double? yogunlukKatsayisi;
        private DataRowView selectedRow;

        public BoyutGuncelleWindow(DataRowView row)
        {
            InitializeComponent();
            selectedRow = row;
            _ = InitAsync();
        }

        private async Task InitAsync()
        {
            txtStokEn.Text = selectedRow["StokEn"]?.ToString();
            txtStokBoy.Text = selectedRow["StokBoy"]?.ToString();
            txtStokAlan.Text = selectedRow["StokAlan"]?.ToString();
            txtStokTonaj.Text = selectedRow["StokTonaj"]?.ToString();

            urunTuru = selectedRow["UrunTuru"]?.ToString();
            _ = double.TryParse(selectedRow["Kalinlik"]?.ToString()?.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out kalinlik);
            _ = int.TryParse(selectedRow["PlakaAdedi"]?.ToString(), out plakaAdedi);

            await LoadYogunlukKatsayisi(urunTuru);

            txtSatisEn.TextChanged += AlanTonajOtomatikHesapla;
            txtSatisBoy.TextChanged += AlanTonajOtomatikHesapla;

            AlanTonajOtomatikHesapla(null, null);
        }

        private void BtnSatisBoyutKaydet_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private async Task LoadYogunlukKatsayisi(string urunTuru)
        {
            try
            {
                var response = await App.SupabaseClient
                    .From<UrunTurleri>()
                    .Where(t => t.UrunTuru == urunTuru)
                    .Get();

                var sonuc = response.Models.FirstOrDefault();
                if (sonuc != null)
                    yogunlukKatsayisi = sonuc.YogunlukKatsayisi;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Yoğunluk katsayısı alınamadı:\n" + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AlanTonajOtomatikHesapla(object sender, EventArgs e)
        {
            txtSatisAlan.Text = "-";
            txtSatisTonaj.Text = "-";

            if (!yogunlukKatsayisi.HasValue)
                return;

            string enText = txtSatisEn.Text.Replace(',', '.');
            string boyText = txtSatisBoy.Text.Replace(',', '.');

            if (double.TryParse(enText, NumberStyles.Any, CultureInfo.InvariantCulture, out double en) &&
                double.TryParse(boyText, NumberStyles.Any, CultureInfo.InvariantCulture, out double boy))
            {
                double alan = (en * boy * plakaAdedi) / 10000.0;
                double tonaj = alan * kalinlik * yogunlukKatsayisi.Value;

                txtSatisAlan.Text = alan.ToString("F5", CultureInfo.GetCultureInfo("tr-TR"));
                txtSatisTonaj.Text = tonaj.ToString("F5", CultureInfo.GetCultureInfo("tr-TR"));
            }
        }
    }
}
