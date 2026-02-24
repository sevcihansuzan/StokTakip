using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;

namespace SideBar_Nav.Models
{
    [Table("user_reader_settings")]
    public class UserReaderSettings : BaseModel
    {
        [PrimaryKey("user_id", false)]
        [Column("user_id")]
        public Guid UserId { get; set; }

        [Column("settings_json")]
        public string? SettingsJson { get; set; }

        [Column("updated_at")]
        public DateTimeOffset? UpdatedAt { get; set; }
    }
}
