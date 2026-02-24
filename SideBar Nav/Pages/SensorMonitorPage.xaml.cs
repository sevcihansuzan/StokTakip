using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using Newtonsoft.Json.Linq;
using SideBar_Nav.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Linq;

// OxyPlot Namespace'leri
using OxyPlot;
using OxyPlot.Series;
using OxyPlot.Axes;

namespace SideBar_Nav.Pages
{
    public partial class SensorMonitorPage : Page, INotifyPropertyChanged
    {
        private readonly IotDatabaseService _dbService;
        private IMqttClient _mqttClient;
        private bool _motorCalisiyor = false;
        private DateTime _motorBaslangic;

        // TABLO LİSTELERİ - UI Binding için
        public ObservableCollection<SensorDataModel> SensorDataList { get; set; }
        public ObservableCollection<ProductTraceModel> ProductTraceList { get; set; }

        // OXYPLOT MODELİ
        private PlotModel _myPlotModel;
        public PlotModel MyPlotModel
        {
            get => _myPlotModel;
            set { _myPlotModel = value; OnPropertyChanged(); }
        }

        private LineSeries _powerSeries;
        private LineSeries _tempSeries;
        private LineSeries _yieldSeries; // Verim Serisi
        private int _dataPointCounter = 0;

        // CANLI VERİ BINDINGLERİ
        private string _akim = "0.00";
        public string Akim { get => _akim; set { _akim = value; OnPropertyChanged(); } }

        private string _voltaj = "0.00";
        public string Voltaj { get => _voltaj; set { _voltaj = value; OnPropertyChanged(); } }

        private string _sicaklik = "0.00";
        public string Sicaklik { get => _sicaklik; set { _sicaklik = value; OnPropertyChanged(); } }

        private string _durum = "Bağlanıyor...";
        public string Durum { get => _durum; set { _durum = value; OnPropertyChanged(); } }

        // KPI'LAR
        private double _verim;
        public double Verim { get => _verim; set { _verim = value; OnPropertyChanged(); } }

        private double _kwhUrun;
        public double KwhUrun { get => _kwhUrun; set { _kwhUrun = value; OnPropertyChanged(); } }

        private double _sureUrun;
        public double SureUrun { get => _sureUrun; set { _sureUrun = value; OnPropertyChanged(); } }

        // RENK DURUMLARI (IKAZLAR)
        private string _saglikliColor = "#28a745"; // Varsayılan Yeşil
        public string SaglikliColor { get => _saglikliColor; set { _saglikliColor = value; OnPropertyChanged(); } }

        private string _bakimColor = "#E0E0E0"; // Varsayılan Gri
        public string BakimColor { get => _bakimColor; set { _bakimColor = value; OnPropertyChanged(); } }

        public SensorMonitorPage()
        {
            InitializeComponent();

            // Koleksiyonları ilklendir
            SensorDataList = new ObservableCollection<SensorDataModel>();
            ProductTraceList = new ObservableCollection<ProductTraceModel>();

            // --- OxyPlot Grafiğini Hazırla ---
            SetupPlot();
            DataContext = this;
            _dbService = new IotDatabaseService();

            // Sayfa açılır açılmaz verilerin akması için:
            SimulasyonBaslat();

            Loaded += SensorMonitorPage_Loaded;
            //MQTT BAĞLANTISI BAŞLAT
            //Task.Run(MqttBaslat);
        }

        private void SetupPlot()
        {
            MyPlotModel = new PlotModel
            {
                Title = "Verim / Zaman Grafiği",
                TitleFontSize = 14,
                PlotAreaBorderThickness = new OxyThickness(0, 0, 0, 1),
                PlotAreaBorderColor = OxyColors.Gray
            };

            // X Ekseni (Zaman/Örnek Akışı)
            MyPlotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Örnek (Zaman)",
                FontSize = 10,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.FromAColor(15, OxyColors.Black)
            });

            // Y Ekseni (Sadece Verim %)
            MyPlotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Verim (%)",
                Minimum = 0,
                Maximum = 105, // %100'ün biraz üstü pay bırakmak iyidir
                FontSize = 10,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.FromAColor(30, OxyColors.Black)
            });

            // Sadece Verim Serisi
            _yieldSeries = new LineSeries
            {
                Title = "Anlık Verim",
                Color = OxyColor.Parse("#28a745"), // Yeşil tonu
                StrokeThickness = 3,
                MarkerType = MarkerType.Circle,
                MarkerSize = 4,
                MarkerFill = OxyColors.White,
                MarkerStroke = OxyColor.Parse("#28a745"),
                MarkerStrokeThickness = 2
            };

            MyPlotModel.Series.Add(_yieldSeries);
        }

        private void SimulasyonBaslat()
        {
            Random rnd = new Random();
            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1); // Her 1 saniyede bir çalışır

            timer.Tick += (s, e) =>
            {
                // 1. Rastgele Veri Üret (85 ile 99 arası)
                double rastgeleVerim = rnd.Next(85, 100); // 100 dahil değil, 85-99 arası

                // 2. KPI Değerlerini Güncelle (Arayüzdeki kartlar için)
                Verim = rastgeleVerim;
                SureUrun = rnd.Next(20, 35); // Örnek süre
                KwhUrun = rnd.NextDouble() * (1.5 - 0.5) + 0.5; // 0.5 - 1.5 arası kWh

                // 3. Grafiği Güncelle
                _yieldSeries.Points.Add(new DataPoint(_dataPointCounter, Verim));
                _dataPointCounter++;

                // Grafik çok dolmasın diye son 30 veriyi göster (Kayan grafik)
                if (_yieldSeries.Points.Count > 30)
                {
                    _yieldSeries.Points.RemoveAt(0);
                }

                // Grafiği Yeniden Çiz
                MyPlotModel.InvalidatePlot(true);

                // 4. İkaz Mantığını Çalıştır (Renkler değişsin)
                UpdateMaintenanceAlerts();

                // 5. Alt Tabloya "Simülasyon" Verisi Ekle
                SensorDataList.Insert(0, new SensorDataModel
                {
                    UrunID = "SIM_TEST",
                    SensorNoktasi = "Sanal-Motor",
                    Zaman = DateTime.Now,
                    Vrms = "220.5 V",
                    Irms = "4.22 A",
                    Guc = (220.5 * 4.22).ToString("0.0") + " W",
                    Sicaklik = rnd.Next(35, 55).ToString() + " °C"
                });

                // Tablo listesi çok uzamasın
                if (SensorDataList.Count > 10) SensorDataList.RemoveAt(SensorDataList.Count - 1);
            };

            timer.Start();
            Durum = "Simülasyon Modu Aktif";
        }

        private async void SensorMonitorPage_Loaded(object sender, RoutedEventArgs e)
        {
            await CekVeListele();
            await KpiGuncelle();
        }

        private async Task CekVeListele()
        {
            try
            {
                // Geçmiş sensör verilerini çek
                var gecmis = await _dbService.GetSonSensorKayitlariAsync();
                SensorDataList.Clear();
                foreach (var x in gecmis) SensorDataList.Add(x);

                // Not: ProductTraceList için DB servisinizde GetSonUretimKayitlariAsync gibi bir metod varsa buraya eklenmeli.
            }
            catch (Exception ex)
            {
                MessageBox.Show("DB veri çekilemedi: " + ex.Message);
            }
        }

        private async Task KpiGuncelle()
        {
            try
            {
                var kpi = await _dbService.GetSonUretimKpiAsync();
                if (kpi == null) return;

                await Dispatcher.InvokeAsync(() =>
                {
                    SureUrun = kpi.SureSn;
                    KwhUrun = kpi.KwhUrun;
                    Verim = kpi.Verim;

                    // İkaz mantığını kontrol et
                    UpdateMaintenanceAlerts();
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() => Durum = "KPI Hata: " + ex.Message);
            }
        }

        private void UpdateMaintenanceAlerts()
        {
            // Verim %90'ın altına düşerse ikaz ver
            if (Verim < 90)
            {
                SaglikliColor = "#FFFFFF"; // Beyaz (Etkisiz)
                BakimColor = "#DC3545";    // Kırmızı (Aktif İkaz)
            }
            else
            {
                SaglikliColor = "#28a745"; // Yeşil (Sağlıklı)
                BakimColor = "#E0E0E0";    // Gri (Pasif)
            }
        }

        private async Task MqttBaslat()
        {
            var factory = new MqttFactory();
            _mqttClient = factory.CreateMqttClient();

            var options = new MqttClientOptionsBuilder()
                .WithTcpServer("broker.hivemq.com", 1883)
                .WithClientId("Wpf_Client_" + Guid.NewGuid())
                .WithCleanSession()
                .Build();

            _mqttClient.UseApplicationMessageReceivedHandler(async e =>
            {
                try
                {
                    string payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                    var data = JObject.Parse(payload);

                    string timestampStr = data["timestamp"]?.ToString();
                    string uptime = data["gecen_sure"]?.ToString() ?? "";

                    DateTime sensorZaman = DateTime.TryParse(timestampStr, out var dt) ? dt : DateTime.Now;

                    double v = data["voltaj_V"]?.Value<double>() ?? 0;
                    double a = data["akim_mA"]?.Value<double>() ?? 0;
                    double g = data["guc_mW"]?.Value<double>() ?? (v * a);
                    double t = data["sicaklik_C"]?.Value<double>() ?? 0;
                    int rpm = data["rpm"]?.Value<int>() ?? 0;

                    // MOTOR MANTIĞI
                    bool motorAcik = (rpm > 0 && a > 50);

                    if (motorAcik && !_motorCalisiyor)
                    {
                        _motorCalisiyor = true;
                        _motorBaslangic = sensorZaman;
                        await _dbService.MotorUretimBaslatAsync("Makine-1", _motorBaslangic);
                    }
                    else if (!motorAcik && _motorCalisiyor)
                    {
                        _motorCalisiyor = false;
                        await _dbService.MotorUretimBitirAsync("Makine-1", _motorBaslangic, sensorZaman);
                        await KpiGuncelle();
                    }

                    // UI GÜNCELLE
                    await Dispatcher.InvokeAsync(() =>
                    {
                        Voltaj = v.ToString("0.0");
                        Akim = a.ToString("0.00");
                        Sicaklik = t.ToString("0.0");

                        // 📈 GRAFİĞİ GÜNCELLE (Güç, Sıcaklık ve Verim)
                        _powerSeries.Points.Add(new DataPoint(_dataPointCounter, g));
                        _tempSeries.Points.Add(new DataPoint(_dataPointCounter, t));
                        _yieldSeries.Points.Add(new DataPoint(_dataPointCounter, Verim));
                        _dataPointCounter++;

                        if (_powerSeries.Points.Count > 50)
                        {
                            _powerSeries.Points.RemoveAt(0);
                            _tempSeries.Points.RemoveAt(0);
                            _yieldSeries.Points.RemoveAt(0);
                        }

                        MyPlotModel.InvalidatePlot(true);

                        // Tabloya Ekle - SensorDataModel burada tanımlı olmalı
                        SensorDataList.Insert(0, new SensorDataModel
                        {
                            UrunID = "CANLI",
                            SensorNoktasi = "Makine-1",
                            Zaman = sensorZaman,
                            Vrms = $"{v:0.0} V",
                            Irms = $"{a:0.00} mA",
                            Guc = $"{g:0.0} mW",
                            Sicaklik = $"{t:0.0} °C"
                        });

                        if (SensorDataList.Count > 200)
                            SensorDataList.RemoveAt(SensorDataList.Count - 1);
                    });

                    // DB KAYDI
                    await _dbService.LogSensorDataAsync("Makine_1", sensorZaman, uptime, v, a, g, t, rpm);
                }
                catch (Exception ex)
                {
                    await Dispatcher.InvokeAsync(() => Durum = "Hata: " + ex.Message);
                }
            });

            try
            {
                await _mqttClient.ConnectAsync(options);
                await _mqttClient.SubscribeAsync("fabrika/makine1/elektrik");
                await Dispatcher.InvokeAsync(() => Durum = "Bağlandı");
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() => Durum = "Bağlantı Hatası: " + ex.Message);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // --- MODELLER (Eksik olan tanımlar buraya eklendi) ---

    public class SensorDataModel
    {
        public string UrunID { get; set; }
        public string SensorNoktasi { get; set; }
        public DateTime Zaman { get; set; }
        public string Vrms { get; set; }
        public string Irms { get; set; }
        public string Guc { get; set; }
        public string Sicaklik { get; set; }
    }

    public class ProductTraceModel
    {
        public string UrunID { get; set; }
        public string UretimSuresi { get; set; }
        public string GirisZamani { get; set; }
        public string CikisZamani { get; set; }
        public string OrtalamaGuc { get; set; }
        public string Verim { get; set; }
        public string MakineDurumu { get; set; }
    }
}