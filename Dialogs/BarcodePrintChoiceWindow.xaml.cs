using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AsiErp.Models;
using AsiErp.Services;

namespace AsiErp.Dialogs
{
    public partial class BarcodePrintChoiceWindow : Window
    {
        private readonly List<ProductItem> _products;
        private ProductItem? _currentProduct;

        public class ProductComboItem
        {
            public ProductItem Product { get; set; } = null!;
            public string DisplayName => $"{Product.Name} ({Product.Sku}) · ₹{Product.RetailPrice:N0}";
        }

        public BarcodePrintChoiceWindow(List<ProductItem> allProducts, string? preselectedId = null)
        {
            InitializeComponent();
            _products = allProducts?.Where(p => p != null).ToList() ?? new List<ProductItem>();

            PopulateProducts(preselectedId);
            UpdateBatchSummary();
            UpdateTileStyles();
        }

        private void PopulateProducts(string? preselectedId)
        {
            CbProduct.Items.Clear();

            var comboItems = _products.Select(p => new ProductComboItem { Product = p }).ToList();
            foreach (var item in comboItems)
            {
                CbProduct.Items.Add(item);
            }

            if (!string.IsNullOrEmpty(preselectedId))
            {
                var match = comboItems.FirstOrDefault(x => x.Product.Id == preselectedId);
                if (match != null)
                {
                    CbProduct.SelectedItem = match;
                    return;
                }
            }

            if (CbProduct.Items.Count > 0)
            {
                CbProduct.SelectedIndex = 0;
            }
        }

        private void CbProduct_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CbProduct.SelectedItem is ProductComboItem item)
            {
                _currentProduct = item.Product;
                LoadSingleProduct(_currentProduct);
            }
        }

        private void LoadSingleProduct(ProductItem prod)
        {
            TxtProdName.Text = prod.Name;
            TxtSku.Text = $"SKU: {prod.Sku}";
            TxtMrp.Text = $"₹ {prod.RetailPrice:N2}";

            CbSize.Items.Clear();
            if (prod.Sizes != null && prod.Sizes.Any())
            {
                foreach (var size in prod.Sizes)
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

            UpdateBarcodeImage(prod);
        }

        private void UpdateBarcodeImage(ProductItem prod)
        {
            string barcode = prod.DisplayBarcode;
            if (string.IsNullOrWhiteSpace(barcode))
            {
                barcode = BarcodeService.GenerateBarcode(prod.Category, prod.SourceType);
            }

            ImgBarcode.Source = BarcodeService.GenerateBarcodeImage(barcode, width: 260, height: 70);
        }

        private void CbSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TxtSize != null && CbSize?.SelectedItem != null)
            {
                TxtSize.Text = $"Size: {CbSize.SelectedItem}";
            }
        }

        private void SelectSingle_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            RbSingle.IsChecked = true;
        }

        private void SelectAll_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            RbAll.IsChecked = true;
        }

        private void Mode_Checked(object sender, RoutedEventArgs e)
        {
            if (PanelSingle == null || PanelAll == null) return;

            bool isSingle = RbSingle.IsChecked == true;
            PanelSingle.Visibility = isSingle ? Visibility.Visible : Visibility.Collapsed;
            PanelAll.Visibility = isSingle ? Visibility.Collapsed : Visibility.Visible;

            UpdateTileStyles();
        }

        private void UpdateTileStyles()
        {
            if (BorderSingle == null || BorderAll == null) return;

            bool isSingle = RbSingle.IsChecked == true;

            BorderSingle.BorderBrush = isSingle ? new SolidColorBrush(Color.FromRgb(79, 70, 229)) : new SolidColorBrush(Color.FromRgb(226, 232, 240));
            BorderSingle.Background = isSingle ? new SolidColorBrush(Color.FromRgb(238, 242, 255)) : Brushes.White;

            BorderAll.BorderBrush = !isSingle ? new SolidColorBrush(Color.FromRgb(79, 70, 229)) : new SolidColorBrush(Color.FromRgb(226, 232, 240));
            BorderAll.Background = !isSingle ? new SolidColorBrush(Color.FromRgb(238, 242, 255)) : Brushes.White;

            if (BtnPrint != null)
            {
                BtnPrint.Content = isSingle ? "🖨️ Print Label" : "🖨️ Print All Labels";
            }
        }

        private void UpdateBatchSummary()
        {
            if (TxtTotalProductsCount == null || TxtTotalBatchLabels == null) return;

            int count = _products.Count;
            TxtTotalProductsCount.Text = $"{count} Products";

            int.TryParse(TxtBatchCopies?.Text, out int copies);
            if (copies < 1) copies = 1;

            int total = count * copies;
            TxtTotalBatchLabels.Text = $"{total} Labels ({copies} copy per item)";
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

        private void IncBatchCopies_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TxtBatchCopies.Text, out int c))
            {
                TxtBatchCopies.Text = (c + 1).ToString();
            }
            else
            {
                TxtBatchCopies.Text = "1";
            }
            UpdateBatchSummary();
        }

        private void DecBatchCopies_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TxtBatchCopies.Text, out int c) && c > 1)
            {
                TxtBatchCopies.Text = (c - 1).ToString();
            }
            else
            {
                TxtBatchCopies.Text = "1";
            }
            UpdateBatchSummary();
        }

        private void TxtBatchCopies_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateBatchSummary();
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var pd = new PrintDialog();
                if (pd.ShowDialog() != true) return;

                if (RbSingle.IsChecked == true)
                {
                    if (_currentProduct == null)
                    {
                        MessageBox.Show("Please select a product to print.", "No Product Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    int.TryParse(TxtCopies.Text, out int copies);
                    if (copies < 1) copies = 1;

                    for (int i = 0; i < copies; i++)
                    {
                        pd.PrintVisual(PrintableLabel, $"Barcode_{_currentProduct.Sku}_Copy{i + 1}");
                    }

                    MessageBox.Show($"Printed {copies} barcode label(s) for '{_currentProduct.Name}' successfully!", "Print Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    Close();
                }
                else
                {
                    if (!_products.Any())
                    {
                        MessageBox.Show("No products available to print.", "Empty Catalog", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    int.TryParse(TxtBatchCopies.Text, out int batchCopies);
                    if (batchCopies < 1) batchCopies = 1;

                    int totalPrinted = 0;
                    foreach (var prod in _products)
                    {
                        string size = prod.Sizes?.FirstOrDefault() ?? "Free size";
                        var visual = CreateLabelVisual(prod, size);

                        for (int i = 0; i < batchCopies; i++)
                        {
                            pd.PrintVisual(visual, $"Barcode_{prod.Sku}_{i + 1}");
                            totalPrinted++;
                        }
                    }

                    MessageBox.Show($"Successfully sent {totalPrinted} barcode labels ({_products.Count} products) to printer!", "Batch Printing Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Printing error: {ex.Message}", "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private FrameworkElement CreateLabelVisual(ProductItem prod, string size)
        {
            var border = new Border
            {
                Background = Brushes.White,
                Width = 340,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 10, 14, 10),
                BorderBrush = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                BorderThickness = new Thickness(1)
            };

            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

            stack.Children.Add(new TextBlock
            {
                Text = "AFRAH SAMIYA INDUSTRIES",
                FontWeight = FontWeights.Bold,
                FontSize = 10.5,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                HorizontalAlignment = HorizontalAlignment.Center
            });

            stack.Children.Add(new TextBlock
            {
                Text = "SHOES & UNIFORMS · FOOTWEAR DIVISION",
                FontSize = 8,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 1, 0, 5)
            });

            stack.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromRgb(226, 232, 240)), Margin = new Thickness(0, 0, 0, 5) });

            stack.Children.Add(new TextBlock
            {
                Text = prod.Name,
                FontWeight = FontWeights.Bold,
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 310
            });

            var dockTag = new DockPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 3, 0, 5) };
            dockTag.Children.Add(new TextBlock { Text = $"SKU: {prod.Sku}", FontSize = 9.5, Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)), Margin = new Thickness(0, 0, 8, 0) });
            dockTag.Children.Add(new TextBlock { Text = "·", Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)), Margin = new Thickness(0, 0, 8, 0) });
            dockTag.Children.Add(new TextBlock { Text = $"Size: {size}", FontWeight = FontWeights.Bold, FontSize = 9.5, Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59)) });
            stack.Children.Add(dockTag);

            string barcode = prod.DisplayBarcode;
            if (string.IsNullOrWhiteSpace(barcode))
            {
                barcode = BarcodeService.GenerateBarcode(prod.Category, prod.SourceType);
            }
            var barcodeImage = new Image
            {
                Width = 240,
                Height = 68,
                Stretch = Stretch.Uniform,
                Source = BarcodeService.GenerateBarcodeImage(barcode, width: 260, height: 70)
            };
            var imgBorder = new Border { Background = Brushes.White, Padding = new Thickness(4, 2, 4, 2), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 4) };
            imgBorder.Child = barcodeImage;
            stack.Children.Add(imgBorder);

            stack.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromRgb(226, 232, 240)), Margin = new Thickness(0, 2, 0, 5) });

            var dockPrice = new DockPanel { HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(6, 0, 6, 0) };
            var priceStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            priceStack.Children.Add(new TextBlock { Text = "MAX RETAIL PRICE", FontSize = 7.5, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)), FontWeight = FontWeights.SemiBold });
            priceStack.Children.Add(new TextBlock { Text = "(Incl. of all taxes)", FontSize = 7, Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)) });
            dockPrice.Children.Add(priceStack);

            var mrpBlock = new TextBlock
            {
                Text = $"₹ {prod.RetailPrice:N2}",
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            dockPrice.Children.Add(mrpBlock);
            stack.Children.Add(dockPrice);

            border.Child = stack;

            // Measure and arrange so it's fully rendered for printing
            border.Measure(new Size(340, 280));
            border.Arrange(new Rect(0, 0, 340, 280));
            border.UpdateLayout();

            return border;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
