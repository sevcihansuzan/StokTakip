using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SideBar_Nav.Models
{
    [Table("UrunTurleri")]
    public class UrunTurleri : BaseModel
    {
        [PrimaryKey("Id", false)] public int Id { get; set; }
        [Column("UrunTuru")] public string UrunTuru { get; set; }
        [Column("YogunlukKatsayisi")] public double YogunlukKatsayisi { get; set; }
    }
}
