using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;

[Table("activity_logs")]
public class ActivityLog : BaseModel
{
    [PrimaryKey("Id", false)] public int Id { get; set; }

    [Column("user_id")] public Guid? UserId { get; set; }

    [Column("personel")] public string Personel { get; set; } = string.Empty;
    [Column("eylem")] public string Eylem { get; set; } = string.Empty;
    [Column("detay")] public string Detay { get; set; } = string.Empty;

    // DB’de default now() var; istersen nullable yapýp hiç set etme, DB doldursun
    [Column("zaman")] public DateTime? Zaman { get; set; }
}
