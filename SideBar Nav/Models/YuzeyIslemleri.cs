using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SideBar_Nav.Models
{
    [Table("YuzeyIslemleri")]
    public class YuzeyIslemleri : BaseModel
    {
        [PrimaryKey("Id", false)]
        public int Id { get; set; }

        [Column("YuzeyIslemi")]
        public string YuzeyIslemi { get; set; }
    }
}
