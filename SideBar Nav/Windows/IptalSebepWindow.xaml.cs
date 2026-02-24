using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;

namespace SideBar_Nav.Windows
{
    /// <summary>
    /// Interaction logic for IptalSebepWindow.xaml
    /// </summary>
    public partial class IptalSebepWindow : Window
    {
        public string IptalSebep => txtSebep.Text.Trim();

        public IptalSebepWindow()
        {
            InitializeComponent();
        }

        private void BtnIptal_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(IptalSebep))
            {
                MessageBox.Show("Lütfen iptal sebebini girin.");
                return;
            }

            this.DialogResult = true;
            this.Close();
        }

        private void BtnVazgec_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }

}
