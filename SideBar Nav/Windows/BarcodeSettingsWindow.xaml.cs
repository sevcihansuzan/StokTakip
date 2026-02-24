using System;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Windows;

namespace SideBar_Nav.Windows
{
    public partial class BarcodeSettingsWindow : Window
    {
        private readonly SerialPort _port;
        private readonly ObservableCollection<CmdItem> _all = new();
        private readonly ObservableCollection<CmdItem> _view = new();

        public class CmdItem
        {
            public string Category { get; set; }
            public string Name { get; set; }
            public string Code { get; set; }        // gönderilecek ASCII komut
            public string Description { get; set; }
        }

        public BarcodeSettingsWindow(SerialPort port)
        {
            InitializeComponent();
            _port = port ?? throw new ArgumentNullException(nameof(port));
            if (!_port.IsOpen) throw new InvalidOperationException("SerialPort açık değil.");

            SeedCommands();
            BindLists();
        }

        private void BindLists()
        {
            dgCommands.ItemsSource = _view;
            ApplyFilter();

            var cats = _all.Select(x => x.Category).Distinct().OrderBy(x => x).ToList();
            cats.Insert(0, "Hepsi");
            cmbCategory.ItemsSource = cats;
            cmbCategory.SelectedIndex = 0;
        }

        private void ApplyFilter()
        {
            _view.Clear();
            string q = (txtSearch.Text ?? "").Trim().ToLowerInvariant();
            string cat = (cmbCategory.SelectedItem as string) ?? "Hepsi";

            foreach (var c in _all)
            {
                if (cat != "Hepsi" && !string.Equals(c.Category, cat, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!string.IsNullOrEmpty(q))
                {
                    var hay = (c.Name + " " + c.Code + " " + c.Description).ToLowerInvariant();
                    if (!hay.Contains(q)) continue;
                }
                _view.Add(c);
            }
        }

        private void txtSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => ApplyFilter();
        private void cmbCategory_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => ApplyFilter();

        // --- Hızlı butonlar ---
        private void BtnSerialPortOpen_Click(object sender, RoutedEventArgs e) => SendOne("~M00510000.", "Serial Port Open");

        private void BtnConfigOn_Click(object sender, RoutedEventArgs e) => SendOne("~M00910001.", "Config ON");
        private void BtnConfigOff_Click(object sender, RoutedEventArgs e) => SendOne("~M00910000.", "Config OFF");
        private void BtnSaveUserDefault_Click(object sender, RoutedEventArgs e) => SendOne("~MA5F0506A.", "Save User Default");
        private void BtnRestoreUserDefault_Click(object sender, RoutedEventArgs e) => SendOne("~MA5F08F37.", "Restore User Default");
        private void BtnFactoryReset_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Factory reset göndermek istediğine emin misin?", "Onay", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                SendOne("~MA5F01B2C.", "Factory Reset");
        }

        // --- Komut gönder ---
        private void BtnSendSelected_Click(object sender, RoutedEventArgs e)
        {
            var items = dgCommands.SelectedItems.Cast<CmdItem>().ToList();
            if (!items.Any())
            {
                MessageBox.Show("Göndermek için listeden en az bir komut seçin.");
                return;
            }


            foreach (var it in items)
                SendOne(it.Code, $"{it.Name}");
        }

        private void BtnSendDirect_Click(object sender, RoutedEventArgs e)
        {
            string code = txtDirectCode.Text?.Trim();
            if (string.IsNullOrEmpty(code))
            {
                MessageBox.Show("Göndermek için bir komut yazın.");
                return;
            }
            if (!code.EndsWith(".")) code += "."; // güvenlik: nokta yoksa ekle



            SendOne(code, "Manual");
        }

        private void BtnTrigger_Click(object sender, RoutedEventArgs e) => SendOne("~T.", "Trigger");

        private void SendOne(string asciiCommand, string tag)
        {
            try
            {
                if (!_port.IsOpen) throw new InvalidOperationException("Port kapalı.");
                // CR/LF kullanıyorsan yine de protokolün nokta (.) ile bittiğinden emin ol:
                if (!asciiCommand.EndsWith(".")) asciiCommand += ".";
                _port.Write(asciiCommand);
                AppendLog($"> {tag}: {asciiCommand}");
            }
            catch (Exception ex)
            {
                AppendLog($"! Hata ({tag}): {ex.Message}");
            }
        }

        private void AppendLog(string line)
        {
            txtLog.AppendText(line + Environment.NewLine);
            txtLog.ScrollToEnd();
        }

        // --- Komut kataloğu ---
        private void SeedCommands()
        {
            // System / Defaults
            _all.Add(new CmdItem { Category = "System", Name = "Config ON", Code = "~M00910001.", Description = "Scan Code Configuration Function switch ON" });
            _all.Add(new CmdItem { Category = "System", Name = "Config OFF", Code = "~M00910000.", Description = "Scan Code Configuration Function switch OFF" });
            _all.Add(new CmdItem { Category = "System", Name = "Save User Default", Code = "~MA5F0506A.", Description = "Mevcut ayarları user default olarak kaydet" });
            _all.Add(new CmdItem { Category = "System", Name = "Restore User Default", Code = "~MA5F08F37.", Description = "User default ayarlarını yükle" });
            _all.Add(new CmdItem { Category = "System", Name = "Factory Reset", Code = "~MA5F01B2C.", Description = "Fabrika ayarları" });
            _all.Add(new CmdItem { Category = "System", Name = "Serial Port Open", Code = "~M00510000.", Description = "Serial Port'u aç" });
            // Communication
            _all.Add(new CmdItem { Category = "Comm", Name = "Baud 1200", Code = "~M00F50000.", Description = "Serial baud = 1200" });
            _all.Add(new CmdItem { Category = "Comm", Name = "Baud 2400", Code = "~M00F50001.", Description = "Serial baud = 2400" });
            _all.Add(new CmdItem { Category = "Comm", Name = "Baud 4800", Code = "~M00F50002.", Description = "Serial baud = 4800" });
            _all.Add(new CmdItem { Category = "Comm", Name = "Baud 9600", Code = "~M00F50003.", Description = "Serial baud = 9600 (default)" });
            _all.Add(new CmdItem { Category = "Comm", Name = "Baud 19200", Code = "~M00F50004.", Description = "Serial baud = 19200" });
            _all.Add(new CmdItem { Category = "Comm", Name = "Baud 38400", Code = "~M00F50005.", Description = "Serial baud = 38400" });
            _all.Add(new CmdItem { Category = "Comm", Name = "Baud 57600", Code = "~M00F50006.", Description = "Serial baud = 57600" });
            _all.Add(new CmdItem { Category = "Comm", Name = "Baud 115200", Code = "~M00F50007.", Description = "Serial baud = 115200" });

            // Working Mode
            _all.Add(new CmdItem { Category = "Mode", Name = "Manual Trigger Mode-1", Code = "~M00210000.", Description = "Butonla tetik" });
            _all.Add(new CmdItem { Category = "Mode", Name = "Continuous Mode", Code = "~M00210001.", Description = "Sürekli okuma" });
            _all.Add(new CmdItem { Category = "Mode", Name = "Induction Mode", Code = "~M00210002.", Description = "Sensörle tetik" });
            _all.Add(new CmdItem { Category = "Mode", Name = "Instruction Trigger Mode", Code = "~M00210003.", Description = "Yazılımdan tetik (~T.)" });
            _all.Add(new CmdItem { Category = "Mode", Name = "Manual Trigger Mode-2", Code = "~M00210004.", Description = "Butonla tetik (varyant)" });
            _all.Add(new CmdItem { Category = "Mode", Name = "Instruction Continuous Mode", Code = "~M00210005.", Description = "Komutla sürekli" });

            // Output Format
            _all.Add(new CmdItem { Category = "Output", Name = "Add CRLF ON", Code = "~M00920001.", Description = "Okuma sonuna 0x0D0A ekle" });
            _all.Add(new CmdItem { Category = "Output", Name = "Add CRLF OFF", Code = "~M00920000.", Description = "Satır sonu ekleme" });
            _all.Add(new CmdItem { Category = "Output", Name = "Command Answer ON", Code = "~M00730001.", Description = "Komut tetik cevabı (ACK/NAK) açık" });
            _all.Add(new CmdItem { Category = "Output", Name = "Command Answer OFF", Code = "~M00730000.", Description = "Komut tetik cevabı kapalı" });

            // Lights (örnekler)
            _all.Add(new CmdItem { Category = "Lights", Name = "Aiming OFF", Code = "~M01050000.", Description = "Nişan ışığı kapalı" });
            _all.Add(new CmdItem { Category = "Lights", Name = "Aiming ON (flash)", Code = "~M01050001.", Description = "Nişan ışığı (flash)" });
            _all.Add(new CmdItem { Category = "Lights", Name = "Aiming ON (steady)", Code = "~M01050002.", Description = "Nişan ışığı (sabit)" });

            // Query (örnek)
            _all.Add(new CmdItem { Category = "Query", Name = "Read Product Info", Code = "~QF672.", Description = "Ürün bilgisi sorgu" });
            _all.Add(new CmdItem { Category = "Query", Name = "Read All Setup Codes", Code = "~QFA50.", Description = "Ayarları oku (toplu)" });

            // Trigger
            _all.Add(new CmdItem { Category = "Trigger", Name = "Trigger Scan", Code = "~T.", Description = "Tetikle ve oku" });
        }

    }
}
