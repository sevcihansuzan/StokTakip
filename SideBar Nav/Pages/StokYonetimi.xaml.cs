using SideBar_Nav.Models;
using SideBar_Nav.Services;
using SideBar_Nav.Windows;
using Supabase;
using Supabase.Postgrest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SideBar_Nav.Pages
{
    public partial class StokYonetimi : Page
    {
        private List<UrunStok> stokList = new();
        private string selectedEpc;
        private bool _uiReady; // UI hazır mı?
        private CancellationTokenSource? _barcodeCts;
        private bool _barcodeScanInProgress;

        public StokYonetimi()
        {
            InitializeComponent();

            // Sayfa yüklenince varsayılanları ayarla → veriyi çek → filtre uygula
            Loaded += StokYonetimi_Loaded;
            Unloaded += StokYonetimi_Unloaded;

            // Event wiring
            txtFiltreEPC.TextChanged += (s, e) => FiltreUygula();
            txtFiltreBarkod.TextChanged += (s, e) => FiltreUygula();
            txtFiltreBandil.TextChanged += (s, e) => FiltreUygula();
            dateFiltreTarih.SelectedDateChanged += (s, e) => FiltreUygula();
            cmbTarihSecimi.SelectionChanged += cmbTarihSecimi_SelectionChanged;
            txtFiltrePlaka.TextChanged += (s, e) => FiltreUygula();
            cmbUrunTipiFiltre.SelectionChanged += (s, e) => FiltreUygula();
            cmbUrunTuruFiltre.SelectionChanged += (s, e) => FiltreUygula();
            cmbYuzeyIslemFiltre.SelectionChanged += (s, e) => FiltreUygula();
            cmbDurumFiltre.SelectionChanged += cmbDurumFiltre_SelectionChanged;
        }

        private async void StokYonetimi_Loaded(object sender, RoutedEventArgs e)
        {
            BarcodeScannerBridge.ConnectionStateChanged -= BarcodeScannerBridge_ConnectionStateChanged;
            BarcodeScannerBridge.ConnectionStateChanged += BarcodeScannerBridge_ConnectionStateChanged;
            UpdateBarcodeStatusStok(BarcodeScannerBridge.IsConnected);

            // Varsayılanlar
            cmbTarihSecimi.SelectedIndex = 0;
            if (dateFiltreTarih.SelectedDate == null)
                dateFiltreTarih.SelectedDate = DateTime.Today;

            await YukleStokVerisi();

            _uiReady = true;   // XAML tarafı hazır
            FiltreUygula();    // İlk görünüm filtreli gelsin
        }

        private void StokYonetimi_Unloaded(object sender, RoutedEventArgs e)
        {
            BarcodeScannerBridge.ConnectionStateChanged -= BarcodeScannerBridge_ConnectionStateChanged;
            CancelBarcodeScan();
        }

        private void CancelBarcodeScan()
        {
            try { _barcodeCts?.Cancel(); }
            catch { }
            finally
            {
                _barcodeCts?.Dispose();
                _barcodeCts = null;
            }
        }

        private void BarcodeScannerBridge_ConnectionStateChanged(object? sender, bool connected)
        {
            Dispatcher.InvokeAsync(() => UpdateBarcodeStatusStok(connected));
        }

        private void UpdateBarcodeStatusStok(bool connected)
        {
            if (txtBarcodeStatusStok == null)
                return;

            txtBarcodeStatusStok.Text = connected ? "Barkod okuyucu: Bağlı" : "Barkod okuyucu: Kapalı";
            txtBarcodeStatusStok.Foreground = connected ? new SolidColorBrush(Colors.DarkGreen) : new SolidColorBrush(Colors.DarkRed);
        }

        private async void BtnFiltreBarkodOkuStok_Click(object sender, RoutedEventArgs e)
        {
            if (!BarcodeScannerBridge.IsConnected)
            {
                MessageBox.Show("Barkod okuyucu bağlı değil.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_barcodeScanInProgress)
                return;

            _barcodeScanInProgress = true;
            btnFiltreBarkodOkuStok.IsEnabled = false;

            CancelBarcodeScan();
            _barcodeCts = new CancellationTokenSource();

            try
            {
                string data = await BarcodeScannerBridge.TriggerScanAsync(5000, _barcodeCts.Token);
                if (string.Equals(data, "T[ACK]", StringComparison.OrdinalIgnoreCase))
                {
                    data = await BarcodeScannerBridge.TriggerScanAsync(5000, _barcodeCts.Token);
                }

                txtFiltreBarkod.Text = data;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                MessageBox.Show("Barkod okunamadı: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                CancelBarcodeScan();
                btnFiltreBarkodOkuStok.IsEnabled = true;
                _barcodeScanInProgress = false;
            }
        }

        private async Task YukleStokVerisi()
        {
            try
            {
                var response = await App.SupabaseClient
                    .From<UrunStok>()
                    .Order(x => x.UretimTarihi, Constants.Ordering.Descending)
                    .Get();

                stokList = response.Models ?? new List<UrunStok>();
                // ItemsSource'u burada DOLDURMUYORUZ; her zaman FiltreUygula() dolduracak.
            }
            catch (Exception ex)
            {
                MessageBox.Show("Stok verisi yüklenirken hata: " + ex.Message);
            }
        }

        private void cmbTarihSecimi_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dateFiltreTarih.SelectedDate == null)
                dateFiltreTarih.SelectedDate = DateTime.Today;

            FiltreUygula();
        }

        private void FiltreUygula()
        {
            // UI hazır değilse veya grid bulunamıyorsa hiç dokunma
            if (!_uiReady || dataGridStok == null || stokList == null)
                return;

            string epcFilter = (txtFiltreEPC.Text ?? string.Empty).Trim().ToLowerInvariant();
            string barkodFilter = (txtFiltreBarkod.Text ?? string.Empty).Trim().ToLowerInvariant();
            string bandilFilter = (txtFiltreBandil.Text ?? string.Empty).Trim().ToLowerInvariant();
            string plakaFilter = (txtFiltrePlaka.Text ?? string.Empty).Trim().ToLowerInvariant();

            DateTime? tarihFilter = dateFiltreTarih.SelectedDate;
            string tarihSecimi = (cmbTarihSecimi.SelectedItem as ComboBoxItem)?.Content?.ToString();
            DateTime? baslangic = null, bitis = null;

            if (tarihFilter.HasValue)
            {
                var range = DateRangeHelper.GetRange(tarihSecimi, tarihFilter.Value);
                baslangic = range.Start;
                bitis = range.End;
            }

            string urunTipiFilter = (cmbUrunTipiFiltre.SelectedItem as ComboBoxItem)?.Content?.ToString();
            string urunTuruFilter = (cmbUrunTuruFiltre.SelectedItem as ComboBoxItem)?.Content?.ToString();
            string yuzeyIslemFilter = (cmbYuzeyIslemFiltre.SelectedItem as ComboBoxItem)?.Content?.ToString();
            string durumFilter = (cmbDurumFiltre.SelectedItem as ComboBoxItem)?.Content?.ToString();

            var filtered = stokList.Where(x =>
                (string.IsNullOrEmpty(epcFilter) || (x.EPC?.ToLowerInvariant() ?? "").Contains(epcFilter)) &&
                (string.IsNullOrEmpty(barkodFilter) || (x.BarkodNo?.ToLowerInvariant() ?? "").Contains(barkodFilter)) &&
                (string.IsNullOrEmpty(bandilFilter) || (x.BandilNo?.ToLowerInvariant() ?? "").Contains(bandilFilter)) &&
                (!baslangic.HasValue || (x.UretimTarihi >= baslangic && x.UretimTarihi < bitis)) &&
                (string.IsNullOrEmpty(plakaFilter) || (x.PlakaNo?.ToLowerInvariant() ?? "").Contains(plakaFilter)) &&
                (string.IsNullOrEmpty(urunTipiFilter) || urunTipiFilter == "Hepsi" || x.UrunTipi == urunTipiFilter) &&
                (string.IsNullOrEmpty(urunTuruFilter) || urunTuruFilter == "Hepsi" || x.UrunTuru == urunTuruFilter) &&
                (string.IsNullOrEmpty(yuzeyIslemFilter) || yuzeyIslemFilter == "Hepsi" || x.YuzeyIslemi == yuzeyIslemFilter) &&
                (string.IsNullOrEmpty(durumFilter) || durumFilter == "Hepsi" || x.Durum == durumFilter)
            ).ToList();

            dataGridStok.ItemsSource = filtered;
        }

        private string BuildFilterDescription()
        {
            var filters = new List<string>();

            void AddFilter(string label, string? value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    filters.Add($"{label}: {value.Trim()}");
            }

            AddFilter("EPC", txtFiltreEPC.Text);
            AddFilter("Barkod", txtFiltreBarkod.Text);
            AddFilter("Bandıl", txtFiltreBandil.Text);
            AddFilter("Plaka", txtFiltrePlaka.Text);

            if (dateFiltreTarih.SelectedDate is DateTime selectedDate)
            {
                string? selection = (cmbTarihSecimi.SelectedItem as ComboBoxItem)?.Content?.ToString();
                string description = DateRangeHelper.GetRangeDescription(selection, selectedDate);
                filters.Add($"Tarih: {description}");
            }

            string? GetComboValue(ComboBox combo)
            {
                if (combo.SelectedItem is ComboBoxItem item)
                {
                    string? value = item.Content?.ToString();
                    if (!string.IsNullOrWhiteSpace(value) &&
                        !string.Equals(value, "Hepsi", StringComparison.OrdinalIgnoreCase))
                        return value;
                }
                return null;
            }

            AddFilter("Ürün Tipi", GetComboValue(cmbUrunTipiFiltre));
            AddFilter("Ürün Türü", GetComboValue(cmbUrunTuruFiltre));
            AddFilter("Yüzey İşlemi", GetComboValue(cmbYuzeyIslemFiltre));
            AddFilter("Durum", GetComboValue(cmbDurumFiltre));

            return filters.Count > 0 ? string.Join(", ", filters) : "Filtre uygulanmadı";
        }

        private void BtnFiltreSifirla_Click(object sender, RoutedEventArgs e)
        {
            txtFiltreEPC.Text = "";
            txtFiltreBarkod.Text = "";
            txtFiltreBandil.Text = "";
            txtFiltrePlaka.Text = "";
            dateFiltreTarih.SelectedDate = null;
            cmbTarihSecimi.SelectedIndex = 0;

            cmbDurumFiltre.SelectedIndex = -1;
            cmbUrunTipiFiltre.SelectedIndex = -1;
            cmbUrunTuruFiltre.SelectedIndex = -1;
            cmbYuzeyIslemFiltre.SelectedIndex = -1;

            FiltreUygula();
        }

        private void cmbDurumFiltre_SelectionChanged(object sender, SelectionChangedEventArgs e) => FiltreUygula();

        private void dataGridStok_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dataGridStok.SelectedItem is UrunStok selected)
                selectedEpc = selected.EPC;
        }

        private async void btnUrunGuncelle_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridStok.SelectedItem is UrunStok selected)
            {
                var pencere = new UrunGuncelle(selected);
                if (pencere.ShowDialog() == true)
                {
                    await YukleStokVerisi();
                    FiltreUygula();
                }
            }
        }

        private async void btnYenile_Click(object sender, RoutedEventArgs e)
        {
            await YukleStokVerisi();
            FiltreUygula();
        }

        private void BtnStokRaporu_Click(object sender, RoutedEventArgs e)
        {
            var source = dataGridStok.ItemsSource as IEnumerable<UrunStok> ?? dataGridStok.Items.OfType<UrunStok>();
            var filteredItems = source.ToList();

            if (filteredItems.Count == 0)
            {
                MessageBox.Show("Raporlanacak stok bulunamadı.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string filterDescription = BuildFilterDescription();

            try
            {
                var window = new StokRaporWindow(filteredItems, filterDescription)
                {
                    Owner = Application.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Stok raporu hazırlanırken hata oluştu:\n" + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task<string> GetBagliRezervasyonNo(string epc)
        {
            var response = await App.SupabaseClient.From<UrunStok>().Where(x => x.EPC == epc).Get();
            return response.Models.FirstOrDefault()?.RezervasyonNo;
        }

        private async void btnRezervasyonBilgisi_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedEpc))
            {
                MessageBox.Show("Lütfen bir ürün seçin.");
                return;
            }

            try
            {
                var response = await App.SupabaseClient.From<UrunStok>().Where(x => x.EPC == selectedEpc).Get();
                var detay = response.Models.FirstOrDefault();

                if (detay != null)
                {
                    var pencere = new UrunRezervasyonBilgisi(detay.RezervasyonNo);
                    pencere.ShowDialog();
                }
                else
                {
                    MessageBox.Show("Bu ürün herhangi bir rezervasyona ait değil.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Rezervasyon bilgisi aranırken hata: " + ex.Message);
            }
        }

        private async void btnSil_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedEpc))
            {
                MessageBox.Show("Lütfen silmek için bir ürün seçin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string rezervasyonNo = await GetBagliRezervasyonNo(selectedEpc);

            if (rezervasyonNo != null)
            {
                var sonuc = MessageBox.Show(
                    $"Bu ürün, Rezervasyon No: {rezervasyonNo} numaralı bir rezervasyona dahil.\n\n" +
                    "Eğer silerseniz bu ürün hem stoktan hem de rezervasyondan kaldırılacak.\n" +
                    "Eğer bu rezervasyonun son ürünüyse, rezervasyonun kendisi de silinecek.\n\n" +
                    "Silmek istiyor musunuz?",
                    "Uyarı", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (sonuc != MessageBoxResult.Yes) return;

                try
                {
                    var detayResponse = await App.SupabaseClient.From<UrunStok>()
                        .Where(x => x.RezervasyonNo == rezervasyonNo).Get();

                    if (detayResponse.Models.Count == 1)
                        await App.SupabaseClient.From<UrunRezervasyon>()
                            .Where(x => x.RezervasyonNo == rezervasyonNo).Delete();

                    await App.SupabaseClient.From<UrunStok>().Where(x => x.EPC == selectedEpc).Delete();

                    MessageBox.Show("Ürün silindi. Rezervasyon durumu güncellendi.", "Bilgi",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    await YukleStokVerisi();
                    FiltreUygula();
                    selectedEpc = null;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Silme sırasında hata: " + ex.Message);
                }
            }
            else
            {
                var sonuc2 = MessageBox.Show("Seçilen ürünü silmek istiyor musunuz?", "Onay", MessageBoxButton.YesNo);
                if (sonuc2 != MessageBoxResult.Yes) return;

                try
                {
                    await App.SupabaseClient.From<UrunStok>().Where(x => x.EPC == selectedEpc).Delete();
                    MessageBox.Show("Ürün silindi.");
                    await YukleStokVerisi();
                    FiltreUygula();
                    selectedEpc = null;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Silme hatası: " + ex.Message);
                }
            }
        }
    }
}
