using System;
using System.Windows;
using AsiErp.Models;
using AsiErp.Services;

namespace AsiErp.Dialogs
{
    public partial class AddLoanWindow : Window
    {
        private readonly AppDataService _dataService = AppDataService.Instance;

        public AddLoanWindow()
        {
            InitializeComponent();
            _dataService = AppDataService.Instance;
            DpDate.SelectedDate = DateTime.Now;
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            string lender = TxtLender?.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(lender))
            {
                MessageBox.Show("Please enter the lender name (bank or person).", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!double.TryParse(TxtAmount.Text, out double amt) || amt <= 0)
            {
                MessageBox.Show("Please enter a valid loan amount.", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!double.TryParse(TxtInterestRate.Text, out double rate) || rate < 0)
            {
                MessageBox.Show("Please enter a valid interest rate % per year.", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string note = TxtNote?.Text?.Trim() ?? "";

            var loan = new LoanItem
            {
                Id = "l_" + Guid.NewGuid().ToString("N")[..8],
                Lender = lender,
                Date = DpDate.SelectedDate?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd"),
                Amount = amt,
                InterestRate = rate,
                Note = note
            };

            await _dataService.AddLoanAsync(loan);
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
