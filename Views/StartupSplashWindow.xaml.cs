using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AsiErp.Services;

namespace AsiErp.Views
{
    public partial class StartupSplashWindow : Window
    {
        private bool _isLaunching;

        public StartupSplashWindow()
        {
            InitializeComponent();
            Loaded += StartupSplashWindow_Loaded;
        }

        private async void StartupSplashWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await CheckForUpdatesAndLaunchAsync();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private async Task CheckForUpdatesAndLaunchAsync()
        {
            if (_isLaunching) return;
            _isLaunching = true;

            void OnStatusChanged(string msg)
            {
                Dispatcher.Invoke(() =>
                {
                    TxtStatus.Text = msg;
                });
            }

            void OnProgressChanged(int percent)
            {
                Dispatcher.Invoke(() =>
                {
                    PbProgress.IsIndeterminate = false;
                    PbProgress.Value = percent;
                });
            }

            UpdateService.Instance.StatusChanged += OnStatusChanged;
            UpdateService.Instance.DownloadProgressChanged += OnProgressChanged;

            try
            {
                // Scan GitHub for updates with a strict 3.5-second timeout so offline users are never blocked
                var checkTask = UpdateService.Instance.CheckForUpdatesAsync(isManualCheck: false);
                var timeoutTask = Task.Delay(3500);

                var completed = await Task.WhenAny(checkTask, timeoutTask);
                if (completed == checkTask)
                {
                    bool updateApplied = await checkTask;
                    if (updateApplied)
                    {
                        // Update downloaded and Velopack is restarting into the new version
                        TxtStatus.Text = "Restarting into new version...";
                        await Task.Delay(1000);
                        return;
                    }
                }
            }
            catch
            {
                // Proceed directly to main software if network error
            }
            finally
            {
                UpdateService.Instance.StatusChanged -= OnStatusChanged;
                UpdateService.Instance.DownloadProgressChanged -= OnProgressChanged;
            }

            // Launch main software window
            TxtStatus.Text = "Starting ASI ERP...";
            PbProgress.IsIndeterminate = false;
            PbProgress.Value = 100;
            await Task.Delay(300);

            var mainWindow = new MainWindow();
            Application.Current.MainWindow = mainWindow;
            mainWindow.Show();
            Close();
        }
    }
}
