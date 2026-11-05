using System.Windows;
using AsiErp.Services;
using AsiErp.ViewModels;

namespace AsiErp.Dialogs
{
    public partial class StockAdjustWindow : Window
    {
        private readonly StockDisplayItem _item;
        private readonly AppDataService _dataService = AppDataService.Instance;

        public StockAdjustWindow(StockDisplayItem item)
        {
            InitializeComponent();
            _item = item;
            _dataService = AppDataService.Instance;

            TxtItemName.Text = _item.Name;
            TxtCurrentStock.Text = $"Currently on hand: {_item.Stock:N0} {_item.Unit}";
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(TxtQty.Text, out double q) || q <= 0)
            {
                MessageBox.Show("Please enter a valid positive quantity.", "Invalid Quantity", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            double change = CbDirection.SelectedIndex == 0 ? q : -q;
            if (_item.Stock + change < 0)
            {
                MessageBox.Show("Cannot reduce stock below zero.", "Stock Limit", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await _dataService.AdjustStockAsync(_item.Kind, _item.Id, change);
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
