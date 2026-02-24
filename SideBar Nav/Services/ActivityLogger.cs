using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using SideBar_Nav.Models;
using static Supabase.Postgrest.Constants;
using Supabase.Postgrest.Interfaces;
using System.Windows;        // Application için
using SideBar_Nav;           // MainWindow tipi için
namespace SideBar_Nav.Services
{
    public static class ActivityLogger
    {
        // In-memory log list for binding
        public static ObservableCollection<ActivityLog> Logs { get; } = new();

        // Load persisted logs from Supabase with optional filters
        public static async Task LoadAsync(
            DateTime? start = null,
            DateTime? end = null,
            Guid? userId = null,
            string? eylem = null,
            string? detay = null)
        {
            if (App.SupabaseClient == null)
                await App.InitializeSupabase();

            IPostgrestTable<ActivityLog> q = App.SupabaseClient.From<ActivityLog>();

            if (start.HasValue)
                q = q.Filter("zaman", Operator.GreaterThanOrEqual, start.Value.ToUniversalTime().ToString("o"));
            if (end.HasValue)
                q = q.Filter("zaman", Operator.LessThan, end.Value.ToUniversalTime().ToString("o"));

            // <<< YENÝ: personel yerine ID’den filtre
            if (userId.HasValue)
                q = q.Filter("user_id", Operator.Equals, userId.Value.ToString());

            if (!string.IsNullOrWhiteSpace(eylem))
                q = q.Filter("eylem", Operator.ILike, $"%{eylem}%");

            if (!string.IsNullOrWhiteSpace(detay))
                q = q.Filter("detay", Operator.ILike, $"%{detay}%");

            var res = await q.Order(x => x.Zaman, Ordering.Descending).Get();

            App.Current.Dispatcher.Invoke(() =>
            {
                Logs.Clear();
                foreach (var log in res.Models)
                    Logs.Add(log);
            });
        }

        // Log a new activity and persist

        public static async Task Log(string? personel, string eylem, string? detay = null)
        {
            var meId = (Application.Current.MainWindow as MainWindow)?.Vm?.Me?.Id;

            var entry = new ActivityLog
            {
                UserId = meId,  // <<< YENÝ
                Personel = string.IsNullOrWhiteSpace(personel) ? "" : personel,
                Eylem = eylem,
                Detay = detay ?? string.Empty,
                Zaman = DateTime.UtcNow  // <<< öneri
            };

            Logs.Add(entry);
            try { await SaveAsync(entry); }
            catch (Exception ex)
            {
                Logs.Add(new ActivityLog
                {
                    Personel = "SYSTEM",
                    Eylem = "Log Save Failed",
                    Detay = ex.Message,
                    Zaman = DateTime.UtcNow
                });
                Debug.WriteLine(ex.ToString());
            }
        }

        private static async Task SaveAsync(ActivityLog entry)
        {
            if (App.SupabaseClient == null)
                await App.InitializeSupabase();

            await App.SupabaseClient.From<ActivityLog>().Insert(entry);
        }
    }
}
