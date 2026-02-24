using System;
using System.Linq;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SideBar_Nav.Models
{
    [Table("kullanicilar")]
    public class Kullanicilar : BaseModel
    {
        [PrimaryKey("id", false)]
        [Column("id")]
        public Guid Id { get; set; }

        [Column("email")]
        public string? Email { get; set; }

        [Column("ad")]
        public string? Ad { get; set; }

        [Column("pozisyon")]
        public string? Pozisyon { get; set; }

        [Column("avatar_url")]
        public string? AvatarUrl { get; set; }

        [Column("phone")]
        public string? Phone { get; set; }

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; }

        [Column("last_sign_in_at")]
        public DateTimeOffset? LastSignInAt { get; set; }

        // ---- {Sayfa}_allow ----
        [Column("admin_allow")] public bool AdminAllow { get; set; }
        [Column("gpio_ops_allow")] public bool GpioOpsAllow { get; set; }
        [Column("iptal_allow")] public bool IptalAllow { get; set; }
        [Column("kalite_kontrol_allow")] public bool KaliteKontrolAllow { get; set; }
        [Column("kullanici_yonetimi_allow")] public bool KullaniciYonetimiAllow { get; set; }
        [Column("personel_allow")] public bool PersonelAllow { get; set; }
        [Column("reading_allow")] public bool ReadingAllow { get; set; }
        [Column("rez_olustur_allow")] public bool RezOlusturAllow { get; set; }
        [Column("satis_yonetimi_allow")] public bool SatisYonetimiAllow { get; set; }
        [Column("sevkiyat_allow")] public bool SevkiyatAllow { get; set; }
        [Column("stok_yonetimi_allow")] public bool StokYonetimiAllow { get; set; }
        [Column("urun_takip_allow")] public bool UrunTakipAllow { get; set; }
        [Column("user_settings_allow")] public bool UserSettingsAllow { get; set; }
        [Column("uretim_allow")] public bool UretimAllow { get; set; }
      
    }
}
