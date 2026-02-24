using SideBar_Nav.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Supabase.Postgrest.Interfaces;
using static Supabase.Postgrest.Constants;

namespace SideBar_Nav.Services
{
    public static class AuthzService
    {
        public enum PageKey { Admin, GPIO_ops, Iptal, KaliteKontrol, KullaniciYonetimi, Personel, Reading, RezOlustur, SatisYonetimi, Sevkiyat, StokYonetimi, UrunTakip, UserSettings, Uretim }

        public static async Task<Kullanici?> GetMeAsync()
        {
            if (App.SupabaseClient == null) await App.InitializeSupabase();
            if (App.AktifKullanici == null) return null;

            var uid = Guid.Parse(App.AktifKullanici.Id);

            // Güvenli tek kayıt: Limit(1)+Get => yoksa null döner
            var resp = await App.SupabaseClient
                .From<Kullanici>()
                .Select(x => new object[]
                {
                    x.Id, x.Email, x.Ad, x.IsAdmin,
                    x.AdminView, x.AdminEdit,
                    x.GPIOopsView, x.GPIOopsEdit,
                    x.IptalView, x.IptalEdit,
                    x.KaliteKontrolView, x.KaliteKontrolEdit,
                    x.KullaniciYonetimiView, x.KullaniciYonetimiEdit,
                    x.PersonelView, x.PersonelEdit,
                    x.ReadingView, x.ReadingEdit,
                    x.RezOlusturView, x.RezOlusturEdit,
                    x.SatisYonetimiView, x.SatisYonetimiEdit,
                    x.SevkiyatView, x.SevkiyatEdit,
                    x.StokYonetimiView, x.StokYonetimiEdit,
                    x.UrunTakipView, x.UrunTakipEdit,
                    x.UserSettingsView, x.UserSettingsEdit,
                    x.UretimView, x.UretimEdit
                })
                .Filter("id", Operator.Equals, uid.ToString())
                .Limit(1)
                .Get();

            return resp.Models.FirstOrDefault();
        }

        public static bool CanView(Kullanici me, PageKey key) => key switch
        {
            PageKey.Admin => me.AdminView,
            PageKey.GPIO_ops => me.GPIOopsView,
            PageKey.Iptal => me.IptalView,
            PageKey.KaliteKontrol => me.KaliteKontrolView,
            PageKey.KullaniciYonetimi => me.KullaniciYonetimiView,
            PageKey.Personel => me.PersonelView,
            PageKey.Reading => me.ReadingView,
            PageKey.RezOlustur => me.RezOlusturView,
            PageKey.SatisYonetimi => me.SatisYonetimiView,
            PageKey.Sevkiyat => me.SevkiyatView,
            PageKey.StokYonetimi => me.StokYonetimiView,
            PageKey.UrunTakip => me.UrunTakipView,
            PageKey.UserSettings => me.UserSettingsView,
            PageKey.Uretim => me.UretimView,
            _ => false
        };

        public static bool CanEdit(Kullanici me, PageKey key) => key switch
        {
            PageKey.Admin => me.AdminEdit,
            PageKey.GPIO_ops => me.GPIOopsEdit,
            PageKey.Iptal => me.IptalEdit,
            PageKey.KaliteKontrol => me.KaliteKontrolEdit,
            PageKey.KullaniciYonetimi => me.KullaniciYonetimiEdit,
            PageKey.Personel => me.PersonelEdit,
            PageKey.Reading => me.ReadingEdit,
            PageKey.RezOlustur => me.RezOlusturEdit,
            PageKey.SatisYonetimi => me.SatisYonetimiEdit,
            PageKey.Sevkiyat => me.SevkiyatEdit,
            PageKey.StokYonetimi => me.StokYonetimiEdit,
            PageKey.UrunTakip => me.UrunTakipEdit,
            PageKey.UserSettings => me.UserSettingsEdit,
            PageKey.Uretim => me.UretimEdit,
            _ => false
        };
    }
}
