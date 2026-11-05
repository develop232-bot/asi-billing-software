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

        public async Task<bool> CheckForUpdatesAsync(bool isManualCheck = false)
        {
            if (IsChecking || IsDownloading) return false;
            IsChecking = true;

            try
            {
                var source = new GithubSource(Config.GithubRepoUrl, Config.GithubToken, Config.IncludePrerelease);
                var mgr = new UpdateManager(source);

                if (!mgr.IsInstalled)
                {
                    StatusChanged?.Invoke("Running in portable/dev mode");
                    return false;
                }

                StatusChanged?.Invoke("Checking GitHub for updates...");
                var updateInfo = await mgr.CheckForUpdatesAsync();
                if (updateInfo == null)
                {
                    StatusChanged?.Invoke("Software is up to date");
                    return false;
                }

                PendingUpdate = updateInfo;
                string newVer = updateInfo.TargetFullRelease?.Version.ToString() ?? "latest";
                StatusChanged?.Invoke($"🚀 New version found (v{newVer})! Updating automatically...");

                // 100% Fully automatic: download and restart without any prompts
                await DownloadAndApplyAsync(mgr, updateInfo);
                return true;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"Update check: {ex.Message}");
            }
            finally
            {
                IsChecking = false;
            }

            return false;
        }

        public async Task DownloadAndApplyAsync(UpdateManager mgr, UpdateInfo updateInfo)
        {
            if (IsDownloading) return;
            IsDownloading = true;
            DownloadPercent = 0;

            try
            {
                await mgr.DownloadUpdatesAsync(updateInfo, progress =>
                {
                    DownloadPercent = progress;
                    DownloadProgressChanged?.Invoke(progress);
                    StatusChanged?.Invoke($"Downloading update: {progress}%");
                });

                StatusChanged?.Invoke("Update complete! Restarting ASI ERP now...");
                await Task.Delay(1000);

                // Automatically restart into the new version
                mgr.ApplyUpdatesAndRestart(updateInfo.TargetFullRelease);
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
