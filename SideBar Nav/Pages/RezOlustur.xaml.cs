using SideBar_Nav.Windows;
using Supabase.Interfaces;
using Supabase.Postgrest;
using Supabase.Realtime;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using static Supabase.Postgrest.Constants;
using SideBar_Nav.Services;
namespace SideBar_Nav.Pages
{
    public partial class RezOlustur : Page
    {
        private DataTable stokTable = new DataTable();
        private DataTable rezervasyonTable = new DataTable();
        private List<string> firmaListesi = new(); // alıcı firma listesi için
        private CancellationTokenSource? _barcodeCts;
        private bool _barcodeScanInProgress;

        public RezOlustur()
        {
            InitializeComponent();
            Loaded += RezOlustur_Loaded; // async yükleme
            Unloaded += RezOlustur_Unloaded;

            dateTarih.SelectedDate = DateTime.Today;
            dateFiltreTarih.SelectedDate = DateTime.Today;
            cmbTarihSecimi.SelectedIndex = 0;

            txtFiltreEPC.TextChanged += (s, e) => FiltreUygula();
            txtFiltreBarkod.TextChanged += (s, e) => FiltreUygula();
            txtFiltreBandil.TextChanged += (s, e) => FiltreUygula();
            dateFiltreTarih.SelectedDateChanged += (s, e) => FiltreUygula();
            cmbDurumFiltre.SelectionChanged += (s, e) => FiltreUygula();
            cmbAliciFirma.KeyUp += CmbAliciFirma_KeyUp;
            cmbAliciFirma.DropDownOpened += CmbAliciFirma_DropDownOpened;
            txtRezervasyonSorumlusu.Text = GetAktifKullaniciAdi();        

            // ViewModel’de DisplayName değişirse otomatik güncelle
            if (Application.Current?.MainWindow is MainWindow mw)
            {
                mw.Vm.PropertyChanged += VmOnPropertyChanged;
                Unloaded += (_, __) => mw.Vm.PropertyChanged -= VmOnPropertyChanged; // sızıntıyı önle
            }
        }

        private async void RezOlustur_Loaded(object sender, RoutedEventArgs e)
        {
            BarcodeScannerBridge.ConnectionStateChanged -= BarcodeScannerBridge_ConnectionStateChanged;
            BarcodeScannerBridge.ConnectionStateChanged += BarcodeScannerBridge_ConnectionStateChanged;
            UpdateBarcodeStatusRez(BarcodeScannerBridge.IsConnected);

            await LoadUrunStok();
            FiltreUygula(); // filtre otomatik uygulansın
        }

        private void RezOlustur_Unloaded(object sender, RoutedEventArgs e)
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
            Dispatcher.InvokeAsync(() => UpdateBarcodeStatusRez(connected));
        }

        private void UpdateBarcodeStatusRez(bool connected)
        {
            if (txtBarcodeStatusRez == null)
                return;

            txtBarcodeStatusRez.Text = connected ? "Barkod okuyucu: Bağlı" : "Barkod okuyucu: Kapalı";
            txtBarcodeStatusRez.Foreground = connected ? new SolidColorBrush(Colors.DarkGreen) : new SolidColorBrush(Colors.DarkRed);
        }

        private async void BtnFiltreBarkodOkuRez_Click(object sender, RoutedEventArgs e)
        {
            if (!BarcodeScannerBridge.IsConnected)
            {
                MessageBox.Show("Barkod okuyucu bağlı değil.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_barcodeScanInProgress)
                return;

            _barcodeScanInProgress = true;
            btnFiltreBarkodOkuRez.IsEnabled = false;

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
                btnFiltreBarkodOkuRez.IsEnabled = true;
                _barcodeScanInProgress = false;
            }
        }

        private async Task LoadUrunStok()
        {
            try
            {
                var result = await App.SupabaseClient
                    .From<Models.UrunStok>()
                    .Get();

                var list = result.Models;
                stokTable = new DataTable();
                stokTable.Columns.Add("EPC");
                stokTable.Columns.Add("BarkodNo");
                stokTable.Columns.Add("BandilNo");
                stokTable.Columns.Add("PlakaNo");
                stokTable.Columns.Add("UrunTipi");
                stokTable.Columns.Add("UrunTuru");
                stokTable.Columns.Add("YuzeyIslemi");
                stokTable.Columns.Add("Seleksiyon");
                stokTable.Columns.Add("UretimTarihi");
                stokTable.Columns.Add("Kalinlik");
                stokTable.Columns.Add("PlakaAdedi");
                stokTable.Columns.Add("StokEn");
                stokTable.Columns.Add("StokBoy");
                stokTable.Columns.Add("StokAlan");
                stokTable.Columns.Add("StokTonaj");
                stokTable.Columns.Add("Durum");
                stokTable.Columns.Add("RezervasyonNo");
                stokTable.Columns.Add("KaydedenPersonel");
                stokTable.Columns.Add("SatisEn");
                stokTable.Columns.Add("SatisBoy");
                stokTable.Columns.Add("SatisAlan");
                stokTable.Columns.Add("SatisTonaj");
                stokTable.Columns.Add("AliciFirma");

                foreach (var item in result.Models)
                {
                    stokTable.Rows.Add(
                        item.EPC,
                        item.BarkodNo,
                        item.BandilNo,
                        item.PlakaNo,
                        item.UrunTipi,
                        item.UrunTuru,
                        item.YuzeyIslemi,
                        item.Seleksiyon,
                        item.UretimTarihi?.ToString("yyyy-MM-dd HH:mm:ss"),
                        item.Kalinlik,
                        item.PlakaAdedi,
                        item.StokEn,
                        item.StokBoy,
                        item.StokAlan,
                        item.StokTonaj,
                        item.Durum,
                        item.RezervasyonNo,
                        item.KaydedenPersonel,
                        item.SatisEn,
                        item.SatisBoy,
                        item.SatisAlan,
                        item.SatisTonaj,
                        item.AliciFirma
                    );
                }

                dataGrid1.ItemsSource = stokTable.DefaultView;

            }
            catch (Exception ex)
            {
                MessageBox.Show("Stok yüklenirken hata oluştu:\n" + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CmbAliciFirma_DropDownOpened(object sender, EventArgs e)
        {
            try
            {
                var result = await App.SupabaseClient
                    .From<Models.AliciFirmalar>()
                    .Get();

                firmaListesi.Clear();
                foreach (var firma in result.Models)
                    firmaListesi.Add(firma.FirmaAdi);

                cmbAliciFirma.ItemsSource = null;
                cmbAliciFirma.ItemsSource = firmaListesi;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Firma listesi yüklenirken hata:\n" + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private async void CmbAliciFirma_KeyUp(object sender, KeyEventArgs e)
        {
            string girilen = cmbAliciFirma.Text.Trim();
            await FirmaOneriGetir(girilen);
        }

        private void BtnTemizle_Click(object sender, RoutedEventArgs e)
        {
            dateTarih.Text = string.Empty;
            txtRezervasyonKodu.Text = string.Empty;
            txtRezervasyonNo.Text = string.Empty;
            cmbAliciFirma.Text = string.Empty;
            // txtSatisSorumlusu.Text = string.Empty;
        }
        private void BtnFiltreSifirla_Click(object sender, RoutedEventArgs e)
        {
            txtFiltreEPC.Text = "";
            txtFiltreBarkod.Text = "";
            txtFiltreBandil.Text = "";
            dateFiltreTarih.SelectedDate = null;
            cmbTarihSecimi.SelectedIndex = 0;
            cmbDurumFiltre.SelectedIndex = -1;

            FiltreUygula();
        }

        private void cmbTarihSecimi_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FiltreUygula();
        }

        private async Task FirmaOneriGetir(string girilen)
        {
            if (string.IsNullOrWhiteSpace(girilen)) return;

            try
            {
                var result = await App.SupabaseClient
                    .From<Models.AliciFirmalar>()
                    .Filter("FirmaAdi", Supabase.Postgrest.Constants.Operator.ILike, $"%{girilen}%")
                    .Get();

                firmaListesi.Clear();
                foreach (var firma in result.Models)
                    firmaListesi.Add(firma.FirmaAdi);

                cmbAliciFirma.ItemsSource = firmaListesi;
                cmbAliciFirma.IsDropDownOpen = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Firma önerileri alınırken hata:\n" + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnFirmaSec_Click(object sender, RoutedEventArgs e)
        {
            var pencere = new AliciFirmaYonetimiWindow();
            pencere.Owner = Application.Current.MainWindow;
            pencere.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            if (pencere.ShowDialog() == true)
            {
                cmbAliciFirma.Text = pencere.SecilenFirmaAdi;
            }
        }


        private async void FiltreUygula()
        {
            try
            {
                string epcFilter = txtFiltreEPC.Text.Trim();
                string barkodFilter = txtFiltreBarkod.Text.Trim();
                string bandilFilter = txtFiltreBandil.Text.Trim();
                DateTime? tarih = dateFiltreTarih.SelectedDate;
                string tarihSecimi = (cmbTarihSecimi.SelectedItem as ComboBoxItem)?.Content?.ToString();
                string secilenDurum = (cmbDurumFiltre.SelectedItem as ComboBoxItem)?.Content?.ToString();

                var query = App.SupabaseClient.From<Models.UrunStok>();

                if (tarih.HasValue)
                {
                    var (utcStart, utcEnd) = DateRangeHelper.GetUtcRange(tarihSecimi, tarih.Value);

                    query = (ISupabaseTable<Models.UrunStok, RealtimeChannel>)query
                        .Filter("UretimTarihi", Operator.GreaterThanOrEqual, utcStart.ToString("yyyy-MM-ddTHH:mm:ss"))
                        .Filter("UretimTarihi", Operator.LessThan, utcEnd.ToString("yyyy-MM-ddTHH:mm:ss"));
                }


                if (!string.IsNullOrEmpty(secilenDurum) && secilenDurum != "Hepsi")
                    query = (ISupabaseTable<Models.UrunStok, RealtimeChannel>)query.Filter("Durum", Operator.Equals, secilenDurum);

                if (!string.IsNullOrEmpty(epcFilter))
                    query = (ISupabaseTable<Models.UrunStok, RealtimeChannel>)query.Filter("EPC", Operator.ILike, $"%{epcFilter}%");

                if (!string.IsNullOrEmpty(barkodFilter))
                    query = (ISupabaseTable<Models.UrunStok, RealtimeChannel>)query.Filter("BarkodNo", Operator.ILike, $"%{barkodFilter}%");

                if (!string.IsNullOrEmpty(bandilFilter))
                    query = (ISupabaseTable<Models.UrunStok, RealtimeChannel>)query.Filter("BandilNo", Operator.ILike, $"%{bandilFilter}%");

                var result = await query.Get();
                stokTable = new DataTable();
                stokTable.Columns.Add("EPC");
                stokTable.Columns.Add("BarkodNo");
                stokTable.Columns.Add("BandilNo");
                stokTable.Columns.Add("PlakaNo");
                stokTable.Columns.Add("UrunTipi");
                stokTable.Columns.Add("UrunTuru");
                stokTable.Columns.Add("YuzeyIslemi");
                stokTable.Columns.Add("Seleksiyon");
                stokTable.Columns.Add("UretimTarihi");
                stokTable.Columns.Add("Kalinlik");
                stokTable.Columns.Add("PlakaAdedi");
                stokTable.Columns.Add("StokEn");
                stokTable.Columns.Add("StokBoy");
                stokTable.Columns.Add("StokAlan");
                stokTable.Columns.Add("StokTonaj");
                stokTable.Columns.Add("Durum");
                stokTable.Columns.Add("RezervasyonNo");
                stokTable.Columns.Add("KaydedenPersonel");
                stokTable.Columns.Add("SatisEn");
                stokTable.Columns.Add("SatisBoy");
                stokTable.Columns.Add("SatisAlan");
                stokTable.Columns.Add("SatisTonaj");
                stokTable.Columns.Add("AliciFirma");

                foreach (var item in result.Models)
                {
                    stokTable.Rows.Add(
                        item.EPC,
                        item.BarkodNo,
                        item.BandilNo,
                        item.PlakaNo,
                        item.UrunTipi,
                        item.UrunTuru,
                        item.YuzeyIslemi,
                        item.Seleksiyon,
                        item.UretimTarihi?.ToString("yyyy-MM-dd HH:mm:ss"),
                        item.Kalinlik,
                        item.PlakaAdedi,
                        item.StokEn,
                        item.StokBoy,
                        item.StokAlan,
                        item.StokTonaj,
                        item.Durum,
                        item.RezervasyonNo,
                        item.KaydedenPersonel,
                        item.SatisEn,
                        item.SatisBoy,
                        item.SatisAlan,
                        item.SatisTonaj,
                        item.AliciFirma
                    );
                }

                dataGrid1.ItemsSource = stokTable.DefaultView;

            }
            catch (Exception ex)
            {
                MessageBox.Show("Filtreleme sırasında hata oluştu:\n" + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



        private void cmbDurumFiltre_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FiltreUygula();
        }

        private void BtnRezervasyonaEkle_Click(object sender, RoutedEventArgs e)
        {
            if (dataGrid1.SelectedItem is not DataRowView selectedRow)
                return;

            string durum = selectedRow["Durum"].ToString();
            string epc = selectedRow["EPC"].ToString();

            if (durum == "Onay Bekliyor" || durum == "Onaylandı" || durum == "Sevkiyat Tamamlandı")
            {
                MessageBox.Show($"Bu ürün şu anda '{durum}' durumundadır ve rezervasyona eklenemez.",
                                "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Tablo ilk kez oluşturuluyorsa, stok şemasını kopyala ve satış sütunlarını ekle
            if (rezervasyonTable.Columns.Count == 0)
            {
                rezervasyonTable = stokTable.Clone();

                if (!rezervasyonTable.Columns.Contains("SatisEn"))
                    rezervasyonTable.Columns.Add("SatisEn", typeof(string));
                if (!rezervasyonTable.Columns.Contains("SatisBoy"))
                    rezervasyonTable.Columns.Add("SatisBoy", typeof(string));
                if (!rezervasyonTable.Columns.Contains("SatisAlan"))
                    rezervasyonTable.Columns.Add("SatisAlan", typeof(string));
                if (!rezervasyonTable.Columns.Contains("SatisTonaj"))
                    rezervasyonTable.Columns.Add("SatisTonaj", typeof(string));
            }

            // Aynı EPC'yi tekrar ekleme
            foreach (DataRow row in rezervasyonTable.Rows)
            {
                if (row["EPC"].ToString() == epc)
                {
                    MessageBox.Show("Bu ürün zaten rezervasyona eklenmiş.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            // Eklemeye uygunsa, satırı kopyala ve ekle
            rezervasyonTable.ImportRow(selectedRow.Row);
            dataGrid2.ItemsSource = rezervasyonTable.DefaultView;
        }
        private void BtnRezervasyondanCikar_Click(object sender, RoutedEventArgs e)
        {
            if (dataGrid2.SelectedItem is DataRowView selectedRow)
            {
                var info = $"EPC: {selectedRow["EPC"]}";
                rezervasyonTable.Rows.Remove(selectedRow.Row);
                dataGrid2.ItemsSource = rezervasyonTable.DefaultView;
                ActivityLogger.Log(GetAktifKullaniciAdi(), "Rezervasyondan çıkar", info);
            }
            else
            {
                MessageBox.Show("Lütfen çıkarmak istediğiniz ürünü seçin.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }



        private void BtnBoyutGuncelle_Click(object sender, RoutedEventArgs e)
        {
            if (dataGrid2.SelectedItem is DataRowView selectedRow)
            {
                var pencere = new BoyutGuncelleWindow(selectedRow);
                pencere.Owner = Application.Current.MainWindow;
                pencere.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                bool? sonuc = pencere.ShowDialog();

                if (sonuc == true)
                {
                    selectedRow["SatisEn"] = pencere.SatisEn;
                    selectedRow["SatisBoy"] = pencere.SatisBoy;
                    selectedRow["SatisAlan"] = pencere.SatisAlan;
                    selectedRow["SatisTonaj"] = pencere.SatisTonaj;
                    ActivityLogger.Log(GetAktifKullaniciAdi(), "Rezervasyonda boyut güncelle", $"EPC: {selectedRow["EPC"]}, En: {pencere.SatisEn}, Boy: {pencere.SatisBoy}");
                }
            }
        }



        private async void BtnOlustur_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtRezervasyonNo.Text) ||
                string.IsNullOrWhiteSpace(txtRezervasyonKodu.Text) ||
                string.IsNullOrWhiteSpace(cmbAliciFirma.Text) ||
                !dateTarih.SelectedDate.HasValue)
            {
                MessageBox.Show("Lütfen tüm rezervasyon bilgilerini doldurun (tarih, no, kod, firma, sorumlu).",
                                "Eksik Bilgi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (rezervasyonTable.Rows.Count == 0)
            {
                MessageBox.Show("Rezervasyona ürün eklenmemiş.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string rezervasyonNo = txtRezervasyonNo.Text.Trim();

            // 1️⃣ EPC çakışma kontrolü
            foreach (DataRow row in rezervasyonTable.Rows)
            {
                string epc = row["EPC"].ToString();

                var stokKontrol = await App.SupabaseClient
                    .From<Models.UrunStok>()
                    .Filter("EPC", Operator.Equals, epc)
                    .Filter("RezervasyonNo", Operator.NotEqual, "")
                    .Get();

                if (stokKontrol.Models.Count > 0)
                {
                    MessageBox.Show($"Seçilen ürünlerden biri (EPC: {epc}) zaten başka bir rezervasyona atanmış.",
                                    "Çakışan Ürün", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            // 2️⃣ Rezervasyon no tekrar kontrolü
            var rezervasyonKontrol = await App.SupabaseClient
                .From<Models.UrunRezervasyon>()
                .Filter("RezervasyonNo", Operator.Equals, rezervasyonNo)
                .Get();

            if (rezervasyonKontrol.Models.Count > 0)
            {
                MessageBox.Show("Bu rezervasyon numarası zaten kullanılmış. Lütfen farklı bir numara girin.",
                                "Tekrarlanan Rezervasyon No", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 3️⃣ UrunRezervasyon kaydı
            var yeniRezervasyon = new Models.UrunRezervasyon
            {
                RezervasyonNo = rezervasyonNo,
                RezervasyonKodu = txtRezervasyonKodu.Text.Trim(),
                AliciFirma = cmbAliciFirma.Text.Trim(),
                RezervasyonSorumlusu = txtRezervasyonSorumlusu.Text.Trim(),
                IslemTarihi = dateTarih.SelectedDate.HasValue
    ? dateTarih.SelectedDate.Value.Date.Add(DateTime.Now.TimeOfDay)
    : DateTime.Now,
                Durum = "Onay Bekliyor"
            };

            await App.SupabaseClient
                .From<Models.UrunRezervasyon>()
                .Insert(yeniRezervasyon);

            // 4️⃣ Stok güncelle
            foreach (DataRow row in rezervasyonTable.Rows)
            {
                string epc = row["EPC"].ToString();

                var stokKaydi = await App.SupabaseClient
                    .From<Models.UrunStok>()
                    .Filter("EPC", Operator.Equals, epc)
                    .Get();

                if (stokKaydi.Models.Count > 0)
                {
                    var mevcutStok = stokKaydi.Models[0];

                    mevcutStok.SatisEn = row["SatisEn"] != DBNull.Value ? Convert.ToDecimal(row["SatisEn"]) : 0;
                    mevcutStok.SatisBoy = row["SatisBoy"] != DBNull.Value ? Convert.ToDecimal(row["SatisBoy"]) : 0;
                    mevcutStok.SatisAlan = row["SatisAlan"] != DBNull.Value ? Convert.ToDecimal(row["SatisAlan"]) : 0;
                    mevcutStok.SatisTonaj = row["SatisTonaj"] != DBNull.Value ? Convert.ToDecimal(row["SatisTonaj"]) : 0;
                    if(mevcutStok.SatisEn == 0 && mevcutStok.SatisBoy==0 && mevcutStok.SatisAlan == 0 && mevcutStok.SatisTonaj == 0)
                    {
                        mevcutStok.SatisEn = mevcutStok.StokEn;
                        mevcutStok.SatisBoy = mevcutStok.StokBoy;
                        mevcutStok.SatisAlan = mevcutStok.StokAlan;
                        mevcutStok.SatisTonaj = mevcutStok.StokTonaj;
                    }
                    mevcutStok.RezervasyonNo = rezervasyonNo;
                    mevcutStok.Durum = "Onay Bekliyor";
                    mevcutStok.AliciFirma = cmbAliciFirma.Text.Trim();

                    await App.SupabaseClient
                        .From<Models.UrunStok>()
                        .Update(mevcutStok);
                }
            }


            MessageBox.Show("Rezervasyon oluşturuldu.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            ActivityLogger.Log(GetAktifKullaniciAdi(), "Rezervasyon oluştur", $"Rez No: {rezervasyonNo}, Ürün: {rezervasyonTable.Rows.Count}");
            rezervasyonTable.Clear();
            await LoadUrunStok(); // LoadUrunStok metodu async olmalı
        }

        // sorumlunun ismini aktif kullanıcıdan otomatik olarak çekmek için

        private void VmOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainWindowViewModel.DisplayName))
            {
                Dispatcher.Invoke(() =>
                {
                    txtRezervasyonSorumlusu.Text = GetAktifKullaniciAdi();
                });
            }
        }
        private string GetAktifKullaniciAdi()
        {
            var mw = Application.Current?.MainWindow as MainWindow;

            // 1) ViewModel.DisplayName (Ad varsa onu üretmiş oluyor)
            var disp = mw?.Vm?.DisplayName;
            if (!string.IsNullOrWhiteSpace(disp) && disp != "—")
                return disp;

            // 2) Me.Ad
            var ad = mw?.Vm?.Me?.Ad;
            if (!string.IsNullOrWhiteSpace(ad))
                return ad;

            // 3) Supabase oturumundan email fallback
            var email = App.AktifKullanici?.Email;
            if (!string.IsNullOrWhiteSpace(email))
                return email;

            return "—";
        }




    }
}
