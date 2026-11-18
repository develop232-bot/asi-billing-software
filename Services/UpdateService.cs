using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
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
                    StatusChanged?.Invoke("Portable mode: Checking for latest installer...");
                    bool handled = await TryHandlePortableUpdateAsync(isManualCheck);
                    if (handled) return null;
                    if (isManualCheck)
                    {
                        System.Windows.MessageBox.Show(
                            "The app is currently running in portable mode and no newer installer could be found.",
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
                        var currentVer = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "2.7.4";
                        System.Windows.MessageBox.Show(
                            $"Your software is currently up to date!\n\nInstalled Version: ASI ERP v{currentVer}\nLatest Release: v{currentVer}\n\nAll requested features and updates are active.",
                            "Online Update", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    }
                    return null;
                }

                PendingUpdate = updateInfo;
                string newVer = updateInfo.TargetFullRelease?.Version.ToString() ?? "latest";
                StatusChanged?.Invoke($"New version found (v{newVer})! Updating automatically...");
                if (isManualCheck)
                {
                    System.Windows.MessageBox.Show(
                        $"New version v{newVer} found!\n\nDownloading the update now. ASI ERP will restart automatically when complete.",
                        "Online Update", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
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

        private async Task<bool> TryHandlePortableUpdateAsync(bool isManualCheck)
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("AsiErp-Updater");
                if (!string.IsNullOrWhiteSpace(Config.GithubToken))
                {
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Config.GithubToken);
                }

                string apiUrl = "https://api.github.com/repos/develop232-bot/asi-billing-software/releases/latest";
                string json = await client.GetStringAsync(apiUrl);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                string? tag = root.GetProperty("tag_name").GetString();
                if (string.IsNullOrEmpty(tag)) return false;

                var curVer = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                if (Version.TryParse(tag, out var remoteVer) && curVer != null && remoteVer <= curVer)
                {
                    return false;
                }

                string? setupUrl = null;
                if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                {
                    foreach (var a in assets.EnumerateArray())
                    {
                        string? name = a.GetProperty("name").GetString();
                        if (name != null && name.Equals("AsiErp-win-Setup.exe", StringComparison.OrdinalIgnoreCase))
                        {
                            setupUrl = a.GetProperty("browser_download_url").GetString();
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(setupUrl)) return false;

                if (isManualCheck)
                {
                    var ask = System.Windows.MessageBox.Show(
                        $"A new update (v{tag}) is available on GitHub!\n\nWould you like to download and install this update now?",
                        "Update Available", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
                    if (ask != System.Windows.MessageBoxResult.Yes) return true;
                }

                StatusChanged?.Invoke($"Downloading installer v{tag}...");
                string tempSetup = Path.Combine(Path.GetTempPath(), "AsiErp-win-Setup.exe");
                var bytes = await client.GetByteArrayAsync(setupUrl);
                await File.WriteAllBytesAsync(tempSetup, bytes);

                StatusChanged?.Invoke("Starting installer...");
                Process.Start(new ProcessStartInfo(tempSetup) { UseShellExecute = true });
                System.Windows.Application.Current?.Dispatcher.Invoke(() => System.Windows.Application.Current.Shutdown());
                return true;
            }
            catch
            {
                return false;
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
