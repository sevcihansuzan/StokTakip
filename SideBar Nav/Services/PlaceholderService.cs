using System.Windows;
using System.Windows.Controls;
// WPF'de placeholder özelliği olmadığı için oluşturuldu.
namespace SideBar_Nav.Services
{
    // Public + static olmalı
    public static class PlaceholderService
    {
        // Attached DP: Placeholder
        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.RegisterAttached(
                "Placeholder",
                typeof(string),
                typeof(PlaceholderService),
                new PropertyMetadata(string.Empty, OnPlaceholderChanged));

        public static void SetPlaceholder(DependencyObject obj, string value)
            => obj.SetValue(PlaceholderProperty, value);

        public static string GetPlaceholder(DependencyObject obj)
            => (string)obj.GetValue(PlaceholderProperty);

        private static void OnPlaceholderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TextBox tb) return;

            // İlk yükleme
            Apply(tb);

            // Dinleyiciler tek kez eklensin
            tb.GotFocus -= Tb_GotFocus; tb.GotFocus += Tb_GotFocus;
            tb.LostFocus -= Tb_LostFocus; tb.LostFocus += Tb_LostFocus;
            tb.TextChanged -= Tb_TextChanged; tb.TextChanged += Tb_TextChanged;
        }

        private static void Tb_GotFocus(object sender, RoutedEventArgs e) => Apply(sender as TextBox);
        private static void Tb_LostFocus(object sender, RoutedEventArgs e) => Apply(sender as TextBox);
        private static void Tb_TextChanged(object sender, TextChangedEventArgs e) => Apply(sender as TextBox);

        private static void Apply(TextBox? tb)
        {
            if (tb == null) return;
            string hint = GetPlaceholder(tb);

            // Placeholder’ı gerçek text’in yerine yazmıyoruz; overlay gibi göstereceğiz
            // En basit yol: TextBox’ın içinde adeta “watermark” için Tag kullanmak
            // ve görselliği XAML tarafında Style ile halletmek.
            // Ama burada basit görünürlük için Hint’i Tag’e koyuyoruz:
            tb.Tag = hint;

            // Eğer TextBox boşsa, kırık görünmesin diye hiçbir şey yapmaya gerek yok.
            // Görünürlüğü XAML style’ındaki Trigger belirleyecek.
        }
    }
}
