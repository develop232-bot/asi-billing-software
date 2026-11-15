using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AsiErp.Models;
using AsiErp.ViewModels;

namespace AsiErp.Views
{
    public partial class ExportInvoiceView : UserControl
    {
        public ExportInvoiceView()
        {
            InitializeComponent();
        }

        private void ProductSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cb && cb.DataContext is SaleLine line)
            {
                if (cb.SelectedItem is ProductItem p)
                {
                    line.Pid = p.Id;
                    line.Name = p.FullDisplayName;
                    cb.Text = p.FullDisplayName;
                    line.Hsn = p.Hsn;
                    line.Unit = p.Unit;
                    if (DataContext is ExportInvoiceViewModel vm)
                    {
                        double fxRate = vm.FxRate > 0 ? vm.FxRate : 24.10;
                        if (p.ExportPrice > 0) line.Rate = p.ExportPrice;
                        else if (p.DomesticPrice > 0) line.Rate = Math.Round(p.DomesticPrice / fxRate, 2);
                        else if (p.RetailPrice > 0) line.Rate = Math.Round(p.RetailPrice / fxRate, 2);
                    }
                }
            }
        }

        private void ProductComboBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is ComboBox cb && cb.DataContext is SaleLine line)
            {
                string typed = cb.Text?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(typed))
                {
                    line.Name = typed;
                    if (DataContext is ExportInvoiceViewModel vm)
                    {
                        var match = vm.AvailableProducts.FirstOrDefault(x =>
                            string.Equals(x.Name, typed, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(x.Sku, typed, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(x.FullDisplayName, typed, StringComparison.OrdinalIgnoreCase));
                        if (match != null)
                        {
                            line.Pid = match.Id;
                            line.Name = match.FullDisplayName;
                            cb.Text = match.FullDisplayName;
                            cb.SelectedValue = match.Id;
                            line.Hsn = match.Hsn;
                            line.Unit = match.Unit;
                            double fxRate = vm.FxRate > 0 ? vm.FxRate : 24.10;
                            if (match.ExportPrice > 0) line.Rate = match.ExportPrice;
                            else if (match.DomesticPrice > 0) line.Rate = Math.Round(match.DomesticPrice / fxRate, 2);
                            else if (match.RetailPrice > 0) line.Rate = Math.Round(match.RetailPrice / fxRate, 2);
                        }
                    }
                }
            }
        }
    }
}
