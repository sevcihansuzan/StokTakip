using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Pdf;
using SideBar_Nav.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Win32; // SaveFileDialog

namespace SideBar_Nav.Services
{
    public static class ReportPdfService
    {
        private static readonly CultureInfo Culture = new("tr-TR");
        private static readonly string DefaultDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SideBarNavReports");

        // ---- Helper: XGraphics ile ortalı yazı (XTextFormatter değil!) ----
        private static void DrawCentered(XGraphics gfx, string text, XFont font, double y, double pageWidth)
        {
            gfx.DrawString(text, font, XBrushes.Black,
                new XRect(0, y, pageWidth, 30),
                XStringFormats.TopCenter);
        }

        // ---- Helper: "Farklı Kaydet" penceresini aç ve dosya yolunu al ----
        // directory verilirse o klasörü açar; verilmezse varsayılan klasörü (oluşturur).
        private static string GetSavePath(string suggestedFileName, string? directory)
        {
            // Başlangıç klasörünü hazırla
            string initialDir = EnsureDirectory(directory);

            var dlg = new SaveFileDialog
            {
                Title = "PDF'yi Kaydet",
                Filter = "PDF Dosyası (*.pdf)|*.pdf",
                FileName = suggestedFileName,
                InitialDirectory = initialDir,
                AddExtension = true,
                DefaultExt = ".pdf",
                OverwritePrompt = true,
                CheckPathExists = true
            };

            bool? ok = dlg.ShowDialog();
            if (ok == true && !string.IsNullOrWhiteSpace(dlg.FileName))
                return dlg.FileName;

            throw new OperationCanceledException("PDF kaydetme kullanıcı tarafından iptal edildi.");
        }

        public static string GenerateUrunTakipReport(
            IEnumerable<UrunStok> products,
            string selection,
            string periodDescription,
            string? directory = null)
        {
            var productList = (products ?? Enumerable.Empty<UrunStok>())
                .OrderBy(p => p.UretimTarihi ?? DateTime.MinValue)
                .ThenBy(p => p.EPC)
                .ToList();

            if (!productList.Any())
                throw new InvalidOperationException("Raporlanacak ürün bulunamadı.");

            string fileName = SanitizeFileName($"UrunTakip_{selection}_{DateTime.Now:yyyyMMddHHmmss}.pdf");
            string filePath = GetSavePath(fileName, directory);

            using var document = new PdfDocument();
            document.Info.Title = $"Ürün Takip Raporu - {selection}";

            PdfPage page = CreatePage(document);
            XGraphics gfx = XGraphics.FromPdfPage(page);
            var formatter = new XTextFormatter(gfx);

            double margin = 40;
            double y = margin;

            var titleFont = new XFont("Segoe UI", 18, XFontStyleEx.Bold);
            var infoFont = new XFont("Segoe UI", 10, XFontStyleEx.Regular);
            var headerFont = new XFont("Segoe UI", 9, XFontStyleEx.Bold);
            var cellFont = new XFont("Segoe UI", 9, XFontStyleEx.Regular);
            var detailLabelFont = new XFont("Segoe UI", 8, XFontStyleEx.Bold);
            var borderPen = new XPen(XColors.Gray, 0.5);

            void DrawHeader(bool continuation)
            {
                string text = continuation ? "ÜRÜN TAKİP RAPORU (devam)" : "ÜRÜN TAKİP RAPORU";
                DrawCentered(gfx, text, titleFont, y, page.Width);
                y += 35;

                formatter.DrawString($"Periyot: {selection}", infoFont, XBrushes.Black,
                    new XRect(margin, y, page.Width - 2 * margin, 18));
                y += 18;

                formatter.DrawString($"Tarih Aralığı: {periodDescription}", infoFont, XBrushes.Black,
                    new XRect(margin, y, page.Width - 2 * margin, 18));
                y += 24;
            }

            DrawHeader(false);

            DrawProductDetailBlocks(
                document,
                ref page,
                ref gfx,
                ref formatter,
                ref y,
                margin,
                productList,
                headerFont,
                detailLabelFont,
                cellFont,
                borderPen,
                () => DrawHeader(true),
                "Bu raporda ürün bulunamadı.");

            document.Save(filePath);
            return filePath;
        }

        public static string GenerateRezervasyonReport(
            IEnumerable<UrunRezervasyon> reservations,
            IDictionary<string, List<UrunStok>> productsByReservation,
            string selection,
            string periodDescription,
            string? directory = null,
            string? outputPath = null)
        {
            var reservationList = (reservations ?? Enumerable.Empty<UrunRezervasyon>())
                .OrderBy(r => r.IslemTarihi ?? DateTime.MinValue)
                .ThenBy(r => r.RezervasyonNo)
                .ToList();

            if (!reservationList.Any())
                throw new InvalidOperationException("Raporlanacak rezervasyon bulunamadı.");

            string filePath;
            if (!string.IsNullOrWhiteSpace(outputPath))
            {
                string? targetDirectory = Path.GetDirectoryName(outputPath);
                if (string.IsNullOrWhiteSpace(targetDirectory))
                    throw new ArgumentException("outputPath geçerli bir dizin içermelidir.", nameof(outputPath));

                Directory.CreateDirectory(targetDirectory);
                filePath = outputPath;
            }
            else
            {
                string fileName = SanitizeFileName($"SatisYonetimi_{selection}_{DateTime.Now:yyyyMMddHHmmss}.pdf");
                filePath = GetSavePath(fileName, directory);
            }

            using var document = new PdfDocument();
            document.Info.Title = $"Satış Yönetimi Raporu - {selection}";

            PdfPage page = CreatePage(document);
            XGraphics gfx = XGraphics.FromPdfPage(page);
            var formatter = new XTextFormatter(gfx);

            double margin = 40;
            double y = margin;

            var titleFont = new XFont("Segoe UI", 18, XFontStyleEx.Bold);
            var infoFont = new XFont("Segoe UI", 10, XFontStyleEx.Regular);
            var sectionFont = new XFont("Segoe UI", 12, XFontStyleEx.Bold);
            var labelFont = new XFont("Segoe UI", 9, XFontStyleEx.Bold);
            var valueFont = new XFont("Segoe UI", 9, XFontStyleEx.Regular);
            var headerFont = new XFont("Segoe UI", 9, XFontStyleEx.Bold);
            var cellFont = new XFont("Segoe UI", 9, XFontStyleEx.Regular);
            var detailLabelFont = new XFont("Segoe UI", 8, XFontStyleEx.Bold);
            var borderPen = new XPen(XColors.Gray, 0.5);

            void DrawReportHeader(bool continuation)
            {
                string text = continuation ? "SATIŞ YÖNETİMİ RAPORU (devam)" : "SATIŞ YÖNETİMİ RAPORU";
                DrawCentered(gfx, text, titleFont, y, page.Width);
                y += 35;

                formatter.DrawString($"Periyot: {selection}", infoFont, XBrushes.Black,
                    new XRect(margin, y, page.Width - 2 * margin, 18));
                y += 18;

                formatter.DrawString($"Tarih Aralığı: {periodDescription}", infoFont, XBrushes.Black,
                    new XRect(margin, y, page.Width - 2 * margin, 18));
                y += 24;
            }

            DrawReportHeader(false);

            foreach (var reservation in reservationList)
            {
                string headerText = $"Rezervasyon {reservation.RezervasyonNo ?? "-"} - {reservation.AliciFirma ?? "-"}";

                void DrawReservationHeader(bool continuation)
                {
                    string text = continuation ? headerText + " (devam)" : headerText;
                    formatter.DrawString(text, sectionFont, XBrushes.Black,
                        new XRect(margin, y, page.Width - 2 * margin, 18), XStringFormats.TopLeft);
                    y += 20;
                }

                if (EnsureSpace(ref page, document, ref gfx, ref formatter, ref y, margin, 20))
                {
                    DrawReportHeader(true);
                }

                DrawReservationHeader(false);

                var infoRows = new (string Label, string? Value)[]
                {
                    ("Rezervasyon Kodu", reservation.RezervasyonKodu),
                    ("Rezervasyon Sorumlusu", reservation.RezervasyonSorumlusu),
                    ("Satış Sorumlusu", reservation.SatisSorumlusu),
                    ("Durum", reservation.Durum),
                    ("İşlem Tarihi", FormatDateTime(reservation.IslemTarihi)),
                    ("Ürün Çıkış Tarihi", FormatDateTime(reservation.UrunCikisTarihi))
                };

                foreach (var row in infoRows)
                {
                    if (EnsureSpace(ref page, document, ref gfx, ref formatter, ref y, margin, 16))
                    {
                        DrawReportHeader(true);
                        DrawReservationHeader(true);
                    }

                    DrawKeyValue(formatter, margin, y, page.Width - 2 * margin, row.Label, row.Value, labelFont, valueFont);
                    y += 16;
                }

                if (!string.IsNullOrWhiteSpace(reservation.SevkiyatAdresi))
                {
                    double blockHeight = 36;
                    if (EnsureSpace(ref page, document, ref gfx, ref formatter, ref y, margin, blockHeight))
                    {
                        DrawReportHeader(true);
                        DrawReservationHeader(true);
                    }

                    DrawKeyValue(formatter, margin, y, page.Width - 2 * margin,
                        "Sevkiyat Adresi", reservation.SevkiyatAdresi, labelFont, valueFont, blockHeight);
                    y += blockHeight;
                }

                y += 6;

                void DrawProductsHeader()
                {
                    formatter.DrawString("Ürünler", labelFont, XBrushes.Black,
                        new XRect(margin, y, page.Width - 2 * margin, 16), XStringFormats.TopLeft);
                    y += 18;
                }

                if (EnsureSpace(ref page, document, ref gfx, ref formatter, ref y, margin, 18))
                {
                    DrawReportHeader(true);
                    DrawReservationHeader(true);
                }

                DrawProductsHeader();

                var products = GetProducts(productsByReservation, reservation.RezervasyonNo);

                DrawProductDetailBlocks(
                    document,
                    ref page,
                    ref gfx,
                    ref formatter,
                    ref y,
                    margin,
                    products,
                    headerFont,
                    detailLabelFont,
                    cellFont,
                    borderPen,
                    () =>
                    {
                        DrawReportHeader(true);
                        DrawReservationHeader(true);
                        DrawProductsHeader();
                    },
                    "Bu rezervasyon için ürün bulunamadı.");

                y += 12;
            }

            document.Save(filePath);
            return filePath;
        }

        public static string GenerateStokReport(
            IEnumerable<UrunStok> products,
            string? filterDescription,
            string? outputPath = null)
        {
            var productList = (products ?? Enumerable.Empty<UrunStok>())
                .OrderBy(p => p.EPC)
                .ToList();

            if (!productList.Any())
                throw new InvalidOperationException("Raporlanacak stok bulunamadı.");

            string filePath;
            if (!string.IsNullOrWhiteSpace(outputPath))
            {
                string? directory = Path.GetDirectoryName(outputPath);
                if (string.IsNullOrWhiteSpace(directory))
                    throw new ArgumentException("outputPath geçerli bir dizin içermelidir.", nameof(outputPath));

                Directory.CreateDirectory(directory);
                filePath = outputPath;
            }
            else
            {
                string directory = EnsureDirectory(null);
                string fileName = SanitizeFileName($"StokRaporu_{DateTime.Now:yyyyMMddHHmmss}.pdf");
                filePath = Path.Combine(directory, fileName);
            }

            using var document = new PdfDocument();
            document.Info.Title = "Stok Raporu";

            PdfPage page = CreatePage(document, PdfSharp.PageOrientation.Landscape);
            XGraphics gfx = XGraphics.FromPdfPage(page);
            var formatter = new XTextFormatter(gfx);

            double margin = 30;
            double y = margin;

            var titleFont = new XFont("Segoe UI", 18, XFontStyleEx.Bold);
            var infoFont = new XFont("Segoe UI", 10, XFontStyleEx.Regular);
            var sectionFont = new XFont("Segoe UI", 11, XFontStyleEx.Bold);
            var headerFont = new XFont("Segoe UI", 9, XFontStyleEx.Bold);
            var cellFont = new XFont("Segoe UI", 8, XFontStyleEx.Regular);
            var borderPen = new XPen(XColors.Gray, 0.5);

            DateTime generatedAt = DateTime.Now;

            void DrawHeader(bool continuation)
            {
                string title = continuation ? "STOK RAPORU (devam)" : "STOK RAPORU";
                DrawCentered(gfx, title, titleFont, y, page.Width);
                y += 32;

                formatter.DrawString($"Toplam Kayıt: {productList.Count.ToString("N0", Culture)}", infoFont, XBrushes.Black,
                    new XRect(margin, y, page.Width - 2 * margin, 16), XStringFormats.TopLeft);
                y += 16;

                formatter.DrawString($"Oluşturma: {generatedAt:dd.MM.yyyy HH:mm}", infoFont, XBrushes.Black,
                    new XRect(margin, y, page.Width - 2 * margin, 16), XStringFormats.TopLeft);
                y += 18;

                if (!string.IsNullOrWhiteSpace(filterDescription))
                {
                    double filterHeight = 36;
                    formatter.DrawString("Filtreler: " + filterDescription, infoFont, XBrushes.Black,
                        new XRect(margin, y, page.Width - 2 * margin, filterHeight), XStringFormats.TopLeft);
                    y += filterHeight;
                }

                y += 6;
            }

            DrawHeader(false);

            bool EnsureSpaceWithHeader(double requiredHeight)
            {
                bool newPage = EnsureSpace(ref page, document, ref gfx, ref formatter, ref y, margin, requiredHeight,
                    PdfSharp.PageOrientation.Landscape);

                if (newPage)
                    DrawHeader(true);

                return newPage;
            }

            double rowHeight = 30;

            void DrawRow(string[] headers, double[] widths, string[] values, string continuationTitle)
            {
                if (EnsureSpaceWithHeader(rowHeight * 2))
                {
                    formatter.DrawString(continuationTitle, sectionFont, XBrushes.Black,
                        new XRect(margin, y, page.Width - 2 * margin, 18), XStringFormats.TopLeft);
                    y += 20;
                }

                DrawTableHeader(gfx, headers, widths, headerFont, borderPen, margin, ref y, rowHeight);
                DrawTableRow(gfx, formatter, values, widths, cellFont, borderPen, margin, ref y, rowHeight);
            }

            int index = 1;
            foreach (var product in productList)
            {
                EnsureSpaceWithHeader(20);
                formatter.DrawString($"Ürün {index}", sectionFont, XBrushes.Black,
                    new XRect(margin, y, page.Width - 2 * margin, 18), XStringFormats.TopLeft);
                y += 20;

                string continuationTitle = $"Ürün {index} (devam)";

                string[] row1Headers =
                {
                    "ID", "EPC", "Barkod", "Bandıl", "Plaka", "Ürün Tipi", "Ürün Türü", "Yüzey İşlemi", "Seleksiyon"
                };
                double[] row1Widths = { 35, 120, 90, 70, 60, 85, 85, 85, 85 };
                string[] row1Values =
                {
                    product.ID.ToString(Culture),
                    FormatString(product.EPC),
                    FormatString(product.BarkodNo),
                    FormatString(product.BandilNo),
                    FormatString(product.PlakaNo),
                    FormatString(product.UrunTipi),
                    FormatString(product.UrunTuru),
                    FormatString(product.YuzeyIslemi),
                    FormatString(product.Seleksiyon)
                };
                DrawRow(row1Headers, row1Widths, row1Values, continuationTitle);

                string[] row2Headers =
                {
                    "Üretim Tarihi", "Kalınlık (mm)", "Plaka Adedi", "Stok En (cm)", "Stok Boy (cm)",
                    "Stok Alan (m²)", "Stok Tonaj (ton)", "Durum", "Rezervasyon No"
                };
                double[] row2Widths = { 90, 70, 70, 70, 70, 80, 85, 80, 95 };
                string[] row2Values =
                {
                    FormatDateTime(product.UretimTarihi, includeTime: true),
                    FormatDecimal(product.Kalinlik),
                    FormatInt(product.PlakaAdedi),
                    FormatDecimal(product.StokEn),
                    FormatDecimal(product.StokBoy),
                    FormatDecimal(product.StokAlan),
                    FormatDecimal(product.StokTonaj, 3),
                    FormatString(product.Durum),
                    FormatString(product.RezervasyonNo)
                };
                DrawRow(row2Headers, row2Widths, row2Values, continuationTitle);

                string[] row3Headers =
                {
                    "Satış En (cm)", "Satış Boy (cm)", "Satış Alan (m²)", "Satış Tonaj (ton)",
                    "Kaydeden Personel", "Ürün Çıkış Tarihi", "Alıcı Firma"
                };
                double[] row3Widths = { 70, 70, 80, 85, 140, 95, 140 };
                string[] row3Values =
                {
                    FormatDecimal(product.SatisEn),
                    FormatDecimal(product.SatisBoy),
                    FormatDecimal(product.SatisAlan),
                    FormatDecimal(product.SatisTonaj, 3),
                    FormatString(product.KaydedenPersonel),
                    FormatDateTime(product.UrunCikisTarihi, includeTime: true),
                    FormatString(product.AliciFirma)
                };
                DrawRow(row3Headers, row3Widths, row3Values, continuationTitle);

                y += 8;
                index++;
            }

            document.Save(filePath);
            return filePath;
        }

        public static string GenerateIptalReport(
            IEnumerable<RezIptal> cancellations,
            IDictionary<string, List<RezIptalDetay>> detailsByReservation,
            string selection,
            string periodDescription,
            string? directory = null,
            string? outputPath = null)
        {
            var cancellationList = (cancellations ?? Enumerable.Empty<RezIptal>())
                .OrderBy(c => c.IptalTarihi ?? DateTime.MinValue)
                .ThenBy(c => c.RezervasyonNo)
                .ToList();

            if (!cancellationList.Any())
                throw new InvalidOperationException("Raporlanacak iptal kaydı bulunamadı.");

            string filePath;
            if (!string.IsNullOrWhiteSpace(outputPath))
            {
                string? targetDirectory = Path.GetDirectoryName(outputPath);
                if (string.IsNullOrWhiteSpace(targetDirectory))
                    throw new ArgumentException("outputPath geçerli bir dizin içermelidir.", nameof(outputPath));

                Directory.CreateDirectory(targetDirectory);
                filePath = outputPath;
            }
            else
            {
                string fileName = SanitizeFileName($"Iptal_{selection}_{DateTime.Now:yyyyMMddHHmmss}.pdf");
                filePath = GetSavePath(fileName, directory);
            }

            using var document = new PdfDocument();
            document.Info.Title = $"İptal Raporu - {selection}";

            PdfPage page = CreatePage(document);
            XGraphics gfx = XGraphics.FromPdfPage(page);
            var formatter = new XTextFormatter(gfx);

            double margin = 40;
            double y = margin;

            var titleFont = new XFont("Segoe UI", 18, XFontStyleEx.Bold);
            var infoFont = new XFont("Segoe UI", 10, XFontStyleEx.Regular);
            var sectionFont = new XFont("Segoe UI", 12, XFontStyleEx.Bold);
            var labelFont = new XFont("Segoe UI", 9, XFontStyleEx.Bold);
            var valueFont = new XFont("Segoe UI", 9, XFontStyleEx.Regular);
            var headerFont = new XFont("Segoe UI", 9, XFontStyleEx.Bold);
            var cellFont = new XFont("Segoe UI", 9, XFontStyleEx.Regular);
            var detailLabelFont = new XFont("Segoe UI", 8, XFontStyleEx.Bold);
            var borderPen = new XPen(XColors.Gray, 0.5);

            void DrawReportHeader(bool continuation)
            {
                string text = continuation ? "İPTAL RAPORU (devam)" : "İPTAL RAPORU";
                DrawCentered(gfx, text, titleFont, y, page.Width);
                y += 35;

                formatter.DrawString($"Periyot: {selection}", infoFont, XBrushes.Black,
                    new XRect(margin, y, page.Width - 2 * margin, 18));
                y += 18;

                formatter.DrawString($"Tarih Aralığı: {periodDescription}", infoFont, XBrushes.Black,
                    new XRect(margin, y, page.Width - 2 * margin, 18));
                y += 24;
            }

            DrawReportHeader(false);

            foreach (var cancellation in cancellationList)
            {
                string headerText = $"Rezervasyon {cancellation.RezervasyonNo ?? "-"} - {cancellation.AliciFirma ?? "-"}";

                void DrawCancellationHeader(bool continuation)
                {
                    string text = continuation ? headerText + " (devam)" : headerText;
                    formatter.DrawString(text, sectionFont, XBrushes.Black,
                        new XRect(margin, y, page.Width - 2 * margin, 18), XStringFormats.TopLeft);
                    y += 20;
                }

                if (EnsureSpace(ref page, document, ref gfx, ref formatter, ref y, margin, 20))
                {
                    DrawReportHeader(true);
                }

                DrawCancellationHeader(false);

                var infoRows = new (string Label, string? Value)[]
                {
                    ("Rezervasyon Kodu", cancellation.RezervasyonKodu),
                    ("Satış Sorumlusu", cancellation.SatisSorumlusu),
                    ("Durum", cancellation.Durum),
                    ("İşlem Tarihi", FormatDateTime(cancellation.IslemTarihi)),
                    ("İptal Tarihi", FormatDateTime(cancellation.IptalTarihi)),
                    ("İptal Eden Personel", cancellation.IptalEdenPersonel)
                };

                foreach (var row in infoRows)
                {
                    if (EnsureSpace(ref page, document, ref gfx, ref formatter, ref y, margin, 16))
                    {
                        DrawReportHeader(true);
                        DrawCancellationHeader(true);
                    }

                    DrawKeyValue(formatter, margin, y, page.Width - 2 * margin, row.Label, row.Value, labelFont, valueFont);
                    y += 16;
                }

                if (!string.IsNullOrWhiteSpace(cancellation.IptalSebebi))
                {
                    double blockHeight = 36;
                    if (EnsureSpace(ref page, document, ref gfx, ref formatter, ref y, margin, blockHeight))
                    {
                        DrawReportHeader(true);
                        DrawCancellationHeader(true);
                    }

                    DrawKeyValue(formatter, margin, y, page.Width - 2 * margin,
                        "İptal Sebebi", cancellation.IptalSebebi, labelFont, valueFont, blockHeight);
                    y += blockHeight;
                }

                y += 6;

                void DrawProductsHeader()
                {
                    formatter.DrawString("Ürünler", labelFont, XBrushes.Black,
                        new XRect(margin, y, page.Width - 2 * margin, 16), XStringFormats.TopLeft);
                    y += 18;
                }

                if (EnsureSpace(ref page, document, ref gfx, ref formatter, ref y, margin, 18))
                {
                    DrawReportHeader(true);
                    DrawCancellationHeader(true);
                }

                DrawProductsHeader();

                var details = GetCancelledProducts(detailsByReservation, cancellation.RezervasyonNo);

                DrawProductDetailBlocks(
                    document,
                    ref page,
                    ref gfx,
                    ref formatter,
                    ref y,
                    margin,
                    details.Select(CloneToUrunStok),
                    headerFont,
                    detailLabelFont,
                    cellFont,
                    borderPen,
                    () =>
                    {
                        DrawReportHeader(true);
                        DrawCancellationHeader(true);
                        DrawProductsHeader();
                    },
                    "Bu iptal kaydı için ürün bulunamadı.");

                y += 12;
            }

            document.Save(filePath);
            return filePath;
        }

        private static void DrawTable(
            PdfDocument document,
            ref PdfPage page,
            ref XGraphics gfx,
            ref XTextFormatter formatter,
            double margin,
            ref double y,
            string[] headers,
            double[] columnWidths,
            IReadOnlyList<string[]> rows,
            XFont headerFont,
            XFont cellFont,
            XPen borderPen,
            double rowHeight,
            Action onPageBreak)
        {
            if (rows.Count == 0)
                return;

            double headerHeight = rowHeight;
            double startX = margin;

            if (EnsureSpace(ref page, document, ref gfx, ref formatter, ref y, margin, headerHeight))
            {
                onPageBreak();
            }

            DrawTableHeader(gfx, headers, columnWidths, headerFont, borderPen, startX, ref y, headerHeight);

            foreach (var row in rows)
            {
                if (EnsureSpace(ref page, document, ref gfx, ref formatter, ref y, margin, rowHeight))
                {
                    onPageBreak();
                    DrawTableHeader(gfx, headers, columnWidths, headerFont, borderPen, startX, ref y, headerHeight);
                }

                DrawTableRow(gfx, formatter, row, columnWidths, cellFont, borderPen, startX, ref y, rowHeight);
            }
        }

        private static void DrawKeyValue(
            XTextFormatter formatter,
            double startX,
            double y,
            double width,
            string label,
            string? value,
            XFont labelFont,
            XFont valueFont,
            double height = 16)
        {
            double labelWidth = Math.Min(140, width * 0.35);
            formatter.DrawString(label + ":", labelFont, XBrushes.Black,
                new XRect(startX, y, labelWidth, height), XStringFormats.TopLeft);
            formatter.DrawString(string.IsNullOrWhiteSpace(value) ? "-" : value, valueFont, XBrushes.Black,
                new XRect(startX + labelWidth + 4, y, width - labelWidth - 4, height), XStringFormats.TopLeft);
        }

        private static IList<UrunStok> GetProducts(IDictionary<string, List<UrunStok>> lookup, string? rezervasyonNo)
        {
            if (rezervasyonNo != null && lookup.TryGetValue(rezervasyonNo, out var list))
                return list;

            return Array.Empty<UrunStok>();
        }

        private static IList<RezIptalDetay> GetCancelledProducts(IDictionary<string, List<RezIptalDetay>> lookup, string? rezervasyonNo)
        {
            if (rezervasyonNo != null && lookup.TryGetValue(rezervasyonNo, out var list))
                return list;

            return Array.Empty<RezIptalDetay>();
        }

        private static PdfPage CreatePage(PdfDocument document, PdfSharp.PageOrientation orientation = PdfSharp.PageOrientation.Portrait)
        {
            var page = document.AddPage();
            page.Size = PdfSharp.PageSize.A4;
            page.Orientation = orientation;
            return page;
        }

        private static string EnsureDirectory(string? directory)
        {
            directory ??= DefaultDirectory;
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static string SanitizeFileName(string fileName)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                fileName = fileName.Replace(c, '_');
            return fileName;
        }

        private static void DrawTableHeader(XGraphics gfx, string[] headers, double[] columnWidths,
            XFont font, XPen pen, double startX, ref double y, double height)
        {
            double currentX = startX;
            for (int i = 0; i < headers.Length; i++)
            {
                var rect = new XRect(currentX, y, columnWidths[i], height);
                gfx.DrawRectangle(XBrushes.LightGray, rect);
                gfx.DrawRectangle(pen, rect);
                gfx.DrawString(headers[i], font, XBrushes.Black, rect, XStringFormats.Center);
                currentX += columnWidths[i];
            }

            y += height;
        }

        private static void DrawTableRow(XGraphics gfx, XTextFormatter formatter, string[] values, double[] columnWidths,
            XFont font, XPen pen, double startX, ref double y, double height)
        {
            double currentX = startX;
            for (int i = 0; i < values.Length; i++)
            {
                var rect = new XRect(currentX, y, columnWidths[i], height);
                gfx.DrawRectangle(pen, rect);
                formatter.DrawString(string.IsNullOrWhiteSpace(values[i]) ? "-" : values[i], font, XBrushes.Black,
                    new XRect(rect.X + 3, rect.Y + 4, rect.Width - 6, rect.Height - 8));
                currentX += columnWidths[i];
            }

            y += height;
        }

        private static bool EnsureSpace(ref PdfPage page, PdfDocument document, ref XGraphics gfx,
            ref XTextFormatter formatter, ref double y, double margin, double requiredHeight,
            PdfSharp.PageOrientation orientation = PdfSharp.PageOrientation.Portrait)
        {
            if (y + requiredHeight <= page.Height - margin)
                return false;

            page = CreatePage(document, orientation);
            gfx = XGraphics.FromPdfPage(page);
            formatter = new XTextFormatter(gfx);
            y = margin;
            return true;
        }

        // NEW: Extracted helper to avoid using ref parameters inside a local function
        private static void DrawProductHeaderBlock(
            PdfDocument document,
            ref PdfPage page,
            ref XGraphics gfx,
            ref XTextFormatter formatter,
            ref double y,
            double margin,
            double availableWidth,
            double rowHeight,
            XPen borderPen,
            XFont productHeaderFont,
            string headerTitle,
            Action onNewPage)
        {
            if (EnsureSpace(ref page, document, ref gfx, ref formatter, ref y, margin, rowHeight))
                onNewPage();

            var headerRect = new XRect(margin, y, availableWidth, rowHeight);
            gfx.DrawRectangle(XBrushes.LightGray, headerRect);
            gfx.DrawRectangle(borderPen, headerRect);
            formatter.DrawString(headerTitle, productHeaderFont, XBrushes.Black,
                new XRect(headerRect.X + 4, headerRect.Y + 4, headerRect.Width - 8, headerRect.Height - 8));
            y += rowHeight;
        }

        private static void DrawProductDetailBlocks(
            PdfDocument document,
            ref PdfPage page,
            ref XGraphics gfx,
            ref XTextFormatter formatter,
            ref double y,
            double margin,
            IEnumerable<UrunStok> products,
            XFont productHeaderFont,
            XFont labelFont,
            XFont valueFont,
            XPen borderPen,
            Action onNewPage,
            string emptyMessage)
        {
            var productList = (products ?? Enumerable.Empty<UrunStok>()).ToList();
            int pairsPerRow = 3;
            double availableWidth = page.Width - 2 * margin;
            double pairWidth = availableWidth / pairsPerRow;
            double labelWidth = pairWidth * 0.35;
            double valueWidth = pairWidth - labelWidth;
            double rowHeight = 24;

            if (!productList.Any())
            {
                if (EnsureSpace(ref page, document, ref gfx, ref formatter, ref y, margin, rowHeight))
                    onNewPage();

                var rect = new XRect(margin, y, availableWidth, rowHeight);
                gfx.DrawRectangle(borderPen, rect);
                formatter.DrawString(emptyMessage, valueFont, XBrushes.Black,
                    new XRect(rect.X + 4, rect.Y + 4, rect.Width - 8, rect.Height - 8));
                y += rowHeight + 6;
                return;
            }

            var columns = UrunStokColumnHelper.Columns;

            for (int index = 0; index < productList.Count; index++)
            {
                var item = productList[index];
                string title = $"Ürün #{index + 1}";
                string continuationTitle = title + " (devam)";

                // Header (first)
                DrawProductHeaderBlock(
                    document, ref page, ref gfx, ref formatter, ref y,
                    margin, availableWidth, rowHeight, borderPen, productHeaderFont,
                    title, onNewPage);

                var detailPairs = columns
                    .Select(c => (c.Header, Value: c.TextGetter(item)))
                    .ToList();

                int pairIndex = 0;
                while (pairIndex < detailPairs.Count)
                {
                    if (EnsureSpace(ref page, document, ref gfx, ref formatter, ref y, margin, rowHeight))
                    {
                        onNewPage();
                        DrawProductHeaderBlock(
                            document, ref page, ref gfx, ref formatter, ref y,
                            margin, availableWidth, rowHeight, borderPen, productHeaderFont,
                            continuationTitle, onNewPage);
                    }

                    double currentX = margin;
                    for (int pair = 0; pair < pairsPerRow && pairIndex < detailPairs.Count; pair++)
                    {
                        var (label, value) = detailPairs[pairIndex++];

                        var labelRect = new XRect(currentX, y, labelWidth, rowHeight);
                        gfx.DrawRectangle(XBrushes.LightGray, labelRect);
                        gfx.DrawRectangle(borderPen, labelRect);
                        formatter.DrawString(label + ":", labelFont, XBrushes.Black,
                            new XRect(labelRect.X + 3, labelRect.Y + 3, labelRect.Width - 6, labelRect.Height - 6));

                        var valueRect = new XRect(currentX + labelWidth, y, valueWidth, rowHeight);
                        gfx.DrawRectangle(borderPen, valueRect);
                        formatter.DrawString(string.IsNullOrWhiteSpace(value) ? "-" : value, valueFont, XBrushes.Black,
                            new XRect(valueRect.X + 3, valueRect.Y + 3, valueRect.Width - 6, valueRect.Height - 6));

                        currentX += pairWidth;
                    }

                    y += rowHeight;
                }

                y += 6;
            }
        }

        private static UrunStok CloneToUrunStok(RezIptalDetay detail)
        {
            return new UrunStok
            {
                ID = detail.ID,
                EPC = detail.EPC,
                BarkodNo = detail.BarkodNo,
                BandilNo = detail.BandilNo,
                PlakaNo = detail.PlakaNo,
                UrunTipi = detail.UrunTipi,
                UrunTuru = detail.UrunTuru,
                YuzeyIslemi = detail.YuzeyIslemi,
                Seleksiyon = detail.Seleksiyon,
                UretimTarihi = detail.UretimTarihi,
                Kalinlik = detail.Kalinlik,
                PlakaAdedi = detail.PlakaAdedi,
                StokEn = detail.StokEn,
                StokBoy = detail.StokBoy,
                StokAlan = detail.StokAlan,
                StokTonaj = detail.StokTonaj,
                SatisEn = detail.SatisEn,
                SatisBoy = detail.SatisBoy,
                SatisAlan = detail.SatisAlan,
                SatisTonaj = detail.SatisTonaj,
                Durum = detail.Durum,
                RezervasyonNo = detail.RezervasyonNo,
                KaydedenPersonel = detail.KaydedenPersonel,
                UrunCikisTarihi = detail.UrunCikisTarihi,
                AliciFirma = detail.AliciFirma
            };
        }

        private static string FormatString(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
        }

        private static string FormatInt(int? value)
        {
            return value.HasValue ? value.Value.ToString("N0", Culture) : "-";
        }

        private static string CombineValues(string? first, string? second, string separator = " / ")
        {
            bool hasFirst = !string.IsNullOrWhiteSpace(first);
            bool hasSecond = !string.IsNullOrWhiteSpace(second);

            if (hasFirst && hasSecond)
                return first + separator + second;
            if (hasFirst)
                return first!;
            if (hasSecond)
                return second!;
            return "-";
        }

        private static string FormatDateTime(DateTime? date, bool includeTime = false)
        {
            if (!date.HasValue)
                return "-";

            return includeTime
                ? date.Value.ToString("dd.MM.yyyy HH:mm", Culture)
                : date.Value.ToString("dd.MM.yyyy", Culture);
        }

        private static string FormatDecimal(decimal? value, int decimals = 2)
        {
            if (!value.HasValue)
                return "-";

            return value.Value.ToString($"N{decimals}", Culture);
        }
    }
}
