using Microsoft.Win32;
using SideBar_Nav.Services;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace SideBar_Nav.Windows
{
    public partial class PackingListWindow : Window
    {
        private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("tr-TR");

        private readonly string rezervasyonNo;
        private PackingListService.PackingListSnapshot? currentSnapshot;
        private string? currentPdfPath;
        private string? currentExcelPath;
        private string? previewPdfPath;

        public PackingListWindow(string rezervasyonNo)
        {
            InitializeComponent();
            this.rezervasyonNo = rezervasyonNo;
            Loaded += PackingListWindow_Loaded;
        }

        private async void PackingListWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadPackingListAsync();
        }

        private async Task LoadPackingListAsync()
        {
            btnRefresh.IsEnabled = false;
            btnDownloadPdf.IsEnabled = false;
            btnPrintPdf.IsEnabled = false;
            btnDownloadExcel.IsEnabled = false;

            currentPdfPath = null;
            currentExcelPath = null;
            lblPdfFileName.Text = "-";
            lblPdfGeneratedAt.Text = "-";
            lblExcelFileName.Text = "-";
            lblExcelGeneratedAt.Text = "-";

            try
            {
                currentSnapshot = await PackingListService.GetPackingListSnapshotAsync(rezervasyonNo);
                var reservation = currentSnapshot.Reservation ?? throw new InvalidOperationException("Rezervasyon bilgisi alınamadı.");

                lblRezervasyonNo.Text = reservation.RezervasyonNo ?? "-";
                lblRezervasyonKodu.Text = reservation.RezervasyonKodu ?? "-";
                lblAliciFirma.Text = string.IsNullOrWhiteSpace(reservation.AliciFirma) ? "-" : reservation.AliciFirma;
                lblRezervasyonSorumlusu.Text = string.IsNullOrWhiteSpace(reservation.RezervasyonSorumlusu) ? "-" : reservation.RezervasyonSorumlusu;
                lblSatisSorumlusu.Text = string.IsNullOrWhiteSpace(reservation.SatisSorumlusu) ? "-" : reservation.SatisSorumlusu;
                lblDurum.Text = string.IsNullOrWhiteSpace(reservation.Durum) ? "-" : reservation.Durum;
                lblIslemTarihi.Text = FormatDate(reservation.IslemTarihi);
                lblUrunCikisTarihi.Text = FormatDate(reservation.UrunCikisTarihi);
                txtSevkiyatAdresi.Text = string.IsNullOrWhiteSpace(reservation.SevkiyatAdresi) ? "-" : reservation.SevkiyatAdresi;

                lblToplamUrun.Text = currentSnapshot.Items.Count.ToString(Culture);
                lblToplamPlaka.Text = currentSnapshot.TotalPlateCount.ToString("N0", Culture);
                lblToplamAlan.Text = PackingListService.FormatDecimal(currentSnapshot.TotalArea);
                lblToplamTonaj.Text = PackingListService.FormatDecimal(currentSnapshot.TotalTonaj, 3);

                string tempDirectory = Path.Combine(Path.GetTempPath(), "SideBarNavPackingList");
                Directory.CreateDirectory(tempDirectory);
                string stamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");

                string pdfPath = Path.Combine(tempDirectory, $"PackingList_{reservation.RezervasyonNo}_{stamp}.pdf");
                currentPdfPath = PackingListService.GeneratePackingListPdf(currentSnapshot, directory: null, outputPath: pdfPath);
                lblPdfFileName.Text = Path.GetFileName(currentPdfPath);
                lblPdfGeneratedAt.Text = DateTime.Now.ToString("dd.MM.yyyy HH:mm", Culture);
                UpdatePdfPreview(currentPdfPath);

                bool pdfExists = !string.IsNullOrEmpty(currentPdfPath) && File.Exists(currentPdfPath);
                btnDownloadPdf.IsEnabled = pdfExists;
                btnPrintPdf.IsEnabled = pdfExists;

                string excelPath = Path.Combine(tempDirectory, $"PackingList_{reservation.RezervasyonNo}_{stamp}.xlsx");
                var excelResult = ReportExcelService.GeneratePackingListReport(currentSnapshot, directory: null, outputPath: excelPath);
                currentExcelPath = excelResult.FilePath;
                lblExcelFileName.Text = Path.GetFileName(currentExcelPath);
                lblExcelGeneratedAt.Text = excelResult.GeneratedAt.ToString("dd.MM.yyyy HH:mm", Culture);
                UpdateExcelPreview(excelResult.PreviewHtml);

                bool excelExists = !string.IsNullOrEmpty(currentExcelPath) && File.Exists(currentExcelPath);
                btnDownloadExcel.IsEnabled = excelExists;
            }
            catch (Exception ex)
            {
                pdfBrowser.NavigateToString($"<html><body><p>Packing list oluşturulurken hata oluştu:<br/>{System.Net.WebUtility.HtmlEncode(ex.Message)}</p></body></html>");
                UpdateExcelPreview(null);
                MessageBox.Show($"Packing list yüklenirken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnRefresh.IsEnabled = true;
            }
        }

        private static string FormatDate(DateTime? value)
        {
            return value.HasValue ? value.Value.ToLocalTime().ToString("dd.MM.yyyy", Culture) : "-";
        }

        private void UpdatePdfPreview(string? sourcePath)
        {
            CleanupPreviewFile();

            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
            {
                pdfBrowser.NavigateToString("<html><body><p>PDF dosyası oluşturulamadı.</p></body></html>");
                previewPdfPath = null;
                return;
            }

            try
            {
                string tempPath = Path.Combine(Path.GetTempPath(), $"PackingListPreview_{rezervasyonNo}_{Guid.NewGuid():N}.pdf");
                File.Copy(sourcePath, tempPath, true);
                previewPdfPath = tempPath;
                pdfBrowser.Navigate(new Uri(tempPath));
            }
            catch
            {
                previewPdfPath = sourcePath;
                pdfBrowser.Navigate(new Uri(sourcePath));
            }
        }

        private void UpdateExcelPreview(string? html)
        {
            if (excelBrowser == null)
                return;

            if (string.IsNullOrWhiteSpace(html))
            {
                excelBrowser.NavigateToString("<html><body><p>Excel önizlemesi oluşturulamadı.</p></body></html>");
            }
            else
            {
                excelBrowser.NavigateToString(html);
            }
        }

        private void CleanupPreviewFile()
        {
            if (!string.IsNullOrEmpty(previewPdfPath) && File.Exists(previewPdfPath))
            {
                try
                {
                    File.Delete(previewPdfPath);
                }
                catch
                {
                    // ignore cleanup errors
                }
                finally
                {
                    previewPdfPath = null;
                }
            }
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadPackingListAsync();
        }

        private void BtnDownloadPdf_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(currentPdfPath) || !File.Exists(currentPdfPath))
            {
                MessageBox.Show("İndirilecek bir PDF bulunamadı.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SaveFileDialog dialog = new()
            {
                Filter = "PDF Dosyaları (*.pdf)|*.pdf",
                FileName = Path.GetFileName(currentPdfPath)
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    File.Copy(currentPdfPath, dialog.FileName, true);
                    MessageBox.Show("Packing list PDF dosyası indirildi.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Dosya kaydedilirken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnPrintPdf_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(currentPdfPath) || !File.Exists(currentPdfPath))
            {
                MessageBox.Show("Yazdırılacak bir PDF bulunamadı.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var startInfo = new ProcessStartInfo(currentPdfPath)
                {
                    UseShellExecute = true,
                    Verb = "print",
                    CreateNoWindow = true
                };

                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Yazdırma işlemi başlatılırken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnDownloadExcel_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(currentExcelPath) || !File.Exists(currentExcelPath))
            {
                MessageBox.Show("İndirilecek bir Excel dosyası bulunamadı.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SaveFileDialog dialog = new()
            {
                Filter = "Excel Dosyaları (*.xlsx)|*.xlsx",
                FileName = Path.GetFileName(currentExcelPath)
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    File.Copy(currentExcelPath, dialog.FileName, true);
                    MessageBox.Show("Packing list Excel dosyası indirildi.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Dosya kaydedilirken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            CleanupPreviewFile();
            base.OnClosed(e);
        }
    }
}
