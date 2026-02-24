using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SideBar_Nav.Models
{
    [Table("AliciFirmalar")]
    public class AliciFirmalar : BaseModel
    {
        [PrimaryKey("ID", false)]
        public int ID { get; set; }

        [Column("FirmaAdi")]
        public string FirmaAdi { get; set; }

        [Column("VergiNo")] public string VergiNo { get; set; }
        [Column("VergiDairesi")] public string VergiDairesi { get; set; }
        [Column("Telefon")] public string Telefon { get; set; }
        [Column("YetkiliAdi")] public string YetkiliAdi { get; set; }
        [Column("Email")] public string Email { get; set; }
        [Column("SevkiyatAdresi")] public string SevkiyatAdresi { get; set; }
        [Column("FaturaAdresi")] public string FaturaAdresi { get; set; }
        [Column("Ulke")] public string Ulke { get; set; }
        [Column("Sehir")] public string Sehir { get; set; }
        [Column("Ilce")] public string Ilce { get; set; }
        [Column("PostaKodu")] public string PostaKodu { get; set; }
        [Column("Notlar")] public string Notlar { get; set; }
        [Column("OlusturmaTarihi")] public DateTime? OlusturmaTarihi { get; set; }
        [Column("GuncellemeTarihi")] public DateTime? GuncellemeTarihi { get; set; }
    }
}