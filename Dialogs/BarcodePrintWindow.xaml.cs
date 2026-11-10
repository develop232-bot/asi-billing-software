using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AsiErp.Models;
using AsiErp.Services;

namespace AsiErp.Dialogs
{
    public partial class BarcodePrintWindow : Window
    {
        private readonly ProductItem _product;

        public BarcodePrintWindow(ProductItem product, int initialCopies = 1)
        {
            InitializeComponent();
            _product = product ?? throw new ArgumentNullException(nameof(product));

            LoadProductInfo();
            if (initialCopies > 0 && TxtCopies != null)
            {
                TxtCopies.Text = initialCopies.ToString();
            }
        }

        private void LoadProductInfo()
        {
            TxtProdName.Text = _product.Name;
            TxtSku.Text = $"SKU: {_product.Sku}";
            TxtMrp.Text = $"₹ {_product.RetailPrice:N2}";

            // Populate Sizes
            CbSize.Items.Clear();
            if (_product.Sizes != null && _product.Sizes.Any())
            {
                foreach (var size in _product.Sizes)
                {
                    CbSize.Items.Add(size);
                }
                CbSize.SelectedIndex = 0;
            }
            else
            {
                CbSize.Items.Add("Free size");
                CbSize.SelectedIndex = 0;
            }

            UpdateBarcodeImage();
        }

        private void UpdateBarcodeImage()
        {
            string barcode = _product.DisplayBarcode;
            if (string.IsNullOrWhiteSpace(barcode))
            {
                barcode = BarcodeService.GenerateBarcode(_product.Category, _product.SourceType);
            }

            ImgBarcode.Source = BarcodeService.GenerateBarcodeImage(barcode, width: 280, height: 75);
        }

        private void CbSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TxtSize != null && CbSize?.SelectedItem != null)
            {
                TxtSize.Text = $"Size: {CbSize.SelectedItem}";
            }
        }

        private void IncCopies_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TxtCopies.Text, out int c))
            {
                TxtCopies.Text = (c + 1).ToString();
            }
            else
            {
                TxtCopies.Text = "1";
            }
        }

        private void DecCopies_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TxtCopies.Text, out int c) && c > 1)
            {
                TxtCopies.Text = (c - 1).ToString();
            }
            else
            {
                TxtCopies.Text = "1";
            }
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            int.TryParse(TxtCopies.Text, out int copies);
            if (copies < 1) copies = 1;

            try
            {
                var pd = new PrintDialog();
                if (pd.ShowDialog() == true)
                {
                    for (int i = 0; i < copies; i++)
                    {
                        pd.PrintVisual(PrintableLabel, $"Barcode_{_product.Sku}_Copy{i + 1}");
                    }
                    MessageBox.Show($"Printed {copies} barcode label(s) successfully!", "Print Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Printing error: {ex.Message}", "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}