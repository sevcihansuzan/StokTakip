using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SideBar_Nav.Models
{
    [Table("RezIptalDetay")]
    public class RezIptalDetay : BaseModel
    {
        [PrimaryKey("ID", false)] public int ID { get; set; }
        [Column("RezervasyonNo")] public string RezervasyonNo { get; set; }
        [Column("EPC")] public string EPC { get; set; }
        [Column("BarkodNo")] public string BarkodNo { get; set; }
        [Column("BandilNo")] public string BandilNo { get; set; }
        [Column("PlakaNo")] public string PlakaNo { get; set; }
        [Column("UrunTipi")] public string UrunTipi { get; set; }
        [Column("UrunTuru")] public string UrunTuru { get; set; }
        [Column("YuzeyIslemi")] public string YuzeyIslemi { get; set; }
        [Column("Seleksiyon")] public string Seleksiyon { get; set; }
        [Column("UretimTarihi")] public DateTime? UretimTarihi { get; set; }
        [Column("Kalinlik")] public decimal? Kalinlik { get; set; }
        [Column("PlakaAdedi")] public int? PlakaAdedi { get; set; }
        [Column("StokEn")] public decimal? StokEn { get; set; }
        [Column("StokBoy")] public decimal? StokBoy { get; set; }
        [Column("StokAlan")] public decimal? StokAlan { get; set; }
        [Column("StokTonaj")] public decimal? StokTonaj { get; set; }
        [Column("SatisEn")] public decimal? SatisEn { get; set; }
        [Column("SatisBoy")] public decimal? SatisBoy { get; set; }
        [Column("SatisAlan")] public decimal? SatisAlan { get; set; }
        [Column("SatisTonaj")] public decimal? SatisTonaj { get; set; }
        [Column("Durum")] public string Durum { get; set; }
        [Column("KaydedenPersonel")] public string KaydedenPersonel { get; set; }
        [Column("UrunCikisTarihi")] public DateTime? UrunCikisTarihi { get; set; }
        [Column("AliciFirma")] public string AliciFirma { get; set; }
    }
}
