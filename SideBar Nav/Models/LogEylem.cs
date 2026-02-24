using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SideBar_Nav.Models
{
    [Table("log_eylem")]
    public class LogEylem : BaseModel
    {
        [PrimaryKey("id", false)]
        public int Id { get; set; }

        [Column("eylem")]
        public string Eylem { get; set; } = string.Empty;
    }
}
