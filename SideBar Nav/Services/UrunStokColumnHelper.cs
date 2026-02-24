using SideBar_Nav.Models;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace SideBar_Nav.Services
{
    internal static class UrunStokColumnHelper
    {
        internal record Column(
            string Header,
            Func<UrunStok, object?> ValueGetter,
            Func<UrunStok, string> TextGetter,
            string? NumberFormat = null);

        private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("tr-TR");

        internal static IReadOnlyList<Column> Columns { get; } = new List<Column>
        {
            new("ID", p => p.ID, p => FormatInt(p.ID), "#,##0"),
            new("EPC", p => ValueOrNull(p.EPC), p => ValueOrDash(p.EPC)),
            new("Barkod", p => ValueOrNull(p.BarkodNo), p => ValueOrDash(p.BarkodNo)),
            new("Bandıl", p => ValueOrNull(p.BandilNo), p => ValueOrDash(p.BandilNo)),
            new("Plaka", p => ValueOrNull(p.PlakaNo), p => ValueOrDash(p.PlakaNo)),
            new("Ürün Tipi", p => ValueOrNull(p.UrunTipi), p => ValueOrDash(p.UrunTipi)),
            new("Ürün Türü", p => ValueOrNull(p.UrunTuru), p => ValueOrDash(p.UrunTuru)),
            new("Yüzey İşlemi", p => ValueOrNull(p.YuzeyIslemi), p => ValueOrDash(p.YuzeyIslemi)),
            new("Seleksiyon", p => ValueOrNull(p.Seleksiyon), p => ValueOrDash(p.Seleksiyon)),
            new("Üretim Tarihi", p => ToLocal(p.UretimTarihi), p => FormatDateTime(p.UretimTarihi), "dd.MM.yyyy HH:mm"),
            new("Kalınlık", p => p.Kalinlik, p => FormatDecimal(p.Kalinlik), "#,##0.00"),
            new("Plaka Adedi", p => p.PlakaAdedi, p => FormatInt(p.PlakaAdedi), "#,##0"),
            new("Stok En", p => p.StokEn, p => FormatDecimal(p.StokEn), "#,##0.00"),
            new("Stok Boy", p => p.StokBoy, p => FormatDecimal(p.StokBoy), "#,##0.00"),
            new("Stok Alan", p => p.StokAlan, p => FormatDecimal(p.StokAlan), "#,##0.00"),
            new("Stok Tonaj", p => p.StokTonaj, p => FormatDecimal(p.StokTonaj, 3), "#,##0.000"),
            new("Satış En", p => p.SatisEn, p => FormatDecimal(p.SatisEn), "#,##0.00"),
            new("Satış Boy", p => p.SatisBoy, p => FormatDecimal(p.SatisBoy), "#,##0.00"),
            new("Satış Alan", p => p.SatisAlan, p => FormatDecimal(p.SatisAlan), "#,##0.00"),
            new("Satış Tonaj", p => p.SatisTonaj, p => FormatDecimal(p.SatisTonaj, 3), "#,##0.000"),
            new("Durum", p => ValueOrNull(p.Durum), p => ValueOrDash(p.Durum)),
            new("Rezervasyon No", p => ValueOrNull(p.RezervasyonNo), p => ValueOrDash(p.RezervasyonNo)),
            new("Kaydeden Personel", p => ValueOrNull(p.KaydedenPersonel), p => ValueOrDash(p.KaydedenPersonel)),
            new("Ürün Çıkış Tarihi", p => ToLocal(p.UrunCikisTarihi), p => FormatDateTime(p.UrunCikisTarihi), "dd.MM.yyyy HH:mm"),
            new("Alıcı Firma", p => ValueOrNull(p.AliciFirma), p => ValueOrDash(p.AliciFirma)),
        };

        internal static string FormatInt(int? value)
            => value.HasValue ? value.Value.ToString("N0", Culture) : "-";

        internal static string FormatDecimal(decimal? value, int decimals = 2)
            => value.HasValue ? value.Value.ToString($"N{decimals}", Culture) : "-";

        internal static string FormatDateTime(DateTime? value, bool includeTime = true)
        {
            if (!value.HasValue)
                return "-";

            DateTime local = value.Value.ToLocalTime();
            return includeTime
                ? local.ToString("dd.MM.yyyy HH:mm", Culture)
                : local.ToString("dd.MM.yyyy", Culture);
        }

        internal static string ValueOrDash(string? value)
            => string.IsNullOrWhiteSpace(value) ? "-" : value!;

        private static string? ValueOrNull(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value;

        private static DateTime? ToLocal(DateTime? value)
            => value?.ToLocalTime();
    }
}
