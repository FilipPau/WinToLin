using System;
using System.Windows;

namespace WinToLin
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // UI thread crashes
            DispatcherUnhandledException += (s, ex) =>
            {
                MessageBox.Show(
                    ex.Exception.ToString(),
                    "Dispatcher Crash",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );

                ex.Handled = true;
            };

            // NON-UI thread crashes
            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
            {
                MessageBox.Show(
                    ex.ExceptionObject.ToString(),
                    "AppDomain Crash",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            };

            TaskScheduler.UnobservedTaskException += (s, ex) =>
            {
                MessageBox.Show(
                    ex.Exception.ToString(),
                    "Task Crash",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );

                ex.SetObserved();
            };
        }
    }
}