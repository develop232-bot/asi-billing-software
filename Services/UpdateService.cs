using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Velopack;
using Velopack.Sources;

namespace AsiErp.Services
{
    public class UpdateConfig
    {
        public string GithubRepoUrl { get; set; } = "https://github.com/develop232-bot/asi-billing-software";
        public bool AutoCheckOnStartup { get; set; } = true;
        public bool IncludePrerelease { get; set; } = false;
        public string? GithubToken { get; set; } = null;
    }

    public class UpdateService
    {
        private static UpdateService? _instance;
        public static UpdateService Instance => _instance ??= new UpdateService();

        private readonly string _configPath;
        public UpdateConfig Config { get; private set; }

        public bool IsChecking { get; private set; }
        public bool IsDownloading { get; private set; }
        public int DownloadPercent { get; private set; }
        public UpdateInfo? PendingUpdate { get; private set; }

        public event Action<string>? StatusChanged;
        public event Action<int>? DownloadProgressChanged;

        private UpdateService()
        {
            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AsiErp");
            Directory.CreateDirectory(appData);
            _configPath = Path.Combine(appData, "update_config.json");
            Config = LoadConfig();
        }

        private UpdateConfig LoadConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    string json = File.ReadAllText(_configPath);
                    return JsonSerializer.Deserialize<UpdateConfig>(json) ?? new UpdateConfig();
                }
            }
            catch { }
            return new UpdateConfig();
        }

        public void SaveConfig(UpdateConfig config)
        {
            Config = config;
            try
            {
                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configPath, json);
            }
            catch { }
        }

        public async Task<bool> CheckForUpdatesAsync(bool isManualCheck = false)
        {
            if (IsChecking || IsDownloading) return false;
            IsChecking = true;
            StatusChanged?.Invoke("Checking GitHub for updates...");

            try
            {
                var source = new GithubSource(Config.GithubRepoUrl, Config.GithubToken, Config.IncludePrerelease);
                var mgr = new UpdateManager(source);

                if (!mgr.IsInstalled)
                {
                    if (isManualCheck)
                    {
                        MessageBox.Show(
                            "The application is running in portable or development mode.\n\n" +
                            "Velopack auto-updates are active when installed via the Velopack Setup package.\n\n" +
                            $"Configured GitHub Remote: {Config.GithubRepoUrl}",
                            "Velopack Update Check",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    StatusChanged?.Invoke("Running in portable/dev mode");
                    return false;
                }

                var updateInfo = await mgr.CheckForUpdatesAsync();
                if (updateInfo == null)
                {
                    StatusChanged?.Invoke("You are using the latest version.");
                    if (isManualCheck)
                    {
                        MessageBox.Show("Your software is up to date! No new versions found on GitHub.",
                            "Software Up to Date", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    return false;
                }

                PendingUpdate = updateInfo;
                string newVer = updateInfo.TargetFullRelease?.Version.ToString() ?? "New";
                StatusChanged?.Invoke($"New version available: v{newVer}");

                var result = MessageBox.Show(
                    $"🚀 A new update (v{newVer}) is available on GitHub!\n\n" +
                    $"Would you like to download and install this update now?",
                    "Update Available",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    await DownloadAndApplyAsync(mgr, updateInfo);
                    return true;
                }
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"Update check failed: {ex.Message}");
                if (isManualCheck)
                {
                    MessageBox.Show(
                        $"Could not check GitHub releases: {ex.Message}\n\nRepository: {Config.GithubRepoUrl}",
                        "Update Check Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            finally
            {
                IsChecking = false;
            }

            return false;
        }

        public async Task DownloadAndApplyAsync(UpdateManager mgr, UpdateInfo updateInfo)
        {
            IsDownloading = true;
            DownloadPercent = 0;
            StatusChanged?.Invoke("Downloading update from GitHub...");

            try
            {
                await mgr.DownloadUpdatesAsync(updateInfo, progress =>
                {
                    DownloadPercent = progress;
                    DownloadProgressChanged?.Invoke(progress);
                    StatusChanged?.Invoke($"Downloading update: {progress}%");
                });

                StatusChanged?.Invoke("Update ready! Restarting application...");

                var restartChoice = MessageBox.Show(
                    "Update downloaded successfully!\n\nRestart ASI ERP now to apply the update?",
                    "Restart to Update",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (restartChoice == MessageBoxResult.Yes)
                {
                    mgr.ApplyUpdatesAndRestart(updateInfo.TargetFullRelease);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to download or apply update: {ex.Message}", "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusChanged?.Invoke($"Update error: {ex.Message}");
            }
            finally
            {
                IsDownloading = false;
            }
        }
    }
}
