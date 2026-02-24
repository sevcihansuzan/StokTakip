using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SideBar_Nav.Models
{
    [Table("Yetkiler")]
    public class Yetkiler : BaseModel
    {
        [PrimaryKey("id", false)]
        public int Id { get; set; }

        [Column("ad")]
        public string Ad { get; set; }
    }
}
