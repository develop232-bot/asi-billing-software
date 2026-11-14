using System.Windows;

namespace AsiErp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var splash = new Views.StartupSplashWindow();
            splash.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                Services.AppDataService.Instance.SaveLocalState();
                Services.AppDataService.Instance.SyncQueue.SaveQueue();
            }
            catch { }
            base.OnExit(e);
        }
    }
}
