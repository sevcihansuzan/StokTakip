using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;

namespace SideBar_Nav.Models
{

    [Table("RezIptal")]
    public class RezIptal : BaseModel
    {
        [PrimaryKey("ID", false)] public int ID { get; set; }
        [Column("RezervasyonNo")] public string RezervasyonNo { get; set; }
        [Column("IslemTarihi")] public DateTime? IslemTarihi { get; set; }
        [Column("RezervasyonKodu")] public string RezervasyonKodu { get; set; }
        [Column("AliciFirma")] public string AliciFirma { get; set; }
        [Column("SatisSorumlusu")] public string SatisSorumlusu { get; set; }
        [Column("Durum")] public string Durum { get; set; }
        [Column("KaydedenPersonel")] public string KaydedenPersonel { get; set; }
        [Column("UrunCikisTarihi")] public DateTime? UrunCikisTarihi { get; set; }
        [Column("SevkiyatAdresi")] public string SevkiyatAdresi { get; set; }
        [Column("IptalTarihi")] public DateTime? IptalTarihi { get; set; }
        [Column("IptalEdenKullanici")] public string IptalEdenKullanici { get; set; }
        [Column("IptalSebebi")] public string IptalSebebi { get; set; }
        [Column("IptalEdenPersonel")] public string IptalEdenPersonel { get; set; }
    }
}
