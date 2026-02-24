using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using SideBar_Nav.Models;
using SideBar_Nav.Services;
using static Supabase.Postgrest.Constants;

namespace SideBar_Nav.Windows
{
    public partial class UrunStokWindow : Window
    {
        private readonly string rezervasyonNo;
        private List<UrunStok>? tumStoklar;

        public UrunStokWindow(string rezervasyonNo)
        {
            InitializeComponent();
            this.rezervasyonNo = rezervasyonNo;

            _ = YukleComboBoxVerileri();
            _ = YukleStokUrunler();

            // Text/filter eventleri
            txtFiltreEPC.TextChanged += (s, e) => FiltreUygula();
            txtFiltreBarkod.TextChanged += (s, e) => FiltreUygula();
            txtFiltreBandil.TextChanged += (s, e) => FiltreUygula();
            txtFiltrePlaka.TextChanged += (s, e) => FiltreUygula();
            dateUretimTarihi.SelectedDateChanged += (s, e) => FiltreUygula();

            // Combo eventleri
            cmbUrunTipi.SelectionChanged += (s, e) => FiltreUygula();
            cmbUrunTuru.SelectionChanged += (s, e) => FiltreUygula();
            cmbYuzeyIslem.SelectionChanged += (s, e) => FiltreUygula();
        }

        private async Task YukleComboBoxVerileri()
        {
            try
            {
                // ÜRÜN TİPİ: (ayrı master tablon yoksa geçici olarak stoktan)
                var stokResp = await App.SupabaseClient.From<UrunStok>().Get();
                var tipler = (stokResp.Models ?? new List<UrunStok>())
                    .Select(x => x.UrunTipi)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // ÜRÜN TÜRLERİ: master tablo
                var turResp = await App.SupabaseClient.From<UrunTurleri>().Get();
                var turler = (turResp.Models ?? new List<UrunTurleri>())
                    .Select(t => t.UrunTuru)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // YÜZEY İŞLEMLERİ: master tablo
                var yzResp = await App.SupabaseClient.From<YuzeyIslemleri>().Get();
                var yuzeyler = (yzResp.Models ?? new List<YuzeyIslemleri>())
                    .Select(y => y.YuzeyIslemi)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // Başa "Hepsi"
                tipler.Insert(0, "Hepsi");
                turler.Insert(0, "Hepsi");
                yuzeyler.Insert(0, "Hepsi");

                // Combobox'lara doğrudan string listesi veriyoruz
                cmbUrunTipi.ItemsSource = tipler;
                cmbUrunTuru.ItemsSource = turler;
                cmbYuzeyIslem.ItemsSource = yuzeyler;

                cmbUrunTipi.SelectedIndex = 0;
                cmbUrunTuru.SelectedIndex = 0;
                cmbYuzeyIslem.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Filtre listeleri yüklenirken hata: " + ex.Message,
                                "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private async Task YukleStokUrunler()
        {
            try
            {
                var response = await App.SupabaseClient
                    .From<UrunStok>()
                    .Where(x => x.Durum == "Stokta")
                    .Get();

                tumStoklar = response.Models ?? new List<UrunStok>();
                dataGridStok.ItemsSource = tumStoklar;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Stok verisi yüklenirken hata: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FiltreUygula()
        {
            if (tumStoklar == null) return;

            string epcFilter = (txtFiltreEPC.Text ?? "").Trim().ToLower();
            string barkodFilter = (txtFiltreBarkod.Text ?? "").Trim().ToLower();
            string bandilFilter = (txtFiltreBandil.Text ?? "").Trim().ToLower();
            string plakaFilter = (txtFiltrePlaka.Text ?? "").Trim().ToLower();
            DateTime? tarihFilter = dateUretimTarihi.SelectedDate;

            string? urunTipiFilter = cmbUrunTipi.SelectedItem as string;
            string? urunTuruFilter = cmbUrunTuru.SelectedItem as string;
            string? yuzeyIslemFilter = cmbYuzeyIslem.SelectedItem as string;

            bool tipPasif = string.IsNullOrEmpty(urunTipiFilter) || urunTipiFilter.Equals("Hepsi", StringComparison.OrdinalIgnoreCase);
            bool turPasif = string.IsNullOrEmpty(urunTuruFilter) || urunTuruFilter.Equals("Hepsi", StringComparison.OrdinalIgnoreCase);
            bool yuzeyPasif = string.IsNullOrEmpty(yuzeyIslemFilter) || yuzeyIslemFilter.Equals("Hepsi", StringComparison.OrdinalIgnoreCase);

            var filtered = tumStoklar.Where(x =>
                (string.IsNullOrEmpty(epcFilter) || (x.EPC?.ToLower().Contains(epcFilter) ?? false)) &&
                (string.IsNullOrEmpty(barkodFilter) || (x.BarkodNo?.ToLower().Contains(barkodFilter) ?? false)) &&
                (string.IsNullOrEmpty(bandilFilter) || (x.BandilNo?.ToLower().Contains(bandilFilter) ?? false)) &&
                (string.IsNullOrEmpty(plakaFilter) || (x.PlakaNo?.ToLower().Contains(plakaFilter) ?? false)) &&
                (!tarihFilter.HasValue || x.UretimTarihi?.Date == tarihFilter.Value.Date) &&
                (tipPasif || string.Equals(x.UrunTipi, urunTipiFilter, StringComparison.OrdinalIgnoreCase)) &&
                (turPasif || string.Equals(x.UrunTuru, urunTuruFilter, StringComparison.OrdinalIgnoreCase)) &&
                (yuzeyPasif || string.Equals(x.YuzeyIslemi, yuzeyIslemFilter, StringComparison.OrdinalIgnoreCase))
            ).ToList();

            dataGridStok.ItemsSource = filtered;
        }


        private async void BtnYenile_Click(object sender, RoutedEventArgs e)
        {
            // Reset tüm filtreler
            txtFiltreEPC.Text = "";
            txtFiltreBarkod.Text = "";
            txtFiltreBandil.Text = "";
            txtFiltrePlaka.Text = "";
            dateUretimTarihi.SelectedDate = null;

            cmbUrunTipi.SelectedIndex = 0;
            cmbUrunTuru.SelectedIndex = 0;
            cmbYuzeyIslem.SelectedIndex = 0;

            await YukleStokUrunler();
            FiltreUygula();
        }

        private async void BtnEkle_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridStok.SelectedItem is not UrunStok selectedRow)
            {
                MessageBox.Show("Lütfen bir ürün seçin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var rezervasyon = await App.SupabaseClient
                    .From<UrunRezervasyon>()
                    .Filter("RezervasyonNo", Operator.Equals, rezervasyonNo)
                    .Single();

                if (rezervasyon == null)
                {
                    MessageBox.Show("Rezervasyon bulunamadı.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                selectedRow.Durum = "Onay Bekliyor";
                selectedRow.RezervasyonNo = rezervasyonNo;
                selectedRow.AliciFirma = rezervasyon.AliciFirma;

                await App.SupabaseClient.From<UrunStok>().Update(selectedRow);

                MessageBox.Show("Ürün rezervasyona eklendi.");
                ActivityLogger.Log(GetAktifKullaniciAdi(), "Rezervasyona ürün eklendi", $"Rez No: {rezervasyonNo}, EPC: {selectedRow.EPC}");

                await YukleStokUrunler();
                FiltreUygula();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Hata: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string GetAktifKullaniciAdi()
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                var disp = mw.Vm?.DisplayName;
                if (!string.IsNullOrWhiteSpace(disp) && disp != "—")
                    return disp;

                var email = mw.Vm?.Me?.Email ?? App.AktifKullanici?.Email;
                if (!string.IsNullOrWhiteSpace(email))
                    return email;
            }
            return "—";
        }
    }
}
