// Sevkiyat.xaml.cs (aktif rezervasyon seçimi + manuel sevkiyat tamamlama + canlı datagrid güncelleme + eksik EPC kontrolü)
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Supabase.Postgrest;
using static Supabase.Postgrest.Constants;
using SideBar_Nav.Models;
using SideBar_Nav.Services;
using System.Threading.Tasks;
using System.IO;

namespace SideBar_Nav.Pages
{
    public partial class Sevkiyat : Page
    {
        private ObservableCollection<DataRow> uygunEPCListesi = new ObservableCollection<DataRow>();
        private ObservableCollection<DataRow> uygunsuzEPCListesi = new ObservableCollection<DataRow>();
        private ObservableCollection<DataRow> rezervasyonDetayListesi = new ObservableCollection<DataRow>();
        private Dictionary<int, Timer> sevkiyatGpoTimers = new Dictionary<int, Timer>();
        private DispatcherTimer epcTemizlikTimer;
        private Dictionary<string, DateTime> epcOkumaZamanlari = new Dictionary<string, DateTime>();
        private string aktifRezervasyonNo = null;
        private HashSet<string> okunanRezervasyonlar = new HashSet<string>();
        private Queue<string> epcQueue = new Queue<string>();
        //private HashSet<string> processedEpcs = new HashSet<string>();
        public static string AktifRezervasyonNo_Global { get; private set; }
        public Sevkiyat()
        {
            InitializeComponent();
            dataGridUygun.ItemsSource = uygunEPCListesi;
            dataGridUygunsuz.ItemsSource = uygunsuzEPCListesi;
            dataGridRezervasyonDetay.ItemsSource = rezervasyonDetayListesi;

            App.SharedReader.TagsReported += OnTagReported;
            //
            epcTemizlikTimer = new DispatcherTimer();
            epcTemizlikTimer.Interval = TimeSpan.FromSeconds(3);
            epcTemizlikTimer.Tick += EpctemizlikTimer_Tick;
            epcTemizlikTimer.Start();

            GPIO_ops.RegisterLists(uygunEPCListesi, uygunsuzEPCListesi);
        }

        private async void YükleTümRezervasyonlar()
        {
            cmbAktifRezervasyon.Items.Clear();

            // ❗ foreach sırasında koleksiyon değişebilir: bu yüzden ToList() kullan
            foreach (string rez in okunanRezervasyonlar.ToList())
            {
                var result = await App.SupabaseClient
                    .From<UrunRezervasyon>()
                    .Filter("RezervasyonNo", Operator.Equals, rez)
                    .Filter("Durum", Operator.Equals, "Onaylandı")
                    .Get();

                if (result.Models.Count > 0 && !cmbAktifRezervasyon.Items.Contains(rez))
                    cmbAktifRezervasyon.Items.Add(rez); // ✅ tekrar ekleme engelleniyor
            }
        }


        private void BtnTumunuSabitle_Click(object sender, RoutedEventArgs e)
        {
            foreach (DataRow row in uygunEPCListesi)
            {
                if (!row.Table.Columns.Contains("IsFixed"))
                    row.Table.Columns.Add("IsFixed", typeof(bool));
                row["IsFixed"] = true;
            }
        }

        private void EpctemizlikTimer_Tick(object sender, EventArgs e)
        {
            var eskiZaman = DateTime.Now.AddSeconds(-2);
            var silinecekEPCler = epcOkumaZamanlari.Where(x => x.Value < eskiZaman).Select(x => x.Key).ToList();

            foreach (var epc in silinecekEPCler)
            {
                epcOkumaZamanlari.Remove(epc);

                var uygunSatir = uygunEPCListesi.FirstOrDefault(x => x["EPC"].ToString() == epc);
                var uygunsuzSatir = uygunsuzEPCListesi.FirstOrDefault(x => x["EPC"].ToString() == epc);

                if (uygunSatir != null && !Convert.ToBoolean(uygunSatir["IsFixed"]))
                    _ = uygunEPCListesi.Remove(uygunSatir);

                if (uygunsuzSatir != null && !Convert.ToBoolean(uygunsuzSatir["IsFixed"]))
                    _ = uygunsuzEPCListesi.Remove(uygunsuzSatir);
                bool uygunBos = !uygunEPCListesi.Any();
                bool uygunsuzBos = !uygunsuzEPCListesi.Any();

                if (uygunBos && uygunsuzBos)
                {
                    // Durum kalmadı, GPO'ları sıfırla
                    GPIO_ops.TriggerSelectedScenarioWithDurum("boşalt"); 
                }
                // EpctemizlikTimer_Tick sonunda
                if (!uygunEPCListesi.Any() && !uygunsuzEPCListesi.Any())
                {
                    TriggerGPOBasedOnDurum("boşalt");
                }
            }
        }
        private void OnTagReported(RfidData tag)
        {
            _ = Task.Run(async () =>
            {
                string epc = tag.EPC;

                if (epcOkumaZamanlari.ContainsKey(epc))
                {
                    epcOkumaZamanlari[epc] = DateTime.Now;
                    return;
                }
                else
                {
                    epcOkumaZamanlari.Add(epc, DateTime.Now);
                }

                if (epcQueue.Contains(epc))
                    return;

                await Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    if (uygunEPCListesi.Any(x => x["EPC"].ToString() == epc) ||
                        uygunsuzEPCListesi.Any(x => x["EPC"].ToString() == epc))
                    {
                        return;
                    }

                    var urun = await GetUrunStokSatiri(epc);

                    // 🎯 Eğer UrunStok'ta yoksa uygunsuz listesine ekle
                    if (urun == null)
                    {
                        var dt = new DataTable();
                        dt.Columns.Add("EPC");
                        dt.Columns.Add("BarkodNo");
                        dt.Columns.Add("BandilNo");
                        dt.Columns.Add("Durum");
                        dt.Columns.Add("IsFixed", typeof(bool));

                        var row = dt.NewRow();
                        row["EPC"] = epc;
                        row["BarkodNo"] = "YOK";
                        row["BandilNo"] = "-";
                        row["Durum"] = "UrunStok'ta Yok";
                        row["IsFixed"] = false;
                        dt.Rows.Add(row);

                        uygunsuzEPCListesi.Add(row);

                        // GPO tetikle
                        TriggerGPOBasedOnDurum("yok");
                        return;
                    }

                    // ✅ Normal akış
                    okunanRezervasyonlar.Add(urun["RezervasyonNo"].ToString());
                    YükleTümRezervasyonlar();

                    string rezervasyonNo = urun["RezervasyonNo"].ToString();
                    string durum = urun["Durum"].ToString().ToLower();

                    if (aktifRezervasyonNo == null)
                    {
                        aktifRezervasyonNo = rezervasyonNo;
                        txtRezervasyonNo.Text = rezervasyonNo;
                        txtAliciFirma.Text = urun["AliciFirma"].ToString();
                        await YukleRezervasyonDetay(rezervasyonNo);
                        cmbAktifRezervasyon.SelectedItem = rezervasyonNo;
                    }

                    if (rezervasyonNo == aktifRezervasyonNo && durum == "onaylandı")
                    {
                        if (!uygunEPCListesi.Any(x => x["EPC"].ToString() == epc))
                        {
                            uygunEPCListesi.Add(urun);
                            //TriggerGPOBasedOnDurum(durum);
                            GPIO_ops.takeRezNo(rezervasyonNo);
                            GPIO_ops.TriggerSelectedScenarioWithDurum(durum);
                        }
                    }
                    else
                    {
                        if (!uygunsuzEPCListesi.Any(x => x["EPC"].ToString() == epc))
                        {
                            uygunsuzEPCListesi.Add(urun);
                            //TriggerGPOBasedOnDurum(durum);
                            GPIO_ops.takeRezNo(rezervasyonNo);
                            GPIO_ops.TriggerSelectedScenarioWithDurum(durum);
                        }
                    }
                });
            });
        }

        private async Task YukleRezervasyonDetay(string rezervasyonNo)
        {
            rezervasyonDetayListesi.Clear();

            try
            {
                var result = await App.SupabaseClient
                    .From<UrunStok>()
                    .Filter("RezervasyonNo", Operator.Equals, rezervasyonNo)
                    .Get();

                var dt = new DataTable();
                dt.Columns.Add("EPC");
                dt.Columns.Add("BarkodNo");
                dt.Columns.Add("BandilNo");

                foreach (var item in result.Models)
                {
                    dt.Rows.Add(item.EPC, item.BarkodNo, item.BandilNo);
                }

                foreach (DataRow row in dt.Rows)
                {
                    rezervasyonDetayListesi.Add(row);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Detaylar yüklenirken hata oluştu:\n" + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnSevkiyatiTamamla_Click(object sender, RoutedEventArgs e)
        {
            if (aktifRezervasyonNo == null)
            {
                MessageBox.Show("Henüz geçerli bir rezervasyon seçilmedi.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // ✅ Görmezden gelinmeyen uygunsuz EPC varsa işlem durdurulur
            if (uygunsuzEPCListesi.Any(x => !(x.Table.Columns.Contains("Ignore") && (bool)x["Ignore"])))
            {
                MessageBox.Show("Sevkiyat alanında uygun olmayan ve görmezden gelinmeyen ürünler var. Lütfen çıkarınız veya işaretleyiniz.", "Uygunsuz Ürün", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!await TümEPCOkunduMu(aktifRezervasyonNo))
            {
                MessageBox.Show($"Rezervasyon {aktifRezervasyonNo} içindeki tüm ürünler okutulmamış. Lütfen eksik ürünleri okutun.", "Eksik Ürün", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await FinalizeReservation(aktifRezervasyonNo);
        }

        private async Task<bool> TümEPCOkunduMu(string rezervasyonNo)
        {
            var detayResult = await App.SupabaseClient
                .From<UrunStok>()
                .Filter("RezervasyonNo", Operator.Equals, rezervasyonNo)
                .Get();

            var epcList = detayResult.Models.Select(x => x.EPC).ToList();
            var okunan = uygunEPCListesi.Select(x => x["EPC"].ToString()).ToHashSet();
            return epcList.All(epc => okunan.Contains(epc));
        }

        private async Task FinalizeReservation(string rezervasyonNo)
        {
            try
            {
                var simdi = DateTime.Now;

                // 1. Update UrunStok table
                var stokResult = await App.SupabaseClient
                    .From<UrunStok>()
                    .Filter("RezervasyonNo", Operator.Equals, rezervasyonNo)
                    .Get();

                foreach (var stok in stokResult.Models)
                {
                    stok.Durum = "Sevkiyat Tamamlandı";
                    stok.UrunCikisTarihi = simdi;
                    await App.SupabaseClient.From<UrunStok>().Update(stok);
                }

                // 2. Update UrunRezervasyon table
                var rezervasyonResult = await App.SupabaseClient
                    .From<UrunRezervasyon>()
                    .Filter("RezervasyonNo", Operator.Equals, rezervasyonNo)
                    .Get();

                var rezervasyon = rezervasyonResult.Models.FirstOrDefault();
                if (rezervasyon != null)
                {
                    rezervasyon.Durum = "Sevkiyat Tamamlandı";
                    rezervasyon.UrunCikisTarihi = simdi;
                    await App.SupabaseClient.From<UrunRezervasyon>().Update(rezervasyon);
                }

                try
                {
                    var snapshot = await PackingListService.GetPackingListSnapshotAsync(rezervasyonNo);
                    PackingListService.GeneratePackingListPdf(snapshot);
                    var excelPath = Path.Combine(PackingListService.DefaultDirectory, $"PackingList_{snapshot.Reservation?.RezervasyonNo}.xlsx");
                    ReportExcelService.GeneratePackingListReport(snapshot, outputPath: excelPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Packing list güncellenirken hata oluştu:\n{ex.Message}", "Packing List", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                // ✅ Sabitlemeler sıfırlanır
                foreach (DataRow row in uygunEPCListesi)
                {
                    if (row.Table.Columns.Contains("IsFixed"))
                        row["IsFixed"] = false;
                }

                // ✅ Ignore (görmezden gel) bayrakları sıfırlanır
                foreach (DataRow row in uygunsuzEPCListesi)
                {
                    if (row.Table.Columns.Contains("Ignore"))
                        row["Ignore"] = false;
                }

                MessageBox.Show($"Rezervasyon {rezervasyonNo} için sevkiyat tamamlandı.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Hata: " + ex.Message);
            }
        }

        private async Task<DataRow> GetUrunStokSatiri(string epc)
        {
            try
            {
                var result = await App.SupabaseClient
                    .From<UrunStok>()
                    .Filter("EPC", Operator.Equals, epc)
                    .Get();

                if (result.Models.Count == 0) return null;

                var stok = result.Models[0];
                var dt = new DataTable();

                // Add all necessary columns
                dt.Columns.Add("EPC");
                dt.Columns.Add("BarkodNo");
                dt.Columns.Add("BandilNo");
                dt.Columns.Add("PlakaNo");
                dt.Columns.Add("UrunTipi");
                dt.Columns.Add("UrunTuru");
                dt.Columns.Add("YuzeyIslemi");
                dt.Columns.Add("Seleksiyon");
                dt.Columns.Add("UretimTarihi");
                dt.Columns.Add("Kalinlik");
                dt.Columns.Add("StokEn");
                dt.Columns.Add("StokBoy");
                dt.Columns.Add("PlakaAdedi");
                dt.Columns.Add("StokAlan");
                dt.Columns.Add("StokTonaj");
                dt.Columns.Add("SatisEn");
                dt.Columns.Add("SatisBoy");
                dt.Columns.Add("SatisAlan");
                dt.Columns.Add("SatisTonaj");
                dt.Columns.Add("Durum");
                dt.Columns.Add("RezervasyonNo");
                dt.Columns.Add("AliciFirma");
                dt.Columns.Add("IsFixed", typeof(bool));
                // Add the row with data
                var row = dt.NewRow();
                row["EPC"] = stok.EPC;
                row["BarkodNo"] = stok.BarkodNo;
                row["BandilNo"] = stok.BandilNo;
                row["PlakaNo"] = stok.PlakaNo;
                row["UrunTipi"] = stok.UrunTipi;
                row["UrunTuru"] = stok.UrunTuru;
                row["YuzeyIslemi"] = stok.YuzeyIslemi;
                row["Seleksiyon"] = stok.Seleksiyon;
                row["UretimTarihi"] = stok.UretimTarihi;
                row["Kalinlik"] = stok.Kalinlik;
                row["StokEn"] = stok.StokEn;
                row["StokBoy"] = stok.StokBoy;
                row["PlakaAdedi"] = stok.PlakaAdedi;
                row["StokAlan"] = stok.StokAlan;
                row["StokTonaj"] = stok.StokTonaj;
                row["SatisEn"] = stok.SatisEn;
                row["SatisBoy"] = stok.SatisBoy;
                row["SatisAlan"] = stok.SatisAlan;
                row["SatisTonaj"] = stok.SatisTonaj;
                row["Durum"] = stok.Durum;
                row["RezervasyonNo"] = stok.RezervasyonNo;
                row["AliciFirma"] = stok.AliciFirma;
                row["IsFixed"] = false;
                dt.Rows.Add(row);
                return dt.Rows[0];
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ürün bilgisi alınırken hata:\n" + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }
        private void TriggerGPOBasedOnDurum(string durum)
        {
            GPIO_ops.TriggerSelectedScenarioWithDurum(durum);

        }

        private async void cmbAktifRezervasyon_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbAktifRezervasyon.SelectedItem != null)
            {
                string selectedRezervasyonNo = cmbAktifRezervasyon.SelectedItem.ToString();
                aktifRezervasyonNo = selectedRezervasyonNo;

                AktifRezervasyonNo_Global = selectedRezervasyonNo;
                // Mevcut listeleri temizle
                uygunEPCListesi.Clear();
                uygunsuzEPCListesi.Clear();
                rezervasyonDetayListesi.Clear();

                // Rezervasyon detaylarını yükle
                await YukleRezervasyonDetay(selectedRezervasyonNo);

                // Rezervasyon bilgilerini getir
                var result = await App.SupabaseClient
                    .From<UrunRezervasyon>()
                    .Filter("RezervasyonNo", Operator.Equals, selectedRezervasyonNo)
                    .Single();

                if (result != null)
                {
                    txtRezervasyonNo.Text = selectedRezervasyonNo;
                    txtAliciFirma.Text = result.AliciFirma;
                }
            }
        }


    }
}
