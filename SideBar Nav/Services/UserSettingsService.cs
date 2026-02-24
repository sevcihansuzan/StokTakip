using SideBar_Nav.Models;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using static Supabase.Postgrest.Constants;

namespace SideBar_Nav.Services
{
    public class UserSettingsService
    {
        public async Task<ReaderSettingsSnapshot?> GetSettingsAsync(Guid userId)
        {
            if (App.SupabaseClient == null)
                await App.InitializeSupabase();

            var response = await App.SupabaseClient
                .From<UserReaderSettings>()
                .Filter("user_id", Operator.Equals, userId.ToString())
                .Limit(1)
                .Get();

            var record = response.Models.FirstOrDefault();
            if (record?.SettingsJson is { Length: > 0 })
            {
                return JsonSerializer.Deserialize<ReaderSettingsSnapshot>(record.SettingsJson);
            }

            return null;
        }

        public async Task SaveSettingsAsync(Guid userId, ReaderSettingsSnapshot snapshot)
        {
            if (App.SupabaseClient == null)
                await App.InitializeSupabase();

            var record = new UserReaderSettings
            {
                UserId = userId,
                SettingsJson = JsonSerializer.Serialize(snapshot),
                UpdatedAt = DateTimeOffset.UtcNow
            };

            await App.SupabaseClient.From<UserReaderSettings>().Upsert(record);
        }
    }
}
