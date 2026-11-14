using System;
using System.Windows;
using System.Windows.Controls;
using AsiErp.Models;
using AsiErp.Services;

namespace AsiErp.Dialogs
{
    public partial class AddAssetWindow : Window
    {
        private readonly AppDataService _dataService = AppDataService.Instance;

        public AddAssetWindow()
        {
            InitializeComponent();
            _dataService = AppDataService.Instance;
            DpDate.SelectedDate = DateTime.Now;
        }

        private void CbKind_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CbKind?.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                if (TxtRate != null)
                {
                    TxtRate.Text = item.Tag.ToString() + ".0";
                }
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            string name = TxtName?.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Please enter what the asset is.", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!double.TryParse(TxtCost.Text, out double cost) || cost <= 0)
            {
                MessageBox.Show("Please enter a valid purchase cost.", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!double.TryParse(TxtRate.Text, out double rate) || rate < 0)
            {
                MessageBox.Show("Please enter a valid depreciation rate % per year.", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string kind = (CbKind.SelectedItem as ComboBoxItem)?.Content?.ToString()
                ?? (!string.IsNullOrWhiteSpace(CbKind.Text) ? CbKind.Text.Trim() : "Machinery");

            var asset = new AssetItem
            {
                Id = "a_" + Guid.NewGuid().ToString("N")[..8],
                Name = name,
                Kind = kind,
                Date = DpDate.SelectedDate?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd"),
                Cost = cost,
                DepreciationRate = rate
            };

            await _dataService.AddAssetAsync(asset);
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
