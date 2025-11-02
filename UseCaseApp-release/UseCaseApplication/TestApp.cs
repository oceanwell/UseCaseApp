using System;
using System.Windows;

namespace UseCaseApplication
{
    public class TestApp : Application
    {
        [STAThread]
        public static void Main()
        {
            try
            {
                var app = new TestApp();
                app.Run();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}\n\nДетали: {ex.StackTrace}", "Ошибка запуска", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                var window = new MainWindow();
                window.Show();
                base.OnStartup(e);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании окна: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}


