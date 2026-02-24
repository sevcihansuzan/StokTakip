using SideBar_Nav.Models;
using SideBar_Nav.Pages;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SideBar_Nav
{
    public partial class MainWindow : Window
    {
        // ViewModel
        public MainWindowViewModel Vm { get; } = new MainWindowViewModel();

        // Sayfa örneklerini burada saklıyoruz
        private readonly Dictionary<Type, Page> pageCache = new();

        // Sayfa -> izin kontrolü haritası
        private readonly Dictionary<Type, Func<Kullanicilar, bool>> pagePermission = new();

        public MainWindow()
        {
            InitializeComponent();

            // DataContext
            DataContext = Vm;

            // İzin haritasını hazırla
            BuildPermissionMap();

            // Tüm sayfaları oluştur ve sakla
            InitializeAllPages();

            // İlk açılışta LoginPage
            navframe.Navigate(pageCache[typeof(LoginPage)]);

            this.Closing += MainWindow_Closing;
            connectionIndicator.Fill = new SolidColorBrush(Colors.Red);
            readingIndicator.Fill = new SolidColorBrush(Colors.Red);

            // Başlangıçta kullanıcı adı
            GuncelleAktifKullanici();

        }



        

        private void BuildPermissionMap()
        {
            // İzin kontrolleri: hangi sayfaya girmek için hangi allow şart?
            // Giriş/LoginPage için kontrol yok.
            pagePermission[typeof(UrunTakip)] = me => me?.UrunTakipAllow ?? false;
            pagePermission[typeof(Personel)] = me => me?.PersonelAllow ?? false;
            pagePermission[typeof(Sevkiyat)] = me => me?.SevkiyatAllow ?? false;
            pagePermission[typeof(KaliteKontrol)] = me => me?.KaliteKontrolAllow ?? false;
            pagePermission[typeof(StokYonetimi)] = me => me?.StokYonetimiAllow ?? false;
            pagePermission[typeof(SatisYonetimi)] = me => me?.SatisYonetimiAllow ?? false;
            pagePermission[typeof(Iptal)] = me => me?.IptalAllow ?? false;
            pagePermission[typeof(GPIO_ops)] = me => me?.GpioOpsAllow ?? false;
            pagePermission[typeof(Reading)] = me => me?.ReadingAllow ?? false;
            pagePermission[typeof(Admin)] = me => me?.AdminAllow ?? false;
            pagePermission[typeof(RezOlustur)] = me => me?.RezOlusturAllow ?? false;

            // KullaniciYonetimi sayfan varsa (listeye eklendiği için):
            pagePermission[typeof(KullaniciYonetimi)] = me => me?.KullaniciYonetimiAllow ?? false;
            pagePermission[typeof(Uretim)] = me => me?.UretimAllow ?? false;
            // YENİ: Sensör Takip sayfası için izin tanımı
            // Şimdilik herkese açık yapmak istersen:
            pagePermission[typeof(SensorMonitorPage)] = me => true;
        }

        public void GuncelleAktifKullanici()
        {
            // Ekrandaki metni ViewModel üzerinden ver
            if (FindName("txtAktifKullanici") is TextBlock tb)
                tb.Text = Vm?.DisplayName ?? "—";
        }

        public T GetPage<T>() where T : Page
        {
            if (pageCache.TryGetValue(typeof(T), out Page page))
                return (T)page;

            throw new Exception($"Sayfa bulunamadı: {typeof(T).Name}");
        }

        private void InitializeAllPages()
        {
            // Uygulamada yer alan tüm sayfalar
            var pages = new List<Type>
            {
                typeof(UrunTakip),
                typeof(Personel),
                typeof(Sevkiyat),
                typeof(KaliteKontrol),
                typeof(StokYonetimi),
                typeof(SatisYonetimi),
                typeof(Iptal),
                typeof(GPIO_ops),
                typeof(Reading),
                typeof(Admin),
                typeof(RezOlustur),
                typeof(LoginPage),
                typeof(KullaniciYonetimi),
                typeof(Uretim),
                typeof(SensorMonitorPage)
            };

            foreach (var pageType in pages)
            {
                if (pageCache.ContainsKey(pageType)) continue;

                Page instance = (Page)Activator.CreateInstance(pageType);

                // LoginPage ise event bağla
                if (instance is LoginPage loginPage)
                {
                    loginPage.LoginSuccess += () =>
                    {
                        // LoginPage, başarılı girişte Vm.SetMe(...) çağırıyor.
                        // Burada sadece ekranda kullanıcı adını tazeleyelim.
                        Dispatcher.Invoke(GuncelleAktifKullanici);
                    };
                }

                pageCache[pageType] = instance;
            }
        }

        private void sidebar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = sidebar.SelectedItem as NavButton;
            if (selected?.Navlink == null)
                return;

            Type pageType = selected.Navlink;

            // LoginPage için izne bakma
            if (pageType != typeof(LoginPage))
            {
                // İzin kontrolü varsa çalıştır
                if (pagePermission.TryGetValue(pageType, out var check))
                {
                    var me = Vm?.Me;
                    bool allowed = check?.Invoke(me) ?? false;

                    if (!allowed)
                    {
                        // İzin yok: geçişi engelle
                        e.Handled = true;
                        sidebar.SelectedItem = null;
                        MessageBox.Show("Bu sayfa için yetkiniz yok.", "Yetki Yok", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
            }

            if (pageCache.TryGetValue(pageType, out Page targetPage))
            {
                navframe.Navigate(targetPage);
            }
            else
            {
                MessageBox.Show("Sayfa önceden oluşturulmamış!");
            }
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (App.SharedReader.isRunning)
            {
                App.SharedReader.StopReader();
                MessageBox.Show("Okuma işlemi durduruldu");
            }

            if (App.SharedReader.isConnected)
            {
                App.SharedReader.DisconnectReader();
                MessageBox.Show("Bağlantı kesildi");
            }
        }

        public void SetConnectionIndicatorColor(Color color)
        {
            connectionIndicator.Fill = new SolidColorBrush(color);
        }

        public void SetReadingIndicatorColor(Color color)
        {
            readingIndicator.Fill = new SolidColorBrush(color);
        }
    }
}
