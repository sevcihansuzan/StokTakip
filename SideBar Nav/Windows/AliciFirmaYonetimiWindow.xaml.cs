using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Data;
using SideBar_Nav.Models;

namespace SideBar_Nav.Windows
{
    public partial class AliciFirmaYonetimiWindow : Window
    {
        public string SecilenFirmaAdi { get; private set; }
        public string SecilenSevkiyatAdresi { get; private set; }

        private List<AliciFirmalar> firmaListesi = new();

        public AliciFirmaYonetimiWindow()
        {
            InitializeComponent();
            _ = FirmalariYukle();
        }

        private async Task FirmalariYukle()
        {
            var response = await App.SupabaseClient.From<AliciFirmalar>().Get();
            firmaListesi = response.Models;
            dataGridFirmalar.ItemsSource = firmaListesi;
        }

        private async void BtnYeniEkle_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtFirmaAdi.Text) || string.IsNullOrWhiteSpace(txtSevkiyatAdresi.Text))
            {
                MessageBox.Show("Firma adı ve sevkiyat adresi zorunludur.", "Eksik Bilgi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var yeniFirma = new AliciFirmalar
            {
                FirmaAdi = txtFirmaAdi.Text.Trim(),
                VergiNo = txtVergiNo.Text.Trim(),
                VergiDairesi = txtVergiDairesi.Text.Trim(),
                Telefon = txtTelefon.Text.Trim(),
                YetkiliAdi = txtYetkili.Text.Trim(),
                Email = txtEmail.Text.Trim(),
                SevkiyatAdresi = txtSevkiyatAdresi.Text.Trim(),
                FaturaAdresi = txtFaturaAdresi.Text.Trim(),
                Ulke = txtUlke.Text.Trim(),
                Sehir = txtSehir.Text.Trim(),
                Ilce = txtIlce.Text.Trim(),
                PostaKodu = txtPostaKodu.Text.Trim(),
                Notlar = txtNotlar.Text.Trim(),
                OlusturmaTarihi = DateTime.Now
            };

            await App.SupabaseClient.From<AliciFirmalar>().Insert(yeniFirma);
            MessageBox.Show("Firma eklendi.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            await FirmalariYukle();
            SecilenFirmaAdi = yeniFirma.FirmaAdi;
            SecilenSevkiyatAdresi = yeniFirma.SevkiyatAdresi;
            txtArama.Text = SecilenFirmaAdi;
            Temizle();
        }

        private async void BtnGuncelle_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridFirmalar.SelectedItem is not AliciFirmalar secilenFirma)
            {
                MessageBox.Show("Güncellemek için bir firma seçin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            secilenFirma.FirmaAdi = txtFirmaAdi.Text.Trim();
            secilenFirma.VergiNo = txtVergiNo.Text.Trim();
            secilenFirma.VergiDairesi = txtVergiDairesi.Text.Trim();
            secilenFirma.Telefon = txtTelefon.Text.Trim();
            secilenFirma.YetkiliAdi = txtYetkili.Text.Trim();
            secilenFirma.Email = txtEmail.Text.Trim();
            secilenFirma.SevkiyatAdresi = txtSevkiyatAdresi.Text.Trim();
            secilenFirma.FaturaAdresi = txtFaturaAdresi.Text.Trim();
            secilenFirma.Ulke = txtUlke.Text.Trim();
            secilenFirma.Sehir = txtSehir.Text.Trim();
            secilenFirma.Ilce = txtIlce.Text.Trim();
            secilenFirma.PostaKodu = txtPostaKodu.Text.Trim();
            secilenFirma.Notlar = txtNotlar.Text.Trim();
            secilenFirma.GuncellemeTarihi = DateTime.Now;

            await App.SupabaseClient.From<AliciFirmalar>().Update(secilenFirma);
            MessageBox.Show("Firma güncellendi.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            await FirmalariYukle();
            Temizle();
        }

        private async void BtnSil_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridFirmalar.SelectedItem is AliciFirmalar secilenFirma)
            {
                var rezervasyonlar = await App.SupabaseClient.From<UrunRezervasyon>().Where(x => x.AliciFirma == secilenFirma.FirmaAdi).Get();

                if (rezervasyonlar.Models.Any())
                {
                    MessageBox.Show("Bu firmaya bağlı rezervasyonlar var. Silme işlemi engellendi.", "İşlem İptal", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show("Bu firmayı silmek istiyor musunuz?", "Onay", MessageBoxButton.YesNo);
                if (result == MessageBoxResult.Yes)
                {
                    await App.SupabaseClient.From<AliciFirmalar>().Delete(secilenFirma);
                    await FirmalariYukle();
                    Temizle();
                }
            }
        }

        private void BtnSecKapat_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridFirmalar.SelectedItem is AliciFirmalar secilenFirma)
            {
                SecilenFirmaAdi = secilenFirma.FirmaAdi;
                SecilenSevkiyatAdresi = secilenFirma.SevkiyatAdresi;
                DialogResult = true;
            }
        }

        private void dataGridFirmalar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dataGridFirmalar.SelectedItem is AliciFirmalar secilenFirma)
            {
                txtFirmaAdi.Text = secilenFirma.FirmaAdi;
                txtVergiNo.Text = secilenFirma.VergiNo;
                txtVergiDairesi.Text = secilenFirma.VergiDairesi;
                txtTelefon.Text = secilenFirma.Telefon;
                txtYetkili.Text = secilenFirma.YetkiliAdi;
                txtEmail.Text = secilenFirma.Email;
                txtSevkiyatAdresi.Text = secilenFirma.SevkiyatAdresi;
                txtFaturaAdresi.Text = secilenFirma.FaturaAdresi;
                txtUlke.Text = secilenFirma.Ulke;
                txtSehir.Text = secilenFirma.Sehir;
                txtIlce.Text = secilenFirma.Ilce;
                txtPostaKodu.Text = secilenFirma.PostaKodu;
                txtNotlar.Text = secilenFirma.Notlar;
            }
        }

        private void txtArama_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filtre = txtArama.Text.Trim().ToLower();
            dataGridFirmalar.ItemsSource = firmaListesi
                .Where(f => f.FirmaAdi != null && f.FirmaAdi.ToLower().Contains(filtre))
                .ToList();
        }

        private void Temizle()
        {
            txtFirmaAdi.Clear();
            txtVergiNo.Clear();
            txtVergiDairesi.Clear();
            txtTelefon.Clear();
            txtYetkili.Clear();
            txtEmail.Clear();
            txtSevkiyatAdresi.Clear();
            txtFaturaAdresi.Clear();
            txtUlke.Clear();
            txtSehir.Clear();
            txtIlce.Clear();
            txtPostaKodu.Clear();
            txtNotlar.Clear();
            dataGridFirmalar.UnselectAll();
        }
    }
}
