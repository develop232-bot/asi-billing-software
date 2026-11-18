using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
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

        private UpdateManager? _currentMgr;

        public async Task<UpdateInfo?> CheckOnlyForUpdatesAsync(bool isManualCheck = false)
        {
            if (IsChecking || IsDownloading)
            {
                if (isManualCheck)
                {
                    System.Windows.MessageBox.Show(
                        IsDownloading ? $"An update is currently downloading ({DownloadPercent}%). Please wait a moment..." : "Checking GitHub for updates, please wait a moment...",
                        "Online Update", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                return null;
            }
            IsChecking = true;

            try
            {
                var source = new GithubSource(Config.GithubRepoUrl, Config.GithubToken, Config.IncludePrerelease);
                _currentMgr = new UpdateManager(source);

                if (!_currentMgr.IsInstalled)
                {
                    StatusChanged?.Invoke("Running in portable/dev mode");
                    if (isManualCheck)
                    {
                        System.Windows.MessageBox.Show(
                            "The app is currently running in portable/development mode.\n\nTo enable automated online updates, run the setup installer (AsiErp-win-Setup.exe) located on your Desktop.",
                            "Online Update", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    }
                    return null;
                }

                StatusChanged?.Invoke("Checking GitHub for updates...");
                var updateInfo = await _currentMgr.CheckForUpdatesAsync();
                if (updateInfo == null)
                {
                    StatusChanged?.Invoke("Software is up to date");
                    if (isManualCheck)
                    {
                        var currentVer = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "2.7.2";
                        System.Windows.MessageBox.Show(
                            $"Your software is currently up to date!\n\nInstalled Version: ASI ERP v{currentVer}\nLatest Release: v{currentVer}\n\nAll requested features and updates are active.",
                            "Online Update", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    }
                    return null;
                }

                PendingUpdate = updateInfo;
                string newVer = updateInfo.TargetFullRelease?.Version.ToString() ?? "latest";
                StatusChanged?.Invoke($"New version found (v{newVer})! Updating automatically...");
                return updateInfo;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"Update check: {ex.Message}");
                if (isManualCheck)
                {
                    System.Windows.MessageBox.Show($"Could not check for online updates:\n{ex.Message}", "Online Update", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                }
                return null;
            }
            finally
            {
                IsChecking = false;
            }
        }

        public async Task<bool> CheckForUpdatesAsync(bool isManualCheck = false)
        {
            var updateInfo = await CheckOnlyForUpdatesAsync(isManualCheck);
            if (updateInfo != null)
            {
                await DownloadAndApplyAsync(updateInfo);
                return true;
            }
            return false;
        }

        public async Task DownloadAndApplyAsync(UpdateInfo updateInfo)
        {
            if (IsDownloading) return;
            IsDownloading = true;
            DownloadPercent = 0;

            try
            {
                if (_currentMgr == null)
                {
                    var source = new GithubSource(Config.GithubRepoUrl, Config.GithubToken, Config.IncludePrerelease);
                    _currentMgr = new UpdateManager(source);
                }

                await _currentMgr.DownloadUpdatesAsync(updateInfo, progress =>
                {
                    DownloadPercent = progress;
                    DownloadProgressChanged?.Invoke(progress);
                    StatusChanged?.Invoke($"Downloading update: {progress}%");
                });

                StatusChanged?.Invoke("Update complete! Restarting ASI ERP now...");
                await Task.Delay(1000);

                // Automatically restart into the new version
                _currentMgr.ApplyUpdatesAndRestart(updateInfo.TargetFullRelease);
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"Auto-update error: {ex.Message}");
            }
            finally
            {
                IsDownloading = false;
            }
        }
    }
}
