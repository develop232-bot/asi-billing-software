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

        private void ItemSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cb && cb.DataContext is PurchaseLine line)
            {
                if (cb.SelectedItem is SourcingItemOption opt)
                {
                    line.ItemId = opt.Id;
                    line.Name = opt.Name;
                    cb.Text = opt.Name;
                    if (!string.IsNullOrWhiteSpace(opt.Unit)) line.Unit = opt.Unit;
                    if (opt.Rate > 0) line.Rate = opt.Rate;
                    if (opt.Gst > 0) line.GstPercent = opt.Gst;
                    line.Sku = opt.Sku;
                }
            }
        }

        private void ItemComboBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is ComboBox cb && cb.DataContext is PurchaseLine line)
            {
                string typed = cb.Text?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(typed))
                {
                    line.Name = typed;
                    if (DataContext is PurchaseEntryViewModelBase vm)
                    {
                        var match = vm.AvailableItems.FirstOrDefault(x =>
                            string.Equals(x.Name, typed, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(x.Sku, typed, StringComparison.OrdinalIgnoreCase));
                        if (match != null)
                        {
                            line.ItemId = match.Id;
                            line.Name = match.Name;
                            cb.Text = match.Name;
                            cb.SelectedValue = match.Id;
                            if (string.IsNullOrWhiteSpace(line.Unit)) line.Unit = match.Unit;
                            if (line.Rate <= 0 && match.Rate > 0) line.Rate = match.Rate;
                            if (match.Gst > 0) line.GstPercent = match.Gst;
                            line.Sku = match.Sku;
                        }
                    }
                }
            }
        }
    }
}
