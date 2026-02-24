using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SideBar_Nav.Models
{

    [Table("UrunRezervasyon")]
    public class UrunRezervasyon : BaseModel
    {
        [PrimaryKey("ID", false)] public int ID { get; set; }
        [Column("RezervasyonNo")] public string RezervasyonNo { get; set; }
        [Column("IslemTarihi")] public DateTime? IslemTarihi { get; set; }
        [Column("RezervasyonKodu")] public string RezervasyonKodu { get; set; }
        [Column("AliciFirma")] public string AliciFirma { get; set; }
        [Column("SatisSorumlusu")] public string SatisSorumlusu { get; set; }
        [Column("RezervasyonSorumlusu")] public string RezervasyonSorumlusu { get; set; }
        [Column("Durum")] public string Durum { get; set; }
        [Column("KaydedenPersonel")] public string KaydedenPersonel { get; set; }
        [Column("UrunCikisTarihi")] public DateTime? UrunCikisTarihi { get; set; }
        [Column("SevkiyatAdresi")] public string SevkiyatAdresi { get; set; }
    }
}
