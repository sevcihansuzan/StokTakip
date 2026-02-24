using SideBar_Nav.Windows;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Supabase.Postgrest;
using Supabase.Interfaces;
using Supabase.Realtime;
using static Supabase.Postgrest.Constants;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Linq;
using SideBar_Nav.Services;
using SideBar_Nav.Models;

namespace SideBar_Nav.Pages
{
    public partial class SatisYonetimi : Page
    {
        private DataTable rezervasyonTable = new DataTable();
        private List<UrunRezervasyon> filteredRezervasyonlar = new();
        private string selectedRezervasyonNo;
        private List<string> firmaListesi = new();
        public SatisYonetimi()
        {
            InitializeComponent();
            Loaded += SatisYonetimi_Loaded; // Sürekli olarak yenilenmesi için
            dateFiltreTarih.SelectedDate = DateTime.Today;
            cmbTarihSecimi.SelectedIndex = 0;
            txtFiltreNo.TextChanged += async (s, e) => await FiltreUygula();
            txtFiltreKodu.TextChanged += async (s, e) => await FiltreUygula();
            txtFiltreFirma.KeyUp += CmbAliciFirma_KeyUp;
            txtFiltreFirma.DropDownOpened += TxtFiltreFirma_DropDownOpened;
            txtFiltreFirma.SelectionChanged += async (s, e) =>
            {
                if (txtFiltreFirma.SelectedItem != null)
                    txtFiltreFirma.Text = txtFiltreFirma.SelectedItem.ToString();

                await FiltreUygula();
            };
            txtFiltreRezSorumlu.TextChanged += async (s, e) => await FiltreUygula();
            txtFiltreSorumlu.TextChanged += async (s, e) => await FiltreUygula();
            dateFiltreTarih.SelectedDateChanged += async (s, e) => await FiltreUygula();
            cmbDurumFiltre.SelectionChanged += async (s, e) => await FiltreUygula();

            txtFiltreEPC.TextChanged += async (s, e) => await FiltreUygula();
            _ = FiltreUygula();
        }

        private async void cmbTarihSecimi_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await FiltreUygula();
        }
        private async void SatisYonetimi_Loaded(object sender, RoutedEventArgs e)
        {
            await FiltreUygula();
        }

        private async void CmbAliciFirma_KeyUp(object sender, KeyEventArgs e)
        {
            string girilen = txtFiltreFirma.Text.Trim();
            await FirmaOneriGetir(girilen);
        }
        private async void TxtFiltreFirma_DropDownOpened(object sender, EventArgs e)
        {
            try
            {
                var result = await App.SupabaseClient
                    .From<Models.AliciFirmalar>()
                    .Get();

                firmaListesi.Clear();
                foreach (var firma in result.Models)
                    firmaListesi.Add(firma.FirmaAdi);

                txtFiltreFirma.ItemsSource = null;
                txtFiltreFirma.ItemsSource = firmaListesi;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Firma listesi yüklenirken hata:\n" + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task FirmaOneriGetir(string girilen)
        {
            if (string.IsNullOrWhiteSpace(girilen)) return;

            try
            {
                var result = await App.SupabaseClient
                    .From<Models.AliciFirmalar>()
                    .Filter("FirmaAdi", Operator.ILike, $"%{girilen}%")
                    .Get();

                firmaListesi.Clear();
                foreach (var firma in result.Models)
                    firmaListesi.Add(firma.FirmaAdi);

                txtFiltreFirma.ItemsSource = firmaListesi;
                txtFiltreFirma.IsDropDownOpen = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Firma önerileri yüklenirken hata:\n" + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async Task FiltreUygula()
        {
            string no = txtFiltreNo.Text.Trim().ToLower();
            string kod = txtFiltreKodu.Text.Trim().ToLower();
            string firma = txtFiltreFirma.Text.Trim().ToLower();
            string rezSorumlu = txtFiltreRezSorumlu.Text.Trim().ToLower();
            string sorumlu = txtFiltreSorumlu.Text.Trim().ToLower();
            string epc = txtFiltreEPC.Text.Trim().ToLower();
            DateTime? secilenTarih = dateFiltreTarih.SelectedDate;
            string tarihSecimi = (cmbTarihSecimi.SelectedItem as ComboBoxItem)?.Content?.ToString();
            string durum = (cmbDurumFiltre.SelectedItem as ComboBoxItem)?.Content?.ToString();

            try
            {
                var query = App.SupabaseClient
                    .From<Models.UrunRezervasyon>()
                    .Select("*");

                if (!string.IsNullOrEmpty(no))
                    query = query.Filter("RezervasyonNo", Operator.ILike, $"%{no}%");

                if (!string.IsNullOrEmpty(kod))
                    query = query.Filter("RezervasyonKodu", Operator.ILike, $"%{kod}%");

                if (!string.IsNullOrEmpty(firma))
                    query = query.Filter("AliciFirma", Operator.ILike, $"%{firma}%");

                if (!string.IsNullOrEmpty(rezSorumlu))
                    query = query.Filter("RezervasyonSorumlusu", Operator.ILike, $"%{rezSorumlu}%");

                if (!string.IsNullOrEmpty(sorumlu))
                    query = query.Filter("SatisSorumlusu", Operator.ILike, $"%{sorumlu}%");

                if (!string.IsNullOrEmpty(durum) && durum != "Hepsi")
                    query = query.Filter("Durum", Operator.Equals, durum);

                if (secilenTarih.HasValue)
                {
                    var (utcStart, utcEnd) = DateRangeHelper.GetUtcRange(tarihSecimi, secilenTarih.Value);

                    query = query
                        .Filter("IslemTarihi", Operator.GreaterThanOrEqual, utcStart.ToString("yyyy-MM-ddTHH:mm:ss"))
                        .Filter("IslemTarihi", Operator.LessThan, utcEnd.ToString("yyyy-MM-ddTHH:mm:ss"));
                }

                if (!string.IsNullOrEmpty(epc))
                {
                    var epcResult = await App.SupabaseClient
                        .From<Models.UrunStok>()
                        .Select("RezervasyonNo")
                        .Filter("EPC", Operator.ILike, $"%{epc}%")
                        .Get();

                    var rezNoList = epcResult.Models.Select(m => m.RezervasyonNo).ToList();
                    if (rezNoList.Any())
                        query = query.Filter("RezervasyonNo", Operator.In, rezNoList);
                    else
                        query = query.Filter("RezervasyonNo", Operator.Equals, "NO_MATCH");
                }

                var result = await query.Get();
                filteredRezervasyonlar = result.Models.ToList();

                rezervasyonTable = new DataTable();
                rezervasyonTable.Columns.Add("RezervasyonNo");
                rezervasyonTable.Columns.Add("RezervasyonKodu");
                rezervasyonTable.Columns.Add("AliciFirma");
                rezervasyonTable.Columns.Add("RezervasyonSorumlusu");
                rezervasyonTable.Columns.Add("SatisSorumlusu");
                rezervasyonTable.Columns.Add("IslemTarihi");
                rezervasyonTable.Columns.Add("Durum");
                rezervasyonTable.Columns.Add("UrunCikisTarihi");
                rezervasyonTable.Columns.Add("SevkiyatAdresi");

                foreach (var item in filteredRezervasyonlar)
                {
                    rezervasyonTable.Rows.Add(
                        item.RezervasyonNo,
                        item.RezervasyonKodu,
                        item.AliciFirma,
                        item.RezervasyonSorumlusu,
                        item.SatisSorumlusu,
                        item.IslemTarihi?.ToString("yyyy-MM-dd HH:mm:ss"),
                        item.Durum,
                        item.UrunCikisTarihi,
                        item.SevkiyatAdresi
                    );
                }

                dataGridRezervasyonlar.ItemsSource = rezervasyonTable.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Filtreleme sırasında hata oluştu:\n" + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string? BuildFilterDescription()
        {
            var filters = new List<string>();

            void AddFilter(string label, string? value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    filters.Add($"{label}: {value.Trim()}");
            }

            AddFilter("Rezervasyon No", txtFiltreNo.Text);
            AddFilter("Rezervasyon Kodu", txtFiltreKodu.Text);
            AddFilter("Alıcı Firma", txtFiltreFirma.Text);
            AddFilter("Rezervasyon Sorumlusu", txtFiltreRezSorumlu.Text);
            AddFilter("Satış Sorumlusu", txtFiltreSorumlu.Text);
            AddFilter("EPC", txtFiltreEPC.Text);

            if (dateFiltreTarih.SelectedDate is DateTime selectedDate)
            {
                string? selection = (cmbTarihSecimi.SelectedItem as ComboBoxItem)?.Content?.ToString();
                string description = DateRangeHelper.GetRangeDescription(selection, selectedDate);
                filters.Add($"Tarih: {description}");
            }

            if (cmbDurumFiltre.SelectedItem is ComboBoxItem durumItem)
            {
                string? durumValue = durumItem.Content?.ToString();
                if (!string.IsNullOrWhiteSpace(durumValue) && !string.Equals(durumValue, "Hepsi", StringComparison.OrdinalIgnoreCase))
                    filters.Add($"Durum: {durumValue}");
            }

            return filters.Count > 0 ? string.Join(", ", filters) : null;
        }


        private async void BtnFiltreSifirla_Click(object sender, RoutedEventArgs e)
        {
            txtFiltreNo.Text = "";
            txtFiltreKodu.Text = "";
            txtFiltreFirma.Text = "";
            dateFiltreTarih.SelectedDate = null;
            cmbTarihSecimi.SelectedIndex = 0;
            cmbDurumFiltre.SelectedIndex = -1;

            await FiltreUygula();
        }

        private async void BtnPdfRapor_Click(object sender, RoutedEventArgs e)
        {
            if (cmbTarihSecimi.SelectedItem == null || !dateFiltreTarih.SelectedDate.HasValue)
            {
                MessageBox.Show("Lütfen rapor oluşturmak için tarih ve periyot seçiniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await FiltreUygula();

            if (filteredRezervasyonlar.Count == 0)
            {
                MessageBox.Show("Seçilen periyotta listelenecek rezervasyon bulunamadı.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string selection = (cmbTarihSecimi.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Günlük";
            DateTime selectedDate = dateFiltreTarih.SelectedDate!.Value;

            try
            {
                var rezervasyonNolar = filteredRezervasyonlar
                    .Select(r => r.RezervasyonNo)
                    .Where(no => !string.IsNullOrWhiteSpace(no))
                    .Distinct()
                    .ToList();

                var productLookup = new Dictionary<string, List<UrunStok>>();

                if (rezervasyonNolar.Count > 0)
                {
                    var productResult = await App.SupabaseClient
                        .From<UrunStok>()
                        .Filter("RezervasyonNo", Operator.In, rezervasyonNolar)
                        .Get();

                    productLookup = productResult.Models
                        .GroupBy(p => p.RezervasyonNo)
                        .ToDictionary(g => g.Key, g => g.ToList());
                }

                string periodDescription = DateRangeHelper.GetRangeDescription(selection, selectedDate);
                string? filterDescription = BuildFilterDescription();

                var window = new SatisRaporWindow(
                    filteredRezervasyonlar.ToList(),
                    productLookup,
                    selection,
                    periodDescription,
                    filterDescription)
                {
                    Owner = Application.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };

                window.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"PDF oluşturma sırasında hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void dataGridRezervasyonlar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dataGridRezervasyonlar.SelectedItem is DataRowView selectedRow)
            {
                selectedRezervasyonNo = selectedRow["RezervasyonNo"].ToString();
                await YukleDetaylar();
            }
        }

        private async Task YukleDetaylar()
        {
            if (string.IsNullOrEmpty(selectedRezervasyonNo))
                return;

            try
            {
                var result = await App.SupabaseClient
                    .From<Models.UrunStok>()
                    .Filter("RezervasyonNo", Operator.Equals, selectedRezervasyonNo)
                    .Get();

                var detayTable = new DataTable();
                detayTable.Columns.Add("RezervasyonNo");
                detayTable.Columns.Add("EPC");
                detayTable.Columns.Add("BarkodNo");
                detayTable.Columns.Add("BandilNo");
                detayTable.Columns.Add("PlakaNo");
                detayTable.Columns.Add("UrunTipi");
                detayTable.Columns.Add("UrunTuru");
                detayTable.Columns.Add("YuzeyIslemi");
                detayTable.Columns.Add("Seleksiyon");
                detayTable.Columns.Add("UretimTarihi");
                detayTable.Columns.Add("Kalinlik");
                detayTable.Columns.Add("StokEn");
                detayTable.Columns.Add("StokBoy");
                detayTable.Columns.Add("PlakaAdedi");
                detayTable.Columns.Add("StokAlan");
                detayTable.Columns.Add("StokTonaj");
                detayTable.Columns.Add("SatisEn");
                detayTable.Columns.Add("SatisBoy");
                detayTable.Columns.Add("SatisAlan");
                detayTable.Columns.Add("SatisTonaj");

                detayTable.Columns.Add("Durum");
                detayTable.Columns.Add("AliciFirma");

                foreach (var item in result.Models)
                {
                    detayTable.Rows.Add(
                        item.RezervasyonNo,
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
                        item.StokEn,
                        item.StokBoy,
                        item.PlakaAdedi,
                        item.StokAlan,
                        item.StokTonaj,
                        item.SatisEn,
                        item.SatisBoy,
                        item.SatisAlan,
                        item.SatisTonaj,
                        item.Durum,
                        item.AliciFirma
                    );
                }

                dataGridDetaylar.ItemsSource = detayTable.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Detaylar yüklenirken hata oluştu:\n" + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async void BtnOnayla_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedRezervasyonNo))
            {
                MessageBox.Show("Lütfen bir rezervasyon seçin.");
                return;
            }

            if (dataGridRezervasyonlar.SelectedItem is DataRowView selectedRow)
            {
                string mevcutDurum = selectedRow["Durum"]?.ToString();
                if (mevcutDurum == "Onaylandı")
                {
                    MessageBox.Show($"Bu rezervasyon zaten '{mevcutDurum}' durumunda.", "Uyarı",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            try
            {
                var aktif = GetAktifKullaniciAdi();

                // 1) Ana rezervasyonu güncelle: DURUM + SATIŞ SORUMLUSU
                var updateRez = await App.SupabaseClient
                    .From<Models.UrunRezervasyon>()
                    .Where(x => x.RezervasyonNo == selectedRezervasyonNo)
                    .Set(x => x.Durum, "Onaylandı")
                    .Set(x => x.SatisSorumlusu, aktif)                 // ✅ aktif kullanıcı
                                                                       // .Set(x => x.RezervasyonSorumlusu, aktif)        // (opsiyonel) kolonu da güncellemek istersen
                    .Update();

                // 2) Stokları güncelle
                await App.SupabaseClient
                    .From<Models.UrunStok>()
                    .Where(x => x.RezervasyonNo == selectedRezervasyonNo)
                    .Set(x => x.Durum, "Onaylandı")
                    .Update();

                try
                {
                    var snapshot = await PackingListService.GetPackingListSnapshotAsync(selectedRezervasyonNo);
                    PackingListService.GeneratePackingListPdf(snapshot);
                    var excelPath = Path.Combine(PackingListService.DefaultDirectory, $"PackingList_{snapshot.Reservation?.RezervasyonNo}.xlsx");
                    ReportExcelService.GeneratePackingListReport(snapshot, outputPath: excelPath);
                }
                catch (Exception packEx)
                {
                    MessageBox.Show($"Packing list oluşturulurken hata oluştu:\n{packEx.Message}", "Packing List", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                // 3) Gridde anında göster (yeniden yükleme de yapacağız)
                if (dataGridRezervasyonlar.SelectedItem is DataRowView row)
                    row["SatisSorumlusu"] = aktif; // ✅

                MessageBox.Show("Rezervasyon onaylandı.");
                ActivityLogger.Log(GetAktifKullaniciAdi(), "Rezervasyon onayla", $"Rez No: {selectedRezervasyonNo}");
                await FiltreUygula();
                await YukleDetaylar();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Hata: " + ex.Message);
            }
        }

        private async void BtnOnayiGeriAl_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedRezervasyonNo))
            {
                MessageBox.Show("Lütfen bir rezervasyon seçin.");
                return;
            }

            DataRowView selectedRow = dataGridRezervasyonlar.SelectedItem as DataRowView;
            if (selectedRow != null)
            {
                string mevcutDurum = selectedRow["Durum"].ToString();

                if (mevcutDurum != "Onaylandı")
                {
                    MessageBox.Show($"Bu rezervasyon şu anda '{mevcutDurum}' durumunda. Yalnızca 'Onaylandı' durumundaki rezervasyonların onayı geri alınabilir.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            try
            {
                // 1. Ana rezervasyonu güncelle
                await App.SupabaseClient
                    .From<Models.UrunRezervasyon>()
                    .Where(x => x.RezervasyonNo == selectedRezervasyonNo)
                    .Set(x => x.Durum, "Onay Bekliyor")
                    .Update();

                // 2. Stokları güncelle
                await App.SupabaseClient
                    .From<Models.UrunStok>()
                    .Where(x => x.RezervasyonNo == selectedRezervasyonNo)
                    .Set(x => x.Durum, "Onay Bekliyor")
                    .Update();

                MessageBox.Show("Rezervasyonun onayı geri alındı. Durumu 'Onay Bekliyor' olarak güncellendi.");
                ActivityLogger.Log(GetAktifKullaniciAdi(), "Onayı geri al", $"Rez No: {selectedRezervasyonNo}");
                await FiltreUygula();
                await YukleDetaylar();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Hata: " + ex.Message);
            }
        }

        private async void BtnIptalEt_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedRezervasyonNo))
            {
                MessageBox.Show("Lütfen bir rezervasyon seçin.");
                return;
            }

            if (dataGridRezervasyonlar.SelectedItem is not DataRowView selectedRow)
                return;

            string mevcutDurum = selectedRow["Durum"].ToString();

            if (mevcutDurum == "İptal")
            {
                MessageBox.Show("Bu rezervasyon zaten iptal edilmiş.");
                return;
            }

            if (mevcutDurum == "Sevkiyat Tamamlandı")
            {
                MessageBox.Show("Sevkiyatı tamamlanmış bir rezervasyon iptal edilemez.");
                return;
            }

            // Check if any products are already shipped
            var detayResult = await App.SupabaseClient
                .From<Models.UrunStok>()
                .Select("EPC")
                .Filter("RezervasyonNo", Operator.Equals, selectedRezervasyonNo)
                .Filter("Durum", Operator.Equals, "Sevkiyat Tamamlandı")
                .Get();

            if (detayResult.Models.Count > 0)
            {
                MessageBox.Show("Bu rezervasyonda sevkiyatı tamamlanmış ürün(ler) bulunduğu için iptal edilemez.");
                return;
            }

            // İptal bilgilerini kullanıcıdan al
            IptalSebepWindow iptalPencere = new IptalSebepWindow();
            iptalPencere.Owner = Application.Current.MainWindow;
            iptalPencere.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            if (iptalPencere.ShowDialog() != true)
                return;

            string sebep = iptalPencere.IptalSebep;
            string personel = GetAktifKullaniciAdi();

            try
            {
                // Get reservation details
                var rezervasyon = await App.SupabaseClient
                    .From<Models.UrunRezervasyon>()
                    .Filter("RezervasyonNo", Operator.Equals, selectedRezervasyonNo)
                    .Single();

                // Create cancellation record
                var iptalKayit = new Models.RezIptal
                {
                    RezervasyonNo = rezervasyon.RezervasyonNo,
                    IslemTarihi = rezervasyon.IslemTarihi,
                    RezervasyonKodu = rezervasyon.RezervasyonKodu,
                    AliciFirma = rezervasyon.AliciFirma,
                    SatisSorumlusu = rezervasyon.SatisSorumlusu,
                    Durum = "İptal",
                    KaydedenPersonel = rezervasyon.KaydedenPersonel,
                    UrunCikisTarihi = rezervasyon.UrunCikisTarihi,
                    SevkiyatAdresi = rezervasyon.SevkiyatAdresi,
                    IptalSebebi = sebep,
                    IptalEdenPersonel = personel,
                    IptalTarihi = DateTime.UtcNow,
                    IptalEdenKullanici = App.AktifKullanici?.Email
                };

                await App.SupabaseClient
                    .From<Models.RezIptal>()
                    .Insert(iptalKayit);

                // Get and archive details
                var detaylar = await App.SupabaseClient
                    .From<Models.UrunStok>()
                    .Filter("RezervasyonNo", Operator.Equals, selectedRezervasyonNo)
                    .Get();

                foreach (var detay in detaylar.Models)
                {
                    var iptalDetay = new Models.RezIptalDetay
                    {
                        RezervasyonNo = detay.RezervasyonNo,
                        EPC = detay.EPC,
                        BarkodNo = detay.BarkodNo,
                        BandilNo = detay.BandilNo,
                        PlakaNo = detay.PlakaNo,
                        UrunTipi = detay.UrunTipi,
                        UrunTuru = detay.UrunTuru,
                        YuzeyIslemi = detay.YuzeyIslemi,
                        Seleksiyon = detay.Seleksiyon,
                        UretimTarihi = detay.UretimTarihi,
                        Kalinlik = detay.Kalinlik,
                        StokEn = detay.StokEn,
                        StokBoy = detay.StokBoy,
                        PlakaAdedi = detay.PlakaAdedi,
                        StokAlan = detay.StokAlan,
                        StokTonaj = detay.StokTonaj,
                        SatisEn = detay.SatisEn,
                        SatisBoy = detay.SatisBoy,
                        SatisAlan = detay.SatisAlan,
                        SatisTonaj = detay.SatisTonaj,
                        KaydedenPersonel = detay.KaydedenPersonel,
                        Durum = "İptal",
                        AliciFirma = detay.AliciFirma
                    };

                    await App.SupabaseClient
                        .From<Models.RezIptalDetay>()
                        .Insert(iptalDetay);

                    // Update stock status
                    var stokResult = await App.SupabaseClient
                        .From<Models.UrunStok>()
                        .Where(x => x.EPC == detay.EPC)
                        .Get();

                    var stok = stokResult.Models.FirstOrDefault();
                    if (stok != null)
                    {
                        stok.Durum = "Stokta";
                        stok.RezervasyonNo = null;
                        stok.AliciFirma = null;

                        // >>> Satış ölçülerini sıfırla
                        stok.SatisEn = 0m;
                        stok.SatisBoy = 0m;
                        stok.SatisAlan = 0m;
                        stok.SatisTonaj = 0m;
                        // <<<

                        await App.SupabaseClient.From<Models.UrunStok>().Update(stok);
                    }

                }

                // Delete reservation
                await App.SupabaseClient
                    .From<Models.UrunRezervasyon>()
                    .Filter("RezervasyonNo", Operator.Equals, selectedRezervasyonNo)
                    .Delete();

                MessageBox.Show("Rezervasyon başarıyla iptal edildi ve kayıtlar arşivlendi.");
                ActivityLogger.Log(GetAktifKullaniciAdi(), "Rezervasyon iptal", $"Rez No: {selectedRezervasyonNo}, Sebep: {sebep}");
                await FiltreUygula();
                await YukleDetaylar();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Hata: " + ex.Message);
            }
        }

        private async void BtnPackingList_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridRezervasyonlar.SelectedItem is not DataRowView selectedRow)
            {
                MessageBox.Show("Lütfen bir rezervasyon seçin.");
                return;
            }

            string durum = selectedRow["Durum"].ToString();
            if (durum != "Onaylandı" && durum != "Sevkiyat Tamamlandı")
            {
                MessageBox.Show("Packing List yalnızca 'Onaylandı' ve 'Sevkiyat Tamamlandı' durumundaki rezervasyonlar için görüntülenebilir.");
                return;
            }

            string rezervasyonNo = selectedRow["RezervasyonNo"].ToString();

            try
            {
                var rezervasyon = await App.SupabaseClient
                    .From<Models.UrunRezervasyon>()
                    .Filter("RezervasyonNo", Operator.Equals, rezervasyonNo)
                    .Single();

                var detaylar = await App.SupabaseClient
                    .From<Models.UrunStok>()
                    .Filter("RezervasyonNo", Operator.Equals, rezervasyonNo)
                    .Get();

                PackingListWindow window = new PackingListWindow(rezervasyon.RezervasyonNo);

                window.Owner = Application.Current.MainWindow;
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Packing list yüklenirken hata:\n" + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        /*
        private async void BtnRezervasyonSil_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedRezervasyonNo))
            {
                MessageBox.Show("Lütfen silinecek bir rezervasyon seçin.");
                return;
            }

            var sonuc = MessageBox.Show("Seçilen rezervasyon silinecek ve ürünler tekrar stokta görünecek. Emin misiniz?",
                                         "Onay", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (sonuc != MessageBoxResult.Yes)
                return;

            try
            {
                // 1. Önce stok kayıtlarını al
                var detayResult = await App.SupabaseClient
                    .From<Models.UrunStok>()
                    .Filter("RezervasyonNo", Operator.Equals, selectedRezervasyonNo)
                    .Get();

                // 2. Tüm EPC'lerin stok durumlarını güncelle
                foreach (var stok in detayResult.Models)
                {
                    stok.Durum = "Stokta";
                    stok.RezervasyonNo = null;
                    stok.AliciFirma = null;
                    await App.SupabaseClient.From<Models.UrunStok>().Update(stok);
                }

                // 3. Ana rezervasyon sil
                await App.SupabaseClient
                    .From<Models.UrunRezervasyon>()
                    .Filter("RezervasyonNo", Operator.Equals, selectedRezervasyonNo)
                    .Delete();

                MessageBox.Show("Rezervasyon başarıyla silindi ve ürünler tekrar stokta olarak işaretlendi.");
                ActivityLogger.Log(GetAktifKullaniciAdi(), "Rezervasyon sil", $"Rez No: {selectedRezervasyonNo}");
                selectedRezervasyonNo = null;
                await FiltreUygula();
                dataGridDetaylar.ItemsSource = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Hata: " + ex.Message);
            }
        }
        */
        private async void BtnUrunEkle_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedRezervasyonNo))
            {
                MessageBox.Show("Lütfen önce bir rezervasyon seçin.");
                return;
            }

            DataRowView selectedRow = dataGridRezervasyonlar.SelectedItem as DataRowView;
            if (selectedRow != null)
            {
                string mevcutDurum = selectedRow["Durum"].ToString();

                if (mevcutDurum != "Onay Bekliyor")
                {
                    MessageBox.Show("Sadece 'Onay Bekliyor' durumundaki rezervasyonlara ürün eklenebilir.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            // UrunStokWindow'u aç ve rezervasyon numarasını parametre olarak geç
            UrunStokWindow stokWindow = new UrunStokWindow(selectedRezervasyonNo);
            stokWindow.ShowDialog();

            // Ekleme yapıldıysa detayları yeniden yükle
            await YukleDetaylar();
            ActivityLogger.Log(GetAktifKullaniciAdi(), "Rezervasyona ürün ekle", $"Rez No: {selectedRezervasyonNo}");
        }

        private async void BtnUrunCikar_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridDetaylar.SelectedItem is not DataRowView selectedRow)
            {
                MessageBox.Show("Lütfen rezervasyondan çıkarılacak bir ürün seçin.");
                return;
            }

            if (selectedRow != null)
            {
                string mevcutDurum = selectedRow["Durum"].ToString();

                if (mevcutDurum != "Onay Bekliyor")
                {
                    MessageBox.Show("Sadece 'Onay Bekliyor' durumundaki rezervasyonlardan ürün çıkartılabilir.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string epc = selectedRow["EPC"].ToString();
                string rezNo = selectedRow["RezervasyonNo"].ToString();

                try
                {
                    // 1. Bu ürün dışında başka ürün var mı kontrol et
                    var kalan = await App.SupabaseClient
                        .From<Models.UrunStok>()
                        .Filter("RezervasyonNo", Operator.Equals, rezNo)
                        .Filter("EPC", Operator.NotEqual, epc)
                        .Get();

                    // Eğer bu son üründeyse kullanıcıya sor
                    bool sonUrunMu = kalan.Models.Count == 0;
                    if (sonUrunMu)
                    {
                        var sonuc = MessageBox.Show("Bu ürün rezervasyondaki son üründür. Ürünle birlikte rezervasyon da silinecek. Devam etmek istiyor musunuz?",
                                                    "Uyarı", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                        if (sonuc != MessageBoxResult.Yes)
                            return;
                    }

                    // 2. Stok durumunu güncelle
                    var stokResult = await App.SupabaseClient
                        .From<Models.UrunStok>()
                        .Where(x => x.EPC == epc)
                        .Get();

                    var stok = stokResult.Models.FirstOrDefault();
                    if (stok != null)
                    {
                        stok.Durum = "Stokta";
                        stok.RezervasyonNo = null;
                        stok.AliciFirma = null;
                        await App.SupabaseClient.From<Models.UrunStok>().Update(stok);
                    }

                    // 3. Son ürünse rezervasyonu da sil
                    if (sonUrunMu)
                    {
                        await App.SupabaseClient
                            .From<Models.UrunRezervasyon>()
                            .Filter("RezervasyonNo", Operator.Equals, rezNo)
                            .Delete();

                        MessageBox.Show("Ürün çıkarıldı. Bu ürün rezervasyondaki son ürün olduğu için rezervasyon da silindi.",
                                        "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                        selectedRezervasyonNo = null;
                    }
                    else
                    {
                        MessageBox.Show("Ürün başarıyla rezervasyondan çıkarıldı.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                    }

                    dataGridDetaylar.ItemsSource = null;
                    await FiltreUygula();
                    await YukleDetaylar();
                    ActivityLogger.Log(GetAktifKullaniciAdi(), "Rezervasyondan ürün çıkar", $"Rez No: {rezNo}, EPC: {epc}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Hata: " + ex.Message);
                }
            }
        }


        private async void BtnBoyutGuncelle_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridDetaylar.SelectedItem is DataRowView selectedRow)
            {
                var pencere = new BoyutGuncelleWindow(selectedRow);
                pencere.Owner = Application.Current.MainWindow;
                pencere.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                bool? sonuc = pencere.ShowDialog();

                if (sonuc == true)
                {
                    // Kullanıcı girişlerini güvenli şekilde decimal'e çevir
                    if (decimal.TryParse(pencere.SatisEn, out decimal satisEn) &&
                        decimal.TryParse(pencere.SatisBoy, out decimal satisBoy) &&
                        decimal.TryParse(pencere.SatisAlan, out decimal satisAlan) &&
                        decimal.TryParse(pencere.SatisTonaj, out decimal satisTonaj))
                    {
                        try
                        {
                            string epc = selectedRow["EPC"].ToString();
                            string rezNo = selectedRow["RezervasyonNo"].ToString();

                            // UrunStok güncelle
                            var stokResult = await App.SupabaseClient
                                .From<Models.UrunStok>()
                                .Where(x => x.EPC == epc)
                                .Get();

                            var stok = stokResult.Models.FirstOrDefault();
                            if (stok != null)
                            {
                                stok.SatisEn = satisEn;
                                stok.SatisBoy = satisBoy;
                                stok.SatisAlan = satisAlan;
                                stok.SatisTonaj = satisTonaj;
                                await App.SupabaseClient.From<Models.UrunStok>().Update(stok);
                            }

                            // DataGrid'i yenile
                            await YukleDetaylar();
                            ActivityLogger.Log(GetAktifKullaniciAdi(), "Rezervasyonda boyut güncelle", $"Rez No: {rezNo}, EPC: {epc}, En: {satisEn}, Boy: {satisBoy}");
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Hata: " + ex.Message);
                        }
                    }
                    else
                    {
                        MessageBox.Show("Lütfen geçerli sayısal değerler girin.", "Hatalı Giriş", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
        }
        // SatisYonetimi.xaml.cs (class SatisYonetimi içine ekle)
        private string GetAktifKullaniciAdi()
        {
            var mw = Application.Current?.MainWindow as MainWindow;

            // ViewModel'in ürettiği görünen ad (Ad varsa onu verir, yoksa email döner)
            var disp = mw?.Vm?.DisplayName;
            if (!string.IsNullOrWhiteSpace(disp) && disp != "—")
                return disp;

            // Yedekler
            var ad = mw?.Vm?.Me?.Ad;
            if (!string.IsNullOrWhiteSpace(ad)) return ad;

            var email = mw?.Vm?.Me?.Email ?? App.AktifKullanici?.Email;
            return string.IsNullOrWhiteSpace(email) ? "—" : email;
        }

    }
}




