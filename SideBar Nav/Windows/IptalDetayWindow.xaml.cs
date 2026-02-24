using System.Data;
using System.Windows;

namespace SideBar_Nav.Windows
{
    public partial class IptalDetayWindow : Window
    {
        public IptalDetayWindow(DataRowView row)
        {
            InitializeComponent();
            txtBilgi.Text = $"Rezervasyon No: {row["RezervasyonNo"]}\n" +
                            $"Rezervasyon Kodu: {row["RezervasyonKodu"]}\n" +
                            $"Alıcı Firma: {row["AliciFirma"]}\n" +
                            $"Satış Sorumlusu: {row["SatisSorumlusu"]}\n" +
                            $"İşlem Tarihi: {row["IslemTarihi"]}\n" +
                            $"İptal Tarihi: {row["IptalTarihi"]}\n" +
                            $"İptal Eden: {row["IptalEdenPersonel"]}";
            txtSebep.Text = row["IptalSebebi"].ToString();
        }

        private void BtnKapat_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
