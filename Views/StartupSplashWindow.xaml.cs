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
                // Quick 3.5-second scan to see if an update exists on GitHub
                var checkTask = UpdateService.Instance.CheckOnlyForUpdatesAsync(isManualCheck: false);
                var timeoutTask = Task.Delay(3500);

                var completed = await Task.WhenAny(checkTask, timeoutTask);
                if (completed == checkTask)
                {
                    var updateInfo = await checkTask;
                    if (updateInfo != null)
                    {
                        // An update IS available! Keep the splash open and download it with live progress!
                        string ver = updateInfo.TargetFullRelease?.Version.ToString() ?? "latest";
                        TxtStatus.Text = $"Downloading update v{ver}...";
                        await UpdateService.Instance.DownloadAndApplyAsync(updateInfo);
                        TxtStatus.Text = "Restarting into new version...";
                        await Task.Delay(1200);
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

            // Auto-sync directly with Cloud Firestore on startup
            TxtStatus.Text = "Syncing with cloud database...";
            PbProgress.IsIndeterminate = true;
            try
            {
                var cloudSyncTask = AppDataService.Instance.LoadFromCloudDatabaseAsync();
                var syncTimeout = Task.Delay(4000);
                await Task.WhenAny(cloudSyncTask, syncTimeout);
            }
            catch
            {
                // Fallback gracefully to local cached state if offline
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
