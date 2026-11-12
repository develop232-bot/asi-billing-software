using System;
using System.Globalization;
using System.Windows;
using AsiErp.Models;
using AsiErp.Services;

namespace AsiErp.Dialogs
{
    public partial class RepayLoanWindow : Window
    {
        private readonly AppDataService _dataService = AppDataService.Instance;
        private readonly LoanItem _loan;

        public RepayLoanWindow(LoanItem loan)
        {
            InitializeComponent();
            _dataService = AppDataService.Instance;
            _loan = loan ?? throw new ArgumentNullException(nameof(loan));

            TxtLenderBanner.Text = _loan.Lender;
            TxtBalanceBanner.Text = $"Balance remaining: {_loan.BalanceRemaining.ToString("C0", new CultureInfo("en-IN"))}";
            DpDate.SelectedDate = DateTime.Now;
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            double.TryParse(TxtPrincipal.Text, out double principal);
            double.TryParse(TxtInterest.Text, out double interest);

            if (principal < 0 || interest < 0 || (principal + interest) <= 0)
            {
                MessageBox.Show("Please enter an amount greater than zero.", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (principal > (_loan.BalanceRemaining + 0.5))
            {
                MessageBox.Show($"Principal amount cannot exceed the remaining loan balance of {_loan.BalanceRemaining.ToString("C0", new CultureInfo("en-IN"))}.", "Exceeds Balance", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var repayment = new LoanRepayment
            {
                Id = "lp_" + Guid.NewGuid().ToString("N")[..8],
                Date = DpDate.SelectedDate?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd"),
                Principal = principal,
                Interest = interest
            };

            await _dataService.AddLoanRepaymentAsync(_loan.Id, repayment);
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
