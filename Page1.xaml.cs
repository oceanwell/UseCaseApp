using System.Windows;
using System.Windows.Input;

namespace UseCaseApplication
{
    public partial class Page1 : Window
    {
        public Page1()
        {
            InitializeComponent();
        }

        private void ZagolovokOkna_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void Svernyt_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void Razvernut_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = this.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void Zakryt_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // Обработка кнопки "Файл"
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            // Обработка кнопки "Помощь"
        }
    }
}