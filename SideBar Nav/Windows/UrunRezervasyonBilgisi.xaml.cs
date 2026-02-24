using System;
using System.Linq;
using System.Windows;
using System.Threading.Tasks;

using SideBar_Nav.Models;

namespace SideBar_Nav.Windows
{
    public partial class UrunRezervasyonBilgisi : Window
    {
        public UrunRezervasyonBilgisi(string rezervasyonNo)
        {
            InitializeComponent();
            lblRezNo.Text = rezervasyonNo;
            _ = YukleRezervasyonBilgisi(rezervasyonNo);
            _ = YukleUrunler(rezervasyonNo);
        }

        private async Task YukleRezervasyonBilgisi(string rezNo)
        {
            try
            {
                var response = await App.SupabaseClient
                    .From<UrunRezervasyon>()
                    .Where(x => x.RezervasyonNo == rezNo)
                    .Get();

                var bilgi = response.Models.FirstOrDefault();
                if (bilgi != null)
                {
                    lblFirma.Text = bilgi.AliciFirma;
                    lblDurum.Text = bilgi.Durum;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Rezervasyon bilgisi yüklenirken hata: " + ex.Message);
            }
        }

        private async Task YukleUrunler(string rezNo)
        {
            try
            {
                var response = await App.SupabaseClient
                    .From<UrunStok>()
                    .Where(x => x.RezervasyonNo == rezNo)
                    .Get();

                dataGridUrunler.ItemsSource = response.Models;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ürün listesi yüklenirken hata: " + ex.Message);
            }
        }
    }
}
