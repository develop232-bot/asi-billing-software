using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AsiErp.Models;
using AsiErp.Services;

namespace AsiErp.Dialogs
{
    public partial class ProductEditWindow : Window
    {
        private readonly AppDataService _dataService = AppDataService.Instance;
        private readonly object? _existingItem;
        private string _type = "mfg"; // mfg, ready, raw, gen
        private readonly bool _isEdit;

        public ProductEditWindow(string type = "mfg", object? existing = null)
        {
            InitializeComponent();
            _dataService = AppDataService.Instance;
            _type = type;
            _existingItem = existing;
            _isEdit = existing != null;

            // Set combobox
            foreach (ComboBoxItem item in CbItemType.Items)
            {
                if (item.Tag?.ToString() == _type)
                {
                    CbItemType.SelectedItem = item;
                    break;
                }
            }

            if (_isEdit)
            {
                CbItemType.IsEnabled = false;
                TxtTitle.Text = "Edit Item";
                LoadExistingData();
            }
            else
            {
                TxtTitle.Text = "Add New Item";
                AutoSku();
                AutoBarcode();
                UpdateFieldsForType();
            }
        }

        private void LoadExistingData()
        {
            if (_existingItem is ProductItem p)
            {
                TxtName.Text = p.Name;
                TxtUnit.Text = p.Unit;
                TxtSizes.Text = string.Join(",", p.Sizes ?? new List<string>());
                TxtRetailPrice.Text = p.RetailPrice.ToString();
                TxtBulkPrice.Text = p.BulkPrice.ToString();
                TxtDomPrice.Text = p.DomesticPrice.ToString();
                TxtExpPrice.Text = p.ExportPrice.ToString("F2");
                TxtStock.Text = p.Stock.ToString();
                TxtReorder.Text = p.ReorderLevel.ToString();
                TxtHsn.Text = p.Hsn;
                TxtSku.Text = p.Sku ?? "";
                TxtBarcode.Text = string.IsNullOrWhiteSpace(p.Barcode) ? BarcodeService.GenerateBarcode() : p.Barcode;

                foreach (ComboBoxItem itm in CbCategory.Items)
                {
                    if (itm.Content?.ToString() == p.Category) { CbCategory.SelectedItem = itm; break; }
                }
            }
            else if (_existingItem is RawMaterialItem r)
            {
                TxtName.Text = r.Name;
                TxtUnit.Text = r.Unit;
                TxtRate.Text = r.Rate.ToString();
                TxtGst.Text = r.Gst.ToString();
                TxtStock.Text = r.Stock.ToString();
                TxtReorder.Text = r.ReorderLevel.ToString();
            }
            else if (_existingItem is GeneralItem g)
            {
                TxtName.Text = g.Name;
                TxtUnit.Text = g.Unit;
                TxtRate.Text = g.Rate.ToString();
                TxtGst.Text = g.Gst.ToString();
                TxtStock.Text = g.Stock.ToString();
                TxtReorder.Text = g.ReorderLevel.ToString();
            }

            UpdateFieldsForType();
        }

        private void AutoSku()
        {
            if (TxtSku != null)
            {
                int nextNum = _dataService.Products.Count + 1;
                TxtSku.Text = "ASI-" + (_type == "mfg" ? "MFG" : "RDY") + "-" + nextNum.ToString("D3");
            }
        }

        private void AutoBarcode()
        {
            if (TxtBarcode != null)
            {
                TxtBarcode.Text = BarcodeService.GenerateBarcode();
            }
        }

        private void AutoSku_Click(object sender, RoutedEventArgs e) => AutoSku();

        private void AutoBarcode_Click(object sender, RoutedEventArgs e) => AutoBarcode();

        private void SizePreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string sizes)
            {
                TxtSizes.Text = sizes;
            }
        }

        private void BulkUpload_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new BulkProductImportWindow { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                DialogResult = true;
                Close();
            }
        }

        private void PrintBarcode_Click(object sender, RoutedEventArgs e)
        {
            string name = string.IsNullOrWhiteSpace(TxtName.Text) ? "Product Item" : TxtName.Text.Trim();
            double.TryParse(TxtRetailPrice.Text, out double retail);

            var tempProd = new ProductItem
            {
                Id = (_existingItem as ProductItem)?.Id ?? "p_preview",
                Name = name,
                Category = (CbCategory.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Shoe",
                Sku = string.IsNullOrWhiteSpace(TxtSku.Text) ? "ASI-MFG-001" : TxtSku.Text.Trim(),
                Barcode = string.IsNullOrWhiteSpace(TxtBarcode.Text) ? BarcodeService.GenerateBarcode() : TxtBarcode.Text.Trim(),
                RetailPrice = retail,
                Sizes = TxtSizes.Text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList(),
                SourceType = _type == "mfg" ? "M" : "R"
            };

            var dlg = new BarcodePrintWindow(tempProd) { Owner = this };
            dlg.ShowDialog();
        }

        private void ItemType_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (CbItemType?.SelectedItem is ComboBoxItem item && item.Tag is string t)
            {
                _type = t;
                if (!_isEdit)
                {
                    AutoSku();
                }
                UpdateFieldsForType();
            }
        }

        private void UpdateFieldsForType()
        {
            bool isProd = _type == "mfg" || _type == "ready";

            if (CatWrap != null) CatWrap.Visibility = isProd ? Visibility.Visible : Visibility.Collapsed;
            if (SizesWrap != null) SizesWrap.Visibility = isProd ? Visibility.Visible : Visibility.Collapsed;
            if (SkuBarcodeWrap != null) SkuBarcodeWrap.Visibility = isProd ? Visibility.Visible : Visibility.Collapsed;
            if (RetailPriceWrap != null) RetailPriceWrap.Visibility = isProd ? Visibility.Visible : Visibility.Collapsed;
            if (BulkPriceWrap != null) BulkPriceWrap.Visibility = isProd ? Visibility.Visible : Visibility.Collapsed;
            if (DomesticPriceWrap != null) DomesticPriceWrap.Visibility = isProd ? Visibility.Visible : Visibility.Collapsed;
            if (ExportPriceWrap != null) ExportPriceWrap.Visibility = isProd ? Visibility.Visible : Visibility.Collapsed;
            if (HsnWrap != null) HsnWrap.Visibility = isProd ? Visibility.Visible : Visibility.Collapsed;

            if (RateWrap != null) RateWrap.Visibility = !isProd ? Visibility.Visible : Visibility.Collapsed;
            if (GstWrap != null) GstWrap.Visibility = !isProd ? Visibility.Visible : Visibility.Collapsed;
            if (BtnFooterPrintBarcode != null) BtnFooterPrintBarcode.Visibility = isProd ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Category_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (CbCategory?.SelectedItem is ComboBoxItem item && TxtSizes != null && !_isEdit)
            {
                string cat = item.Content.ToString() ?? "Shoe";
                TxtSizes.Text = cat switch
                {
                    "Shoe" => "6,7,8,9,10,11",
                    "Uniform" => "S,M,L,XL,XXL",
                    _ => ""
                };
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            string name = TxtName.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Please enter the item name.", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            double.TryParse(TxtStock.Text, out double stock);
            double.TryParse(TxtReorder.Text, out double reorder);

            if (_type == "raw")
            {
                double.TryParse(TxtRate.Text, out double rate);
                double.TryParse(TxtGst.Text, out double gst);
                var rm = _existingItem as RawMaterialItem ?? new RawMaterialItem { Id = "r_" + Guid.NewGuid().ToString("N")[..8] };
                rm.Name = name;
                rm.Unit = TxtUnit.Text.Trim();
                rm.Rate = rate;
                rm.Gst = gst;
                rm.Stock = stock;
                rm.ReorderLevel = reorder;
                await _dataService.SaveRawMaterialAsync(rm);
            }
            else if (_type == "gen")
            {
                double.TryParse(TxtRate.Text, out double rate);
                double.TryParse(TxtGst.Text, out double gst);
                var gi = _existingItem as GeneralItem ?? new GeneralItem { Id = "g_" + Guid.NewGuid().ToString("N")[..8] };
                gi.Name = name;
                gi.Unit = TxtUnit.Text.Trim();
                gi.Rate = rate;
                gi.Gst = gst;
                gi.Stock = stock;
                gi.ReorderLevel = reorder;
                await _dataService.SaveGeneralItemAsync(gi);
            }
            else
            {
                double.TryParse(TxtRetailPrice.Text, out double retail);
                double.TryParse(TxtBulkPrice.Text, out double bulk);
                double.TryParse(TxtDomPrice.Text, out double dom);
                double.TryParse(TxtExpPrice.Text, out double exp);

                var prod = _existingItem as ProductItem ?? new ProductItem
                {
                    Id = "p_" + Guid.NewGuid().ToString("N")[..8],
                };

                prod.Sku = string.IsNullOrWhiteSpace(TxtSku.Text)
                    ? "ASI-" + (_type == "mfg" ? "MFG" : "RDY") + "-" + (_dataService.Products.Count + 1).ToString("D3")
                    : TxtSku.Text.Trim();

                prod.Barcode = string.IsNullOrWhiteSpace(TxtBarcode.Text)
                    ? BarcodeService.GenerateBarcode()
                    : TxtBarcode.Text.Trim();

                prod.Name = name;
                prod.Category = (CbCategory.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Shoe";
                prod.SourceType = _type == "mfg" ? "M" : "R";
                prod.Unit = TxtUnit.Text.Trim();
                prod.Sizes = TxtSizes.Text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
                prod.RetailPrice = retail;
                prod.BulkPrice = bulk;
                prod.DomesticPrice = dom;
                prod.ExportPrice = exp;
                prod.Stock = stock;
                prod.ReorderLevel = reorder;
                prod.Hsn = TxtHsn.Text.Trim();
                prod.Gst = retail <= 2500 ? 5 : 18;
                prod.Bom = new();

                await _dataService.SaveProductAsync(prod);
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
