using System;
using System.Windows;
using AsiErp.Models;
using AsiErp.Services;

namespace AsiErp.Dialogs
{
    public partial class InvestmentWindow : Window
    {
        private readonly AppDataService _dataService = AppDataService.Instance;

        public InvestmentWindow()
        {
            InitializeComponent();
            _dataService = AppDataService.Instance;
            DpDate.SelectedDate = DateTime.Now;
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(TxtAmount.Text, out double amt) || amt <= 0)
            {
                MessageBox.Show("Please enter a valid investment amount.", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string by = CbInvestedBy?.Text?.Trim() ?? "";
            string purpose = CbPurpose?.Text?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(by) || string.IsNullOrWhiteSpace(purpose))
            {
                MessageBox.Show("Please enter both the investor name and the purpose.", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var inv = new InvestmentItem
            {
                Id = "i_" + Guid.NewGuid().ToString("N")[..8],
                Date = DpDate.SelectedDate?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd"),
                InvestedBy = by,
                Purpose = purpose,
                Amount = amt,
                IsPreStart = ChkPre.IsChecked == true
            };

            await _dataService.AddInvestmentAsync(inv);
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
