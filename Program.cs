using System;
using Velopack;

namespace AsiErp
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                // VelopackApp handles install, update, and restart arguments automatically
                VelopackApp.Build()
                    .SetAutoApplyOnStartup(true)
                    .Run();
            }
            catch
            {
                // Fallback gracefully in development environment
            }

            var app = new App();
            try
            {
                app.InitializeComponent();
            }
            catch (System.IO.IOException)
            {
                try
                {
                    var uri = new System.Uri("/AsiErp;component/App.xaml", System.UriKind.Relative);
                    System.Windows.Application.LoadComponent(app, uri);
                }
                catch { }
            }
            app.Run();
        }
    }
}
