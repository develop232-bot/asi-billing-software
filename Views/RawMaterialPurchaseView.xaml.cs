using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AsiErp.Models;
using AsiErp.ViewModels;

namespace AsiErp.Views
{
    public partial class RawMaterialPurchaseView : UserControl
    {
        public RawMaterialPurchaseView()
        {
            InitializeComponent();
        }

        private void ItemTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb && tb.DataContext is PurchaseLine line)
            {
                string typed = tb.Text?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(typed))
                {
                    line.Name = typed;
                    if (DataContext is PurchaseEntryViewModelBase vm)
                    {
                        var match = vm.AvailableItems.FirstOrDefault(x =>
                            string.Equals(x.Name, typed, StringComparison.OrdinalIgnoreCase) ||
                            (!string.IsNullOrWhiteSpace(x.Sku) && string.Equals(x.Sku, typed, StringComparison.OrdinalIgnoreCase)))
                            ?? vm.AvailableItems.FirstOrDefault(x =>
                                (!string.IsNullOrWhiteSpace(x.Name) && x.Name.StartsWith(typed, StringComparison.OrdinalIgnoreCase)) ||
                                (!string.IsNullOrWhiteSpace(x.Name) && typed.Contains(x.Name, StringComparison.OrdinalIgnoreCase)));
                        if (match != null)
                        {
                            line.ItemId = match.Id;
                            line.Name = match.Name;
                            tb.Text = match.Name;
                            if (string.IsNullOrWhiteSpace(line.Unit)) line.Unit = match.Unit;
                            line.Sku = match.Sku;
                        }
                    }
                }
            }
        }
    }
}
