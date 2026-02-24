using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
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
using System.Windows.Controls;
using System.Windows.Threading;
using SideBar_Nav.Models;
using System.Collections.ObjectModel;
using System.Data;

namespace SideBar_Nav.Pages
{
    public partial class GPIO_ops : Page
    {

        public static ComboBox cmbGPO1_Static;
        public static ComboBox cmbGPO2_Static;
        public static ComboBox cmbGPO3_Static;
        public static ComboBox cmbGPO4_Static;
        //
        private DispatcherTimer gpoKontrolTimer;
        //
        private static string sonBilinenDurum = null;
        private static DispatcherTimer durumKontrolTimer;

        public static string sonRezervasyonNo;

        public static ObservableCollection<DataRow> UygunList_Global;
        public static ObservableCollection<DataRow> UygunsuzList_Global;
        public GPIO_ops()
        {
            InitializeComponent();
            InitComboBoxes();
            // Sayfa yüklenince ComboBox referanslarını static olarak ata
            cmbGPO1_Static = cmbGPO1;
            cmbGPO2_Static = cmbGPO2;
            cmbGPO3_Static = cmbGPO3;
            cmbGPO4_Static = cmbGPO4;
            // ⏱ GPO kontrol zamanlayıcısı
            gpoKontrolTimer = new DispatcherTimer();
            gpoKontrolTimer.Interval = TimeSpan.FromSeconds(1); // her 0.5 saniyede bir kontrol
            gpoKontrolTimer.Tick += GpoKontrolTimer_Tick;
            gpoKontrolTimer.Start();

            // Duruma göre sürekli kontrol
            durumKontrolTimer = new DispatcherTimer();
            durumKontrolTimer.Interval = TimeSpan.FromSeconds(1); // Her 0.5 saniyede bir kontrol et
            durumKontrolTimer.Tick += DurumKontrolTimer_Tick;
            durumKontrolTimer.Start();

        }
        public static void RegisterLists(
    ObservableCollection<DataRow> uygunListe,
    ObservableCollection<DataRow> uygunsuzListe)
        {
            UygunList_Global = uygunListe;
            UygunsuzList_Global = uygunsuzListe;
        }
        private void GpoKontrolTimer_Tick(object sender, EventArgs e)
        {
            // Start/Stop senaryoları için sürekli kontrol et
            TriggerSelectedScenario();
        }
        private void DurumKontrolTimer_Tick(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(sonBilinenDurum))
            {
                TriggerSelectedScenarioWithDurum(sonBilinenDurum);
            }
        }

        public static void TriggerSelectedScenario()
        {
            TriggerGPOBySelection(cmbGPO1_Static, 1);
            TriggerGPOBySelection(cmbGPO2_Static, 2);
            TriggerGPOBySelection(cmbGPO3_Static, 3);
            TriggerGPOBySelection(cmbGPO4_Static, 4);
        }

        private static void TriggerGPOBySelection(ComboBox combo, int gpoNo)
        {
            if (combo == null || combo.SelectedIndex < 0)
                return;

            string selected = combo.SelectedItem.ToString();

            // Sadece StartReader veya StopReader için işlem yap
            if (selected != "StartReader" && selected != "StopReader")
                return;

            var reader = App.SharedReader;
            bool shouldTurnOn = (selected == "StartReader" && reader.isConnected && reader.isRunning)
                             || (selected == "StopReader" && reader.isConnected && !reader.isRunning);

            // GPO'ya karşılık gelen ON/OFF metodunu seç
            Action gpoOn = null;
            Action gpoOff = null;

            switch (gpoNo)
            {
                case 1: gpoOn = reader.GPO1_ON; gpoOff = reader.GPO1_OFF; break;
                case 2: gpoOn = reader.GPO2_ON; gpoOff = reader.GPO2_OFF; break;
                case 3: gpoOn = reader.GPO3_ON; gpoOff = reader.GPO3_OFF; break;
                case 4: gpoOn = reader.GPO4_ON; gpoOff = reader.GPO4_OFF; break;
            }

            // GPO açık mı diye kontrol edebilmek için mevcut durumu takip etmeliyiz.
            // Bunu yapmak için reader nesnesinde GPO1State gibi bool durumları tutmak gerekir.
            // Eğer bunlar yoksa, geçici çözüm olarak her tick'te aynı komutu tekrar vermemeyi sağlayacak state değişkeni gerekir.
            // Ancak şimdi doğrudan çağırıyoruz:
            if (shouldTurnOn)
                gpoOn?.Invoke();
            else
                gpoOff?.Invoke();
        }


        private void InitComboBoxes()
        {
            var scenarioOptions = new List<string>
            {
                "Senaryo Seç",
                "StartReader",
                "StopReader",
                "Ürün Sevkiyata Uygun",
                "Ürün Sevkiyata Uygun Değil"
            };

            cmbGPO1.ItemsSource = scenarioOptions;
            cmbGPO2.ItemsSource = scenarioOptions;
            cmbGPO3.ItemsSource = scenarioOptions;
            cmbGPO4.ItemsSource = scenarioOptions;

            cmbGPO1.SelectedIndex = 0;
            cmbGPO2.SelectedIndex = 0;
            cmbGPO3.SelectedIndex = 0;
            cmbGPO4.SelectedIndex = 0;
        }


        #region Secilen senaryolara gore temel GPO islemleri
        private void cmbGPO1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbGPO1.SelectedIndex == 0)
            {
                App.SharedReader.GPO1_OFF();
            }
            if (cmbGPO1.SelectedIndex == 1)
            {
                if (App.SharedReader.isConnected && App.SharedReader.isRunning)
                    App.SharedReader.GPO1_ON();
                else
                {
                    App.SharedReader.GPO1_OFF();
                }
            }
            if (cmbGPO1.SelectedIndex == 2)
            {
                if (App.SharedReader.isConnected && !App.SharedReader.isRunning)
                    App.SharedReader.GPO1_ON();
                else
                {
                    App.SharedReader.GPO1_OFF();
                }
            }

        }

        private void cmbGPO2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbGPO2.SelectedIndex == 0)
            {
                App.SharedReader.GPO2_OFF();
            }
            if (cmbGPO2.SelectedIndex == 1)
            {
                if (App.SharedReader.isConnected && App.SharedReader.isRunning)
                    App.SharedReader.GPO2_ON();
                else
                {
                    App.SharedReader.GPO2_OFF();
                }
            }
            if (cmbGPO2.SelectedIndex == 2)
            {
                if (App.SharedReader.isConnected && !App.SharedReader.isRunning)
                    App.SharedReader.GPO2_ON();
                else
                {
                    App.SharedReader.GPO2_OFF();
                }
            }
        }

        private void cmbGPO3_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbGPO3.SelectedIndex == 0)
            {
                App.SharedReader.GPO3_OFF();
            }
            if (cmbGPO3.SelectedIndex == 1)
            {
                if (App.SharedReader.isConnected && App.SharedReader.isRunning)
                    App.SharedReader.GPO3_ON();
            }
            if (cmbGPO3.SelectedIndex == 2)
            {
                if (App.SharedReader.isConnected && !App.SharedReader.isRunning)
                    App.SharedReader.GPO3_ON();
            }
        }

        private void cmbGPO4_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbGPO4.SelectedIndex == 0)
            {
                App.SharedReader.GPO4_OFF();
            }
            if (cmbGPO4.SelectedIndex == 1)
            {
                if (App.SharedReader.isConnected && App.SharedReader.isRunning)
                    App.SharedReader.GPO4_ON();
            }
            if (cmbGPO4.SelectedIndex == 2)
            {
                if (App.SharedReader.isConnected && !App.SharedReader.isRunning)
                    App.SharedReader.GPO4_ON();
            }
        }
        public void Sifirla()
        {
            cmbGPO1.SelectedIndex = 0;
            cmbGPO2.SelectedIndex = 0;
            cmbGPO3.SelectedIndex = 0;
            cmbGPO4.SelectedIndex = 0;
        }
        private void Sifirla_Click(object sender, RoutedEventArgs e)
        {
            Sifirla();
        }
        #endregion
        public static void takeRezNo(string rezervasyonNo)
        {
            sonRezervasyonNo = rezervasyonNo;
        }
       
        public static void TriggerSelectedScenarioWithDurum(string durum)
        {
            sonBilinenDurum = durum;

            bool bosalt = durum == "boşalt";
            if (bosalt)
            {
                // SADECE Ürün senaryoları için kapat
                TurnOffIfUrunSenaryo(cmbGPO1_Static, App.SharedReader.GPO1_OFF);
                TurnOffIfUrunSenaryo(cmbGPO2_Static, App.SharedReader.GPO2_OFF);
                TurnOffIfUrunSenaryo(cmbGPO3_Static, App.SharedReader.GPO3_OFF);
                TurnOffIfUrunSenaryo(cmbGPO4_Static, App.SharedReader.GPO4_OFF);
                return;
            }

            bool uygunVar = UygunList_Global != null && UygunList_Global.Any();
            bool uygunsuzVar = UygunsuzList_Global != null && UygunsuzList_Global.Any();

            HandleSingleGPO(cmbGPO1_Static, App.SharedReader.GPO1_ON, App.SharedReader.GPO1_OFF, uygunVar, uygunsuzVar);
            HandleSingleGPO(cmbGPO2_Static, App.SharedReader.GPO2_ON, App.SharedReader.GPO2_OFF, uygunVar, uygunsuzVar);
            HandleSingleGPO(cmbGPO3_Static, App.SharedReader.GPO3_ON, App.SharedReader.GPO3_OFF, uygunVar, uygunsuzVar);
            HandleSingleGPO(cmbGPO4_Static, App.SharedReader.GPO4_ON, App.SharedReader.GPO4_OFF, uygunVar, uygunsuzVar);
        }

        private static void TurnOffIfUrunSenaryo(ComboBox cmb, Action off)
        {
            var selected = cmb?.SelectedItem?.ToString();
            if (selected?.StartsWith("Ürün") == true)
                off();
        }

        private static void HandleSingleGPO(ComboBox cmb, Action on, Action off, bool uygunVar, bool uygunsuzVar)
        {
            var selected = cmb?.SelectedItem?.ToString();

            if (selected == "Ürün Sevkiyata Uygun" && uygunVar)
                on();
            else if (selected == "Ürün Sevkiyata Uygun Değil" && uygunsuzVar)
                on();
            else
                off();
        }

    }
}
