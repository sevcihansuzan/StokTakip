using ClosedXML.Excel;
using Microsoft.Win32;
using SideBar_Nav.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace SideBar_Nav.Services
{
    public static class ReportExcelService
    {
        public record ExcelReportResult(string FilePath, string? PreviewHtml, DateTime GeneratedAt);

        private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("tr-TR");
        private static readonly string DefaultDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SideBarNavReports");

        private static readonly IReadOnlyList<UrunStokColumnHelper.Column> ProductColumns =
            UrunStokColumnHelper.Columns;

        public static ExcelReportResult GenerateUrunTakipReport(
            IEnumerable<UrunStok> products,
            string selection,
            string periodDescription,
            string? directory = null,
            string? outputPath = null,
            bool includePreview = true)
        {
            var productList = (products ?? Enumerable.Empty<UrunStok>())
                .OrderBy(p => p.UretimTarihi ?? DateTime.MinValue)
                .ThenBy(p => p.EPC)
                .ToList();

            if (!productList.Any())
                throw new InvalidOperationException("Raporlanacak ürün bulunamadı.");

            string filePath = ResolveFilePath(outputPath, directory,
                $"UrunTakip_{selection}_{DateTime.Now:yyyyMMddHHmmss}.xlsx", allowDialog: true);

            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.Worksheets.Add("Ürün Takip");

                WriteUrunStokReportSheet(
                    sheet,
                    title: "Ürün Takip Raporu",
                    summaryRows: new (string Label, string? Value)[]
                    {
                        ("Periyot", selection),
                        ("Tarih Aralığı", periodDescription),
                        ("Toplam Ürün", productList.Count.ToString("N0", Culture))
                    },
                    products: productList,
                    tableTitle: "Ürün Listesi");

                workbook.SaveAs(filePath);
            }

            string? previewHtml = includePreview
                ? BuildSimpleTableHtml(
                    title: $"Ürün Takip Raporu ({selection})",
                    descriptionLines: new[]
                    {
                        $"Tarih Aralığı: {WebUtility.HtmlEncode(periodDescription)}",
                        $"Toplam Ürün: {productList.Count.ToString("N0", Culture)}"
                    },
                    headers: ProductColumns.Select(c => c.Header).ToArray(),
                    rows: productList
                        .Take(50)
                        .Select(p => ProductColumns
                            .Select(c => HtmlEncode(c.TextGetter(p)))
                            .ToArray()),
                    totalRowCount: productList.Count)
                : null;

            return new ExcelReportResult(filePath, previewHtml, DateTime.Now);
        }

        public static ExcelReportResult GenerateRezervasyonReport(
            IReadOnlyList<UrunRezervasyon> reservations,
            IDictionary<string, List<UrunStok>> productsByReservation,
            string selection,
            string periodDescription,
            string? filterDescription,
            string? directory = null,
            string? outputPath = null)
        {
            var reservationList = (reservations ?? Array.Empty<UrunRezervasyon>())
                .OrderBy(r => r.IslemTarihi ?? DateTime.MinValue)
                .ThenBy(r => r.RezervasyonNo)
                .ToList();

            if (!reservationList.Any())
                throw new InvalidOperationException("Raporlanacak rezervasyon bulunamadı.");

            string filePath = ResolveFilePath(outputPath, directory,
                $"SatisYonetimi_{selection}_{DateTime.Now:yyyyMMddHHmmss}.xlsx", allowDialog: false);

            using (var workbook = new XLWorkbook())
            {
                var orderedProducts = new List<UrunStok>();
                foreach (var reservation in reservationList)
                {
                    if (reservation.RezervasyonNo != null &&
                        productsByReservation.TryGetValue(reservation.RezervasyonNo, out var items) &&
                        items != null)
                    {
                        orderedProducts.AddRange(items);
                    }
                }

                var summaryRows = new List<(string Label, string? Value)>
                {
                    ("Periyot", selection),
                    ("Tarih Aralığı", periodDescription),
                    ("Toplam Rezervasyon", reservationList.Count.ToString("N0", Culture))
                };

                if (!string.IsNullOrWhiteSpace(filterDescription))
                    summaryRows.Add(("Filtreler", filterDescription));

                var summarySheet = workbook.Worksheets.Add("Satış Raporu");
                WriteUrunStokReportSheet(
                    summarySheet,
                    title: "Satış Yönetimi Raporu",
                    summaryRows: summaryRows,
                    products: orderedProducts,
                    tableTitle: "Ürün Listesi");

                var reservationSheet = workbook.Worksheets.Add("Rezervasyonlar");
                string[] reservationHeaders =
                {
                    "Rezervasyon No",
                    "Rezervasyon Kodu",
                    "Alıcı Firma",
                    "Rezervasyon Sorumlusu",
                    "Satış Sorumlusu",
                    "Durum",
                    "İşlem Tarihi",
                    "Ürün Çıkış Tarihi"
                };

                for (int i = 0; i < reservationHeaders.Length; i++)
                    reservationSheet.Cell(1, i + 1).Value = reservationHeaders[i];

                for (int i = 0; i < reservationList.Count; i++)
                {
                    var reservation = reservationList[i];
                    int row = i + 2;
                    reservationSheet.Cell(row, 1).Value = reservation.RezervasyonNo;
                    reservationSheet.Cell(row, 2).Value = reservation.RezervasyonKodu;
                    reservationSheet.Cell(row, 3).Value = reservation.AliciFirma;
                    reservationSheet.Cell(row, 4).Value = reservation.RezervasyonSorumlusu;
                    reservationSheet.Cell(row, 5).Value = reservation.SatisSorumlusu;
                    reservationSheet.Cell(row, 6).Value = reservation.Durum;
                    reservationSheet.Cell(row, 7).Value = FormatDate(reservation.IslemTarihi, includeTime: true);
                    reservationSheet.Cell(row, 8).Value = FormatDate(reservation.UrunCikisTarihi, includeTime: true);
                }

                reservationSheet.Columns().AdjustToContents();

                workbook.SaveAs(filePath);
            }

            string? previewHtml = BuildRezervasyonPreviewHtml(
                reservationList,
                productsByReservation,
                selection,
                periodDescription,
                filterDescription);

            return new ExcelReportResult(filePath, previewHtml, DateTime.Now);
        }

        public static ExcelReportResult GenerateIptalReport(
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

            string filePath = ResolveFilePath(outputPath, directory,
                $"Iptal_{selection}_{DateTime.Now:yyyyMMddHHmmss}.xlsx", allowDialog: false);

            using (var workbook = new XLWorkbook())
            {
                var cancelledProducts = new List<UrunStok>();
                foreach (var cancellation in cancellationList)
                {
                    if (cancellation.RezervasyonNo != null &&
                        detailsByReservation.TryGetValue(cancellation.RezervasyonNo, out var details) &&
                        details != null)
                    {
                        cancelledProducts.AddRange(details.Select(CloneToUrunStok));
                    }
                }

                var summarySheet = workbook.Worksheets.Add("İptal Raporu");
                WriteUrunStokReportSheet(
                    summarySheet,
                    title: "İptal Raporu",
                    summaryRows: new (string Label, string? Value)[]
                    {
                        ("Periyot", selection),
                        ("Tarih Aralığı", periodDescription),
                        ("Toplam İptal", cancellationList.Count.ToString("N0", Culture))
                    },
                    products: cancelledProducts,
                    tableTitle: "İptal Edilen Ürünler");

                var cancellationSheet = workbook.Worksheets.Add("İptaller");
                string[] cancellationHeaders =
                {
                    "Rezervasyon No",
                    "Rezervasyon Kodu",
                    "Alıcı Firma",
                    "Satış Sorumlusu",
                    "Durum",
                    "İşlem Tarihi",
                    "İptal Tarihi",
                    "İptal Eden",
                    "İptal Sebebi"
                };

                for (int i = 0; i < cancellationHeaders.Length; i++)
                    cancellationSheet.Cell(1, i + 1).Value = cancellationHeaders[i];

                for (int i = 0; i < cancellationList.Count; i++)
                {
                    var cancellation = cancellationList[i];
                    int row = i + 2;
                    cancellationSheet.Cell(row, 1).Value = cancellation.RezervasyonNo;
                    cancellationSheet.Cell(row, 2).Value = cancellation.RezervasyonKodu;
                    cancellationSheet.Cell(row, 3).Value = cancellation.AliciFirma;
                    cancellationSheet.Cell(row, 4).Value = cancellation.SatisSorumlusu;
                    cancellationSheet.Cell(row, 5).Value = cancellation.Durum;
                    cancellationSheet.Cell(row, 6).Value = FormatDate(cancellation.IslemTarihi, includeTime: true);
                    cancellationSheet.Cell(row, 7).Value = FormatDate(cancellation.IptalTarihi, includeTime: true);
                    cancellationSheet.Cell(row, 8).Value = cancellation.IptalEdenPersonel;
                    cancellationSheet.Cell(row, 9).Value = cancellation.IptalSebebi;
                }

                cancellationSheet.Columns().AdjustToContents();

                workbook.SaveAs(filePath);
            }

            string? previewHtml = BuildIptalPreviewHtml(
                cancellationList,
                detailsByReservation,
                selection,
                periodDescription);

            return new ExcelReportResult(filePath, previewHtml, DateTime.Now);
        }

        public static ExcelReportResult GenerateStokReport(
            IEnumerable<UrunStok> products,
            string? filterDescription,
            string? directory = null,
            string? outputPath = null)
        {
            var productList = (products ?? Enumerable.Empty<UrunStok>())
                .OrderBy(p => p.EPC)
                .ToList();

            if (!productList.Any())
                throw new InvalidOperationException("Raporlanacak stok bulunamadı.");

            string filePath = ResolveFilePath(outputPath, directory,
                $"StokRaporu_{DateTime.Now:yyyyMMddHHmmss}.xlsx", allowDialog: false);

            using (var workbook = new XLWorkbook())
            {
                var summaryRows = new List<(string Label, string? Value)>
                {
                    ("Toplam Kayıt", productList.Count.ToString("N0", Culture))
                };

                if (!string.IsNullOrWhiteSpace(filterDescription))
                    summaryRows.Add(("Filtreler", filterDescription));

                var sheet = workbook.Worksheets.Add("Stok Raporu");
                WriteUrunStokReportSheet(
                    sheet,
                    title: "Stok Raporu",
                    summaryRows: summaryRows,
                    products: productList,
                    tableTitle: "Stok Listesi");

                workbook.SaveAs(filePath);
            }

            string? previewHtml = BuildSimpleTableHtml(
                title: "Stok Raporu",
                descriptionLines: new[]
                {
                    $"Toplam Kayıt: {productList.Count.ToString("N0", Culture)}",
                    string.IsNullOrWhiteSpace(filterDescription) ? "" : $"Filtreler: {HtmlEncode(filterDescription)}"
                }.Where(line => !string.IsNullOrEmpty(line)),
                headers: ProductColumns.Select(c => c.Header).ToArray(),
                rows: productList
                    .Take(50)
                    .Select(p => ProductColumns
                        .Select(c => HtmlEncode(c.TextGetter(p)))
                        .ToArray()),
                totalRowCount: productList.Count);

            return new ExcelReportResult(filePath, previewHtml, DateTime.Now);
        }

        public static ExcelReportResult GeneratePackingListReport(
            PackingListService.PackingListSnapshot snapshot,
            string? directory = null,
            string? outputPath = null)
        {
            if (snapshot.Reservation == null)
                throw new InvalidOperationException("Rezervasyon bilgisi eksik.");

            string filePath = ResolveFilePath(
                outputPath,
                directory,
                $"PackingList_{snapshot.Reservation.RezervasyonNo}_{DateTime.Now:yyyyMMddHHmmss}.xlsx",
                allowDialog: false);

            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.Worksheets.Add("Packing List");
                int currentRow = 1;

                sheet.Cell(currentRow, 1).SetValue("PACKING LIST");
                sheet.Range(currentRow, 1, currentRow, 9).Merge();
                sheet.Cell(currentRow, 1).Style.Font.Bold = true;
                sheet.Cell(currentRow, 1).Style.Font.FontSize = 16;
                sheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                sheet.Cell(currentRow, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                currentRow += 2;

                if (snapshot.Reservation != null)
                {
                    var reservation = snapshot.Reservation;
                    var infoPairs = new (string Label, string? Value)[]
                    {
                ("Rezervasyon No", reservation.RezervasyonNo),
                ("Rezervasyon Kodu", reservation.RezervasyonKodu),
                ("Alıcı Firma", reservation.AliciFirma),
                ("Rezervasyon Sorumlusu", reservation.RezervasyonSorumlusu),
                ("Satış Sorumlusu", reservation.SatisSorumlusu),
                ("İşlem Tarihi", FormatDate(reservation.IslemTarihi)),
                ("Ürün Çıkış Tarihi", FormatDate(reservation.UrunCikisTarihi)),
                ("Durum", reservation.Durum)
                    };

                    int half = (infoPairs.Length + 1) / 2;
                    for (int i = 0; i < half; i++)
                    {
                        WriteInfoRow(sheet, currentRow + i, 1, infoPairs[i]);
                        if (i + half < infoPairs.Length)
                            WriteInfoRow(sheet, currentRow + i, 4, infoPairs[i + half]);
                    }

                    currentRow += half;

                    if (!string.IsNullOrWhiteSpace(reservation.SevkiyatAdresi))
                    {
                        currentRow++;
                        sheet.Cell(currentRow, 1).SetValue("Sevkiyat Adresi");
                        sheet.Cell(currentRow, 1).Style.Font.Bold = true;
                        sheet.Range(currentRow, 2, currentRow, 9).Merge();
                        sheet.Cell(currentRow, 2).SetValue(reservation.SevkiyatAdresi);
                        sheet.Cell(currentRow, 2).Style.Alignment.WrapText = true;
                        currentRow++;
                    }
                }

                currentRow++;

                var totals = new (string Label, string Value)[]
                {
            ("Toplam Ürün", snapshot.Items.Count.ToString("N0", Culture)),
            ("Toplam Plaka Adedi", snapshot.TotalPlateCount.ToString("N0", Culture)),
            ("Toplam Satış Alanı", FormatDecimal(snapshot.TotalArea)),
            ("Toplam Satış Tonajı", FormatDecimal(snapshot.TotalTonaj, 3))
                };

                int totalsRow = currentRow;
                for (int i = 0; i < totals.Length; i++)
                {
                    int column = 1 + (i * 2);
                    sheet.Cell(totalsRow, column).SetValue(totals[i].Label);
                    sheet.Cell(totalsRow, column).Style.Font.Bold = true;
                    sheet.Cell(totalsRow, column + 1).SetValue(totals[i].Value);
                }

                currentRow = totalsRow + 2;

                var headers = new[] { "No" }
                    .Concat(ProductColumns.Select(c => c.Header))
                    .ToArray();

                for (int i = 0; i < headers.Length; i++)
                    sheet.Cell(currentRow, i + 1).SetValue(headers[i]);

                sheet.Range(currentRow, 1, currentRow, headers.Length).Style.Font.Bold = true;
                sheet.Range(currentRow, 1, currentRow, headers.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#E5E7EB");
                sheet.Range(currentRow, 1, currentRow, headers.Length).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                currentRow++;

                if (snapshot.Items.Any())
                {
                    int index = 1;
                    foreach (var item in snapshot.Items)
                    {
                        var indexCell = sheet.Cell(currentRow, 1);
                        indexCell.SetValue(index);
                        indexCell.Style.NumberFormat.Format = "#,##0";

                        for (int col = 0; col < ProductColumns.Count; col++)
                        {
                            var column = ProductColumns[col];
                            var targetCell = sheet.Cell(currentRow, col + 2);
                            var value = column.ValueGetter(item);     // object?
                            var displayText = column.TextGetter(item);// string

                            WriteCell(targetCell, value, displayText, column.NumberFormat);
                        }

                        currentRow++;
                        index++;
                    }
                }
                else
                {
                    sheet.Cell(currentRow, 1).SetValue("Veri bulunamadı.");
                    sheet.Range(currentRow, 1, currentRow, headers.Length).Merge();
                    sheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    currentRow++;
                }

                sheet.Columns().AdjustToContents();
                sheet.Rows().AdjustToContents();

                workbook.SaveAs(filePath);

                // --- local helpers ---
                void WriteInfoRow(IXLWorksheet worksheet, int rowIndex, int labelColumn, (string Label, string? Value) entry)
                {
                    worksheet.Cell(rowIndex, labelColumn).SetValue(entry.Label);
                    worksheet.Cell(rowIndex, labelColumn).Style.Font.Bold = true;
                    worksheet.Cell(rowIndex, labelColumn + 1).SetValue(
                        string.IsNullOrWhiteSpace(entry.Value) ? "-" : entry.Value);
                }

                static void WriteCell(IXLCell cell, object? value, string? displayText, string? numberFormat)
                {
                    // Tür-güvenli yazım
                    if (value is null)
                    {
                        cell.SetValue(displayText ?? string.Empty);
                    }
                    else
                    {
                        switch (value)
                        {
                            case int v: cell.SetValue(v); break;
                            case long v: cell.SetValue(v); break;
                            case float v: cell.SetValue(v); break;
                            case double v: cell.SetValue(v); break;
                            case decimal v: cell.SetValue(v); break;
                            case bool v: cell.SetValue(v); break;
                            case DateTime v: cell.SetValue(v); break;
                            case TimeSpan v: cell.SetValue(v); break;
                            default:
                                cell.SetValue(value.ToString());
                                break;
                        }
                    }

                    if (!string.IsNullOrEmpty(numberFormat))
                        cell.Style.NumberFormat.Format = numberFormat;
                }
            }

            string? previewHtml = BuildPackingListPreviewHtml(snapshot);
            return new ExcelReportResult(filePath, previewHtml, DateTime.Now);
        }



        private static void WriteUrunStokReportSheet(
            IXLWorksheet sheet,
            string title,
            IEnumerable<(string Label, string? Value)> summaryRows,
            IReadOnlyList<UrunStok> products,
            string tableTitle,
            bool includeIndexColumn = true)
        {
            if (sheet is null)
                throw new ArgumentNullException(nameof(sheet));
            if (summaryRows is null)
                throw new ArgumentNullException(nameof(summaryRows));
            if (products is null)
                throw new ArgumentNullException(nameof(products));

            int columnCount = includeIndexColumn ? ProductColumns.Count + 1 : ProductColumns.Count;
            int currentRow = 1;

            sheet.Cell(currentRow, 1).SetValue(title);
            sheet.Cell(currentRow, 1).Style.Font.Bold = true;
            sheet.Cell(currentRow, 1).Style.Font.FontSize = 14;
            sheet.Range(currentRow, 1, currentRow, Math.Max(2, columnCount)).Merge();
            sheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

            currentRow += 2;
            currentRow = WriteInfoRows(sheet, currentRow, summaryRows);
            currentRow++;

            WriteUrunStokTable(sheet, ref currentRow, products, tableTitle, includeIndexColumn);

            sheet.Columns().AdjustToContents();
            sheet.Rows().AdjustToContents();
        }

        private static int WriteInfoRows(
            IXLWorksheet sheet,
            int startRow,
            IEnumerable<(string Label, string? Value)> rows)
        {
            if (sheet is null)
                throw new ArgumentNullException(nameof(sheet));
            if (rows is null)
                throw new ArgumentNullException(nameof(rows));

            int row = startRow;

            foreach (var (label, value) in rows)
            {
                if (string.IsNullOrWhiteSpace(label))
                    continue;

                sheet.Cell(row, 1).SetValue(label);
                sheet.Cell(row, 1).Style.Font.Bold = true;
                sheet.Cell(row, 2).SetValue(string.IsNullOrWhiteSpace(value) ? "-" : value);
                row++;
            }

            return row;
        }

        private static void WriteUrunStokTable(
            IXLWorksheet sheet,
            ref int currentRow,
            IReadOnlyList<UrunStok> products,
            string? tableTitle,
            bool includeIndexColumn)
        {
            if (sheet is null)
                throw new ArgumentNullException(nameof(sheet));
            if (products is null)
                throw new ArgumentNullException(nameof(products));

            if (!string.IsNullOrWhiteSpace(tableTitle))
            {
                sheet.Cell(currentRow, 1).SetValue(tableTitle);
                sheet.Cell(currentRow, 1).Style.Font.Bold = true;
                currentRow++;
            }

            var headers = includeIndexColumn
                ? new[] { "No" }.Concat(ProductColumns.Select(c => c.Header)).ToArray()
                : ProductColumns.Select(c => c.Header).ToArray();

            for (int i = 0; i < headers.Length; i++)
                sheet.Cell(currentRow, i + 1).SetValue(headers[i]);

            var headerRange = sheet.Range(currentRow, 1, currentRow, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#E5E7EB");
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            currentRow++;

            if (products.Count == 0)
            {
                sheet.Cell(currentRow, 1).SetValue("Veri bulunamadı.");
                sheet.Range(currentRow, 1, currentRow, headers.Length).Merge();
                sheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                currentRow++;
                return;
            }

            int index = 1;
            foreach (var product in products)
            {
                int columnOffset = 0;

                if (includeIndexColumn)
                {
                    var indexCell = sheet.Cell(currentRow, 1);
                    indexCell.SetValue(index);
                    indexCell.Style.NumberFormat.Format = "#,##0";
                    columnOffset = 1;
                }

                for (int colIndex = 0; colIndex < ProductColumns.Count; colIndex++)
                {
                    var column = ProductColumns[colIndex];
                    var cell = sheet.Cell(currentRow, colIndex + 1 + columnOffset);

                    var value = column.ValueGetter(product);
                    var text = column.TextGetter(product);

                    WriteCell(cell, value, text, column.NumberFormat);
                }

                currentRow++;
                index++;
            }
        }

        /// <summary>
        /// Tür-güvenli hücre yazımı. (Sınıfta zaten varsa bu kopyayı kaldırıp mevcut olanı kullanın.)
        /// </summary>
        private static void WriteCell(IXLCell cell, object? value, string? displayText, string? numberFormat)
        {
            if (value is null)
            {
                cell.SetValue(displayText ?? string.Empty);
            }
            else
            {
                switch (value)
                {
                    case int v: cell.SetValue(v); break;
                    case long v: cell.SetValue(v); break;
                    case float v: cell.SetValue(v); break;
                    case double v: cell.SetValue(v); break;
                    case decimal v: cell.SetValue(v); break;
                    case bool v: cell.SetValue(v); break;
                    case DateTime v: cell.SetValue(v); break;
                    case TimeSpan v: cell.SetValue(v); break;
                    default:
                        cell.SetValue(value.ToString());
                        break;
                }
            }

            if (!string.IsNullOrEmpty(numberFormat))
                cell.Style.NumberFormat.Format = numberFormat;
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

        private static string ResolveFilePath(string? outputPath, string? directory, string suggestedFileName, bool allowDialog)
        {
            if (!string.IsNullOrWhiteSpace(outputPath))
            {
                string? dir = Path.GetDirectoryName(outputPath);
                if (string.IsNullOrWhiteSpace(dir))
                    throw new ArgumentException("outputPath geçerli bir dizin içermelidir.", nameof(outputPath));

                Directory.CreateDirectory(dir);
                return outputPath;
            }

            if (allowDialog)
            {
                return GetSavePath(suggestedFileName, directory);
            }

            string targetDirectory = EnsureDirectory(directory);
            string sanitized = SanitizeFileName(suggestedFileName);
            return Path.Combine(targetDirectory, sanitized);
        }

        private static string GetSavePath(string suggestedFileName, string? directory)
        {
            string initialDir = EnsureDirectory(directory);
            var dlg = new SaveFileDialog
            {
                Title = "Excel'i Kaydet",
                Filter = "Excel Dosyası (*.xlsx)|*.xlsx",
                FileName = SanitizeFileName(suggestedFileName),
                InitialDirectory = initialDir,
                AddExtension = true,
                DefaultExt = ".xlsx",
                OverwritePrompt = true,
                CheckPathExists = true
            };

            bool? ok = dlg.ShowDialog();
            if (ok == true && !string.IsNullOrWhiteSpace(dlg.FileName))
                return dlg.FileName;

            throw new OperationCanceledException("Excel kaydetme kullanıcı tarafından iptal edildi.");
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

        private static string FormatDate(DateTime? value, bool includeTime = false)
        {
            if (!value.HasValue)
                return "-";

            return includeTime
                ? value.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm", Culture)
                : value.Value.ToLocalTime().ToString("dd.MM.yyyy", Culture);
        }

        private static string HtmlEncode(string? value)
            => WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(value) ? "-" : value);

        private static string BuildSimpleTableHtml(
            string title,
            IEnumerable<string> descriptionLines,
            string[] headers,
            IEnumerable<string[]> rows,
            int? totalRowCount = null)
        {
            var sb = new StringBuilder();
            sb.Append("<html><head><meta charset=\"utf-8\"/><style>");
            sb.Append("body{font-family:'Segoe UI',sans-serif;margin:20px;}h1{font-size:20px;}");
            sb.Append(".meta{margin-bottom:6px;color:#374151;}");
            sb.Append(".table-wrapper{overflow-x:auto;margin-top:12px;}");
            sb.Append("table{border-collapse:collapse;min-width:100%;white-space:nowrap;}th,td{border:1px solid #d1d5db;padding:6px;text-align:left;}th{background:#e5e7eb;}");
            sb.Append("</style></head><body>");
            sb.AppendFormat("<h1>{0}</h1>", WebUtility.HtmlEncode(title));

            foreach (var line in descriptionLines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                    sb.AppendFormat("<div class='meta'>{0}</div>", line);
            }

            sb.Append("<div class='table-wrapper'><table><thead><tr>");
            foreach (var header in headers)
                sb.AppendFormat("<th>{0}</th>", WebUtility.HtmlEncode(header));
            sb.Append("</tr></thead><tbody>");

            int count = 0;
            foreach (var row in rows)
            {
                sb.Append("<tr>");
                foreach (var cell in row)
                    sb.AppendFormat("<td>{0}</td>", cell);
                sb.Append("</tr>");
                count++;
            }

            if (count == 0)
                sb.Append("<tr><td colspan=\"" + headers.Length + "\">Veri bulunamadı.</td></tr>");

            sb.Append("</tbody></table></div>");

            if (totalRowCount.HasValue && totalRowCount.Value > count)
            {
                sb.AppendFormat(
                    "<div class='meta'>Toplam {0} satır var. Önizlemede ilk {1} gösterilmektedir.</div>",
                    totalRowCount.Value.ToString("N0", Culture),
                    count.ToString("N0", Culture));
            }

            sb.Append("</body></html>");
            return sb.ToString();
        }

        private static string BuildRezervasyonPreviewHtml(
            IReadOnlyList<UrunRezervasyon> reservations,
            IDictionary<string, List<UrunStok>> productsByReservation,
            string selection,
            string periodDescription,
            string? filterDescription)
        {
            var sb = new StringBuilder();
            sb.Append("<html><head><meta charset=\"utf-8\"/><style>");
            sb.Append("body{font-family:'Segoe UI',sans-serif;margin:20px;}h1{font-size:20px;margin-bottom:6px;}h2{font-size:16px;margin-top:20px;margin-bottom:6px;}");
            sb.Append("table{border-collapse:collapse;min-width:100%;white-space:nowrap;}th,td{border:1px solid #d1d5db;padding:6px;text-align:left;}th{background:#e5e7eb;}");
            sb.Append(".meta{margin-bottom:10px;color:#374151;}.table-wrapper{overflow-x:auto;margin-top:8px;}");
            sb.Append("</style></head><body>");
            sb.AppendFormat("<h1>Satış Yönetimi Raporu ({0})</h1>", WebUtility.HtmlEncode(selection));
            sb.AppendFormat("<div class='meta'>Tarih Aralığı: {0}</div>", WebUtility.HtmlEncode(periodDescription));
            if (!string.IsNullOrWhiteSpace(filterDescription))
                sb.AppendFormat("<div class='meta'>Filtreler: {0}</div>", WebUtility.HtmlEncode(filterDescription));
            sb.AppendFormat("<div class='meta'>Toplam Rezervasyon: {0}</div>", reservations.Count.ToString("N0", Culture));

            foreach (var reservation in reservations)
            {
                sb.AppendFormat("<h2>{0} - {1}</h2>",
                    WebUtility.HtmlEncode(reservation.RezervasyonNo ?? "-"),
                    WebUtility.HtmlEncode(reservation.AliciFirma ?? "-"));
                sb.Append("<table><tbody>");
                AppendMetaRow(sb, "Rezervasyon Kodu", reservation.RezervasyonKodu);
                AppendMetaRow(sb, "Rezervasyon Sorumlusu", reservation.RezervasyonSorumlusu);
                AppendMetaRow(sb, "Satış Sorumlusu", reservation.SatisSorumlusu);
                AppendMetaRow(sb, "Durum", reservation.Durum);
                AppendMetaRow(sb, "İşlem Tarihi", FormatDate(reservation.IslemTarihi, includeTime: true));
                AppendMetaRow(sb, "Ürün Çıkış Tarihi", FormatDate(reservation.UrunCikisTarihi, includeTime: true));
                sb.Append("</tbody></table>");

                if (reservation.RezervasyonNo != null &&
                    productsByReservation.TryGetValue(reservation.RezervasyonNo, out var items) &&
                    items.Any())
                {
                    var previewItems = items.Take(25).ToList();
                    sb.Append("<div class='table-wrapper'><table><thead><tr>");
                    foreach (var column in ProductColumns)
                        sb.AppendFormat("<th>{0}</th>", WebUtility.HtmlEncode(column.Header));
                    sb.Append("</tr></thead><tbody>");

                    foreach (var item in previewItems)
                    {
                        sb.Append("<tr>");
                        foreach (var column in ProductColumns)
                            sb.AppendFormat("<td>{0}</td>", HtmlEncode(column.TextGetter(item)));
                        sb.Append("</tr>");
                    }

                    if (items.Count > previewItems.Count)
                    {
                        sb.AppendFormat("<tr><td colspan='{0}'>Toplam {1} ürün var. Önizlemede ilk {2} gösterilmektedir.</td></tr>",
                            ProductColumns.Count,
                            items.Count.ToString("N0", Culture),
                            previewItems.Count.ToString("N0", Culture));
                    }

                    sb.Append("</tbody></table></div>");
                }
            }

            sb.Append("</body></html>");
            return sb.ToString();
        }

        private static string BuildIptalPreviewHtml(
            IReadOnlyList<RezIptal> cancellations,
            IDictionary<string, List<RezIptalDetay>> details,
            string selection,
            string periodDescription)
        {
            var sb = new StringBuilder();
            sb.Append("<html><head><meta charset=\"utf-8\"/><style>");
            sb.Append("body{font-family:'Segoe UI',sans-serif;margin:20px;}h1{font-size:20px;margin-bottom:6px;}h2{font-size:16px;margin-top:18px;margin-bottom:6px;}");
            sb.Append("table{border-collapse:collapse;min-width:100%;white-space:nowrap;}th,td{border:1px solid #d1d5db;padding:6px;text-align:left;}th{background:#e5e7eb;}");
            sb.Append(".meta{margin-bottom:8px;color:#374151;}.table-wrapper{overflow-x:auto;margin-top:8px;}");
            sb.Append("</style></head><body>");
            sb.AppendFormat("<h1>İptal Raporu ({0})</h1>", WebUtility.HtmlEncode(selection));
            sb.AppendFormat("<div class='meta'>Tarih Aralığı: {0}</div>", WebUtility.HtmlEncode(periodDescription));
            sb.AppendFormat("<div class='meta'>Toplam İptal: {0}</div>", cancellations.Count.ToString("N0", Culture));

            foreach (var cancellation in cancellations)
            {
                sb.AppendFormat("<h2>{0} - {1}</h2>",
                    HtmlEncode(cancellation.RezervasyonNo),
                    HtmlEncode(cancellation.AliciFirma));
                sb.Append("<table><tbody>");
                AppendMetaRow(sb, "Rezervasyon Kodu", cancellation.RezervasyonKodu);
                AppendMetaRow(sb, "Satış Sorumlusu", cancellation.SatisSorumlusu);
                AppendMetaRow(sb, "Durum", cancellation.Durum);
                AppendMetaRow(sb, "İşlem Tarihi", FormatDate(cancellation.IslemTarihi, includeTime: true));
                AppendMetaRow(sb, "İptal Tarihi", FormatDate(cancellation.IptalTarihi, includeTime: true));
                AppendMetaRow(sb, "İptal Eden", cancellation.IptalEdenPersonel);
                AppendMetaRow(sb, "İptal Sebebi", cancellation.IptalSebebi);
                sb.Append("</tbody></table>");

                if (cancellation.RezervasyonNo != null &&
                    details.TryGetValue(cancellation.RezervasyonNo, out var items) &&
                    items.Any())
                {
                    var previewItems = items.Take(25).Select(CloneToUrunStok).ToList();
                    sb.Append("<div class='table-wrapper'><table><thead><tr>");
                    foreach (var column in ProductColumns)
                        sb.AppendFormat("<th>{0}</th>", WebUtility.HtmlEncode(column.Header));
                    sb.Append("</tr></thead><tbody>");

                    foreach (var item in previewItems)
                    {
                        sb.Append("<tr>");
                        foreach (var column in ProductColumns)
                            sb.AppendFormat("<td>{0}</td>", HtmlEncode(column.TextGetter(item)));
                        sb.Append("</tr>");
                    }

                    if (items.Count > previewItems.Count)
                    {
                        sb.AppendFormat("<tr><td colspan='{0}'>Toplam {1} ürün var. Önizlemede ilk {2} gösterilmektedir.</td></tr>",
                            ProductColumns.Count,
                            items.Count.ToString("N0", Culture),
                            previewItems.Count.ToString("N0", Culture));
                    }

                    sb.Append("</tbody></table></div>");
                }
            }

            sb.Append("</body></html>");
            return sb.ToString();
        }


        private static string BuildPackingListPreviewHtml(PackingListService.PackingListSnapshot snapshot)
        {
            var sb = new StringBuilder();
            sb.Append("<html><head><meta charset=\"utf-8\"/><style>");
            sb.Append("body{font-family:'Segoe UI',sans-serif;margin:20px;}h1{font-size:20px;margin-bottom:6px;}h2{font-size:16px;margin-top:18px;margin-bottom:6px;}");
            sb.Append("table{border-collapse:collapse;min-width:100%;white-space:nowrap;}th,td{border:1px solid #d1d5db;padding:6px;text-align:left;}th{background:#e5e7eb;}");
            sb.Append(".meta{margin-bottom:8px;color:#374151;}.table-wrapper{overflow-x:auto;margin-top:8px;}");
            sb.Append("</style></head><body>");
            sb.Append("<h1>Packing List</h1>");
            if (snapshot.Reservation != null)
            {
                AppendInfo(sb, "Rezervasyon No", snapshot.Reservation.RezervasyonNo);
                AppendInfo(sb, "Alıcı Firma", snapshot.Reservation.AliciFirma);
                AppendInfo(sb, "Rezervasyon Sorumlusu", snapshot.Reservation.RezervasyonSorumlusu);
                AppendInfo(sb, "Satış Sorumlusu", snapshot.Reservation.SatisSorumlusu);
                AppendInfo(sb, "Durum", snapshot.Reservation.Durum);
            }

            sb.AppendFormat("<div class='meta'>Toplam Ürün: {0}</div>", snapshot.Items.Count.ToString("N0", Culture));
            sb.AppendFormat("<div class='meta'>Toplam Plaka: {0}</div>", snapshot.TotalPlateCount.ToString("N0", Culture));
            sb.AppendFormat("<div class='meta'>Toplam Satış Alanı: {0}</div>", FormatDecimal(snapshot.TotalArea));
            sb.AppendFormat("<div class='meta'>Toplam Satış Tonajı: {0}</div>", FormatDecimal(snapshot.TotalTonaj, 3));

            var previewItems = snapshot.Items.Take(50).ToList();
            sb.Append("<div class='table-wrapper'><table><thead><tr><th>No</th>");
            foreach (var column in ProductColumns)
                sb.AppendFormat("<th>{0}</th>", WebUtility.HtmlEncode(column.Header));
            sb.Append("</tr></thead><tbody>");

            for (int i = 0; i < previewItems.Count; i++)
            {
                var item = previewItems[i];
                sb.AppendFormat("<tr><td>{0}</td>", (i + 1).ToString(Culture));
                foreach (var column in ProductColumns)
                    sb.AppendFormat("<td>{0}</td>", HtmlEncode(column.TextGetter(item)));
                sb.Append("</tr>");
            }

            if (snapshot.Items.Count > previewItems.Count)
            {
                sb.AppendFormat("<tr><td colspan='{0}'>Toplam {1} satır var. Önizlemede ilk {2} gösterilmektedir.</td></tr>",
                    ProductColumns.Count + 1,
                    snapshot.Items.Count.ToString("N0", Culture),
                    previewItems.Count.ToString("N0", Culture));
            }

            if (previewItems.Count == 0)
                sb.AppendFormat("<tr><td colspan='{0}'>Veri bulunamadı.</td></tr>", ProductColumns.Count + 1);

            sb.Append("</tbody></table></div>");
            sb.Append("</body></html>");
            return sb.ToString();
        }

        private static void AppendMetaRow(StringBuilder sb, string label, string? value)
        {
            sb.AppendFormat("<tr><th style='width:200px'>{0}</th><td>{1}</td></tr>",
                WebUtility.HtmlEncode(label),
                HtmlEncode(value));
        }

        private static void AppendInfo(StringBuilder sb, string label, string? value)
        {
            sb.AppendFormat("<div><strong>{0}:</strong> {1}</div>",
                WebUtility.HtmlEncode(label),
                HtmlEncode(value));
        }

        private static string FormatDecimal(decimal? value, int decimals = 2)
        {
            if (!value.HasValue)
                return "-";

            return value.Value.ToString($"N{decimals}", Culture);
        }
    }
}
