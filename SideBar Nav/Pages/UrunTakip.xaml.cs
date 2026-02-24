using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Data.SqlClient;
using System.Data;
using SideBar_Nav.Windows;
using System.Collections.ObjectModel;
using System.Globalization;
using SideBar_Nav.Models;
using SideBar_Nav.Services;
using Supabase.Postgrest;
using Supabase;

using System.IO.Ports;
using System.Threading;
using System.Diagnostics;
namespace SideBar_Nav.Pages
{
    /// <summary>
    /// Interaction logic for UrunTakip.xaml
    /// </summary>

    public partial class UrunTakip : Page
    {
        private Queue<string> epcQueue = new Queue<string>();
        private HashSet<string> processedEpcs = new HashSet<string>();
        private ObservableCollection<string> liveEpcs = new ObservableCollection<string>();

        private DataTable urunStokTable;
        private SqlDataAdapter urunStokAdapter;
        private List<UrunStok> filteredUrunler = new();

        private readonly UserSettingsService userSettingsService = new();
        private bool settingsLoadedFromSupabase;
        private MainWindowViewModel? mainVm;


        public UrunTakip()
        {
            InitializeComponent();
            btnSettings.Visibility = Visibility.Collapsed;
            Loaded += UrunTakip_Loaded;
            Unloaded += UrunTakip_Unloaded;
            cmbTarihSecimi.SelectedIndex = 0;
            dateSecim.SelectedDate = DateTime.Today;
            dateUretimTarihi.SelectedDate = DateTime.Today;
            App.SharedReader.TagsReported += OnTagReported;
            UrunTurleriniYukle();
            YuzeyIslemleriniYukle();
            LoadComPorts();

            BarcodeScannerBridge.Configure(TriggerScanAsync, () => _scanner != null && _scanner.IsOpen);

        }
        private SerialPort _scanner;
        private TaskCompletionSource<string> _readTcs;
        private readonly object _serialLock = new();
        private string _lastSelectedPort;

        // >>> EKLENENLER <<<
        private bool _isScanning = false;
        private CancellationTokenSource _scanCts;
        private void CancelScanIfAny()
        {
            try { _scanCts?.Cancel(); } catch { }
        }
        // <<< EKLENENLER >>>

        // Örn. sayfa yüklendiğinde aç
        // --- SAYFA YAŞAM DÖNGÜSÜ ---

        // UrunTakip.xaml.cs
        private void btnBarcodeSettings_Click(object sender, RoutedEventArgs e)
        {
            // _scanner senin sayfanda açık olan SerialPort (önceki mesajdaki kodu kullandıysan)
            if (_scanner == null || !_scanner.IsOpen)
            {
                MessageBox.Show("Önce barkod okuyucuya (COM) bağlanın.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var win = new BarcodeSettingsWindow(_scanner) { Owner = Window.GetWindow(this) };
            win.ShowDialog();
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            SafeClosePort();
        }

        // --- COM LİSTELEME / YENİLEME ---
        private void LoadComPorts()
        {
            try
            {
                var ports = SerialPort.GetPortNames().OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
                cmbComPorts.ItemsSource = ports;
                if (!string.IsNullOrEmpty(_lastSelectedPort) && ports.Contains(_lastSelectedPort))
                    cmbComPorts.SelectedItem = _lastSelectedPort;
                else if (ports.Count > 0 && cmbComPorts.SelectedItem == null)
                    cmbComPorts.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("COM portları listelenemedi: " + ex.Message);
            }
        }

        private void btnRefreshCom_Click(object sender, RoutedEventArgs e) => LoadComPorts();

        private void cmbComPorts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Kullanıcı COM’u değiştiğinde açık bir bağlantı varsa güvenli kapat
            if (_scanner != null && _scanner.IsOpen)
            {
                SafeClosePort();
                UpdateComUiState(false);
            }
            _lastSelectedPort = cmbComPorts.SelectedItem as string ?? "";
        }

        // --- BAĞLAN / KES ---
        private void btnConnectCom_Click(object sender, RoutedEventArgs e)
        {
            var portName = cmbComPorts.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(portName))
            {
                MessageBox.Show("Lütfen bir COM port seçin.");
                return;
            }

            try
            {
                SafeOpenPort(portName);
                UpdateComUiState(true);
                // (İsteğe bağlı) _lastSelectedPort’u sakla:
                // Properties.Settings.Default.LastCom = portName; Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                SafeClosePort();
                UpdateComUiState(false);
                MessageBox.Show("Bağlanırken hata: " + ex.Message);
            }
        }

        private void btnDisconnectCom_Click(object sender, RoutedEventArgs e)
        {
            // >>> EKLENDİ: devam eden okumayı iptal et, sonra kapat
            CancelScanIfAny();
            // <<<
            SafeClosePort();
            UpdateComUiState(false);
        }

        private void UpdateComUiState(bool connected)
        {
            btnConnectCom.IsEnabled = !connected;
            btnDisconnectCom.IsEnabled = connected;
            cmbComPorts.IsEnabled = !connected;
            btnRefreshCom.IsEnabled = !connected;

            lblComStatus.Text = connected ? "Bağlı" : "Kapalı";
            lblComStatus.Foreground = connected ? new SolidColorBrush(Colors.DarkGreen) : new SolidColorBrush(Colors.DarkRed);

            BarcodeScannerBridge.NotifyConnectionChanged(connected);
        }

        private void SafeOpenPort(string portName)
        {
            SafeClosePort(); // varsa kapat

            _scanner = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One)
            {
                NewLine = "\r\n",               // SSCOM’dan ~M00920001. gönderdiysen (CRLF)
                Encoding = Encoding.ASCII,
                ReadTimeout = 5000,
                WriteTimeout = 1000,
                Handshake = Handshake.None
            };
            _scanner.DataReceived += Scanner_DataReceived;
            _scanner.ErrorReceived += Scanner_ErrorReceived;
            _scanner.PinChanged += Scanner_PinChanged;

            try
            {
                _scanner.Open();
            }
            catch
            {
                // eventleri detach et
                _scanner.DataReceived -= Scanner_DataReceived;
                _scanner.ErrorReceived -= Scanner_ErrorReceived;
                _scanner.PinChanged -= Scanner_PinChanged;
                _scanner.Dispose();
                _scanner = null;
                throw;
            }
        }

        private void SafeClosePort()
        {
            try
            {
                if (_scanner != null)
                {
                    _scanner.DataReceived -= Scanner_DataReceived;
                    _scanner.ErrorReceived -= Scanner_ErrorReceived;
                    _scanner.PinChanged -= Scanner_PinChanged;

                    if (_scanner.IsOpen)
                    {
                        try { _scanner.DiscardInBuffer(); } catch { }
                        try { _scanner.DiscardOutBuffer(); } catch { }
                        _scanner.Close();
                    }
                    _scanner.Dispose();
                    _scanner = null;
                }
                BarcodeScannerBridge.NotifyConnectionChanged(false);
            }
            catch { /* yut */ }
        }

        // --- DATA RECEIVED / HATA OLAYLARI ---
        private void Scanner_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string payload;
                lock (_serialLock)
                {
                    // CRLF açıksa:
                    if (_scanner.NewLine != null)
                        payload = _scanner.ReadLine();
                    else
                        payload = _scanner.ReadExisting(); // CRLF kapalıysa
                }

                payload = payload.Trim();
                _readTcs?.TrySetResult(payload);
            }
            catch (TimeoutException tex)
            {
                _readTcs?.TrySetException(tex);
            }
            catch (Exception ex)
            {
                _readTcs?.TrySetException(ex);
                // Örn: kablo çekildi—UI’yı güvenli güncelle
                Dispatcher.Invoke(() =>
                {
                    UpdateComUiState(false);
                    SafeClosePort();
                });
            }
        }

        private void Scanner_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            // Hataları logla, UI’ye yansıt (çökmesin)
            Debug.WriteLine("Serial error: " + e.EventType);
        }

        private void Scanner_PinChanged(object sender, SerialPinChangedEventArgs e)
        {
            // Bazı durumlarda kopma/pin değişimi yakalanır
            Debug.WriteLine("Serial pin changed: " + e.EventType);
        }

        // --- TETİKLE & OKU ---
        private async Task<string> TriggerScanAsync(int timeoutMs, CancellationToken externalToken)
        {
            if (_scanner == null || !_scanner.IsOpen)
                throw new InvalidOperationException("Barkod okuyucu bağlı değil.");

            // tek atımlık okuma
            _readTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                lock (_serialLock)
                {
                    _scanner.DiscardInBuffer();
                    _scanner.Write("~T."); // Instruction trigger komutu
                }
            }
            catch (Exception ex)
            {
                _readTcs.TrySetException(ex);
            }

            using var timeoutCts = new CancellationTokenSource(timeoutMs);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, externalToken);
            using (linked.Token.Register(() =>
                _readTcs.TrySetException(new OperationCanceledException("Okuma iptal edildi veya zaman aşımı."))))
            {
                return await _readTcs.Task.ConfigureAwait(false);
            }
        }

        // --- “Barkod Oku (HID)” butonun Click’i (adını değiştirebilirsin) ---
        private async void BtnBarkodOku_Click(object sender, RoutedEventArgs e)
        {
            // Bağlantı yoksa hiç başlatma (otomatik bağlama yok)
            if (_scanner == null || !_scanner.IsOpen)
            {
                MessageBox.Show("Barkod Okuyucu Bağlı Değil", "Uyarı",
                                 MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Zaten bir okuma sürüyorsa tekrar başlatma
            if (_isScanning)
                return;

            _isScanning = true;
            btnBarkodOku.IsEnabled = false;

            _scanCts?.Dispose();
            _scanCts = new CancellationTokenSource();

            try
            {
                // İlk cevap
                var data = await TriggerScanAsync(5000, _scanCts.Token);

                // Bazı cihazlar önce T[ACK], sonra barkodu yollar; bu durumda tekrar bekle
                if (string.Equals(data, "T[ACK]", StringComparison.OrdinalIgnoreCase))
                {
                    data = await TriggerScanAsync(5000, _scanCts.Token);
                }

                await Dispatcher.InvokeAsync(() => txtBarkodNo.Text = data);
            }
            catch (OperationCanceledException)
            {
                // Kes’e basıldı veya timeout — sessiz geç
            }
            catch (Exception ex)
            {
                MessageBox.Show("Okuma hatası: " + ex.Message);
            }
            finally
            {
                _isScanning = false;
                btnBarkodOku.IsEnabled = true;
                _scanCts?.Dispose();
                _scanCts = null;
            }
        }

        private async void UrunTakip_Loaded(object sender, RoutedEventArgs e)
        {
            if (mainVm == null && Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainVm = mainWindow.Vm;
                if (mainVm != null)
                {
                    mainVm.PropertyChanged += MainVm_PropertyChanged;
                    UpdateSettingsButtonVisibility(mainVm.Me?.UserSettingsAllow ?? false);
                }
            }
            else if (mainVm != null)
            {
                UpdateSettingsButtonVisibility(mainVm.Me?.UserSettingsAllow ?? false);
            }

            await EnsureSettingsLoadedAsync();
        }

        private void UrunTakip_Unloaded(object sender, RoutedEventArgs e)
        {
            if (mainVm != null)
            {
                mainVm.PropertyChanged -= MainVm_PropertyChanged;
                mainVm = null;
            }

            //BarcodeScannerBridge.Clear();
        }

        private void MainVm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainWindowViewModel.Me) || e.PropertyName == "Me.UserSettingsAllow")
            {
                Dispatcher.InvokeAsync(async () =>
                {
                    bool allowed = mainVm?.Me?.UserSettingsAllow ?? false;
                    UpdateSettingsButtonVisibility(allowed);
                    if (e.PropertyName == nameof(MainWindowViewModel.Me))
                    {
                        await EnsureSettingsLoadedAsync(forceReload: true);
                    }
                });
            }
        }

        private void UpdateSettingsButtonVisibility(bool allowed)
        {
            if (btnSettings == null)
                return;

            btnSettings.Visibility = allowed ? Visibility.Visible : Visibility.Collapsed;
            btnSettings.IsEnabled = allowed;
        }

        private Guid? GetCurrentUserId()
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                var id = mw.Vm?.Me?.Id ?? Guid.Empty;
                if (id != Guid.Empty)
                    return id;
            }

            if (App.AktifKullanici != null && Guid.TryParse(App.AktifKullanici.Id, out Guid parsed))
                return parsed;

            return null;
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            bool allowed = mainVm?.Me?.UserSettingsAllow ?? false;
            if (!allowed)
            {
                MessageBox.Show("Bu işlem için yetkiniz yok.", "Yetki Yok", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var ownerWindow = Window.GetWindow(this);
            var window = new UserSettingsWindow
            {
                Owner = ownerWindow
            };
            window.ShowDialog();
        }

        private async Task EnsureSettingsLoadedAsync(bool forceReload = false)
        {
            if (forceReload)
                settingsLoadedFromSupabase = false;

            if (settingsLoadedFromSupabase)
                return;

            settingsLoadedFromSupabase = true;

            Guid? userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                try
                {
                    var snapshot = await userSettingsService.GetSettingsAsync(userId.Value);
                    if (snapshot != null)
                    {
                        App.SharedReader.LoadFromSnapshot(snapshot);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Kullanıcı ayarları yüklenemedi: {ex.Message}");
                }
            }
        }
        #region baglanti
        #region Iptextbox
        private void UpdatePlaceholders()
        {
            IPPlaceholder.Visibility = string.IsNullOrWhiteSpace(IPTextBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        }
        private void IPTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            IPPlaceholder.Visibility = Visibility.Collapsed;
        }
        private void IPTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(IPTextBox.Text))
            {
                IPPlaceholder.Visibility = Visibility.Visible;
            }
        }
        #endregion
        private void buttonConnect_Click(object sender, RoutedEventArgs e)
        {
            string ipAddress = IPTextBox.Text.Trim();
            if (string.IsNullOrEmpty(ipAddress))
            {
                MessageBox.Show("Lütfen bir IP adresi girin!", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (!App.SharedReader.isConnected)
            {
                App.SharedReader.ReaderHostname = ipAddress;
                if (App.SharedReader.LastSavedSettings != null)
                {
                    App.SharedReader.LoadFromSnapshot(App.SharedReader.LastSavedSettings);
                }
                else
                {
                    App.SharedReader.LoadFromSnapshot(ReaderSettingsSnapshot.CreateDefault(), remember: false);
                }

                App.SharedReader.ConnectReader();
                if (App.SharedReader.isConnected == false)
                {
                    MessageBox.Show("Bağlantı başarısız.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;

                }
                MessageBox.Show("Bağlantı başarılı.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            else
            {
                MessageBox.Show("Okuyucu zaten bağlı!", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }
        public void buttonStart_Click(object sender, RoutedEventArgs e)
        {
            if (App.SharedReader.isConnected) // Bağlantı Kontrolü
            {
                if (!App.SharedReader.isRunning) // Okuma Durumu Kontrolü
                {
                    if (!App.SharedReader.isSettingsApplied)
                    {
                        if (!App.SharedReader.EnsureSettingsApplied())
                        {
                            MessageBox.Show("Ayarlar uygulanamadı. Lütfen okuyucu bağlantısını kontrol edin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                    }

                    if (App.SharedReader.isSettingsApplied) // Ayar Kontrolü(urun takip)
                    {
                        App.SharedReader.StartReader();
                        MessageBox.Show("Okuma işlemi başlatıldı");
                    }
                    else
                    {
                        MessageBox.Show("Ayarlar uygulanamadı. Lütfen tekrar deneyin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    MessageBox.Show("Zaten okuma işlemi yapılıyor.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                MessageBox.Show("Okuyucu bağlı değil.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        public void buttonStop_Click(object sender, RoutedEventArgs e)
        {
            if (App.SharedReader.isConnected)
            {
                if (App.SharedReader.isRunning)
                {
                    App.SharedReader.StopReader();
                    MessageBox.Show("Okuma işlemi durduruldu");
                }
                else
                {
                    MessageBox.Show("Şuan zaten okuma işlemi yapılmıyor.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                //throw new InvalidOperationException("Reader is not connected.");
                MessageBox.Show("Okuyucu bağlı değil.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        private void buttonDisconnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (App.SharedReader.isConnected)
                {

                    if (App.SharedReader.isRunning)
                    {
                        MessageBox.Show("Lütfen önce okuma işlemini durdurun.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    else
                    {
                        App.SharedReader.DisconnectReader();
                        MessageBox.Show("Bağlantı başarıyla kesildi.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    MessageBox.Show("Okuyucu zaten bağlı değil.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Bağlantıyı keserken bir hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion
        private void OnTagReported(RfidData tag)
        {
            // RFID thread'ini bloklamamak için ayrı Task başlat
            _ = Task.Run(async () =>
            {
                string epc = tag.EPC;

                // Kuyrukta veya daha önce işlenmişse geç
                if (epcQueue.Contains(epc) || processedEpcs.Contains(epc))
                    return;

                // Supabase üzerinden kontrol et
                if (await IsEpcInDatabaseAsync(epc))
                {
                    processedEpcs.Add(epc);
                    return;
                }

                // UI thread içinde güncelle
                Application.Current.Dispatcher.Invoke(() =>
                {
                    epcQueue.Enqueue(epc);
                    if (!liveEpcs.Contains(epc))
                        liveEpcs.Add(epc);

                    if (string.IsNullOrWhiteSpace(txtEPC.Text))
                    {
                        SetNextEpcFromQueue();
                    }
                });
            });
        }

        private void SetNextEpcFromQueue()
        {
            if (epcQueue.Count > 0)
            {
                var next = epcQueue.Dequeue();
                liveEpcs.Remove(next);
                txtEPC.Text = next;
            }
            else
            {
                txtEPC.Text = "";
            }
        }


        private async Task<bool> IsEpcInDatabaseAsync(string epc)
        {
            try
            {
                var result = await App.SupabaseClient
                    .From<UrunStok>() // models klasöründe tanımlanmış olmalı
                    .Filter("EPC", Constants.Operator.Equals, epc)
                    .Get();

                return result.Models.Count > 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("EPC kontrolü sırasında hata: " + ex.Message);
                return false;
            }
        }

        private void BtnEpcList_Click(object sender, RoutedEventArgs e)
        {
            var pencere = new EpcSelectWindow(liveEpcs);
            if (pencere.ShowDialog() == true)
            {
                string secilen = pencere.SelectedEpc;
                txtEPC.Text = secilen;
                liveEpcs.Remove(secilen);
                epcQueue = new Queue<string>(epcQueue.Where(x => x != secilen));
            }
        }
        private async Task UrunTurleriniYukle() //void
        {
            try
            {
                var result = await App.SupabaseClient
                    .From<UrunTurleri>()
                    .Get();

                // Supabase'ten gelen verileri DataTable’a dönüştür
                DataTable dt = new DataTable();
                dt.Columns.Add("UrunTuru", typeof(string));
                dt.Columns.Add("YogunlukKatsayisi", typeof(double));

                foreach (var item in result.Models)
                {
                    dt.Rows.Add(item.UrunTuru, item.YogunlukKatsayisi);
                }

                cmbUrunTuru.ItemsSource = dt.DefaultView;
                cmbUrunTuru.DisplayMemberPath = "UrunTuru";
                cmbUrunTuru.SelectedValuePath = "YogunlukKatsayisi";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ürün türleri yüklenemedi: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private async void BtnUrunTuruEkle_Click(object sender, RoutedEventArgs e)
        {
            var pencere = new UrunTuruEkleWindow();
            pencere.ShowDialog();

            if (pencere.DegisiklikYapildi)
            {
                await Task.Delay(200); // Supabase'e yeni kayıt düşmesi için küçük gecikme (isteğe bağlı)

                await Dispatcher.InvokeAsync(UrunTurleriniYukle); // artık async olduğu için bu şekilde çağırıyoruz

                if (!string.IsNullOrEmpty(pencere.SonEklenenUrunTuru))
                {
                    foreach (DataRowView item in cmbUrunTuru.Items)
                    {
                        if (item["UrunTuru"].ToString() == pencere.SonEklenenUrunTuru)
                        {
                            cmbUrunTuru.SelectedItem = item;
                            break;
                        }
                    }
                }
            }
        }
        /*
        private async void YuzeyIslemleriniYukle()
        {
            try
            {
                var result = await App.SupabaseClient
                    .From<YuzeyIslemleri>()
                    .Get();

                DataTable dt = new DataTable();
                dt.Columns.Add("YuzeyIslemi", typeof(string));

                foreach (var item in result.Models)
                {
                    dt.Rows.Add(item.YuzeyIslemi);
                }

                cmbYuzeyIslemi.ItemsSource = dt.DefaultView;
                cmbYuzeyIslemi.DisplayMemberPath = "YuzeyIslemi";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Yüzey işlemleri yüklenemedi: " + ex.Message);
            }
        }*/
        private async void YuzeyIslemleriniYukle()
        {
            try
            {
                var result = await App.SupabaseClient
                    .From<YuzeyIslemleri>()
                    .Get();

                DataTable dt = new DataTable();
                dt.Columns.Add("YuzeyIslemi", typeof(string));

                foreach (var item in result.Models)
                    dt.Rows.Add(item.YuzeyIslemi);

                cmbYuzeyIslemi.ItemsSource = dt.DefaultView;
                cmbYuzeyIslemi.DisplayMemberPath = "YuzeyIslemi";
                cmbYuzeyIslemi.SelectedValuePath = "YuzeyIslemi";   // << EKLENDİ
            }
            catch (Exception ex)
            {
                MessageBox.Show("Yüzey işlemleri yüklenemedi: " + ex.Message);
            }
        }

        private async void BtnYuzeyIslemiEkle_Click(object sender, RoutedEventArgs e)
        {
            var pencere = new YuzeyIslemiEkleWindow(); // Açılacak küçük pencere
            pencere.ShowDialog();

            if (pencere.DegisiklikYapildi)
            {
                await Task.Delay(200);
                await Dispatcher.InvokeAsync(YuzeyIslemleriniYukle);

                if (!string.IsNullOrEmpty(pencere.SonEklenenYuzeyIslemi))
                {
                    foreach (DataRowView item in cmbYuzeyIslemi.Items)
                    {
                        if (item["YuzeyIslemi"].ToString() == pencere.SonEklenenYuzeyIslemi)
                        {
                            cmbYuzeyIslemi.SelectedItem = item;
                            break;
                        }
                    }
                }
            }
        }


        private void AlanTonajOtomatikHesapla(object sender, EventArgs e)
        {
            try
            {
                double kalinlik = 0, en = 0, boy = 0, yogunlukKatsayisi = 0;
                int adet = 0;

                bool sayisalDegerlerGecerli =
                    double.TryParse(txtKalınlık.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out kalinlik) &&
                    double.TryParse(txtEn.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out en) &&
                    double.TryParse(txtBoy.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out boy) &&
                    int.TryParse(txtPlakaAdedi.Text, out adet);

                bool yogunlukGecerli =
                    cmbUrunTuru.SelectedValue != null &&
                    double.TryParse(cmbUrunTuru.SelectedValue.ToString().Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out yogunlukKatsayisi);

                if (sayisalDegerlerGecerli && yogunlukGecerli)
                {
                    double alan = (en * boy * adet) / 10000.0;
                    double tonaj = alan * kalinlik * yogunlukKatsayisi;

                    txtAlan.Text = alan.ToString("F5", CultureInfo.GetCultureInfo("tr-TR"));
                    txtTonaj.Text = tonaj.ToString("F5", CultureInfo.GetCultureInfo("tr-TR"));
                }
                else
                {
                    txtAlan.Text = "-";
                    txtTonaj.Text = "-";
                }
            }
            catch
            {
                txtAlan.Text = "-";
                txtTonaj.Text = "-";
            }
        }







        /*
        private async void BtnKaydet_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool hasError = false;
                SolidColorBrush redBrush = new SolidColorBrush(Colors.Red);
                SolidColorBrush normalBrush = new SolidColorBrush(Colors.Gray);

                TextBox[] requiredTextBoxes = {
            txtEPC, txtBarkodNo, txtBandilNo, txtPlakaNo, txtSeleksiyon,
            txtKalınlık, txtEn, txtBoy, txtPlakaAdedi
        };

                foreach (TextBox tb in requiredTextBoxes)
                {
                    tb.BorderBrush = string.IsNullOrWhiteSpace(tb.Text) ? redBrush : normalBrush;
                    tb.BorderThickness = new Thickness(1);
                    hasError |= string.IsNullOrWhiteSpace(tb.Text);
                }

                ComboBox[] requiredComboBoxes = { cmbUrunTipi, cmbUrunTuru, cmbYuzeyIslemi };
                foreach (ComboBox cb in requiredComboBoxes)
                {
                    bool invalid = cb.SelectedItem == null;
                    cb.BorderBrush = invalid ? Brushes.DarkMagenta : Brushes.Blue;
                    cb.BorderThickness = new Thickness(invalid ? 2 : 1);
                    hasError |= invalid;
                }

                if (!dateUretimTarihi.SelectedDate.HasValue)
                {
                    dateUretimTarihi.BorderBrush = redBrush;
                    dateUretimTarihi.BorderThickness = new Thickness(2);
                    hasError = true;
                }
                else
                {
                    dateUretimTarihi.BorderBrush = normalBrush;
                    dateUretimTarihi.BorderThickness = new Thickness(1);
                }

                if (hasError)
                {
                    MessageBox.Show("Lütfen tüm alanları doldurun.", "Eksik Bilgi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string epc = txtEPC.Text.Trim();
                string barkodNo = txtBarkodNo.Text.Trim();

                // 1. EPC kontrol
                var epcCheck = await App.SupabaseClient
                    .From<UrunStok>()
                    .Filter("EPC", Supabase.Postgrest.Constants.Operator.Equals, epc)
                    .Get();

                if (epcCheck.Models.Count > 0)
                {
                    MessageBox.Show("Bu EPC zaten kayıtlı!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                    processedEpcs.Add(epc);

                    SetNextEpcFromQueue();
                    return;
                }

                // 2. Barkod kontrol
                var barkodCheck = await App.SupabaseClient
                    .From<UrunStok>()
                    .Filter("BarkodNo", Supabase.Postgrest.Constants.Operator.Equals, barkodNo)
                    .Get();

                if (barkodCheck.Models.Count > 0)
                {
                    MessageBox.Show("Bu Barkod No zaten kayıtlı!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 3. Yeni ürün oluştur
                var yeniUrun = new UrunStok
                {
                    EPC = epc,
                    BarkodNo = barkodNo,
                    BandilNo = txtBandilNo.Text.Trim(),
                    PlakaNo = txtPlakaNo.Text.Trim(),
                    UrunTipi = (cmbUrunTipi.SelectedItem as ComboBoxItem)?.Content.ToString(),
                    UrunTuru = (cmbUrunTuru.SelectedItem as DataRowView)?["UrunTuru"].ToString(),
                    YuzeyIslemi = (cmbYuzeyIslemi.SelectedItem as ComboBoxItem)?.Content.ToString(),
                    Seleksiyon = txtSeleksiyon.Text.Trim(),
                    UretimTarihi = dateUretimTarihi.SelectedDate.HasValue
                    ? dateUretimTarihi.SelectedDate.Value.Date.Add(DateTime.Now.TimeOfDay)
                    : DateTime.Now,
                    Kalinlik = (decimal)double.Parse(txtKalınlık.Text.Trim().Replace(',', '.'), CultureInfo.InvariantCulture),
                    StokEn = (decimal)double.Parse(txtEn.Text.Trim().Replace(',', '.'), CultureInfo.InvariantCulture),
                    StokBoy = (decimal)double.Parse(txtBoy.Text.Trim().Replace(',', '.'), CultureInfo.InvariantCulture),
                    PlakaAdedi = int.Parse(txtPlakaAdedi.Text.Trim()),
                    StokAlan = (decimal)double.Parse(txtAlan.Text.Replace("Alan (m²): ", "").Trim().Replace(',', '.'), CultureInfo.InvariantCulture),
                    StokTonaj = (decimal)double.Parse(txtTonaj.Text.Replace("Tonaj (kg): ", "").Trim().Replace(',', '.'), CultureInfo.InvariantCulture),
                    Durum = "Stokta",
                    KaydedenPersonel = GetAktifKullaniciGorunenAd()
                };

                await App.SupabaseClient.From<UrunStok>().Insert(yeniUrun);

                MessageBox.Show("Kayıt başarılı.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                processedEpcs.Add(epc);
                BtnTemizle_Click(sender, e);
                TarihFiltrele();

                // 4. Sıradaki EPC'ye geç
                while (epcQueue.Count > 0)
                {
                    string nextEpc = epcQueue.Dequeue();

                    var kontrol = await App.SupabaseClient
                        .From<UrunStok>()
                        .Filter("EPC", Supabase.Postgrest.Constants.Operator.Equals, nextEpc)
                        .Get();

                    if (kontrol.Models.Count == 0)
                    {
                        txtEPC.Text = nextEpc;
                        liveEpcs.Remove(nextEpc);
                        return;
                    }

                    processedEpcs.Add(nextEpc);
                    liveEpcs.Remove(nextEpc);
                }

                txtEPC.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kayıt yapılırken hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        */
        private async void BtnKaydet_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool hasError = false;
                SolidColorBrush redBrush = new SolidColorBrush(Colors.Red);
                SolidColorBrush normalBrush = new SolidColorBrush(Colors.Gray);

                TextBox[] requiredTextBoxes = {
            txtEPC, txtBarkodNo, txtBandilNo, txtPlakaNo, txtSeleksiyon,
            txtKalınlık, txtEn, txtBoy, txtPlakaAdedi
        };

                foreach (TextBox tb in requiredTextBoxes)
                {
                    tb.BorderBrush = string.IsNullOrWhiteSpace(tb.Text) ? redBrush : normalBrush;
                    tb.BorderThickness = new Thickness(1);
                    hasError |= string.IsNullOrWhiteSpace(tb.Text);
                }

                ComboBox[] requiredComboBoxes = { cmbUrunTipi, cmbUrunTuru, cmbYuzeyIslemi };
                foreach (ComboBox cb in requiredComboBoxes)
                {
                    // Yüzey İşlemi DataTable'dan geldiği için SelectedValue kontrol ediyoruz
                    bool invalid = (cb == cmbYuzeyIslemi) ? cb.SelectedValue == null
                                                          : cb.SelectedItem == null;

                    cb.BorderBrush = invalid ? Brushes.DarkMagenta : Brushes.Blue;
                    cb.BorderThickness = new Thickness(invalid ? 2 : 1);
                    hasError |= invalid;
                }

                if (!dateUretimTarihi.SelectedDate.HasValue)
                {
                    dateUretimTarihi.BorderBrush = redBrush;
                    dateUretimTarihi.BorderThickness = new Thickness(2);
                    hasError = true;
                }
                else
                {
                    dateUretimTarihi.BorderBrush = normalBrush;
                    dateUretimTarihi.BorderThickness = new Thickness(1);
                }

                if (hasError)
                {
                    MessageBox.Show("Lütfen tüm alanları doldurun.", "Eksik Bilgi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string epc = txtEPC.Text.Trim();
                string barkodNo = txtBarkodNo.Text.Trim();

                // 1. EPC kontrol
                var epcCheck = await App.SupabaseClient
                    .From<UrunStok>()
                    .Filter("EPC", Supabase.Postgrest.Constants.Operator.Equals, epc)
                    .Get();

                if (epcCheck.Models.Count > 0)
                {
                    MessageBox.Show("Bu EPC zaten kayıtlı!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                    processedEpcs.Add(epc);

                    SetNextEpcFromQueue();
                    return;
                }

                // 2. Barkod kontrol
                var barkodCheck = await App.SupabaseClient
                    .From<UrunStok>()
                    .Filter("BarkodNo", Supabase.Postgrest.Constants.Operator.Equals, barkodNo)
                    .Get();

                if (barkodCheck.Models.Count > 0)
                {
                    MessageBox.Show("Bu Barkod No zaten kayıtlı!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 3. Yeni ürün oluştur
                var yeniUrun = new UrunStok
                {
                    EPC = epc,
                    BarkodNo = barkodNo,
                    BandilNo = txtBandilNo.Text.Trim(),
                    PlakaNo = txtPlakaNo.Text.Trim(),
                    UrunTipi = (cmbUrunTipi.SelectedItem as ComboBoxItem)?.Content.ToString(),
                    UrunTuru = (cmbUrunTuru.SelectedItem as DataRowView)?["UrunTuru"].ToString(),

                    // 🔧 DÜZELTME: Yüzey işlemi SelectedValue ile alınır
                    YuzeyIslemi = cmbYuzeyIslemi.SelectedValue?.ToString(),

                    Seleksiyon = txtSeleksiyon.Text.Trim(),
                    UretimTarihi = dateUretimTarihi.SelectedDate.HasValue
                        ? dateUretimTarihi.SelectedDate.Value.Date.Add(DateTime.Now.TimeOfDay)
                        : DateTime.Now,
                    Kalinlik = (decimal)double.Parse(txtKalınlık.Text.Trim().Replace(',', '.'), CultureInfo.InvariantCulture),
                    StokEn = (decimal)double.Parse(txtEn.Text.Trim().Replace(',', '.'), CultureInfo.InvariantCulture),
                    StokBoy = (decimal)double.Parse(txtBoy.Text.Trim().Replace(',', '.'), CultureInfo.InvariantCulture),
                    PlakaAdedi = int.Parse(txtPlakaAdedi.Text.Trim()),
                    StokAlan = (decimal)double.Parse(txtAlan.Text.Replace("Alan (m²): ", "").Trim().Replace(',', '.'), CultureInfo.InvariantCulture),
                    StokTonaj = (decimal)double.Parse(txtTonaj.Text.Replace("Tonaj (kg): ", "").Trim().Replace(',', '.'), CultureInfo.InvariantCulture),
                    Durum = "Stokta",
                    KaydedenPersonel = GetAktifKullaniciGorunenAd()
                };

                await App.SupabaseClient.From<UrunStok>().Insert(yeniUrun);

                MessageBox.Show("Kayıt başarılı.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                ActivityLogger.Log(GetAktifKullaniciGorunenAd(), "Ürün Kaydet", $"EPC: {yeniUrun.EPC}, Barkod: {yeniUrun.BarkodNo}");
                processedEpcs.Add(epc);
                BtnTemizle_Click(sender, e);
                await TarihFiltrele(); // küçük iyileştirme: await'le

                // 4. Sıradaki EPC'ye geç
                while (epcQueue.Count > 0)
                {
                    string nextEpc = epcQueue.Dequeue();

                    var kontrol = await App.SupabaseClient
                        .From<UrunStok>()
                        .Filter("EPC", Supabase.Postgrest.Constants.Operator.Equals, nextEpc)
                        .Get();

                    if (kontrol.Models.Count == 0)
                    {
                        txtEPC.Text = nextEpc;
                        liveEpcs.Remove(nextEpc);
                        return;
                    }

                    processedEpcs.Add(nextEpc);
                    liveEpcs.Remove(nextEpc);
                }

                txtEPC.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kayıt yapılırken hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private async void dataGridRapor_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {
                _ = Dispatcher.BeginInvoke(async () =>
                {
                    try
                    {
                        dataGridRapor.CommitEdit(DataGridEditingUnit.Row, true);

                        if (dataGridRapor.SelectedItem is DataRowView row)
                        {
                            var updatedItem = await MapRowToUrunStok(row);
                            await App.SupabaseClient.From<UrunStok>().Update(updatedItem);
                            MessageBox.Show("Güncelleme başarılı");
                            await TarihFiltrele();
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Güncelleme hatası: " + ex.Message);
                    }
                }, System.Windows.Threading.DispatcherPriority.Background);
            }
        }


        private void BtnTemizle_Click(object sender, RoutedEventArgs e)
        {
            txtEPC.Clear();
            txtBarkodNo.Clear();
            txtBandilNo.Clear();
            txtPlakaNo.Clear();
            cmbUrunTipi.SelectedIndex = -1;
            cmbUrunTuru.SelectedIndex = -1;
            cmbYuzeyIslemi.SelectedIndex = -1;
            txtSeleksiyon.Clear();
            dateUretimTarihi.SelectedDate = DateTime.Now;
            txtKalınlık.Clear();
            txtEn.Clear();
            txtBoy.Clear();
            txtPlakaAdedi.Clear();
            txtAlan.Text = "-";
            txtTonaj.Text = "-";
            SetNextEpcFromQueue();
        }

        private void cmbTarihSecimi_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _ = TarihFiltrele();
        }

        private void dateSecim_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbTarihSecimi.SelectedItem == null || dateSecim.SelectedDate == null)
                return;

            _ = TarihFiltrele();
        }
        
        public async Task TarihFiltrele()
        {
            if (cmbTarihSecimi.SelectedItem == null || dateSecim.SelectedDate == null)
                return;

            string secim = (cmbTarihSecimi.SelectedItem as ComboBoxItem)?.Content.ToString();
            DateTime tarih = dateSecim.SelectedDate.Value;
            List<UrunStok> filteredList = new();

            var allItems = await App.SupabaseClient.From<UrunStok>().Get();

            switch (secim)
            {
                case "Günlük":
                    filteredList = allItems.Models.Where(u => u.UretimTarihi?.Date == tarih.Date).ToList();
                    break;
                case "Haftalık":
                    int fark = ((int)tarih.DayOfWeek + 6) % 7;
                    DateTime baslangic = tarih.AddDays(-fark);
                    DateTime bitis = baslangic.AddDays(6);
                    filteredList = allItems.Models.Where(u => u.UretimTarihi >= baslangic && u.UretimTarihi <= bitis).ToList();
                    break;
                case "Aylık":
                    filteredList = allItems.Models.Where(u => u.UretimTarihi?.Month == tarih.Month && u.UretimTarihi?.Year == tarih.Year).ToList();
                    break;
                case "Yıllık":
                    filteredList = allItems.Models.Where(u => u.UretimTarihi?.Year == tarih.Year).ToList();
                    break;
            }

            urunStokTable = new DataTable();
            var props = typeof(UrunStok).GetProperties();
            foreach (var prop in props)
            {
                urunStokTable.Columns.Add(prop.Name);
            }

            filteredUrunler = filteredList.ToList();

            foreach (var item in filteredUrunler)
            {
                var row = urunStokTable.NewRow();
                foreach (var prop in props)
                {
                    row[prop.Name] = prop.GetValue(item) ?? DBNull.Value;
                }
                urunStokTable.Rows.Add(row);
            }


            dataGridRapor.ItemsSource = urunStokTable.DefaultView;
        }


        private async void btnYenile_Click(object sender, RoutedEventArgs e)
        {
            await TarihFiltrele();
        }

        private async void btnPdfRapor_Click(object sender, RoutedEventArgs e)
        {
            if (cmbTarihSecimi.SelectedItem == null || !dateSecim.SelectedDate.HasValue)
            {
                MessageBox.Show("Lütfen rapor oluşturmak için tarih ve periyot seçiniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await TarihFiltrele();

            if (filteredUrunler.Count == 0)
            {
                MessageBox.Show("Seçili periyotta listelenecek ürün bulunamadı.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string selection = (cmbTarihSecimi.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Günlük";
            DateTime selectedDate = dateSecim.SelectedDate!.Value;

            try
            {
                string periodDescription = DateRangeHelper.GetRangeDescription(selection, selectedDate);
                string filePath = ReportPdfService.GenerateUrunTakipReport(filteredUrunler, selection, periodDescription);
                MessageBox.Show($"PDF raporu oluşturuldu:\n{filePath}", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"PDF oluşturma sırasında hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void btnExcelRapor_Click(object sender, RoutedEventArgs e)
        {
            if (cmbTarihSecimi.SelectedItem == null || !dateSecim.SelectedDate.HasValue)
            {
                MessageBox.Show("Lütfen rapor oluşturmak için tarih ve periyot seçiniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await TarihFiltrele();

            if (filteredUrunler.Count == 0)
            {
                MessageBox.Show("Seçili periyotta listelenecek ürün bulunamadı.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string selection = (cmbTarihSecimi.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Günlük";
            DateTime selectedDate = dateSecim.SelectedDate!.Value;

            try
            {
                string periodDescription = DateRangeHelper.GetRangeDescription(selection, selectedDate);
                var result = ReportExcelService.GenerateUrunTakipReport(
                    filteredUrunler,
                    selection,
                    periodDescription,
                    includePreview: false);

                MessageBox.Show($"Excel raporu oluşturuldu:\n{result.FilePath}", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                // kullanıcı kaydetme işlemini iptal etti; uyarı gösterme.
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Excel oluşturma sırasında hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void btnSil_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridRapor.SelectedItem is not DataRowView selectedRow)
            {
                MessageBox.Show("Lütfen silmek için bir satır seçin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string epc = selectedRow["EPC"].ToString();
            string barkod = selectedRow["BarkodNo"]?.ToString() ?? "";
            string durum = selectedRow["Durum"].ToString();

            if (!string.Equals(durum, "Stokta", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Bir rezervasyon içerisinde bulunan bir ürün silinemez.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                await App.SupabaseClient
                    .From<UrunStok>()
                    .Filter("EPC", Supabase.Postgrest.Constants.Operator.Equals, epc)
                    .Delete();

                selectedRow.Delete();
                processedEpcs.Remove(epc);

                if (string.IsNullOrWhiteSpace(txtEPC.Text))
                {
                    if (!epcQueue.Contains(epc))
                    {
                        epcQueue.Enqueue(epc);
                        if (!liveEpcs.Contains(epc))
                            liveEpcs.Add(epc);
                    }
                    SetNextEpcFromQueue();
                }

                MessageBox.Show("Satır başarıyla silindi.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                ActivityLogger.Log(GetAktifKullaniciGorunenAd(), "Ürün Sil", $"EPC: {epc}, Barkod: {barkod}");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Silme işlemi sırasında hata oluştu: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task<UrunStok> MapRowToUrunStok(DataRowView row)
        {
            var table = row.Row.Table;

            return new UrunStok
            {
                EPC = row["EPC"]?.ToString(),
                BarkodNo = row["BarkodNo"]?.ToString(),
                BandilNo = row["BandilNo"]?.ToString(),
                PlakaNo = row["PlakaNo"]?.ToString(),
                UrunTipi = row["UrunTipi"]?.ToString(),
                UrunTuru = row["UrunTuru"]?.ToString(),
                YuzeyIslemi = row["YuzeyIslemi"]?.ToString(),
                Seleksiyon = row["Seleksiyon"]?.ToString(),
                UretimTarihi = row["UretimTarihi"] != DBNull.Value ? Convert.ToDateTime(row["UretimTarihi"]) : null,
                Kalinlik = row["Kalinlik"] != DBNull.Value ? Convert.ToDecimal(row["Kalinlik"]) : null,
                StokEn = row["StokEn"] != DBNull.Value ? Convert.ToDecimal(row["StokEn"]) : null,
                StokBoy = row["StokBoy"] != DBNull.Value ? Convert.ToDecimal(row["StokBoy"]) : null,
                PlakaAdedi = row["PlakaAdedi"] != DBNull.Value ? Convert.ToInt32(row["PlakaAdedi"]) : null,
                StokAlan = row["StokAlan"] != DBNull.Value ? Convert.ToDecimal(row["StokAlan"]) : null,
                StokTonaj = row["StokTonaj"] != DBNull.Value ? Convert.ToDecimal(row["StokTonaj"]) : null,
                SatisEn = table.Columns.Contains("SatisEn") && row["SatisEn"] != DBNull.Value ? Convert.ToDecimal(row["SatisEn"]) : null,
                SatisBoy = table.Columns.Contains("SatisBoy") && row["SatisBoy"] != DBNull.Value ? Convert.ToDecimal(row["SatisBoy"]) : null,
                SatisAlan = table.Columns.Contains("SatisAlan") && row["SatisAlan"] != DBNull.Value ? Convert.ToDecimal(row["SatisAlan"]) : null,
                SatisTonaj = table.Columns.Contains("SatisTonaj") && row["SatisTonaj"] != DBNull.Value ? Convert.ToDecimal(row["SatisTonaj"]) : null,
                Durum = row["Durum"]?.ToString(),
                RezervasyonNo = table.Columns.Contains("RezervasyonNo") ? row["RezervasyonNo"]?.ToString() : null,
                KaydedenPersonel = table.Columns.Contains("KaydedenPersonel") ? row["KaydedenPersonel"]?.ToString() : null,
                UrunCikisTarihi = table.Columns.Contains("UrunCikisTarihi") && row["UrunCikisTarihi"] != DBNull.Value ? Convert.ToDateTime(row["UrunCikisTarihi"]) : null,
                AliciFirma = table.Columns.Contains("AliciFirma") ? row["AliciFirma"]?.ToString() : null,
            };
        }

        // kullanıcı bilgisi çekmek için olan kısım
        private static string GetAktifKullaniciGorunenAd()
        {
            // 1) MainWindow VM.Me (Kullanicilar tablosundan)
            if (Application.Current.MainWindow is MainWindow mw && mw.Vm?.Me != null)
            {
                var me = mw.Vm.Me;
                if (!string.IsNullOrWhiteSpace(me.Ad)) return me.Ad!;
                if (!string.IsNullOrWhiteSpace(me.Email)) return me.Email!;
            }

            // 2) Son çare: Auth'tan (metadata veya email)
            var auth = App.SupabaseClient?.Auth?.CurrentUser;
            if (auth != null)
            {
                string? Meta(string key)
                {
                    try
                    {
                        return (auth.UserMetadata != null &&
                                auth.UserMetadata.TryGetValue(key, out var v) && v != null)
                            ? v.ToString()
                            : null;
                    }
                    catch { return null; }
                }

                var ad = Meta("ad") ?? Meta("first_name") ?? Meta("name");

                if (!string.IsNullOrWhiteSpace(ad)) return ad;
                if (!string.IsNullOrWhiteSpace(auth.Email)) return auth.Email!;
            }

            return "Bilinmiyor";
        }




    }
}
