using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Supabase.Postgrest;
using static Supabase.Postgrest.Constants;
using SideBar_Nav.Models;
using SideBar_Nav.Services;
using System.Threading.Tasks;
using SideBar_Nav.Windows;

namespace SideBar_Nav.Pages
{
    public partial class Iptal : Page
    {
        private DataTable iptalTable = new DataTable();
        private List<RezIptal> filteredIptaller = new();
        private string selectedRezervasyonNo;
        private List<string> firmaListesi = new();

        public Iptal()
        {
            InitializeComponent();
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

            txtFiltreSorumlu.TextChanged += async (s, e) => await FiltreUygula();
            txtFiltreEPC.TextChanged += async (s, e) => await FiltreUygula();
            dateFiltreTarih.SelectedDateChanged += async (s, e) => await FiltreUygula();
            dateFiltreIptalTarihi.SelectedDateChanged += async (s, e) => await FiltreUygula();

            _ = FiltreUygula();
        }

        private async void cmbTarihSecimi_SelectionChanged(object sender, SelectionChangedEventArgs e)
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
                    .From<AliciFirmalar>()
                    .Get();

                firmaListesi.Clear();
                foreach (var firma in result.Models)
                {
                    firmaListesi.Add(firma.FirmaAdi);
                }

                txtFiltreFirma.ItemsSource = null;
                txtFiltreFirma.ItemsSource = firmaListesi;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Firma listesi yüklenirken hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task FirmaOneriGetir(string girilen)
        {
            if (string.IsNullOrWhiteSpace(girilen)) return;

            try
            {
                var result = await App.SupabaseClient
                    .From<AliciFirmalar>()
                    .Filter(x => x.FirmaAdi, Operator.ILike, $"%{girilen}%")
                    .Get();

                firmaListesi.Clear();
                foreach (var firma in result.Models)
                {
                    firmaListesi.Add(firma.FirmaAdi);
                }

                txtFiltreFirma.ItemsSource = firmaListesi;
                txtFiltreFirma.IsDropDownOpen = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Firma önerileri yüklenirken hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task FiltreUygula()
        {
            try
            {
                string no = txtFiltreNo.Text.Trim().ToLower();
                string kod = txtFiltreKodu.Text.Trim().ToLower();
                string firma = txtFiltreFirma.Text.Trim().ToLower();
                string sorumlu = txtFiltreSorumlu.Text.Trim().ToLower();
                string epc = txtFiltreEPC.Text.Trim().ToLower();
                DateTime? islemTarihi = dateFiltreTarih.SelectedDate;
                DateTime? iptalTarihi = dateFiltreIptalTarihi.SelectedDate;
                string tarihSecimi = (cmbTarihSecimi.SelectedItem as ComboBoxItem)?.Content?.ToString();

                var query = App.SupabaseClient
                    .From<RezIptal>()
                    .Select("*");

                if (!string.IsNullOrWhiteSpace(no))
                    query = query.Filter("RezervasyonNo", Operator.ILike, $"%{no}%");

                if (!string.IsNullOrWhiteSpace(kod))
                    query = query.Filter("RezervasyonKodu", Operator.ILike, $"%{kod}%");

                if (!string.IsNullOrWhiteSpace(firma))
                    query = query.Filter("AliciFirma", Operator.ILike, $"%{firma}%");

                if (!string.IsNullOrWhiteSpace(sorumlu))
                    query = query.Filter("SatisSorumlusu", Operator.ILike, $"%{sorumlu}%");

                if (islemTarihi.HasValue)
                {
                    var startDate = islemTarihi.Value.Date;
                    var endDate = startDate.AddDays(1);
                    query = query.Filter("IslemTarihi", Operator.GreaterThanOrEqual, startDate.ToString("yyyy-MM-dd"))
                               .Filter("IslemTarihi", Operator.LessThan, endDate.ToString("yyyy-MM-dd"));
                }

                if (iptalTarihi.HasValue)
                {
                    var (utcStart, utcEnd) = DateRangeHelper.GetUtcRange(tarihSecimi, iptalTarihi.Value);
                    query = query.Filter("IptalTarihi", Operator.GreaterThanOrEqual, utcStart.ToString("yyyy-MM-ddTHH:mm:ss"))
                               .Filter("IptalTarihi", Operator.LessThan, utcEnd.ToString("yyyy-MM-ddTHH:mm:ss"));
                }

                if (!string.IsNullOrWhiteSpace(epc))
                {
                    var detayResult = await App.SupabaseClient
                        .From<RezIptalDetay>()
                        .Select("RezervasyonNo")
                        .Filter("EPC", Operator.ILike, $"%{epc}%")
                        .Get();

                    var rezervasyonNolar = detayResult.Models.Select(x => x.RezervasyonNo).Distinct().ToList();

                    if (rezervasyonNolar.Count > 0)
                        query = query.Filter("RezervasyonNo", Operator.In, rezervasyonNolar);
                    else
                    {
                        // Eğer eşleşen hiçbir EPC yoksa, sonuç döndürülmeyeceği için erken çıkılır
                        iptalTable.Clear();
                        dataGridIptal.ItemsSource = iptalTable.DefaultView;
                        return;
                    }
                }

                var result = await query.Get();
                filteredIptaller = result.Models.ToList();

                iptalTable.Clear();

                // Sadece bir kere kolonları oluştur
                if (iptalTable.Columns.Count == 0)
                {
                    iptalTable.Columns.Add("RezervasyonNo");
                    iptalTable.Columns.Add("RezervasyonKodu");
                    iptalTable.Columns.Add("AliciFirma");
                    iptalTable.Columns.Add("SatisSorumlusu");
                    iptalTable.Columns.Add("IslemTarihi");
                    iptalTable.Columns.Add("IptalTarihi");
                    iptalTable.Columns.Add("IptalEdenPersonel");
                    iptalTable.Columns.Add("IptalSebebi");
                }

                foreach (var iptal in filteredIptaller)
                {
                    var row = iptalTable.NewRow();
                    row["RezervasyonNo"] = iptal.RezervasyonNo;
                    row["RezervasyonKodu"] = iptal.RezervasyonKodu;
                    row["AliciFirma"] = iptal.AliciFirma;
                    row["SatisSorumlusu"] = iptal.SatisSorumlusu;
                    row["IslemTarihi"] = iptal.IslemTarihi?.ToString("yyyy-MM-dd");
                    row["IptalTarihi"] = iptal.IptalTarihi?.ToString("yyyy-MM-dd");
                    row["IptalEdenPersonel"] = iptal.IptalEdenPersonel;
                    row["IptalSebebi"] = iptal.IptalSebebi;
                    iptalTable.Rows.Add(row);
                }

                dataGridIptal.ItemsSource = iptalTable.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Filtreleme sırasında hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private async void BtnFiltreTemizle_Click(object sender, RoutedEventArgs e)
        {
            txtFiltreNo.Text = string.Empty;
            txtFiltreKodu.Text = string.Empty;
            txtFiltreFirma.Text = string.Empty;
            txtFiltreSorumlu.Text = string.Empty;
            txtFiltreEPC.Text = string.Empty;
            dateFiltreTarih.SelectedDate = null;
            dateFiltreIptalTarihi.SelectedDate = null;
            cmbTarihSecimi.SelectedIndex = 0;

            await FiltreUygula();
        }

        private async void dataGridIptal_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dataGridIptal.SelectedItem is DataRowView selectedRow)
            {
                try
                {
                    selectedRezervasyonNo = selectedRow["RezervasyonNo"].ToString();

                    var result = await App.SupabaseClient
                        .From<RezIptalDetay>()
                        .Filter(x => x.RezervasyonNo, Operator.Equals, selectedRezervasyonNo)
                        .Get();

                    var iptalTarihi = selectedRow["IptalTarihi"].ToString();

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
                    detayTable.Columns.Add("PlakaAdedi");
                    detayTable.Columns.Add("StokEn");
                    detayTable.Columns.Add("StokBoy");
                    detayTable.Columns.Add("StokAlan");
                    detayTable.Columns.Add("StokTonaj");
                    detayTable.Columns.Add("SatisEn");
                    detayTable.Columns.Add("SatisBoy");
                    detayTable.Columns.Add("SatisAlan");
                    detayTable.Columns.Add("SatisTonaj");
                    detayTable.Columns.Add("Durum");
                    detayTable.Columns.Add("KaydedenPersonel");
                    detayTable.Columns.Add("UrunCikisTarihi");
                    detayTable.Columns.Add("AliciFirma");
                    detayTable.Columns.Add("IptalTarihi");

                    foreach (var detay in result.Models)
                    {
                        var row = detayTable.NewRow();
                        row["RezervasyonNo"] = detay.RezervasyonNo;
                        row["EPC"] = detay.EPC;
                        row["BarkodNo"] = detay.BarkodNo;
                        row["BandilNo"] = detay.BandilNo;
                        row["PlakaNo"] = detay.PlakaNo;
                        row["UrunTipi"] = detay.UrunTipi;
                        row["UrunTuru"] = detay.UrunTuru;
                        row["YuzeyIslemi"] = detay.YuzeyIslemi;
                        row["Seleksiyon"] = detay.Seleksiyon;
                        row["UretimTarihi"] = detay.UretimTarihi?.ToString("yyyy-MM-dd");
                        row["Kalinlik"] = detay.Kalinlik;
                        row["PlakaAdedi"] = detay.PlakaAdedi;
                        row["StokEn"] = detay.StokEn;
                        row["StokBoy"] = detay.StokBoy;
                        row["StokAlan"] = detay.StokAlan;
                        row["StokTonaj"] = detay.StokTonaj;
                        row["SatisEn"] = detay.SatisEn;
                        row["SatisBoy"] = detay.SatisBoy;
                        row["SatisAlan"] = detay.SatisAlan;
                        row["SatisTonaj"] = detay.SatisTonaj;
                        row["Durum"] = detay.Durum;
                        row["KaydedenPersonel"] = detay.KaydedenPersonel;
                        row["UrunCikisTarihi"] = detay.UrunCikisTarihi?.ToString("yyyy-MM-dd");
                        row["AliciFirma"] = detay.AliciFirma;
                        row["IptalTarihi"] = iptalTarihi;
                        detayTable.Rows.Add(row);
                    }

                    dataGridIptalDetay.ItemsSource = detayTable.DefaultView;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Detay bilgileri yüklenirken hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BtnPdfRapor_Click(object sender, RoutedEventArgs e)
        {
            if (cmbTarihSecimi.SelectedItem == null || !dateFiltreIptalTarihi.SelectedDate.HasValue)
            {
                MessageBox.Show("Lütfen rapor için iptal tarihi ve periyot seçiniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await FiltreUygula();

            if (filteredIptaller.Count == 0)
            {
                MessageBox.Show("Seçilen periyotta listelenecek iptal kaydı bulunamadı.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string selection = (cmbTarihSecimi.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Günlük";
            DateTime selectedDate = dateFiltreIptalTarihi.SelectedDate!.Value;

            try
            {
                var rezervasyonNolar = filteredIptaller
                    .Select(i => i.RezervasyonNo)
                    .Where(no => !string.IsNullOrWhiteSpace(no))
                    .Distinct()
                    .ToList();

                var detailLookup = new Dictionary<string, List<RezIptalDetay>>();

                if (rezervasyonNolar.Count > 0)
                {
                    var detayResult = await App.SupabaseClient
                        .From<RezIptalDetay>()
                        .Filter("RezervasyonNo", Operator.In, rezervasyonNolar)
                        .Get();

                    detailLookup = detayResult.Models
                        .GroupBy(d => d.RezervasyonNo)
                        .ToDictionary(g => g.Key, g => g.ToList());
                }

                string periodDescription = DateRangeHelper.GetRangeDescription(selection, selectedDate);
                string? filterDescription = BuildFilterDescription();

                var window = new IptalRaporWindow(
                    filteredIptaller.ToList(),
                    detailLookup,
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
                MessageBox.Show($"Rapor oluşturma sırasında hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSebepGoster_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridIptal.SelectedItem is not DataRowView row)
            {
                MessageBox.Show("Lütfen bir iptal kaydı seçin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var window = new IptalDetayWindow(row);
            window.Owner = Application.Current.MainWindow;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.ShowDialog();
        }

        private async void BtnSil_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedRezervasyonNo))
            {
                MessageBox.Show("Lütfen önce silinecek bir iptal kaydı seçin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var onay = MessageBox.Show($"{selectedRezervasyonNo} numaralı iptal kaydı ve detayları silinecek. Emin misiniz?",
                                        "Silme Onayı", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (onay != MessageBoxResult.Yes)
                return;

            try
            {
                // 1. RezIptalDetay kayıtlarını sil
                await App.SupabaseClient
                    .From<RezIptalDetay>()
                    .Filter(x => x.RezervasyonNo, Operator.Equals, selectedRezervasyonNo)
                    .Delete();

                // 2. RezIptal kaydını sil
                await App.SupabaseClient
                    .From<RezIptal>()
                    .Filter(x => x.RezervasyonNo, Operator.Equals, selectedRezervasyonNo)
                    .Delete();

                MessageBox.Show("İptal kaydı ve detayları başarıyla silindi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);

                selectedRezervasyonNo = null;
                dataGridIptalDetay.ItemsSource = null;

                await FiltreUygula(); // Yeniden yükle
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Silme işlemi sırasında hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string? BuildFilterDescription()
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(txtFiltreNo.Text))
                parts.Add($"Rezervasyon No: {txtFiltreNo.Text.Trim()}");

            if (!string.IsNullOrWhiteSpace(txtFiltreKodu.Text))
                parts.Add($"Rezervasyon Kodu: {txtFiltreKodu.Text.Trim()}");

            if (!string.IsNullOrWhiteSpace(txtFiltreFirma.Text))
                parts.Add($"Alıcı Firma: {txtFiltreFirma.Text.Trim()}");

            if (!string.IsNullOrWhiteSpace(txtFiltreSorumlu.Text))
                parts.Add($"Satış Sorumlusu: {txtFiltreSorumlu.Text.Trim()}");

            if (dateFiltreTarih.SelectedDate.HasValue)
                parts.Add($"Rezervasyon Tarihi: {dateFiltreTarih.SelectedDate.Value:dd.MM.yyyy}");

            if (dateFiltreIptalTarihi.SelectedDate.HasValue)
                parts.Add($"İptal Tarihi: {dateFiltreIptalTarihi.SelectedDate.Value:dd.MM.yyyy}");

            if (!string.IsNullOrWhiteSpace(txtFiltreEPC.Text))
                parts.Add($"EPC: {txtFiltreEPC.Text.Trim()}");

            var tarihSecimi = (cmbTarihSecimi.SelectedItem as ComboBoxItem)?.Content?.ToString();
            if (!string.IsNullOrWhiteSpace(tarihSecimi))
                parts.Add($"Tarih Periyodu: {tarihSecimi}");

            return parts.Count == 0 ? null : string.Join(" | ", parts);
        }

    }
}
