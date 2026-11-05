using System;
using System.Windows;
using System.Windows.Controls;
using AsiErp.Models;
using AsiErp.Services;

namespace AsiErp.Dialogs
{
    public partial class ExpenseWindow : Window
    {
        private readonly AppDataService _dataService = AppDataService.Instance;

        public ExpenseWindow()
        {
            InitializeComponent();
            _dataService = AppDataService.Instance;
            DpDate.SelectedDate = DateTime.Now;
        }

        private void Category_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (CbCategory?.SelectedItem is ComboBoxItem item && CbFreq != null)
            {
                string cat = item.Content.ToString() ?? "";
                string freq = cat switch
                {
                    "Salary" or "EB (Electricity)" or "Water Bill" or "ESI / PF" or "Rent" => "Monthly",
                    "Insurance & Licences" => "Yearly",
                    _ => "Daily"
                };

                foreach (ComboBoxItem fi in CbFreq.Items)
                {
                    if (fi.Content?.ToString() == freq)
                    {
                        CbFreq.SelectedItem = fi;
                        break;
                    }
                }
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(TxtAmount.Text, out double amt) || amt <= 0)
            {
                MessageBox.Show("Please enter a valid expense amount.", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string cat = (CbCategory.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Everyday Exp";
            string freq = (CbFreq.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Daily";
            string mode = (CbMode.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Bank";
            string desc = CbDescPreset?.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(desc)) desc = cat;
            string recipient = TxtPaidTo?.Text?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(recipient))
            {
                desc += $" (Paid to: {recipient})";
            }

            var exp = new ExpenseItem
            {
                Id = "e_" + Guid.NewGuid().ToString("N")[..8],
                Date = DpDate.SelectedDate?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd"),
                Category = cat,
                Frequency = freq,
                Description = desc,
                Amount = amt,
                PaymentMode = mode,
                IsAuto = false
            };

            await _dataService.AddExpenseAsync(exp);
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
