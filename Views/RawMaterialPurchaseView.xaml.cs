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
                line.Name = typed;
                if (!string.IsNullOrEmpty(typed) && DataContext is PurchaseEntryViewModelBase vm)
                {
                    var match = vm.AvailableItems.FirstOrDefault(x => string.Equals(x.Name.Trim(), typed, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        line.ItemId = match.Id;
                        line.Name = match.Name;
                        tb.Text = match.Name;
                        if (string.IsNullOrWhiteSpace(line.Unit)) line.Unit = match.Unit;
                        line.Sku = match.Sku;
                    }
                    else
                    {
                        line.ItemId = string.Empty;
                        line.Sku = string.Empty;
                    }
                }
            }
        }
    }
}
