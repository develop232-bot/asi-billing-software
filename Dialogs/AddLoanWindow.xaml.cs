using System;
using System.Windows;
using AsiErp.Models;
using AsiErp.Services;

namespace AsiErp.Dialogs
{
    public partial class AddLoanWindow : Window
    {
        private readonly AppDataService _dataService = AppDataService.Instance;
        private readonly LoanItem? _loanToEdit;

        public AddLoanWindow(LoanItem? loanToEdit = null)
        {
            InitializeComponent();
            _dataService = AppDataService.Instance;
            _loanToEdit = loanToEdit;

            if (_loanToEdit != null)
            {
                Title = "Edit Loan";
                TxtHeaderTitle.Text = "Edit Loan";
                TxtHeaderSub.Text = "Modify borrowed loan details, amount, or interest rate.";
                BtnSave.Content = "Update Loan";

                TxtLender.Text = _loanToEdit.Lender;
                TxtAmount.Text = _loanToEdit.Amount.ToString();
                TxtInterestRate.Text = _loanToEdit.InterestRate.ToString();
                TxtNote.Text = _loanToEdit.Note;

                if (DateTime.TryParse(_loanToEdit.Date, out var dt))
                {
                    DpDate.SelectedDate = dt;
                }
                else
                {
                    DpDate.SelectedDate = DateTime.Now;
                }
            }
            else
            {
                DpDate.SelectedDate = DateTime.Now;
            }
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

            if (_loanToEdit != null)
            {
                _loanToEdit.Lender = lender;
                _loanToEdit.Date = DpDate.SelectedDate?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd");
                _loanToEdit.Amount = amt;
                _loanToEdit.InterestRate = rate;
                _loanToEdit.Note = note;

                await _dataService.UpdateLoanAsync(_loanToEdit);
            }
            else
            {
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
            }

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
