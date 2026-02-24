using SideBar_Nav.Models;
using SideBar_Nav.Pages;
using SideBar_Nav.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SideBar_Nav.Windows
{
    public partial class UserSettingsWindow : Window
    {
        private readonly List<TextBox> hexTextBoxes;
        private readonly UserSettingsService settingsService = new();
        private bool isInitialLoadCompleted;
        private bool hasSupabaseWarningShown;

        public UserSettingsWindow()
        {
            InitializeComponent();

            LoadRfModes();
            LoadSessionModes();
            LoadSearchModes();
            LoadReportModes();
            LoadTxAndRxComboBoxes();

            hexTextBoxes = new List<TextBox>
            {
                TextBox1, TextBox2, TextBox3, TextBox4,
                TextBox5, TextBox6, TextBox7, TextBox8,
                TextBox9, TextBox10, TextBox11, TextBox12,
                TextBox13, TextBox14, TextBox15, TextBox16,
                TextBox17, TextBox18, TextBox19, TextBox20,
                TextBox21, TextBox22, TextBox23, TextBox24
            };

            Loaded += UserSettingsWindow_Loaded;
        }

        private async void UserSettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (isInitialLoadCompleted)
                return;

            isInitialLoadCompleted = true;

            ReaderSettingsSnapshot? snapshot = App.SharedReader.LastSavedSettings;

            if (snapshot == null)
            {
                snapshot = await TryLoadSettingsFromSupabaseAsync();
            }

            if (snapshot == null && App.SharedReader.isSettingsApplied)
            {
                snapshot = App.SharedReader.CreateSnapshot();
            }

            if (snapshot == null)
            {
                snapshot = ReaderSettingsSnapshot.CreateDefault();
            }

            LoadSnapshotToControls(snapshot);
        }

        #region Combo Loaders
        private void LoadRfModes()
        {
            var rfModes = new Dictionary<string, uint>
            {
                { "Max Throughput (Mode 0)", 0 },
                { "Hybrid (Mode 1)", 1 },
                { "Dense Reader M4 (Mode 2)", 2 },
                { "Dense Reader M8 (Mode 3)", 3 },
                { "Dense Reader M4 Two (Mode 5)", 5 },
                { "AutoSet Dense Reader (Mode 1000)", 1000 },
                { "AutoSet Deep Scan (Mode 1002)", 1002 },
                { "AutoSet Static Fast (Mode 1003)", 1003 },
                { "AutoSet Static Dense Reader (Mode 1004)", 1004 }
            };

            cmbRfMode.ItemsSource = rfModes;
            cmbRfMode.DisplayMemberPath = "Key";
            cmbRfMode.SelectedValuePath = "Value";
            cmbRfMode.SelectedIndex = 2;
        }

        private void LoadSearchModes()
        {
            var searchModes = new Dictionary<string, int>
            {
                { "Reader Selected", 0 },
                { "Single Target", 1 },
                { "Dual Target", 2 },
                { "Dual Target BTO a Select", 5 }
            };

            cmbSearchMode.ItemsSource = searchModes;
            cmbSearchMode.DisplayMemberPath = "Key";
            cmbSearchMode.SelectedValuePath = "Value";
            cmbSearchMode.SelectedIndex = 0;
        }

        private void LoadReportModes()
        {
            var reportModes = new Dictionary<string, int>
            {
                { "Wait for Query", 0 },
                { "Individual", 1 },
                { "Batch After Stop", 3 }
            };

            cmbReportMode.ItemsSource = reportModes;
            cmbReportMode.DisplayMemberPath = "Key";
            cmbReportMode.SelectedValuePath = "Value";
            cmbReportMode.SelectedIndex = 1;
        }

        private void LoadTxAndRxComboBoxes()
        {
            var txPowerLevels = Enumerable.Range(0, 5).Select(i => 10 + i * 5).ToList();

            cmbTx1.ItemsSource = txPowerLevels;
            cmbTx2.ItemsSource = txPowerLevels;
            cmbTx3.ItemsSource = txPowerLevels;
            cmbTx4.ItemsSource = txPowerLevels;

            cmbTx1.SelectedIndex = 2;
            cmbTx2.SelectedIndex = 2;
            cmbTx3.SelectedIndex = 2;
            cmbTx4.SelectedIndex = 2;

            var rxSensitivityLevels = Enumerable.Range(0, 13).Select(i => -80 + i * 5).ToList();

            cmbRx1.ItemsSource = rxSensitivityLevels;
            cmbRx2.ItemsSource = rxSensitivityLevels;
            cmbRx3.ItemsSource = rxSensitivityLevels;
            cmbRx4.ItemsSource = rxSensitivityLevels;

            cmbRx1.SelectedIndex = 5;
            cmbRx2.SelectedIndex = 5;
            cmbRx3.SelectedIndex = 5;
            cmbRx4.SelectedIndex = 5;
        }

        private void LoadSessionModes()
        {
            var sessions = new Dictionary<string, ushort>
            {
                { "Session 0 - Fast Inventory", 0 },
                { "Session 1 - Moderate", 1 },
                { "Session 2 - Dense Environment", 2 },
                { "Session 3 - Maximum Tag Population", 3 }
            };

            cmbSession.ItemsSource = sessions;
            cmbSession.DisplayMemberPath = "Key";
            cmbSession.SelectedValuePath = "Value";
            cmbSession.SelectedIndex = 0;
        }
        #endregion

        #region Hex Input Handling
        private void HexInput_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            string input = e.Text.ToUpper();
            const string validHexChars = "0123456789ABCDEF";

            if (!validHexChars.Contains(input))
            {
                e.Handled = true;
            }
        }

        private void HexInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox textBox)
                return;

            if (textBox.Text.Length > 0)
            {
                int caretIndex = textBox.CaretIndex;
                textBox.Text = textBox.Text.ToUpper();
                textBox.CaretIndex = caretIndex;
            }

            if (textBox.Text.Length == textBox.MaxLength)
            {
                MoveToSiblingTextBox(textBox, forward: true);
            }
        }

        private void HexInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not TextBox currentTextBox)
                return;

            if (e.Key == Key.Back && currentTextBox.Text.Length == 0)
            {
                MoveToSiblingTextBox(currentTextBox, forward: false);
            }
        }

        private void MoveToSiblingTextBox(TextBox currentTextBox, bool forward)
        {
            string name = currentTextBox.Name;
            if (!name.StartsWith("TextBox", StringComparison.Ordinal))
                return;

            if (!int.TryParse(name.Substring(7), out int currentIndex))
                return;

            int targetIndex = forward ? currentIndex + 1 : currentIndex - 1;
            if (targetIndex < 1 || targetIndex > hexTextBoxes.Count)
                return;

            var parent = VisualTreeHelper.GetParent(currentTextBox);
            while (parent != null && parent is not Panel)
            {
                parent = VisualTreeHelper.GetParent(parent);
            }

            if (parent is Panel panel)
            {
                var targetTextBox = panel.FindName($"TextBox{targetIndex}") as TextBox;
                if (targetTextBox != null)
                {
                    targetTextBox.Focus();
                    if (!forward)
                        targetTextBox.SelectAll();
                }
            }
        }
        #endregion

        private ReaderSettingsSnapshot? BuildSnapshotFromControls()
        {
            bool antenna1 = chkAntenna1.IsChecked == true;
            bool antenna2 = chkAntenna2.IsChecked == true;
            bool antenna3 = chkAntenna3.IsChecked == true;
            bool antenna4 = chkAntenna4.IsChecked == true;

            if (!antenna1 && !antenna2 && !antenna3 && !antenna4)
            {
                MessageBox.Show("Lütfen en az bir anten seçin.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }

            var snapshot = new ReaderSettingsSnapshot
            {
                SelectedRfMode = (uint)(cmbRfMode.SelectedValue ?? 0u),
                SelectedSearchMode = (int)(cmbSearchMode.SelectedValue ?? 0),
                SelectedReportMode = (int)(cmbReportMode.SelectedValue ?? 1),
                SelectedSession = (ushort)(cmbSession.SelectedValue ?? (ushort)0),
                IncludeLastSeenTime = chkLastSeenTime.IsChecked == true,
                IncludePeakRssi = chkPeakRssi.IsChecked == true,
                IncludePhaseAngle = chkPhaseAngle.IsChecked == true,
                IncludeFirstSeenTime = chkFirstSeenTime.IsChecked == true,
                IncludeSeenCount = chkSeenCount.IsChecked == true,
                IsAntenna1Enabled = antenna1,
                IsAntenna2Enabled = antenna2,
                IsAntenna3Enabled = antenna3,
                IsAntenna4Enabled = antenna4,
                TxPower1 = cmbTx1.SelectedItem is int tx1 ? tx1 : 20,
                TxPower2 = cmbTx2.SelectedItem is int tx2 ? tx2 : 20,
                TxPower3 = cmbTx3.SelectedItem is int tx3 ? tx3 : 20,
                TxPower4 = cmbTx4.SelectedItem is int tx4 ? tx4 : 20,
                RxSensitivity1 = cmbRx1.SelectedItem is int rx1 ? rx1 : -55,
                RxSensitivity2 = cmbRx2.SelectedItem is int rx2 ? rx2 : -55,
                RxSensitivity3 = cmbRx3.SelectedItem is int rx3 ? rx3 : -55,
                RxSensitivity4 = cmbRx4.SelectedItem is int rx4 ? rx4 : -55,
                IsEpcFilteringEnabled = chkEPCEnableFiltering.IsChecked == true,
                IsUserMemoryFilteringEnabled = chkUserMemoryEnableFiltering.IsChecked == true,
                EpcFilterMask = txtEpcMask.Text.Trim(),
                UserFilterMask = txtUserMask.Text.Trim(),
                IsAdvancedFilteringEnabled = chkAdvancedEnableFiltering.IsChecked == true
            };

            if (!string.IsNullOrWhiteSpace(txtAntennaName1.Text) && antenna1)
                snapshot.AntennaNameMap[1] = txtAntennaName1.Text.Trim();
            else if (antenna1)
                snapshot.AntennaNameMap[1] = "Anten 1";

            if (!string.IsNullOrWhiteSpace(txtAntennaName2.Text) && antenna2)
                snapshot.AntennaNameMap[2] = txtAntennaName2.Text.Trim();
            if (!string.IsNullOrWhiteSpace(txtAntennaName3.Text) && antenna3)
                snapshot.AntennaNameMap[3] = txtAntennaName3.Text.Trim();
            if (!string.IsNullOrWhiteSpace(txtAntennaName4.Text) && antenna4)
                snapshot.AntennaNameMap[4] = txtAntennaName4.Text.Trim();

            if (snapshot.IsEpcFilteringEnabled)
            {
                snapshot.EpcBitPointer = ushort.TryParse(txtEpcBitPointer.Text.Trim(), out ushort epcPointer) ? epcPointer : (ushort)32;
                snapshot.EpcBitCount = int.TryParse(txtEpcBitCount.Text.Trim(), out int epcCount) ? epcCount : 16;
            }

            if (snapshot.IsUserMemoryFilteringEnabled)
            {
                snapshot.UserBitPointer = ushort.TryParse(txtUserBitPointer.Text.Trim(), out ushort userPointer) ? userPointer : (ushort)2;
                snapshot.UserBitCount = int.TryParse(txtUserBitCount.Text.Trim(), out int userCount) ? userCount : 1;
            }

            if (snapshot.IsAdvancedFilteringEnabled)
            {
                var hexValues = hexTextBoxes.Select(tb => tb.Text.Trim()).ToList();
                int filledHexCount = hexValues.Count(v => !string.IsNullOrEmpty(v));
                if (filledHexCount > 5)
                {
                    MessageBox.Show("En fazla 5 hex karakter girebilirsiniz! Lütfen filtrelemeyi azaltın.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }
                snapshot.HexFilterValues = hexValues;
            }
            else
            {
                snapshot.HexFilterValues = hexTextBoxes.Select(tb => tb.Text.Trim()).ToList();
            }

            return snapshot;
        }

        private async Task<ReaderSettingsSnapshot?> TryLoadSettingsFromSupabaseAsync()
        {
            Guid? userId = GetCurrentUserId();
            if (userId == null)
                return null;

            try
            {
                var snapshot = await settingsService.GetSettingsAsync(userId.Value);
                if (snapshot != null)
                {
                    App.SharedReader.LoadFromSnapshot(snapshot);
                    return snapshot;
                }
            }
            catch (Exception ex)
            {
                if (!hasSupabaseWarningShown)
                {
                    hasSupabaseWarningShown = true;
                    MessageBox.Show($"Supabase üzerinden kullanıcı ayarları yüklenirken bir hata oluştu: {ex.Message}",
                        "Bilgi", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }

            return null;
        }

        private void LoadSnapshotToControls(ReaderSettingsSnapshot snapshot)
        {
            cmbRfMode.SelectedValue = snapshot.SelectedRfMode;
            cmbSearchMode.SelectedValue = snapshot.SelectedSearchMode;
            cmbReportMode.SelectedValue = snapshot.SelectedReportMode;
            cmbSession.SelectedValue = snapshot.SelectedSession;

            chkLastSeenTime.IsChecked = snapshot.IncludeLastSeenTime;
            chkPeakRssi.IsChecked = snapshot.IncludePeakRssi;
            chkPhaseAngle.IsChecked = snapshot.IncludePhaseAngle;
            chkFirstSeenTime.IsChecked = snapshot.IncludeFirstSeenTime;
            chkSeenCount.IsChecked = snapshot.IncludeSeenCount;

            chkAntenna1.IsChecked = snapshot.IsAntenna1Enabled;
            chkAntenna2.IsChecked = snapshot.IsAntenna2Enabled;
            chkAntenna3.IsChecked = snapshot.IsAntenna3Enabled;
            chkAntenna4.IsChecked = snapshot.IsAntenna4Enabled;

            cmbTx1.SelectedItem = snapshot.TxPower1;
            cmbTx2.SelectedItem = snapshot.TxPower2;
            cmbTx3.SelectedItem = snapshot.TxPower3;
            cmbTx4.SelectedItem = snapshot.TxPower4;

            cmbRx1.SelectedItem = snapshot.RxSensitivity1;
            cmbRx2.SelectedItem = snapshot.RxSensitivity2;
            cmbRx3.SelectedItem = snapshot.RxSensitivity3;
            cmbRx4.SelectedItem = snapshot.RxSensitivity4;

            txtAntennaName1.Text = snapshot.AntennaNameMap.TryGetValue(1, out var name1) ? name1 : string.Empty;
            txtAntennaName2.Text = snapshot.AntennaNameMap.TryGetValue(2, out var name2) ? name2 : string.Empty;
            txtAntennaName3.Text = snapshot.AntennaNameMap.TryGetValue(3, out var name3) ? name3 : string.Empty;
            txtAntennaName4.Text = snapshot.AntennaNameMap.TryGetValue(4, out var name4) ? name4 : string.Empty;

            chkEPCEnableFiltering.IsChecked = snapshot.IsEpcFilteringEnabled;
            chkUserMemoryEnableFiltering.IsChecked = snapshot.IsUserMemoryFilteringEnabled;
            chkAdvancedEnableFiltering.IsChecked = snapshot.IsAdvancedFilteringEnabled;

            txtEpcMask.Text = snapshot.EpcFilterMask ?? string.Empty;
            txtEpcBitPointer.Text = snapshot.EpcBitPointer.ToString();
            txtEpcBitCount.Text = snapshot.EpcBitCount.ToString();
            txtUserMask.Text = snapshot.UserFilterMask ?? string.Empty;
            txtUserBitPointer.Text = snapshot.UserBitPointer.ToString();
            txtUserBitCount.Text = snapshot.UserBitCount.ToString();

            var hexValues = snapshot.HexFilterValues ?? new List<string>();
            for (int i = 0; i < hexTextBoxes.Count; i++)
            {
                hexTextBoxes[i].Text = i < hexValues.Count ? hexValues[i] : string.Empty;
            }
        }

        private async Task SaveSnapshotToSupabaseAsync(ReaderSettingsSnapshot snapshot)
        {
            Guid? userId = GetCurrentUserId();
            if (userId == null)
                return;

            try
            {
                await settingsService.SaveSettingsAsync(userId.Value, snapshot);
            }
            catch (Exception ex)
            {
                if (!hasSupabaseWarningShown)
                {
                    hasSupabaseWarningShown = true;
                    MessageBox.Show($"Supabase üzerine kullanıcı ayarları kaydedilirken bir hata oluştu: {ex.Message}",
                        "Bilgi", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
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

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            chkSeenCount.IsChecked = false;
            chkFirstSeenTime.IsChecked = false;
            chkLastSeenTime.IsChecked = false;
            chkPeakRssi.IsChecked = false;
            chkPhaseAngle.IsChecked = false;

            chkEPCEnableFiltering.IsChecked = false;
            chkUserMemoryEnableFiltering.IsChecked = false;
            chkAdvancedEnableFiltering.IsChecked = false;

            chkAntenna1.IsChecked = true;
            chkAntenna2.IsChecked = false;
            chkAntenna3.IsChecked = false;
            chkAntenna4.IsChecked = false;

            LoadRfModes();
            LoadSessionModes();
            LoadSearchModes();
            LoadReportModes();
            LoadTxAndRxComboBoxes();
        }


        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (App.SharedReader.isConnected && App.SharedReader.isRunning)
            {
                MessageBox.Show("Okuma işlemi devam ederken ayarlar güncellenemez. Lütfen önce okuyucuyu durdurun.",
                    "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ReaderSettingsSnapshot? snapshot = BuildSnapshotFromControls();
            if (snapshot == null)
                return;

            App.SharedReader.LoadFromSnapshot(snapshot);

            bool canApplyDirectly = App.SharedReader.isConnected && !App.SharedReader.isRunning;
            if (canApplyDirectly)
            {
                App.SharedReader.ApplyUserSettings();
            }
            else
            {
                App.SharedReader.isSettingsApplied = false;
            }

            UpdateReadingPage(snapshot);

            await SaveSnapshotToSupabaseAsync(snapshot);

            MessageBox.Show("Ayarlar kaydedildi.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void UpdateReadingPage(ReaderSettingsSnapshot snapshot)
        {
            if (Application.Current.MainWindow is not MainWindow mainWindow)
                return;

            try
            {
                var readingPage = mainWindow.GetPage<Reading>();
                if (readingPage != null)
                {
                    if (cmbRfMode.SelectedItem is KeyValuePair<string, uint> rfMode)
                        readingPage.selectedRfMode = rfMode.Key;
                    if (cmbReportMode.SelectedItem is KeyValuePair<string, int> reportMode)
                        readingPage.selectedReportMode = reportMode.Key;
                    if (cmbSearchMode.SelectedItem is KeyValuePair<string, int> searchMode)
                        readingPage.selectedSearchMode = searchMode.Key;

                    readingPage.selectedSession = snapshot.SelectedSession;
                    readingPage.UpdateSettings();
                }
            }
            catch
            {
                // Reading sayfası henüz oluşturulmamış olabilir, sessizce yoksay.
            }
        }
    }
}
