using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AsiErp.Models;
using AsiErp.Services;

namespace AsiErp.Dialogs
{
    public partial class AddLoanWindow : Window
    {
        private readonly AppDataService _dataService = AppDataService.Instance;
        private readonly LoanItem? _loanToEdit;
        private readonly ObservableCollection<LoanRepayment> _repaymentsList = new();
        private readonly CultureInfo _inrCulture = CultureInfo.GetCultureInfo("en-IN");
        private bool _isInitializing = true;

        public AddLoanWindow(LoanItem? loanToEdit = null)
        {
            InitializeComponent();
            _dataService = AppDataService.Instance;
            _loanToEdit = loanToEdit;

            DgRepayments.ItemsSource = _repaymentsList;
            _repaymentsList.CollectionChanged += (s, e) => RecalculateSummary();

            DpNewRepDate.SelectedDate = DateTime.Now;

            if (_loanToEdit != null)
            {
                Title = "Edit Loan (Full Details & Repayments)";
                TxtHeaderTitle.Text = "Edit Loan";
                TxtHeaderSub.Text = "Modify borrowed loan terms, amount, lender info, and manage payment installments.";
                BtnSave.Content = "Update Loan";

                TxtLender.Text = _loanToEdit.Lender;
                TxtAmount.Text = _loanToEdit.Amount.ToString("0.##");
                TxtInterestRate.Text = _loanToEdit.InterestRate.ToString("0.##");
                TxtNote.Text = _loanToEdit.Note;

                if (DateTime.TryParse(_loanToEdit.Date, out var dt))
                {
                    DpDate.SelectedDate = dt;
                }
                else
                {
                    DpDate.SelectedDate = DateTime.Now;
                }

                CbStatus.SelectedIndex = _loanToEdit.IsManuallyClosed ? 1 : 0;

                // Load existing repayments
                if (_loanToEdit.Repayments != null)
                {
                    foreach (var rep in _loanToEdit.Repayments)
                    {
                        var repCopy = new LoanRepayment
                        {
                            Id = string.IsNullOrWhiteSpace(rep.Id) ? Guid.NewGuid().ToString("N") : rep.Id,
                            Date = rep.Date,
                            Principal = rep.Principal,
                            Interest = rep.Interest
                        };
                        repCopy.PropertyChanged += (s, e) => RecalculateSummary();
                        _repaymentsList.Add(repCopy);
                    }
                }
            }
            else
            {
                DpDate.SelectedDate = DateTime.Now;
            }

            _isInitializing = false;
            RecalculateSummary();
        }

        private void RecalculateSummary()
        {
            if (_isInitializing) return;

            double.TryParse(TxtAmount.Text, out double loanAmt);
            double totalPrincipal = _repaymentsList.Sum(r => r.Principal);
            double totalInterest = _repaymentsList.Sum(r => r.Interest);

            bool manuallyClosed = CbStatus.SelectedIndex == 1;
            double balance = manuallyClosed ? 0 : Math.Max(0, loanAmt - totalPrincipal);
            bool isOpen = !manuallyClosed && balance > 0.5;

            TxtSummaryAmount.Text = string.Format(_inrCulture, "₹{0:N0}", loanAmt);
            TxtSummaryRepaid.Text = string.Format(_inrCulture, "₹{0:N0}", totalPrincipal);
            TxtSummaryBalance.Text = string.Format(_inrCulture, "₹{0:N0}", balance);

            if (isOpen)
            {
                TxtSummaryStatus.Text = "Open";
                TxtSummaryStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xD9, 0x77, 0x06));
                BorderSummaryStatus.Background = new SolidColorBrush(Color.FromRgb(0xFE, 0xF3, 0xC7));
            }
            else
            {
                TxtSummaryStatus.Text = "Closed";
                TxtSummaryStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                BorderSummaryStatus.Background = new SolidColorBrush(Color.FromRgb(0xD1, 0xFA, 0xE5));
            }

            TxtNoRepayments.Visibility = _repaymentsList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void TxtAmount_TextChanged(object sender, TextChangedEventArgs e)
        {
            RecalculateSummary();
        }

        private void CbStatus_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RecalculateSummary();
        }

        private void DgRepayments_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(RecalculateSummary));
        }

        private void BtnAddInstallment_Click(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(TxtNewRepPrincipal.Text, out double principal) || principal < 0)
            {
                MessageBox.Show("Please enter a valid principal payment amount (>= 0).", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!double.TryParse(TxtNewRepInterest.Text, out double interest) || interest < 0)
            {
                MessageBox.Show("Please enter a valid interest payment amount (>= 0).", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (principal <= 0 && interest <= 0)
            {
                MessageBox.Show("Please enter at least a principal or interest payment amount.", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string dateStr = DpNewRepDate.SelectedDate?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd");

            var newRep = new LoanRepayment
            {
                Id = "p_" + Guid.NewGuid().ToString("N")[..8],
                Date = dateStr,
                Principal = principal,
                Interest = interest
            };
            newRep.PropertyChanged += (s, ev) => RecalculateSummary();

            _repaymentsList.Add(newRep);
            TxtNewRepPrincipal.Text = "0";
            TxtNewRepInterest.Text = "0";
            DpNewRepDate.SelectedDate = DateTime.Now;

            RecalculateSummary();
        }

        private void DeleteRepayment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is LoanRepayment rep)
            {
                var res = MessageBox.Show($"Delete repayment on {rep.Date} of ₹{rep.Principal:N0} (Principal)?",
                    "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                {
                    _repaymentsList.Remove(rep);
                    RecalculateSummary();
                }
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
            bool isClosed = CbStatus.SelectedIndex == 1;

            if (_loanToEdit != null)
            {
                _loanToEdit.Lender = lender;
                _loanToEdit.Date = DpDate.SelectedDate?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd");
                _loanToEdit.Amount = amt;
                _loanToEdit.InterestRate = rate;
                _loanToEdit.Note = note;
                _loanToEdit.IsManuallyClosed = isClosed;
                _loanToEdit.Repayments = _repaymentsList.ToList();
                _loanToEdit.NotifyCalculatedProperties();

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
                    Note = note,
                    IsManuallyClosed = isClosed,
                    Repayments = _repaymentsList.ToList()
                };
                loan.NotifyCalculatedProperties();

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