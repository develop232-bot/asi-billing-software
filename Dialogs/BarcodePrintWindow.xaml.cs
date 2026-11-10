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
                    double targetWidth = pd.PrintableAreaWidth > 50 && pd.PrintableAreaWidth < 500
                        ? pd.PrintableAreaWidth
                        : 210;

                    for (int i = 0; i < copies; i++)
                    {
                        var printElement = CreatePrintVisual(targetWidth);
                        pd.PrintVisual(printElement, $"Barcode_{_product.Sku}_Copy{i + 1}");
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

        private FrameworkElement CreatePrintVisual(double targetWidth)
        {
            if (targetWidth <= 50 || double.IsNaN(targetWidth) || double.IsInfinity(targetWidth))
            {
                targetWidth = 210;
            }

            var container = new Border
            {
                Background = System.Windows.Media.Brushes.White,
                Width = targetWidth,
                Padding = new Thickness(6, 6, 6, 6)
            };

            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

            sp.Children.Add(new TextBlock
            {
                Text = "AFRAH SAMIYA INDUSTRIES",
                FontWeight = FontWeights.Bold,
                FontSize = 9.5,
                Foreground = System.Windows.Media.Brushes.Black,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            sp.Children.Add(new Border
            {
                Height = 1,
                Background = System.Windows.Media.Brushes.Black,
                Margin = new Thickness(0, 3, 0, 3)
            });

            sp.Children.Add(new TextBlock
            {
                Text = _product.Name,
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                Foreground = System.Windows.Media.Brushes.Black,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = targetWidth - 12
            });

            string sizeStr = CbSize?.SelectedItem?.ToString() ?? "Free size";
            var dockMeta = new DockPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 3) };
            dockMeta.Children.Add(new TextBlock
            {
                Text = $"SKU: {_product.Sku}",
                FontSize = 8.5,
                Foreground = System.Windows.Media.Brushes.Black,
                Margin = new Thickness(0, 0, 6, 0)
            });
            dockMeta.Children.Add(new TextBlock
            {
                Text = $"· Size: {sizeStr}",
                FontWeight = FontWeights.Bold,
                FontSize = 8.5,
                Foreground = System.Windows.Media.Brushes.Black
            });
            sp.Children.Add(dockMeta);

            string barcode = _product.DisplayBarcode;
            if (string.IsNullOrWhiteSpace(barcode))
            {
                barcode = BarcodeService.GenerateBarcode(_product.Category, _product.SourceType);
            }
            int bcWidth = (int)Math.Max(150, targetWidth - 20);
            var img = new Image
            {
                Source = BarcodeService.GenerateBarcodeImage(barcode, width: bcWidth, height: 60),
                Width = bcWidth,
                Height = 55,
                Stretch = System.Windows.Media.Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 2)
            };
            sp.Children.Add(img);

            sp.Children.Add(new Border
            {
                Height = 1,
                Background = System.Windows.Media.Brushes.Black,
                Margin = new Thickness(0, 3, 0, 3)
            });

            var dockMrp = new DockPanel { HorizontalAlignment = HorizontalAlignment.Stretch, Width = targetWidth - 16 };
            var spMrpLabel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            spMrpLabel.Children.Add(new TextBlock { Text = "MAX RETAIL PRICE", FontSize = 7.5, FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.Black });
            spMrpLabel.Children.Add(new TextBlock { Text = "(Incl. of all taxes)", FontSize = 6.5, Foreground = System.Windows.Media.Brushes.Black });
            DockPanel.SetDock(spMrpLabel, Dock.Left);
            dockMrp.Children.Add(spMrpLabel);

            var txtMrpVal = new TextBlock
            {
                Text = $"₹ {_product.RetailPrice:N2}",
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Foreground = System.Windows.Media.Brushes.Black,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            dockMrp.Children.Add(txtMrpVal);
            sp.Children.Add(dockMrp);

            container.Child = sp;

            container.Measure(new Size(targetWidth, double.PositiveInfinity));
            container.Arrange(new Rect(new Point(0, 0), container.DesiredSize));
            container.UpdateLayout();

            return container;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}