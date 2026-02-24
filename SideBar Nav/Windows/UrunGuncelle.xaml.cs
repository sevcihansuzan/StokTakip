using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using SideBar_Nav.Models;
using static Supabase.Postgrest.Constants;

namespace SideBar_Nav.Windows
{
    public partial class UrunGuncelle : Window
    {
        // UI yardımcıları
        public string StokEn => txtStokEn.Text.Replace('.', ',');
        public string StokBoy => txtStokBoy.Text.Replace('.', ',');
        public string StokAlan => txtStokAlan.Text;
        public string StokTonaj => txtStokTonaj.Text;

        // Durum
        private string epc = "";
        private double kalinlik;
        private int plakaAdedi;
        private double? yogunlukKatsayisi;

        private UrunStok guncellenecekUrun;

        public UrunGuncelle(UrunStok selectedRow)
        {
            InitializeComponent();

            // Model ata
            guncellenecekUrun = selectedRow ?? throw new ArgumentNullException(nameof(selectedRow));
            epc = selectedRow.EPC;

            // Alanları doldur (Ürün Türü liste gelmeden set edilmeyecek)
            txtEPC.Text = selectedRow.EPC;
            txtBarkodNo.Text = selectedRow.BarkodNo;
            txtBandilNo.Text = selectedRow.BandilNo;
            txtPlakaNo.Text = selectedRow.PlakaNo;
            cmbUrunTipi.Text = selectedRow.UrunTipi;
            // cmbUrunTuru.Text = selectedRow.UrunTuru;  // Liste yüklenince SelectedItem ile set edilecek
            cmbYuzeyIslemi.Text = selectedRow.YuzeyIslemi;
            txtSeleksiyon.Text = selectedRow.Seleksiyon;
            dateUretimTarihi.SelectedDate = selectedRow.UretimTarihi;

            txtKalınlık.Text = selectedRow.Kalinlik?.ToString("F3", CultureInfo.InvariantCulture) ?? "";
            txtStokEn.Text = selectedRow.StokEn?.ToString("F3", CultureInfo.InvariantCulture) ?? "";
            txtStokBoy.Text = selectedRow.StokBoy?.ToString("F3", CultureInfo.InvariantCulture) ?? "";
            txtStokAlan.Text = selectedRow.StokAlan?.ToString("F5", CultureInfo.InvariantCulture) ?? "";
            txtStokTonaj.Text = selectedRow.StokTonaj?.ToString("F5", CultureInfo.InvariantCulture) ?? "";

            txtPlakaAdedi.Text = selectedRow.PlakaAdedi?.ToString() ?? "";
            txtDurum.Text = selectedRow.Durum;
            txtRezervasyonNo.Text = selectedRow.RezervasyonNo;
            txtKaydedenPersonel.Text = selectedRow.KaydedenPersonel;
            dateUrunCikisTarihi.SelectedDate = selectedRow.UrunCikisTarihi;
            txtAliciFirma.Text = selectedRow.AliciFirma;

            double.TryParse(txtKalınlık.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out kalinlik);
            int.TryParse(txtPlakaAdedi.Text, out plakaAdedi);

            // Pencere yüklendiğinde listeyi getir ve seçim uygula
            Loaded += UrunGuncelle_Loaded;
        }

        private async void UrunGuncelle_Loaded(object? sender, RoutedEventArgs e)
        {
            var liste = await UrunTurleriniYukle(); // NESNE listesi
            if (liste.Count > 0)
            {
                var match = liste.FirstOrDefault(x =>
                    string.Equals(x.UrunTuru, guncellenecekUrun.UrunTuru, StringComparison.OrdinalIgnoreCase));

                if (match != null)
                {
                    cmbUrunTuru.SelectedItem = match;
                    yogunlukKatsayisi = ConvertToNullableDouble(match.YogunlukKatsayisi);
                }
            }

            Recalc(); // İlk hesap
        }

        /// <summary>
        /// Ürün türlerini nesne (UrunTurleri) listesi olarak yükler ve ComboBox'a bağlar.
        /// </summary>
        private async Task<List<UrunTurleri>> UrunTurleriniYukle()
        {
            try
            {
                var response = await App.SupabaseClient
                    .From<UrunTurleri>()
                    .Select("UrunTuru,YogunlukKatsayisi")
                    .Order(x => x.UrunTuru, Ordering.Ascending)
                    .Get();

                var list = response.Models.ToList();
                cmbUrunTuru.ItemsSource = list; // DisplayMemberPath/SelectedValuePath XAML'de verildi
                return list;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ürün türleri yüklenemedi: " + ex.Message,
                                "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                cmbUrunTuru.ItemsSource = new List<UrunTurleri>();
                return new List<UrunTurleri>();
            }
        }

        /// <summary>
        /// Eğer XAML'de SelectionChanged="cmbUrunTuru_SelectionChanged" bağlarsan bu çalışır.
        /// </summary>
        private void cmbUrunTuru_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateYogunlukFromSelection();
            Recalc();
        }

        /// <summary>
        /// XAML'de hâlâ SelectionChanged/TextChanged="AlanTonajOtomatikHesapla" bağlıysa
        /// uyumluluk için bırakıldı. Gerekirse yogunluk günceller ve hesap yapar.
        /// </summary>
        private void AlanTonajOtomatikHesapla(object? sender, EventArgs? e)
        {
            if (sender is ComboBox cb && cb == cmbUrunTuru)
                UpdateYogunlukFromSelection();

            Recalc();
        }

        private void UpdateYogunlukFromSelection()
        {
            var val = cmbUrunTuru.SelectedValue;
            if (val == null)
            {
                yogunlukKatsayisi = null;
                return;
            }

            switch (val)
            {
                case double d:
                    yogunlukKatsayisi = d; break;
                case float f:
                    yogunlukKatsayisi = f; break;
                case decimal m:
                    yogunlukKatsayisi = (double)m; break;
                case int i:
                    yogunlukKatsayisi = i; break;
                case long l:
                    yogunlukKatsayisi = l; break;
                default:
                    if (double.TryParse(Convert.ToString(val, CultureInfo.InvariantCulture),
                                        NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                        yogunlukKatsayisi = parsed;
                    else
                        yogunlukKatsayisi = null;
                    break;
            }
        }

        private static double? ConvertToNullableDouble(object? value)
        {
            if (value == null) return null;

            switch (value)
            {
                case double d: return d;
                case float f: return f;
                case decimal m: return (double)m;
                case int i: return i;
                case long l: return l;
                default:
                    if (double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture),
                                        NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                        return parsed;
                    return null;
            }
        }

        private void Recalc()
        {
            // Girişleri oku
            double.TryParse(txtKalınlık.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out kalinlik);
            int.TryParse(txtPlakaAdedi.Text, out plakaAdedi);

            txtStokAlan.Text = "-";
            txtStokTonaj.Text = "-";

            if (!yogunlukKatsayisi.HasValue)
                return;

            var enText = txtStokEn.Text.Replace(',', '.');
            var boyText = txtStokBoy.Text.Replace(',', '.');

            if (double.TryParse(enText, NumberStyles.Any, CultureInfo.InvariantCulture, out var en) &&
                double.TryParse(boyText, NumberStyles.Any, CultureInfo.InvariantCulture, out var boy))
            {
                // cm * cm * adet -> m^2: /10000
                var alan = (en * boy * plakaAdedi) / 10000.0;
                var tonaj = alan * kalinlik * yogunlukKatsayisi.Value;

                txtStokAlan.Text = alan.ToString("F5", CultureInfo.GetCultureInfo("tr-TR"));
                txtStokTonaj.Text = tonaj.ToString("F5", CultureInfo.GetCultureInfo("tr-TR"));
            }
        }

        private async void BtnGuncelle_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Formdan modele aktar
                guncellenecekUrun.BarkodNo = txtBarkodNo.Text;
                guncellenecekUrun.BandilNo = txtBandilNo.Text;
                guncellenecekUrun.PlakaNo = txtPlakaNo.Text;
                guncellenecekUrun.UrunTipi = cmbUrunTipi.Text;

                // Ürün türü: DisplayMemberPath="UrunTuru"
                if (cmbUrunTuru.SelectedItem is UrunTurleri ut)
                    guncellenecekUrun.UrunTuru = ut.UrunTuru;
                else
                    guncellenecekUrun.UrunTuru = cmbUrunTuru.Text;

                guncellenecekUrun.YuzeyIslemi = cmbYuzeyIslemi.Text;
                guncellenecekUrun.Seleksiyon = txtSeleksiyon.Text;
                guncellenecekUrun.UretimTarihi = dateUretimTarihi.SelectedDate;

                decimal.TryParse(txtKalınlık.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var kalinlikDecimal);
                decimal.TryParse(txtStokEn.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var stokEnDecimal);
                decimal.TryParse(txtStokBoy.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var stokBoyDecimal);
                decimal.TryParse(txtStokAlan.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var stokAlanDecimal);
                decimal.TryParse(txtStokTonaj.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var stokTonajDecimal);
                int.TryParse(txtPlakaAdedi.Text, out var plakaAdediInt);

                guncellenecekUrun.Kalinlik = kalinlikDecimal;
                guncellenecekUrun.StokEn = stokEnDecimal;
                guncellenecekUrun.StokBoy = stokBoyDecimal;
                guncellenecekUrun.StokAlan = stokAlanDecimal;
                guncellenecekUrun.StokTonaj = stokTonajDecimal;
                guncellenecekUrun.PlakaAdedi = plakaAdediInt;
                //satis için eklenmeli mi?
                guncellenecekUrun.Durum = txtDurum.Text;
                guncellenecekUrun.RezervasyonNo = txtRezervasyonNo.Text;
                guncellenecekUrun.KaydedenPersonel = txtKaydedenPersonel.Text;
                guncellenecekUrun.UrunCikisTarihi = dateUrunCikisTarihi.SelectedDate;
                guncellenecekUrun.AliciFirma = txtAliciFirma.Text;

                // Güncelle
                await App.SupabaseClient.From<UrunStok>().Update(guncellenecekUrun);

                MessageBox.Show("Güncelleme başarılı.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Güncelleme hatası: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
