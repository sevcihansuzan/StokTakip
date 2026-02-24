// Models/Kullanici.cs
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;


// Bu class ve AuthzService.cs chatgpt codex ile yazıldı. Aslında bütün islemler Kullanicilar.cs üzerinden gitmeliydi ama AuthzService.cs tarafından kullanıldığı ve sorunsuz çalıştığı için bu şekilde bıraktım. 

namespace SideBar_Nav.Models
{
    [Table("kullanicilar")]
    public class Kullanici : BaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("email")]
        public string Email { get; set; }

        [Column("ad")]
        public string Ad { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("is_admin")] public bool IsAdmin { get; set; }

        [Column("admin_view")] public bool AdminView { get; set; }
        [Column("admin_edit")] public bool AdminEdit { get; set; }

        [Column("gpio_ops_view")] public bool GPIOopsView { get; set; }
        [Column("gpio_ops_edit")] public bool GPIOopsEdit { get; set; }

        [Column("iptal_view")] public bool IptalView { get; set; }
        [Column("iptal_edit")] public bool IptalEdit { get; set; }

        [Column("kalite_kontrol_view")] public bool KaliteKontrolView { get; set; }
        [Column("kalite_kontrol_edit")] public bool KaliteKontrolEdit { get; set; }

        [Column("kullanici_yonetimi_view")] public bool KullaniciYonetimiView { get; set; }
        [Column("kullanici_yonetimi_edit")] public bool KullaniciYonetimiEdit { get; set; }

        [Column("personel_view")] public bool PersonelView { get; set; }
        [Column("personel_edit")] public bool PersonelEdit { get; set; }

        [Column("reading_view")] public bool ReadingView { get; set; }
        [Column("reading_edit")] public bool ReadingEdit { get; set; }

        [Column("rez_olustur_view")] public bool RezOlusturView { get; set; }
        [Column("rez_olustur_edit")] public bool RezOlusturEdit { get; set; }

        [Column("satis_yonetimi_view")] public bool SatisYonetimiView { get; set; }
        [Column("satis_yonetimi_edit")] public bool SatisYonetimiEdit { get; set; }

        [Column("sevkiyat_view")] public bool SevkiyatView { get; set; }
        [Column("sevkiyat_edit")] public bool SevkiyatEdit { get; set; }

        [Column("stok_yonetimi_view")] public bool StokYonetimiView { get; set; }
        [Column("stok_yonetimi_edit")] public bool StokYonetimiEdit { get; set; }

        [Column("urun_takip_view")] public bool UrunTakipView { get; set; }
        [Column("urun_takip_edit")] public bool UrunTakipEdit { get; set; }

        [Column("user_settings_view")] public bool UserSettingsView { get; set; }
        [Column("user_settings_edit")] public bool UserSettingsEdit { get; set; }

        [Column("uretim_view")] public bool UretimView { get; set; }
        [Column("uretim_edit")] public bool UretimEdit { get; set; }
    }
}