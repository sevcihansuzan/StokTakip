using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using SideBar_Nav;
using SideBar_Nav.Models;
using Supabase;
using Supabase.Postgrest;

namespace SideBar_Nav.Windows
{
    public partial class EpcSelectWindow : Window
    {
        public string SelectedEpc { get; private set; }

        private ObservableCollection<string> epcs = new ObservableCollection<string>();
        private Dictionary<string, DateTime> lastSeen = new Dictionary<string, DateTime>();
        private DispatcherTimer cleanupTimer;
        private HashSet<string> dbExisting = new HashSet<string>();

        public EpcSelectWindow(ObservableCollection<string> initialEpcs)
        {
            InitializeComponent();

            foreach (var epc in initialEpcs)
            {
                epcs.Add(epc);
                lastSeen[epc] = DateTime.Now;
            }

            dataGridEpc.ItemsSource = epcs;

            App.SharedReader.TagsReported += OnTagReported;

            cleanupTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            cleanupTimer.Tick += CleanupTimer_Tick;
            cleanupTimer.Start();

            this.Closing += (s, e) =>
            {
                App.SharedReader.TagsReported -= OnTagReported;
                cleanupTimer.Stop();
            };
        }

        private async void OnTagReported(RfidData tag)
        {
            string epc = tag.EPC;

            {
                if (!dbExisting.Contains(epc))
                {
                    bool exists = await IsEpcInDatabaseAsync(epc);
                    if (exists)
                    {
                        dbExisting.Add(epc);
                        await Dispatcher.InvokeAsync(() =>
                        {
                            epcs.Remove(epc);
                            lastSeen.Remove(epc);
                        });
                        return;
                    }
                }
                else
                {
                    // EPC already known to exist; ensure it's not shown
                    await Dispatcher.InvokeAsync(() =>
                    {
                        epcs.Remove(epc);
                        lastSeen.Remove(epc);
                    });
                    return;
                }
            }

            await Dispatcher.InvokeAsync(() =>
            {
                lastSeen[epc] = DateTime.Now;
                if (!epcs.Contains(epc))
                    epcs.Add(epc);
            });
        }

        private void CleanupTimer_Tick(object sender, EventArgs e)
        {
            if (chkLiveMode.IsChecked != true)
                return;

            var timeout = TimeSpan.FromSeconds(2);
            var toRemove = lastSeen.Where(kvp => DateTime.Now - kvp.Value > timeout)
                                   .Select(kvp => kvp.Key)
                                   .ToList();

            foreach (var epc in toRemove)
            {
                lastSeen.Remove(epc);
                epcs.Remove(epc);
            }
        }

        private async Task<bool> IsEpcInDatabaseAsync(string epc)
        {
            try
            {
                var result = await App.SupabaseClient
                    .From<UrunStok>()
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

        
        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dataGridEpc.SelectedItem is string epc)
            {
                SelectedEpc = epc;
                DialogResult = true;
                Close();
            }
        }

        private void btnEPCsec_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridEpc.SelectedItem is string epc)
            {
                SelectedEpc = epc;
                DialogResult = true;
                Close();
            }
        }
    }
}
