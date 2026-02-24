using SideBar_Nav.Pages;
using System;
using System.Collections.Generic;
using System.Linq;
using Impinj.OctaneSdk;
using SideBar_Nav.Models;
using System.ComponentModel;
using System.Reflection.PortableExecutable;
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
using static SideBar_Nav.RFIDReader;

namespace SideBar_Nav
{
    public class RFIDReader 
    {
        public event Action<RfidData> TagsReported;


        private Dictionary<string, RfidData> tagMemory = new Dictionary<string, RfidData>();
        public List<string> HexFilterValues { get; set; } = new List<string>();


        public bool isConnected = false;
        public bool isRunning = false;
        public bool isSettingsApplied = false; 

        public uint selectedRfMode;
        public int selectedSearchMode;
        public ushort selectedSession;
        public int selectedReportMode;

        public bool includeLastSeenTime;
        public bool includePeakRssi;
        public bool includePhaseAngle;
        public bool includeAntennaPortNumber;
        public bool includeFirstSeenTime;
        public bool includeSeenCount;
        public bool includeTxPower;
        public List<ushort> EnabledAntennaList { get; private set; } = new List<ushort>();
        public Dictionary<ushort, string> AntennaNameMap { get; private set; } = new Dictionary<ushort, string>();

        public bool isEnabledAntenna1;
        public bool isEnabledAntenna2;
        public bool isEnabledAntenna3;
        public bool isEnabledAntenna4;

        public bool enabledAntenna1
        {
            get => isEnabledAntenna1;
            set => isEnabledAntenna1 = value;
        }

        public bool enabledAntenna2
        {
            get => isEnabledAntenna2;
            set => isEnabledAntenna2 = value;
        }

        public bool enabledAntenna3
        {
            get => isEnabledAntenna3;
            set => isEnabledAntenna3 = value;
        }

        public bool enabledAntenna4
        {
            get => isEnabledAntenna4;
            set => isEnabledAntenna4 = value;
        }

        public int RxSensitivity1;
        public int RxSensitivity2;
        public int RxSensitivity3;
        public int RxSensitivity4;

        public int TxPower1;
        public int TxPower2;
        public int TxPower3;
        public int TxPower4;
         
        // Filtering 
        public bool isEPCFilteringEnabled { get; set; } = false;
        public bool isUserMemoryFilteringEnabled { get; set; } = false;
        public string epcFilterMask { get; set; } = "";
        public string userFilterMask { get; set; } = "";

        public ushort epcBitPointer { get; set; }

        public int epcBitCount { get; set; }
        public ushort userBitPointer { get; set; }
        public int userBitCount { get; set; }
        public bool isAdvancedFilteringEnabled { get; set; } = false;
        // GPI events
        // GPI durumlarını tutacak değişkenler
        public bool GPI1 { get; private set; } = false;
        public bool GPI2 { get; private set; } = false;
        public bool GPI3 { get; private set; } = false;
        public bool GPI4 { get; private set; } = false;

        // Connection
        public string ReaderHostname { get; set; }

        public ReaderSettingsSnapshot? LastSavedSettings { get; set; }

        private ImpinjReader reader = new ImpinjReader();

        public void ConnectReader()
        {
            try
            {
                reader.Connect(ReaderHostname);
                if (reader.IsConnected)
                    isConnected = true;
                    var mainWindow = (MainWindow)Application.Current.MainWindow;
                    mainWindow.SetConnectionIndicatorColor(Colors.Green);
                    GPO1_OFF();
                    GPO2_OFF();
                    GPO3_OFF();
                    GPO4_OFF();
                reader.GpiChanged += OnGpiChanged;
                ApplyUserSettings();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error connecting to reader: {ex.Message}");
            }
        }
        public void ApplyDefaultSettings()
        {
            Settings userSettings = reader.QueryDefaultSettings();
            
        }

        public void ApplyUserSettings()
        {

            Settings userSettings = reader.QueryDefaultSettings();

            userSettings.Report.Mode = (ReportMode)selectedReportMode;
            userSettings.RfMode = selectedRfMode;
            userSettings.Session = selectedSession;
            userSettings.SearchMode = (SearchMode)selectedSearchMode;
            
            userSettings.Report.IncludeLastSeenTime = includeLastSeenTime;
            userSettings.Report.IncludePeakRssi = includePeakRssi;
            userSettings.Report.IncludePhaseAngle = includePhaseAngle;
            // userSettings.Report.IncludeAntennaPortNumber = includeAntennaPortNumber; // true olmak zorunda
            userSettings.Report.IncludeAntennaPortNumber = true;
            userSettings.Report.IncludeFirstSeenTime = includeFirstSeenTime;
            userSettings.Report.IncludeSeenCount = includeSeenCount;
            #region Anten kısmı
            userSettings.Antennas.DisableAll();
            EnabledAntennaList.Clear();
            
            if (isEnabledAntenna1)
            {
                userSettings.Antennas.GetAntenna(1).IsEnabled = true;
                userSettings.Antennas.GetAntenna(1).TxPowerInDbm = TxPower1;
                userSettings.Antennas.GetAntenna(1).RxSensitivityInDbm = RxSensitivity1;
                EnabledAntennaList.Add(1);
            }

            if (isEnabledAntenna2)
            {
                userSettings.Antennas.GetAntenna(2).IsEnabled = true;
                userSettings.Antennas.GetAntenna(2).TxPowerInDbm = TxPower2;
                userSettings.Antennas.GetAntenna(2).RxSensitivityInDbm = RxSensitivity2;
                EnabledAntennaList.Add(2);
            }

            if (isEnabledAntenna3)
            {
                userSettings.Antennas.GetAntenna(3).IsEnabled = true;
                userSettings.Antennas.GetAntenna(3).TxPowerInDbm = TxPower3;
                userSettings.Antennas.GetAntenna(3).RxSensitivityInDbm = RxSensitivity3;
                EnabledAntennaList.Add(3);
            }

            if (isEnabledAntenna4)
            {
                userSettings.Antennas.GetAntenna(4).IsEnabled = true;
                userSettings.Antennas.GetAntenna(4).TxPowerInDbm = TxPower4;
                userSettings.Antennas.GetAntenna(4).RxSensitivityInDbm = RxSensitivity4;
                EnabledAntennaList.Add(4);
            }
            #endregion
            #region filtering 1
            if (isEPCFilteringEnabled && isUserMemoryFilteringEnabled)
            {
                // Hem EPC hem User Memory filter aktif

                // EPC Filter
                userSettings.Filters.TagFilter1.MemoryBank = MemoryBank.Epc;
                userSettings.Filters.TagFilter1.BitPointer = epcBitPointer;
                userSettings.Filters.TagFilter1.TagMask = epcFilterMask;
                userSettings.Filters.TagFilter1.BitCount = epcBitCount;

                // User Memory Filter
                userSettings.Filters.TagFilter2.MemoryBank = MemoryBank.User;
                userSettings.Filters.TagFilter2.BitPointer = userBitPointer;
                userSettings.Filters.TagFilter2.TagMask = userFilterMask;
                userSettings.Filters.TagFilter2.BitCount = userBitCount;

                // Her iki filtre de eşleşmeli
                userSettings.Filters.Mode = TagFilterMode.Filter1AndFilter2;
            }
            else if (isEPCFilteringEnabled)
            {
                // Sadece EPC filter aktif

                // EPC Filter
                userSettings.Filters.TagFilter1.MemoryBank = MemoryBank.Epc;
                userSettings.Filters.TagFilter1.BitPointer = epcBitPointer;
                userSettings.Filters.TagFilter1.TagMask = epcFilterMask;
                userSettings.Filters.TagFilter1.BitCount = epcBitCount;

                // Sadece Filter1 kullanılacak
                userSettings.Filters.Mode = TagFilterMode.OnlyFilter1;
            }
            else if (isUserMemoryFilteringEnabled)
            {
                // Sadece User Memory filter aktif

                // User Memory Filter
                userSettings.Filters.TagFilter2.MemoryBank = MemoryBank.User;
                userSettings.Filters.TagFilter2.BitPointer = userBitPointer;
                userSettings.Filters.TagFilter2.TagMask = userFilterMask;
                userSettings.Filters.TagFilter2.BitCount = userBitCount;

                // Sadece Filter1 kullanılacak
                userSettings.Filters.Mode = TagFilterMode.OnlyFilter2;
            }
            else
            {
                // Hiçbir filtre aktif değil

                // Filtrelemeyi kapatıyoruz
                userSettings.Filters.Mode = TagFilterMode.None;
            }
            
            if(isAdvancedFilteringEnabled)
            {
                userSettings.Filters.Mode = TagFilterMode.UseTagSelectFilters;
                userSettings.Filters.TagSelectFilters.Clear(); // Önceki filtreleri temizle

                for (int i = 0; i < HexFilterValues.Count; i++)
                {
                    var hex = HexFilterValues[i];
                    if (!string.IsNullOrWhiteSpace(hex))
                    {
                        TagSelectFilter filter = new TagSelectFilter
                        {
                            MatchAction = StateUnawareAction.Select,
                            NonMatchAction = StateUnawareAction.Unselect,
                            MemoryBank = MemoryBank.Epc,
                            BitPointer = (ushort)(BitPointers.Epc + (i * 4)),
                            TagMask = hex
                        };
                        userSettings.Filters.TagSelectFilters.Add(filter);
                    }
                }
            }

            #endregion
            reader.ApplySettings(userSettings);
            isSettingsApplied = true;
        }

        public void LoadFromSnapshot(ReaderSettingsSnapshot snapshot, bool remember = true)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            selectedRfMode = snapshot.SelectedRfMode;
            selectedSearchMode = snapshot.SelectedSearchMode;
            selectedReportMode = snapshot.SelectedReportMode;
            selectedSession = snapshot.SelectedSession;

            includeLastSeenTime = snapshot.IncludeLastSeenTime;
            includePeakRssi = snapshot.IncludePeakRssi;
            includePhaseAngle = snapshot.IncludePhaseAngle;
            includeFirstSeenTime = snapshot.IncludeFirstSeenTime;
            includeSeenCount = snapshot.IncludeSeenCount;

            isEnabledAntenna1 = snapshot.IsAntenna1Enabled;
            isEnabledAntenna2 = snapshot.IsAntenna2Enabled;
            isEnabledAntenna3 = snapshot.IsAntenna3Enabled;
            isEnabledAntenna4 = snapshot.IsAntenna4Enabled;

            TxPower1 = snapshot.TxPower1;
            TxPower2 = snapshot.TxPower2;
            TxPower3 = snapshot.TxPower3;
            TxPower4 = snapshot.TxPower4;

            RxSensitivity1 = snapshot.RxSensitivity1;
            RxSensitivity2 = snapshot.RxSensitivity2;
            RxSensitivity3 = snapshot.RxSensitivity3;
            RxSensitivity4 = snapshot.RxSensitivity4;

            AntennaNameMap = snapshot.AntennaNameMap != null
                ? new Dictionary<ushort, string>(snapshot.AntennaNameMap)
                : new Dictionary<ushort, string>();

            isEPCFilteringEnabled = snapshot.IsEpcFilteringEnabled;
            isUserMemoryFilteringEnabled = snapshot.IsUserMemoryFilteringEnabled;
            epcFilterMask = snapshot.EpcFilterMask ?? string.Empty;
            epcBitPointer = snapshot.EpcBitPointer;
            epcBitCount = snapshot.EpcBitCount;
            userFilterMask = snapshot.UserFilterMask ?? string.Empty;
            userBitPointer = snapshot.UserBitPointer;
            userBitCount = snapshot.UserBitCount;
            isAdvancedFilteringEnabled = snapshot.IsAdvancedFilteringEnabled;

            HexFilterValues = snapshot.HexFilterValues?.Take(24).ToList() ?? new List<string>();

            if (remember)
            {
                LastSavedSettings = snapshot;
            }

            isSettingsApplied = false;
        }

        public ReaderSettingsSnapshot CreateSnapshot()
        {
            return new ReaderSettingsSnapshot
            {
                SelectedRfMode = selectedRfMode,
                SelectedSearchMode = selectedSearchMode,
                SelectedReportMode = selectedReportMode,
                SelectedSession = selectedSession,
                IncludeLastSeenTime = includeLastSeenTime,
                IncludePeakRssi = includePeakRssi,
                IncludePhaseAngle = includePhaseAngle,
                IncludeFirstSeenTime = includeFirstSeenTime,
                IncludeSeenCount = includeSeenCount,
                IsAntenna1Enabled = isEnabledAntenna1,
                IsAntenna2Enabled = isEnabledAntenna2,
                IsAntenna3Enabled = isEnabledAntenna3,
                IsAntenna4Enabled = isEnabledAntenna4,
                TxPower1 = TxPower1,
                TxPower2 = TxPower2,
                TxPower3 = TxPower3,
                TxPower4 = TxPower4,
                RxSensitivity1 = RxSensitivity1,
                RxSensitivity2 = RxSensitivity2,
                RxSensitivity3 = RxSensitivity3,
                RxSensitivity4 = RxSensitivity4,
                AntennaNameMap = new Dictionary<ushort, string>(AntennaNameMap),
                IsEpcFilteringEnabled = isEPCFilteringEnabled,
                IsUserMemoryFilteringEnabled = isUserMemoryFilteringEnabled,
                EpcFilterMask = epcFilterMask,
                EpcBitPointer = epcBitPointer,
                EpcBitCount = epcBitCount,
                UserFilterMask = userFilterMask,
                UserBitPointer = userBitPointer,
                UserBitCount = userBitCount,
                IsAdvancedFilteringEnabled = isAdvancedFilteringEnabled,
                HexFilterValues = new List<string>(HexFilterValues)
            };
        }

        public bool EnsureSettingsApplied()
        {
            if (isSettingsApplied)
                return true;

            ReaderSettingsSnapshot snapshot;
            if (LastSavedSettings != null)
            {
                snapshot = LastSavedSettings;
                LoadFromSnapshot(snapshot);
            }
            else
            {
                snapshot = ReaderSettingsSnapshot.CreateDefault();
                LoadFromSnapshot(snapshot, remember: false);
            }

            if (!isConnected || isRunning)
                return false;

            ApplyUserSettings();
            return isSettingsApplied;
        }

        public void DisconnectReader()
        {
            if (reader.IsConnected)
            {
                //StopReader();
                //isRunning = false;
                GPIO_ops gPIO_Ops = new GPIO_ops(); // disconnect ile gpolar sıfırlanmalı(off)
                gPIO_Ops.Sifirla();
                reader.Disconnect();
                if (!reader.IsConnected)
                {
                    isConnected = false;
                    var mainWindow = (MainWindow)Application.Current.MainWindow;
                    mainWindow.SetConnectionIndicatorColor(Colors.Red);

                }

            }
            else
            {
                throw new InvalidOperationException("Reader is not connected.");
            }
        }

        public void StartReader()
        {
            if (!reader.IsConnected)
                throw new InvalidOperationException("Reader is not connected.");

            reader.TagsReported -= OnTagsReported;
            reader.TagsReported += OnTagsReported;
            reader.Start();
            isRunning = true;
            var mainWindow = (MainWindow)Application.Current.MainWindow;
            mainWindow.SetReadingIndicatorColor(Colors.Green);
        }

        public void StopReader()
        {
            if (reader.IsConnected)
            {
                if (isRunning)
                {
                    reader.Stop();
                    reader.TagsReported -= OnTagsReported;
                    isRunning = false;
                    var mainWindow = (MainWindow)Application.Current.MainWindow;
                    mainWindow.SetReadingIndicatorColor(Colors.Red);
                }
            }
            else
            {
                throw new InvalidOperationException("Reader is not connected.");
            }
        }

        private void OnTagsReported(ImpinjReader sender, TagReport report)
        {
            foreach (Tag tag in report)
            {   
                string epc = tag.Epc.ToString();
                ushort antenna = tag.AntennaPortNumber;

                if (!EnabledAntennaList.Contains(antenna))
                    continue;

                if (!tagMemory.ContainsKey(epc))
                {
                    var data = new RfidData { EPC = epc, AntennaPorts = new List<ushort> { antenna } };

                    if (includeFirstSeenTime)
                        data.FirstSeenTime = tag.FirstSeenTime.LocalDateTime.AddMinutes(-46).ToString("yyyy-MM-dd HH:mm:ss.fff");

                    if (includeLastSeenTime)
                        data.LastSeenTime = tag.LastSeenTime.LocalDateTime.AddMinutes(-46).ToString("yyyy-MM-dd HH:mm:ss.fff");

                    if (includeSeenCount)
                        data.SeenCount = tag.TagSeenCount;

                    if (includePeakRssi)
                        data.RSSIPeak = tag.PeakRssiInDbm;

                    if (includePhaseAngle)
                        data.Phase = Math.Round(tag.PhaseAngleInRadians * (180 / Math.PI), 2);


                    tagMemory[epc] = data;
                }
                else
                {
                    var existing = tagMemory[epc];

                    if (!existing.AntennaPorts.Contains(antenna))
                        existing.AntennaPorts.Add(antenna);

                    if (includeLastSeenTime)
                        existing.LastSeenTime = tag.LastSeenTime.LocalDateTime.AddMinutes(-46).ToString("yyyy-MM-dd HH:mm:ss");

                    if (includeSeenCount)
                        existing.SeenCount += 1; 

                    if (includePeakRssi)
                        existing.RSSIPeak = tag.PeakRssiInDbm; 

                    if (includePhaseAngle)
                        existing.Phase = Math.Round(tag.PhaseAngleInRadians * (180 / Math.PI), 2); 
                    
                }

                TagsReported?.Invoke(tagMemory[epc]);
            }
        }
        private void OnGpiChanged(ImpinjReader sender, GpiEvent e)
        {
            if (e.PortNumber == 1)
            {
                GPI1 = e.State;

                if (GPI1)
                {
                    if (isConnected && !isRunning)
                    {
                        if (isSettingsApplied)
                        {
                            StartReader();
                            
                        }
                        else
                        {
                            MessageBox.Show("Lütfen önce okuyucu ayarlarını giriniz");
                        }
                    }
                }
                else
                {
                    if (isConnected)
                    {
                        return;
                    }
                }
            }
            if (e.PortNumber == 2)
            {
                GPI2 = e.State;

                if (GPI2)
                {
                    if (isConnected && !isRunning)
                    {
                        if (isConnected && !isRunning)
                        {
                            if (isSettingsApplied)
                            {
                                StartReader();
                                
                            }
                            else
                            {
                                MessageBox.Show("Lütfen önce okuyucu ayarlarını giriniz");
                            }
                        }
                    }
                }
                else
                {
                    if (isConnected)
                    {
                        return;
                    }
                }
            }
            if (e.PortNumber == 3)
            {
                GPI3 = e.State;

                if (GPI3)
                {
                    if (isConnected && isRunning)
                    {
                        StopReader();
                        
                    }
                }
                else
                {
                    if (isConnected)
                    {
                        return ;
                    }
                }
            }
            if (e.PortNumber == 4)
            {
                GPI4 = e.State;

                if (GPI4)
                {
                    if (isConnected)
                    {
                        return;
                    }
                }
                else
                {
                    if (isConnected)
                    {
                        return;
                    }
                }
            }
        }
        public void GPO1_ON()
        {
            try
            {
                if (reader.IsConnected)
                {
                    reader.SetGpo(1, true);
                }
                else
                {
                    // Opsiyonel: log veya kullanıcıya uyarı
                }
            }
            catch (Exception ex)
            {
                // Opsiyonel: log veya kullanıcıya uyarı
            }
        }

        public void GPO1_OFF()
        {
            try
            {
                if (reader.IsConnected)
                {
                    reader.SetGpo(1, false);
                }
                else
                {
                    // Opsiyonel: log veya kullanıcıya uyarı
                }
            }
            catch (Exception ex)
            {
                // Opsiyonel: log veya kullanıcıya uyarı
            }
        }
        public void GPO2_ON()
        {
            try
            {
                if (reader.IsConnected)
                {
                    reader.SetGpo(2, true);
                }
                else
                {
                    // Opsiyonel: log veya kullanıcıya uyarı
                }
            }
            catch (Exception ex)
            {
                // Opsiyonel: log veya kullanıcıya uyarı
            }
        }


        public void GPO2_OFF()
        {
            try
            {
                if (reader.IsConnected)
                {
                    reader.SetGpo(2, false);
                }
                else
                {
                    // Opsiyonel: log veya kullanıcıya uyarı
                }
            }
            catch (Exception ex)
            {
                // Opsiyonel: log veya kullanıcıya uyarı
            }
        }
        public void GPO3_ON()
        {
            try
            {
                if (reader.IsConnected)
                {
                    reader.SetGpo(3, true);
                }
                else
                {
                    // Opsiyonel: log veya kullanıcıya uyarı
                }
            }
            catch (Exception ex)
            {
                // Opsiyonel: log veya kullanıcıya uyarı
            }
        }

        public void GPO3_OFF()
        {
            try
            {
                if (reader.IsConnected)
                {
                    reader.SetGpo(3, false);
                }
                else
                {
                    // Opsiyonel: log veya kullanıcıya uyarı
                }
            }
            catch (Exception ex)
            {
                // Opsiyonel: log veya kullanıcıya uyarı
            }
        }
        public void GPO4_ON()
        {
            try
            {
                if (reader.IsConnected)
                {
                    reader.SetGpo(4, true);
                }
                else
                {
                    // Opsiyonel: log veya kullanıcıya uyarı
                }
            }
            catch (Exception ex)
            {
                // Opsiyonel: log veya kullanıcıya uyarı
            }
        }

        public void GPO4_OFF()
        {
            try
            {
                if (reader.IsConnected)
                {
                    reader.SetGpo(4, false);
                }
                else
                {
                    // Opsiyonel: log veya kullanıcıya uyarı
                }
            }
            catch (Exception ex)
            {
                // Opsiyonel: log veya kullanıcıya uyarı
            }
        }
        public string ProgramEpc(string currentEpc, string bandilNo, string mamulDurumu)
        {
            currentEpc = currentEpc.Replace(" ", "").Trim();
            if (string.IsNullOrWhiteSpace(currentEpc) || currentEpc.Length != 24)
                throw new ArgumentException("EPC tam 96 bit (24 hex karakter) olmalıdır.");

            if (bandilNo.Length != 4)
                throw new ArgumentException("Bandıl numarası 4 haneli hex formatında olmalıdır.");

            // Mamul durumu → hex karşılığı
            string mamulHex = mamulDurumu switch
            {
                "Yarı Mamül" => "0000",
                "Bitmiş Mamül" => "0001",
                _ => throw new ArgumentException("Mamul durumu geçersiz.")
            };

            // EPC'nin başı sabit kalır, son 8 karakter (2 word) değişir
            string newEpc = currentEpc.Substring(0, 16) + bandilNo + mamulHex;

            // EPC yazma işlemi
            TagOpSequence seq = new TagOpSequence
            {
                TargetTag =
        {
            MemoryBank = MemoryBank.Epc,
            BitPointer = BitPointers.Epc,
            Data = currentEpc
        }
            };

            TagWriteOp writeEpc = new TagWriteOp
            {
                MemoryBank = MemoryBank.Epc,
                Data = TagData.FromHexString(newEpc),
                WordPointer = WordPointers.Epc
            };

            seq.Ops.Add(writeEpc);
            reader.AddOpSequence(seq);

            return newEpc;
        }


    }

    public class RfidData : INotifyPropertyChanged
    {
        private string epc;
        private ushort seenCount;
        private string firstSeenTime;
        private string lastSeenTime;
        private double rssiPeak;
        private double phase;
        private List<ushort> antennaPorts = new List<ushort>();

        public string EPC
        {
            get => epc;
            set { epc = value; OnPropertyChanged(nameof(EPC)); }
        }

        public ushort SeenCount
        {
            get => seenCount;
            set { seenCount = value; OnPropertyChanged(nameof(SeenCount)); }
        }

        public string FirstSeenTime
        {
            get => firstSeenTime;
            set { firstSeenTime = value; OnPropertyChanged(nameof(FirstSeenTime)); }
        }

        public string LastSeenTime
        {
            get => lastSeenTime;
            set { lastSeenTime = value; OnPropertyChanged(nameof(LastSeenTime)); }
        }

        public double RSSIPeak
        {
            get => rssiPeak;
            set { rssiPeak = value; OnPropertyChanged(nameof(RSSIPeak)); }
        }

        public double Phase
        {
            get => phase;
            set { phase = value; OnPropertyChanged(nameof(Phase)); }
        }

        public List<ushort> AntennaPorts
        {
            get => antennaPorts;
            set { antennaPorts = value; OnPropertyChanged(nameof(AntennaPorts)); OnPropertyChanged(nameof(AntennaPortDisplay)); }
        }

        public string AntennaPortDisplay => string.Join(",", AntennaPorts.Distinct().OrderBy(p => p));

        public string AntennaPortNameDisplay
        {
            get
            {
                var reader = App.SharedReader; 

                var namedPorts = AntennaPorts
                    .Distinct()
                    .OrderBy(p => p)
                    .Select(p =>
                    {
                        string name = reader.AntennaNameMap.ContainsKey(p) ? reader.AntennaNameMap[p] : $"Anten {p}";
                        return $"({name})";
                        //return $"Port {p} ({name})";
                    });

                return string.Join(", ", namedPorts);
            }
        }

        // Uygunluk durumu propertysi
        //public string UygunlukDurumu { get; set; } // dikkat edilmeli, yeniden bakıcam

        private string uygunlukDurumu;

        public string UygunlukDurumu
        {
            get => uygunlukDurumu;
            set { uygunlukDurumu = value; OnPropertyChanged(nameof(UygunlukDurumu)); }
        }


        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

    }
}
