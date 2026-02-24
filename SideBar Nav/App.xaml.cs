using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Supabase;

// IMPINJ OCTANE SDK operations must be mainly stay in RFIDReader.cs

// Adding new page to the menu:
// 1) Create page
// 2) MainWindow.xaml-->ListBox-->add the page
// 3) Add the page to InitializeAllPages() method in MainWindow.xaml.cs

// Menüde yeni sayfa oluşturulacaksa o sayfa isminin eklenmesi gereken class'lar: MainWindow.xaml.cs, MainWindow.xaml, MainWindowViewModel.cs, KullaniciYonetimi.xaml.cs, Kullanicilar.cs, Kullanici.cs,AuthzService.cs, 
namespace SideBar_Nav
{
    public partial class App : Application
    {
        public static RFIDReader SharedReader = new RFIDReader();

        // 🔐 Supabase Bağlantı Bilgileri: Veritabanı bağlantısı direkt olarak uygulama ekranında girilebilir. 
        public static string SupabaseUrl { get; set; } = "https://cnvvdkoqsyaoraawkgez.supabase.co"; 
        public static string SupabaseKey { get; set; } = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImNudnZka29xc3lhb3JhYXdrZ2V6Iiwicm9sZSI6ImFub24iLCJpYXQiOjE3NTg3ODk0MDYsImV4cCI6MjA3NDM2NTQwNn0.V8piPLWb3-LW-kpFWGrhWY16k7GS2stJruW3LSHxPSg";

        public static Supabase.Client SupabaseClient { get; private set; }

        // 🌐 Supabase Başlatıcı, not: Supabase'e eklenilen her tablo için Models klasörü altında bir dosya oluşturulmalıdır.
        public static async Task InitializeSupabase()
        {
            var options = new Supabase.SupabaseOptions
            {
                AutoConnectRealtime = true
            };

            SupabaseClient = new Supabase.Client(SupabaseUrl, SupabaseKey, options);
            await SupabaseClient.InitializeAsync();
        }

        public static string AktifRezervasyonNo { get; set; } = null; // Sevkiyat.xaml 

        // 🚀 Supabase otomatik başlat
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            await InitializeSupabase();
        }
        public static Supabase.Gotrue.User AktifKullanici { get; set; }

    }
}

// Developed by: Mustafa Gümüş 190403058 
//               

// USED SOFTWARE TOOLS: .NET WPF
//                      SUPABASE
//                      IMPINJ OCTANE SDK
//                      GOOGLE CLOUD
//
// USED HARDWARE TOOLS: IMPINJ R420 RFID Reader
//                      UHF RFID ANTENNAS
//                      IMPINJ GPIO BOX
//                      FOUR CHANNEL RELAY(SONGLE ARDUINO)
//                      SCHNEIDER SIGNAL TOWER 
//                      
//
//                      --this project was developed under the umbrella of AFSUAM™ in three months.--

//                                                   AFSUAM™
//                                                    2025



// may we meet again

