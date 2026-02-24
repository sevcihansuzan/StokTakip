using Microsoft.Win32;
using SideBar_Nav.Models;
using SideBar_Nav.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Windows;

namespace SideBar_Nav.Windows
{
    public partial class SatisRaporWindow : Window
    {
        private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("tr-TR");

        private readonly IReadOnlyList<UrunRezervasyon> reservations;
        private readonly IDictionary<string, List<UrunStok>> productsByReservation;
        private readonly string selection;
        private readonly string periodDescription;
        private readonly string? filterDescription;

        private string? currentPdfPath;
        private string? currentExcelPath;
        private string? previewPdfPath;

        public SatisRaporWindow(
            IReadOnlyList<UrunRezervasyon> reservations,
            IDictionary<string, List<UrunStok>> productsByReservation,
            string selection,
            string periodDescription,
            string? filterDescription)
        {
            InitializeComponent();
            this.reservations = reservations;
            this.productsByReservation = productsByReservation;
            this.selection = selection;
            this.periodDescription = periodDescription;
            this.filterDescription = filterDescription;
            Loaded += SatisRaporWindow_Loaded;
        }

        private void SatisRaporWindow_Loaded(object sender, RoutedEventArgs e)
        {
            lblRecordCount.Text = reservations.Count.ToString("N0", Culture);
            lblSelection.Text = selection;
            lblPeriod.Text = periodDescription;
            lblFilters.Text = string.IsNullOrWhiteSpace(filterDescription)
                ? "Filtre uygulanmadı"
                : filterDescription;

            GenerateReport();
        }

        private void GenerateReport()
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
                string tempDirectory = Path.Combine(Path.GetTempPath(), "SideBarNavSatis");
                Directory.CreateDirectory(tempDirectory);

                string stamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");

                string pdfPath = Path.Combine(tempDirectory, $"SatisRaporu_{stamp}.pdf");
                currentPdfPath = ReportPdfService.GenerateRezervasyonReport(
                    reservations,
                    productsByReservation,
                    selection,
                    periodDescription,
                    directory: null,
                    outputPath: pdfPath);

                lblPdfFileName.Text = Path.GetFileName(currentPdfPath);
                lblPdfGeneratedAt.Text = DateTime.Now.ToString("dd.MM.yyyy HH:mm", Culture);

                UpdatePdfPreview(currentPdfPath);

                bool pdfExists = !string.IsNullOrEmpty(currentPdfPath) && File.Exists(currentPdfPath);
                btnDownloadPdf.IsEnabled = pdfExists;
                btnPrintPdf.IsEnabled = pdfExists;

                string excelPath = Path.Combine(tempDirectory, $"SatisRaporu_{stamp}.xlsx");
                var excelResult = ReportExcelService.GenerateRezervasyonReport(
                    reservations,
                    productsByReservation,
                    selection,
                    periodDescription,
                    filterDescription,
                    directory: null,
                    outputPath: excelPath);

                currentExcelPath = excelResult.FilePath;
                lblExcelFileName.Text = Path.GetFileName(currentExcelPath);
                lblExcelGeneratedAt.Text = excelResult.GeneratedAt.ToString("dd.MM.yyyy HH:mm", Culture);
                UpdateExcelPreview(excelResult.PreviewHtml);

                bool excelExists = !string.IsNullOrEmpty(currentExcelPath) && File.Exists(currentExcelPath);
                btnDownloadExcel.IsEnabled = excelExists;
            }
            catch (Exception ex)
            {
                pdfBrowser.NavigateToString($"<html><body><p>Satış raporu oluşturulurken hata oluştu:<br/>{WebUtility.HtmlEncode(ex.Message)}</p></body></html>");
                UpdateExcelPreview(null);
                MessageBox.Show($"Satış raporu oluşturulurken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnRefresh.IsEnabled = true;
            }
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
                string tempPath = Path.Combine(Path.GetTempPath(), $"SatisRaporPreview_{Guid.NewGuid():N}.pdf");
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
                    // ignore
                }
                finally
                {
                    previewPdfPath = null;
                }
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            GenerateReport();
        }

        private void BtnDownloadPdf_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(currentPdfPath) || !File.Exists(currentPdfPath))
            {
                MessageBox.Show("İndirilecek bir PDF bulunamadı.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "PDF Dosyaları (*.pdf)|*.pdf",
                FileName = Path.GetFileName(currentPdfPath)
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    File.Copy(currentPdfPath, dialog.FileName, true);
                    MessageBox.Show("Satış raporu PDF dosyası kaydedildi.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
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

            var dialog = new SaveFileDialog
            {
                Filter = "Excel Dosyaları (*.xlsx)|*.xlsx",
                FileName = Path.GetFileName(currentExcelPath)
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    File.Copy(currentExcelPath, dialog.FileName, true);
                    MessageBox.Show("Satış raporu Excel dosyası kaydedildi.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
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
