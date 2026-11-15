using System.Windows;

namespace AsiErp.Dialogs
{
    public enum DownloadFormat
    {
        Pdf,
        Csv
    }

    public partial class DownloadFormatDialog : Window
    {
        public DownloadFormat? SelectedFormat { get; private set; }

        public DownloadFormatDialog(string reportTitle = "Download Report")
        {
            InitializeComponent();
            TxtTitle.Text = reportTitle;
        }

        private void BtnPdf_Click(object sender, RoutedEventArgs e)
        {
            SelectedFormat = DownloadFormat.Pdf;
            DialogResult = true;
            Close();
        }

        private void BtnCsv_Click(object sender, RoutedEventArgs e)
        {
            SelectedFormat = DownloadFormat.Csv;
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            SelectedFormat = null;
            DialogResult = false;
            Close();
        }

        public static DownloadFormat? PromptFormat(Window? owner = null, string reportTitle = "Download Report")
        {
            var dialog = new DownloadFormatDialog(reportTitle);
            if (owner != null)
            {
                dialog.Owner = owner;
            }
            else if (Application.Current?.MainWindow != null)
            {
                dialog.Owner = Application.Current.MainWindow;
            }

            var result = dialog.ShowDialog();
            return result == true ? dialog.SelectedFormat : null;
        }
    }
}
