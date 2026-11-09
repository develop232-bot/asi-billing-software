using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AsiErp.Models;
using AsiErp.Services;

namespace AsiErp.Dialogs
{
    public class MultiSelectProductItem : INotifyPropertyChanged
    {
        public ProductItem Product { get; set; } = null!;
        private bool _isSelected;
        private int _copies = 1;
        private string _selectedSize = "";

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }

        public int Copies
        {
            get => _copies;
            set { _copies = Math.Max(1, value); OnPropertyChanged(nameof(Copies)); }
        }

        public string SelectedSize
        {
            get => _selectedSize;
            set { _selectedSize = value; OnPropertyChanged(nameof(SelectedSize)); }
        }

        public string Name => Product.Name;
        public string Sku => Product.Sku;
        public string Barcode => Product.DisplayBarcode;
        public double RetailPrice => Product.RetailPrice;
        public List<string> AvailableSizes => Product.Sizes != null && Product.Sizes.Any() ? Product.Sizes : new List<string> { "Free size" };

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }

    public partial class BarcodePrintChoiceWindow : Window
    {
        private readonly List<ProductItem> _products;
        private ProductItem? _currentProduct;

        private readonly List<MultiSelectProductItem> _allMultiItems = new();
        public ObservableCollection<MultiSelectProductItem> FilteredMultiItems { get; } = new();

        public class ProductComboItem
        {
            public ProductItem Product { get; set; } = null!;
            public string DisplayName => $"{Product.Name} ({Product.Sku}) · ₹{Product.RetailPrice:N0}";
        }

        public BarcodePrintChoiceWindow(List<ProductItem> allProducts, string? preselectedId = null)
        {
            InitializeComponent();
            _products = allProducts?.Where(p => p != null).ToList() ?? new List<ProductItem>();

            InitMultiSelectItems(preselectedId);
            PopulateSingleProducts(preselectedId);
            UpdateBatchSummary();
            UpdateTileStyles();
        }

        private void InitMultiSelectItems(string? preselectedId)
        {
            _allMultiItems.Clear();
            FilteredMultiItems.Clear();

            foreach (var p in _products)
            {
                var item = new MultiSelectProductItem
                {
                    Product = p,
                    IsSelected = !string.IsNullOrEmpty(preselectedId) && p.Id == preselectedId,
                    Copies = 1,
                    SelectedSize = p.Sizes?.FirstOrDefault() ?? "Free size"
                };
                item.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(MultiSelectProductItem.IsSelected) || e.PropertyName == nameof(MultiSelectProductItem.Copies))
                    {
                        UpdateMultiSummary();
                    }
                };
                _allMultiItems.Add(item);
                FilteredMultiItems.Add(item);
            }

            DgMultiProducts.ItemsSource = FilteredMultiItems;
            UpdateMultiSummary();
        }

        private void PopulateSingleProducts(string? preselectedId)
        {
            CbProduct.Items.Clear();

            string q = TxtSearchSingle?.Text?.Trim().ToLowerInvariant() ?? "";
            var filtered = _products.Where(p =>
                string.IsNullOrEmpty(q) ||
                p.Name.ToLowerInvariant().Contains(q) ||
                p.Sku.ToLowerInvariant().Contains(q) ||
                (p.Barcode != null && p.Barcode.ToLowerInvariant().Contains(q))
            ).ToList();

            var comboItems = filtered.Select(p => new ProductComboItem { Product = p }).ToList();
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

        private void TxtSearchSingle_TextChanged(object sender, TextChangedEventArgs e)
        {
            PopulateSingleProducts(_currentProduct?.Id);
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

        // ==================== MODE SWITCHING ====================

        private void SelectSingle_Click(object sender, MouseButtonEventArgs e) => RbSingle.IsChecked = true;
        private void SelectMultiple_Click(object sender, MouseButtonEventArgs e) => RbMultiple.IsChecked = true;
        private void SelectAll_Click(object sender, MouseButtonEventArgs e) => RbAll.IsChecked = true;

        private void Mode_Checked(object sender, RoutedEventArgs e)
        {
            if (PanelSingle == null || PanelMultiple == null || PanelAll == null) return;

            bool isSingle = RbSingle.IsChecked == true;
            bool isMulti = RbMultiple.IsChecked == true;
            bool isAll = RbAll.IsChecked == true;

            PanelSingle.Visibility = isSingle ? Visibility.Visible : Visibility.Collapsed;
            PanelMultiple.Visibility = isMulti ? Visibility.Visible : Visibility.Collapsed;
            PanelAll.Visibility = isAll ? Visibility.Visible : Visibility.Collapsed;

            UpdateTileStyles();
        }

        private void UpdateTileStyles()
        {
            if (BorderSingle == null || BorderMultiple == null || BorderAll == null) return;

            bool isSingle = RbSingle.IsChecked == true;
            bool isMulti = RbMultiple.IsChecked == true;
            bool isAll = RbAll.IsChecked == true;

            var activeBorder = new SolidColorBrush(Color.FromRgb(79, 70, 229));
            var inactiveBorder = new SolidColorBrush(Color.FromRgb(226, 232, 240));
            var activeBg = new SolidColorBrush(Color.FromRgb(238, 242, 255));

            BorderSingle.BorderBrush = isSingle ? activeBorder : inactiveBorder;
            BorderSingle.Background = isSingle ? activeBg : Brushes.White;

            BorderMultiple.BorderBrush = isMulti ? activeBorder : inactiveBorder;
            BorderMultiple.Background = isMulti ? activeBg : Brushes.White;

            BorderAll.BorderBrush = isAll ? activeBorder : inactiveBorder;
            BorderAll.Background = isAll ? activeBg : Brushes.White;

            if (BtnPrint != null)
            {
                if (isSingle) BtnPrint.Content = "🖨️ Print Label";
                else if (isMulti)
                {
                    int total = _allMultiItems.Where(x => x.IsSelected).Sum(x => x.Copies);
                    BtnPrint.Content = total > 0 ? $"🖨️ Print Selected ({total} Labels)" : "🖨️ Print Selected";
                }
                else BtnPrint.Content = "🖨️ Print All Labels";
            }
        }

        // ==================== MULTI-SELECT ACTIONS ====================

        private void TxtSearchMulti_TextChanged(object sender, TextChangedEventArgs e)
        {
            string q = TxtSearchMulti.Text.Trim().ToLowerInvariant();
            FilteredMultiItems.Clear();

            foreach (var item in _allMultiItems)
            {
                if (string.IsNullOrEmpty(q) ||
                    item.Name.ToLowerInvariant().Contains(q) ||
                    item.Sku.ToLowerInvariant().Contains(q) ||
                    (item.Barcode != null && item.Barcode.ToLowerInvariant().Contains(q)))
                {
                    FilteredMultiItems.Add(item);
                }
            }
        }

        private void SelectAllMulti_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in FilteredMultiItems) item.IsSelected = true;
            UpdateMultiSummary();
        }

        private void DeselectAllMulti_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in FilteredMultiItems) item.IsSelected = false;
            UpdateMultiSummary();
        }

        private void MultiItemCheck_Click(object sender, RoutedEventArgs e)
        {
            UpdateMultiSummary();
        }

        private void UpdateMultiSummary()
        {
            if (TxtMultiSummaryBadge == null) return;

            int selectedCount = _allMultiItems.Count(x => x.IsSelected);
            int totalLabels = _allMultiItems.Where(x => x.IsSelected).Sum(x => x.Copies);

            TxtMultiSummaryBadge.Text = $"{selectedCount} items selected · {totalLabels} total labels";
            if (RbMultiple?.IsChecked == true && BtnPrint != null)
            {
                BtnPrint.Content = totalLabels > 0 ? $"🖨️ Print Selected ({totalLabels} Labels)" : "🖨️ Print Selected";
            }
        }

        private void IncMultiDefaultCopies_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TxtMultiDefaultCopies.Text, out int c)) TxtMultiDefaultCopies.Text = (c + 1).ToString();
            else TxtMultiDefaultCopies.Text = "1";
        }

        private void DecMultiDefaultCopies_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TxtMultiDefaultCopies.Text, out int c) && c > 1) TxtMultiDefaultCopies.Text = (c - 1).ToString();
            else TxtMultiDefaultCopies.Text = "1";
        }

        private void ApplyDefaultCopies_Click(object sender, RoutedEventArgs e)
        {
            int.TryParse(TxtMultiDefaultCopies.Text, out int c);
            if (c < 1) c = 1;

            foreach (var item in _allMultiItems.Where(x => x.IsSelected))
            {
                item.Copies = c;
            }
            UpdateMultiSummary();
        }

        private void RowIncCopies_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is MultiSelectProductItem item)
            {
                item.Copies++;
                UpdateMultiSummary();
            }
        }

        private void RowDecCopies_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is MultiSelectProductItem item && item.Copies > 1)
            {
                item.Copies--;
                UpdateMultiSummary();
            }
        }

        private void RowCopies_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateMultiSummary();
        }

        // ==================== BATCH ALL PRODUCTS ====================

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
            if (int.TryParse(TxtCopies.Text, out int c)) TxtCopies.Text = (c + 1).ToString();
            else TxtCopies.Text = "1";
        }

        private void DecCopies_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TxtCopies.Text, out int c) && c > 1) TxtCopies.Text = (c - 1).ToString();
            else TxtCopies.Text = "1";
        }

        private void IncBatchCopies_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TxtBatchCopies.Text, out int c)) TxtBatchCopies.Text = (c + 1).ToString();
            else TxtBatchCopies.Text = "1";
            UpdateBatchSummary();
        }

        private void DecBatchCopies_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TxtBatchCopies.Text, out int c) && c > 1) TxtBatchCopies.Text = (c - 1).ToString();
            else TxtBatchCopies.Text = "1";
            UpdateBatchSummary();
        }

        private void TxtBatchCopies_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateBatchSummary();
        }

        // ==================== EXECUTE PRINTING ====================

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
                else if (RbMultiple.IsChecked == true)
                {
                    var selected = _allMultiItems.Where(x => x.IsSelected).ToList();
                    if (!selected.Any())
                    {
                        MessageBox.Show("Please select at least one product using the checkboxes to print.", "No Products Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    int totalPrinted = 0;
                    foreach (var item in selected)
                    {
                        string size = !string.IsNullOrWhiteSpace(item.SelectedSize) ? item.SelectedSize : (item.Product.Sizes?.FirstOrDefault() ?? "Free size");
                        var visual = CreateLabelVisual(item.Product, size);

                        for (int i = 0; i < item.Copies; i++)
                        {
                            pd.PrintVisual(visual, $"Barcode_{item.Sku}_{i + 1}");
                            totalPrinted++;
                        }
                    }

                    MessageBox.Show($"Successfully sent {totalPrinted} barcode labels across {selected.Count} selected products to printer continuously!", "Continuous Print Complete", MessageBoxButton.OK, MessageBoxImage.Information);
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
