using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Pdf;
using SideBar_Nav.Models;
using Supabase.Postgrest;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static Supabase.Postgrest.Constants;

namespace SideBar_Nav.Services
{
    public static class PackingListService
    {
        private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("tr-TR");

        public class PackingListSnapshot
        {
            public UrunRezervasyon? Reservation { get; set; }
            public List<UrunStok> Items { get; set; } = new();
            public decimal TotalArea { get; set; }
            public decimal TotalTonaj { get; set; }
            public int TotalPlateCount { get; set; }
        }

        public static string DefaultDirectory { get; set; } =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PackingLists");

        public static async Task<PackingListSnapshot> GetPackingListSnapshotAsync(string rezervasyonNo)
        {
            if (string.IsNullOrWhiteSpace(rezervasyonNo))
                throw new ArgumentException("Rezervasyon numarası boş olamaz.", nameof(rezervasyonNo));

            if (App.SupabaseClient == null)
                throw new InvalidOperationException("Supabase bağlantısı henüz başlatılmadı.");

            var rezervasyonResponse = await App.SupabaseClient
                .From<UrunRezervasyon>()
                .Filter("RezervasyonNo", Operator.Equals, rezervasyonNo)
                .Get();

            var reservation = rezervasyonResponse.Models.FirstOrDefault();
            if (reservation == null)
                throw new InvalidOperationException($"Rezervasyon ({rezervasyonNo}) bulunamadı.");

            var stokResponse = await App.SupabaseClient
                .From<UrunStok>()
                .Filter("RezervasyonNo", Operator.Equals, rezervasyonNo)
                .Get();

            var items = stokResponse.Models
                .OrderBy(x => x.BandilNo)
                .ThenBy(x => x.PlakaNo)
                .ThenBy(x => x.EPC)
                .ToList();

            decimal totalArea = items.Sum(x => x.SatisAlan ?? x.StokAlan ?? 0m);
            decimal totalTonaj = items.Sum(x => x.SatisTonaj ?? x.StokTonaj ?? 0m);
            int totalPlate = items.Sum(x => x.PlakaAdedi ?? 0);

            return new PackingListSnapshot
            {
                Reservation = reservation,
                Items = items,
                TotalArea = totalArea,
                TotalTonaj = totalTonaj,
                TotalPlateCount = totalPlate
            };
        }

        public static async Task<string> GeneratePackingListPdfAsync(string rezervasyonNo, string? directory = null, string? outputPath = null)
        {
            var snapshot = await GetPackingListSnapshotAsync(rezervasyonNo);
            return GeneratePackingListPdf(snapshot, directory, outputPath);
        }

        public static string GeneratePackingListPdf(PackingListSnapshot snapshot, string? directory = null, string? outputPath = null)
        {
            if (snapshot.Reservation == null)
                throw new InvalidOperationException("Rezervasyon bilgisi eksik.");

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
                directory ??= DefaultDirectory;
                Directory.CreateDirectory(directory);
                string fileName = $"PackingList_{snapshot.Reservation.RezervasyonNo}.pdf";
                filePath = Path.Combine(directory, fileName);
            }

            using (var document = new PdfDocument())
            {
                document.Info.Title = $"Packing List - {snapshot.Reservation.RezervasyonNo}";
                PdfPage page = CreatePage(document);
                XGraphics gfx = XGraphics.FromPdfPage(page);
                var textFormatter = new XTextFormatter(gfx);

                double margin = 40;
                double y = margin;

                var titleFont = new XFont("Segoe UI", 18, XFontStyleEx.Bold);
                gfx.DrawString("PACKING LIST", titleFont, XBrushes.Black,
                    new XRect(0, y, page.Width, 30), XStringFormats.TopCenter);
                y += 40;

                var labelFont = new XFont("Segoe UI", 10, XFontStyleEx.Bold);
                var valueFont = new XFont("Segoe UI", 10, XFontStyleEx.Regular);
                double columnWidth = (page.Width - 2 * margin) / 2;
                double labelWidth = 120;
                double infoHeight = 18;

                var infoRows = new (string Label, string Value)[]
                {
                    ("Rezervasyon No", snapshot.Reservation.RezervasyonNo),
                    ("Rezervasyon Kodu", snapshot.Reservation.RezervasyonKodu),
                    ("Alıcı Firma", snapshot.Reservation.AliciFirma),
                    ("Rezervasyon Sorumlusu", snapshot.Reservation.RezervasyonSorumlusu),
                    ("Satış Sorumlusu", snapshot.Reservation.SatisSorumlusu),
                    ("İşlem Tarihi", FormatDate(snapshot.Reservation.IslemTarihi)),
                    ("Ürün Çıkış Tarihi", FormatDate(snapshot.Reservation.UrunCikisTarihi)),
                    ("Durum", snapshot.Reservation.Durum)
                };

                for (int i = 0; i < infoRows.Length; i += 2)
                {
                    EnsureSpace(ref page, document, ref gfx, ref textFormatter, ref y, margin, infoHeight);
                    DrawInfoCell(textFormatter, labelFont, valueFont, margin, y, labelWidth, columnWidth, infoRows[i]);

                    if (i + 1 < infoRows.Length)
                        DrawInfoCell(textFormatter, labelFont, valueFont, margin + columnWidth, y, labelWidth, columnWidth, infoRows[i + 1]);

                    y += infoHeight;
                }

                if (!string.IsNullOrWhiteSpace(snapshot.Reservation.SevkiyatAdresi))
                {
                    double adresHeight = 40;
                    EnsureSpace(ref page, document, ref gfx, ref textFormatter, ref y, margin, adresHeight);
                    textFormatter.DrawString("Sevkiyat Adresi:", labelFont, XBrushes.Black,
                        new XRect(margin, y, labelWidth, adresHeight));
                    textFormatter.DrawString(snapshot.Reservation.SevkiyatAdresi, valueFont, XBrushes.Black,
                        new XRect(margin + labelWidth, y, page.Width - 2 * margin - labelWidth, adresHeight));
                    y += adresHeight;
                }

                y += 10;

                var productColumns = UrunStokColumnHelper.Columns;
                var detailHeaderFont = new XFont("Segoe UI", 9, XFontStyleEx.Bold);
                var detailValueFont = new XFont("Segoe UI", 8, XFontStyleEx.Regular);
                var borderPen = new XPen(XColors.Black, 0.5);

                int pairsPerRow = 3;
                double availableWidth = page.Width - 2 * margin;
                double pairWidth = availableWidth / pairsPerRow;
                double detailLabelWidth = pairWidth * 0.35;
                double detailValueWidth = pairWidth - detailLabelWidth;
                double detailRowHeight = 24;

                if (snapshot.Items.Any())
                {
                    int index = 1;
                    foreach (var item in snapshot.Items)
                    {
                        EnsureSpace(ref page, document, ref gfx, ref textFormatter, ref y, margin, detailRowHeight);
                        var headerRect = new XRect(margin, y, availableWidth, detailRowHeight);
                        gfx.DrawRectangle(XBrushes.LightGray, headerRect);
                        gfx.DrawRectangle(borderPen, headerRect);
                        textFormatter.DrawString($"Ürün #{index}", detailHeaderFont, XBrushes.Black,
                            new XRect(headerRect.X + 4, headerRect.Y + 4, headerRect.Width - 8, headerRect.Height - 8));
                        y += detailRowHeight;

                        var detailPairs = productColumns
                            .Select(c => (c.Header, Value: c.TextGetter(item)))
                            .ToList();

                        int pairIndex = 0;

                        while (pairIndex < detailPairs.Count)
                        {
                            EnsureSpace(ref page, document, ref gfx, ref textFormatter, ref y, margin, detailRowHeight);
                            double currentX = margin;

                            for (int pair = 0; pair < pairsPerRow && pairIndex < detailPairs.Count; pair++)
                            {
                                var (label, value) = detailPairs[pairIndex++];

                                var labelRect = new XRect(currentX, y, detailLabelWidth, detailRowHeight);
                                gfx.DrawRectangle(XBrushes.LightGray, labelRect);
                                gfx.DrawRectangle(borderPen, labelRect);
                                textFormatter.DrawString(label + ":", detailHeaderFont, XBrushes.Black,
                                    new XRect(labelRect.X + 3, labelRect.Y + 3, labelRect.Width - 6, labelRect.Height - 6));

                                var valueRect = new XRect(currentX + detailLabelWidth, y, detailValueWidth, detailRowHeight);
                                gfx.DrawRectangle(borderPen, valueRect);
                                textFormatter.DrawString(string.IsNullOrWhiteSpace(value) ? "-" : value, detailValueFont, XBrushes.Black,
                                    new XRect(valueRect.X + 3, valueRect.Y + 3, valueRect.Width - 6, valueRect.Height - 6));

                                currentX += pairWidth;
                            }

                            y += detailRowHeight;
                        }

                        y += 6;
                        index++;
                    }
                }
                else
                {
                    double rowHeight = detailRowHeight;
                    EnsureSpace(ref page, document, ref gfx, ref textFormatter, ref y, margin, rowHeight);
                    var rect = new XRect(margin, y, availableWidth, rowHeight);
                    gfx.DrawRectangle(borderPen, rect);
                    textFormatter.DrawString("Bu rezervasyon için ürün bulunamadı.", detailValueFont, XBrushes.Black,
                        new XRect(rect.X + 4, rect.Y + 4, rect.Width - 8, rect.Height - 8));
                    y += rowHeight;
                }

                y += 10;
                double totalsHeight = 40;
                EnsureSpace(ref page, document, ref gfx, ref textFormatter, ref y, margin, totalsHeight);
                var totalsLabelFont = new XFont("Segoe UI", 10, XFontStyleEx.Bold);
                var totalsValueFont = new XFont("Segoe UI", 10, XFontStyleEx.Regular);
                double totalsWidth = page.Width - 2 * margin;

                gfx.DrawLine(borderPen, margin, y, margin + totalsWidth, y);
                y += 5;

                textFormatter.DrawString($"Toplam Ürün: {snapshot.Items.Count}", totalsLabelFont, XBrushes.Black,
                    new XRect(margin, y, totalsWidth / 2, 18));
                textFormatter.DrawString($"Toplam Plaka Adedi: {snapshot.TotalPlateCount.ToString("N0", Culture)}", totalsValueFont, XBrushes.Black,
                    new XRect(margin + totalsWidth / 2, y, totalsWidth / 2, 18));
                y += 18;

                textFormatter.DrawString($"Toplam Satış Alanı: {FormatDecimal(snapshot.TotalArea)} m²", totalsLabelFont, XBrushes.Black,
                    new XRect(margin, y, totalsWidth / 2, 18));
                textFormatter.DrawString($"Toplam Satış Tonajı: {FormatDecimal(snapshot.TotalTonaj, 3)} ton", totalsValueFont, XBrushes.Black,
                    new XRect(margin + totalsWidth / 2, y, totalsWidth / 2, 18));

                document.Save(filePath);
            }

            return filePath;
        }

        private static PdfPage CreatePage(PdfDocument document)
        {
            var page = document.AddPage();
            page.Size = PageSize.A4;
            page.Orientation = PageOrientation.Landscape;
            return page;
        }

        private static void DrawInfoCell(XTextFormatter formatter, XFont labelFont, XFont valueFont,
            double x, double y, double labelWidth, double columnWidth, (string Label, string Value) data)
        {
            formatter.DrawString(data.Label + ":", labelFont, XBrushes.Black, new XRect(x, y, labelWidth, 18));
            formatter.DrawString(string.IsNullOrWhiteSpace(data.Value) ? "-" : data.Value, valueFont, XBrushes.Black,
                new XRect(x + labelWidth, y, columnWidth - labelWidth, 18));
        }

        private static bool EnsureSpace(ref PdfPage page, PdfDocument document, ref XGraphics gfx,
            ref XTextFormatter formatter, ref double y, double margin, double requiredHeight)
        {
            if (y + requiredHeight <= page.Height - margin)
                return false;

            page = CreatePage(document);
            gfx = XGraphics.FromPdfPage(page);
            formatter = new XTextFormatter(gfx);
            y = margin;
            return true;
        }

        private static string FormatDate(DateTime? date)
        {
            if (!date.HasValue)
                return "-";

            return date.Value.ToLocalTime().ToString("dd.MM.yyyy", Culture);
        }

        public static string FormatDecimal(decimal? value, int decimals = 2)
        {
            if (!value.HasValue)
                return "-";

            string format = "N" + decimals;
            return value.Value.ToString(format, Culture);
        }
    }
}
