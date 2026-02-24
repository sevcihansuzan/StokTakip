using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

namespace SideBar_Nav.Pages
{
    public partial class Reading : Page
    {
        public string selectedRfMode;
        public string selectedSearchMode;
        public string selectedReportMode;
        public ushort selectedSession;

        private ObservableCollection<RfidData> tagList = new ObservableCollection<RfidData>(); //Bu koleksiyon, WPF arayüzündeki DataGrid gibi UI bileşenlerine doğrudan veri bağlamak (data binding) için kullanılır. ObservableCollection sayesinde, listeye yeni bir öğe eklendiğinde, UI otomatik olarak güncellenir. Bu sınıf, WPF’nin INotifyCollectionChanged mekanizmasını desteklediği için her ekleme/silme anında UI'yı canlı tutar.
        private Dictionary<string, RfidData> tagMemory = new Dictionary<string, RfidData>(); // her bir EPC (etiket kimliği) için benzersiz bir referans saklamak amacıyla kullanılır.
        // tag list sadece yeni epc'ler için, tag memory ise mevcut epc'lerin verilerinin güncellenmesi için.
        private CollectionViewSource tagCollectionView; // Filtreleme için

        public Reading()
        {
            InitializeComponent();
            /*App.SharedReader.TagsReported += OnTagReported;
            dataGridTags.ItemsSource = tagList;*/
            
            tagCollectionView = new CollectionViewSource { Source = tagList };
            tagCollectionView.Filter += TagCollectionView_Filter;
            dataGridTags.ItemsSource = tagCollectionView.View;

            App.SharedReader.TagsReported += OnTagReported;
        }
        public void UpdateSettings()
        {
            txtRfMode.Text = selectedRfMode;
            txtSearchMode.Text = selectedSearchMode;
            txtReportMode.Text = selectedReportMode;
            txtSession.Text = selectedSession.ToString();
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

                    if (App.SharedReader.isSettingsApplied) // Ayar Kontrolü
                    {
                        App.SharedReader.StartReader();
                        MessageBox.Show("Okuma işlemi başlatıldı"); // daha sonra yeşil ışık ile değiştir.
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
        private void buttonClear_Click(object sender, RoutedEventArgs e)
        {
            tagList.Clear();
            tagMemory.Clear();
        }

        private void OnTagReported(RfidData tag)
        {
            Dispatcher.Invoke(() =>
            {
                if (tagMemory.ContainsKey(tag.EPC))
                {
                    var existing = tagMemory[tag.EPC];

                    // Yeni anten varsa ekle
                    foreach (var ant in tag.AntennaPorts)
                    {
                        if (!existing.AntennaPorts.Contains(ant))
                            existing.AntennaPorts.Add(ant);
                    }

                    // Güncellenebilecek değerleri kontrol ederek ata
                    existing.SeenCount = tag.SeenCount;

                    if (!string.IsNullOrEmpty(tag.LastSeenTime))
                        existing.LastSeenTime = tag.LastSeenTime;

                    if (tag.RSSIPeak != 0)
                        existing.RSSIPeak = tag.RSSIPeak;

                    if (tag.Phase != 0)
                        existing.Phase = tag.Phase;
                    
                    // UI ObservableCollection zaten değişimi otomatik algılar
                }
                else
                {
                    tagMemory[tag.EPC] = tag;
                    tagList.Add(tag);
                }
            });
        }
        private void epcFilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            tagCollectionView.View.Refresh();
        }
        /*private void TagCollectionView_Filter(object sender, FilterEventArgs e)
        {
            if (e.Item is RfidData tag)
            {
                string filterText = epcFilterTextBox.Text.Trim().ToLower();
                if (string.IsNullOrEmpty(filterText))
                {
                    e.Accepted = true;
                }
                else
                {
                    e.Accepted = tag.EPC.ToLower().Contains(filterText);
                }
            }
        }*/
        private void TagCollectionView_Filter(object sender, FilterEventArgs e)
        {
            if (e.Item is RfidData tag)
            {
                string filterText = epcFilterTextBox.Text.Trim().ToLower();

                if (string.IsNullOrEmpty(filterText))
                {
                    e.Accepted = true;
                    return;
                }

                // Virgülle ayır ve her birini temizle
                var filters = filterText.Split(',')
                                        .Select(f => f.Trim())
                                        .Where(f => !string.IsNullOrEmpty(f))
                                        .ToList();

                // EPC'nin bu filtrelerin herhangi birini içerip içermediğine bak
                e.Accepted = filters.Any(f => tag.EPC.ToLower().Contains(f));
            }
        }

        private void buttonSaveJson_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Filtrelenmiş verileri al
                var filteredData = tagCollectionView.View.Cast<RfidData>().ToList();

                // Veri yoksa kullanıcıyı uyar
                if (filteredData == null || filteredData.Count == 0)
                {
                    MessageBox.Show("Kaydedilecek veri bulunamadı.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Klasör yolu
                string folderPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");

                // Klasör yoksa oluştur
                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                // Dosya adı: tarih-saat bilgisiyle
                string fileName = $"RFID_Kayit_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                string fullPath = System.IO.Path.Combine(folderPath, fileName);

                // JSON olarak yaz
                string json = System.Text.Json.JsonSerializer.Serialize(filteredData, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(fullPath, json);

                MessageBox.Show($"Veriler başarıyla kaydedildi:\n{fullPath}", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void buttonExportExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var filteredData = tagCollectionView.View.Cast<RfidData>().ToList();

                if (filteredData == null || filteredData.Count == 0)
                {
                    MessageBox.Show("Dışa aktarılacak veri bulunamadı.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = $"RFID_Kayit_{DateTime.Now:yyyyMMdd_HHmmss}",
                    DefaultExt = ".xlsx",
                    Filter = "Excel dosyaları (*.xlsx)|*.xlsx"
                };

                bool? result = saveFileDialog.ShowDialog();

                if (result != true)
                {
                    // Kullanıcı iptal etti
                    return;
                }

                string fullPath = saveFileDialog.FileName;

                using (var workbook = new ClosedXML.Excel.XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Tag Data");

                    // Başlık satırı
                    worksheet.Cell(1, 1).Value = "EPC";
                    worksheet.Cell(1, 2).Value = "Seen Count";
                    worksheet.Cell(1, 3).Value = "First Seen";
                    worksheet.Cell(1, 4).Value = "Last Seen";
                    worksheet.Cell(1, 5).Value = "RSSI (dBm)";
                    worksheet.Cell(1, 6).Value = "Phase";
                    worksheet.Cell(1, 7).Value = "Antenna(s)";

                    // Verileri doldur
                    for (int i = 0; i < filteredData.Count; i++)
                    {
                        var tag = filteredData[i];
                        worksheet.Cell(i + 2, 1).Value = tag.EPC;
                        worksheet.Cell(i + 2, 2).Value = tag.SeenCount;
                        worksheet.Cell(i + 2, 3).Value = tag.FirstSeenTime;
                        worksheet.Cell(i + 2, 4).Value = tag.LastSeenTime;
                        worksheet.Cell(i + 2, 5).Value = tag.RSSIPeak;
                        worksheet.Cell(i + 2, 6).Value = tag.Phase;
                        worksheet.Cell(i + 2, 7).Value = string.Join(",", tag.AntennaPorts.Distinct());
                    }

                    workbook.SaveAs(fullPath);
                }

                MessageBox.Show($"Excel dosyası başarıyla oluşturuldu:\n{fullPath}", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Excel kaydında hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


    }
}
