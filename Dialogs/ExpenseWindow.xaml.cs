using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using AsiErp.Models;
using AsiErp.Services;

namespace AsiErp.Dialogs
{
    public partial class ExpenseWindow : Window
    {
        private readonly AppDataService _dataService = AppDataService.Instance;

        private readonly Dictionary<string, string[]> _typesMap = new()
        {
            ["Employee"] = new[] { "Salary", "OT", "Travel Allowance", "Fuel Allowance" },
            ["Utility"] = new[] { "Rent", "EB", "Water Bill", "Phone Bill", "Mobile Bill", "Internet Bill" },
            ["Misc Exp"] = new[] { "Tea", "Water", "Others" }
        };

        public ExpenseWindow()
        {
            InitializeComponent();
            _dataService = AppDataService.Instance;
            DpDate.SelectedDate = DateTime.Now;
            UpdateTypes("Employee");
        }

        private void Category_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (CbCategory?.SelectedItem is ComboBoxItem item)
            {
                string group = item.Content.ToString() ?? "Employee";
                UpdateTypes(group);
                if (GridEmployee != null)
                {
                    GridEmployee.Visibility = (group == "Employee") ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        private void UpdateTypes(string group)
        {
            if (CbType == null) return;
            CbType.Items.Clear();
            if (_typesMap.TryGetValue(group, out var types))
            {
                foreach (var t in types)
                {
                    CbType.Items.Add(new ComboBoxItem { Content = t });
                }
                CbType.SelectedIndex = 0;
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            string group = (CbCategory.SelectedItem as ComboBoxItem)?.Content?.ToString()
                ?? (!string.IsNullOrWhiteSpace(CbCategory.Text) ? CbCategory.Text.Trim() : "Employee");
            string type = (CbType.SelectedItem as ComboBoxItem)?.Content?.ToString()
                ?? (!string.IsNullOrWhiteSpace(CbType.Text) ? CbType.Text.Trim() : "");

            if (string.IsNullOrWhiteSpace(type))
            {
                MessageBox.Show("Please select an expense type.", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!double.TryParse(TxtAmount.Text, out double amt) || amt <= 0)
            {
                MessageBox.Show("Please enter an amount.", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string emp = TxtEmployeeName?.Text?.Trim() ?? "";
            string pos = CbPosition?.Text?.Trim() ?? "";

            if (group == "Employee")
            {
                if (string.IsNullOrWhiteSpace(emp))
                {
                    MessageBox.Show("Enter the employee name", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (string.IsNullOrWhiteSpace(pos))
                {
                    MessageBox.Show("Enter the employee position", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            string details = TxtDetails?.Text?.Trim() ?? "";
            string mode = (CbPaidBy.SelectedItem as ComboBoxItem)?.Content?.ToString()
                ?? (!string.IsNullOrWhiteSpace(CbPaidBy.Text) ? CbPaidBy.Text.Trim() : "Cash");

            var expense = new ExpenseItem
            {
                Id = "e_" + Guid.NewGuid().ToString("N")[..8],
                Date = DpDate.SelectedDate?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd"),
                Group = group,
                Category = type,
                EmployeeName = (group == "Employee") ? emp : "",
                Position = (group == "Employee") ? pos : "",
                Description = details,
                Amount = amt,
                PaymentMode = mode,
                IsAuto = false
            };

            await _dataService.AddExpenseAsync(expense);
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
