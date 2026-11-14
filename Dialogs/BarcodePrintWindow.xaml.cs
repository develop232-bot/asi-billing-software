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
            UpdateSelectedSizeLabel();
        }

        private void CbSize_LostFocus(object sender, RoutedEventArgs e)
        {
            UpdateSelectedSizeLabel();
        }

        private void CbSize_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            UpdateSelectedSizeLabel();
        }

        private void UpdateSelectedSizeLabel()
        {
            if (TxtSize == null || CbSize == null) return;
            string sz = !string.IsNullOrWhiteSpace(CbSize.Text) ? CbSize.Text.Trim() : (CbSize.SelectedItem?.ToString() ?? "");
            if (!string.IsNullOrEmpty(sz))
            {
                TxtSize.Text = $"Size: {sz}";
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
            if (!int.TryParse(TxtCopies.Text, out int copies) || copies < 1)
            {
                copies = 1;
            }

            try
            {
                var pd = new PrintDialog();
                if (pd.ShowDialog() == true)
                {
                    // Standard 2-inch thermal sticker label width = 50.8mm = ~192 DIP, height = 25mm = ~94 DIP
                    double labelWidth = (pd.PrintableAreaWidth > 50 && pd.PrintableAreaWidth < 300)
                        ? pd.PrintableAreaWidth
                        : 185;

                    double labelHeight = (pd.PrintableAreaHeight > 50 && pd.PrintableAreaHeight < 200)
                        ? Math.Min(pd.PrintableAreaHeight, 88)
                        : 88;

                    // Build FixedDocument with exact page count matching requested copies
                    var fixedDoc = new System.Windows.Documents.FixedDocument();
                    for (int i = 0; i < copies; i++)
                    {
                        var page = new System.Windows.Documents.FixedPage
                        {
                            Width = labelWidth,
                            Height = labelHeight
                        };

                        var printVisual = CreatePrintVisual(labelWidth, labelHeight);
                        System.Windows.Controls.Canvas.SetLeft(printVisual, 0);
                        System.Windows.Controls.Canvas.SetTop(printVisual, 0);
                        page.Children.Add(printVisual);

                        var pageContent = new System.Windows.Documents.PageContent();
                        ((System.Windows.Markup.IAddChild)pageContent).AddChild(page);
                        fixedDoc.Pages.Add(pageContent);
                    }

                    pd.PrintDocument(fixedDoc.DocumentPaginator, $"Barcode_{_product.Sku}_{copies}Labels");
                    MessageBox.Show($"Sent {copies} barcode label(s) to printer successfully!", "Print Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Printing error: {ex.Message}", "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private FrameworkElement CreatePrintVisual(double targetWidth, double targetHeight)
        {
            // For 2-inch thermal sticker label (50mm x 25mm / 50mm x 30mm)
            // Safe printable bounds to guarantee zero clipping and zero spill over label gap
            double contentWidth = 146;

            var container = new Border
            {
                Background = System.Windows.Media.Brushes.White,
                Width = targetWidth > 0 ? targetWidth : 185,
                Height = targetHeight > 0 ? targetHeight : 88,
                ClipToBounds = true
            };

            var sp = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Width = contentWidth
            };

            // 1. Company Brand Name - ASI only
            sp.Children.Add(new TextBlock
            {
                Text = "ASI",
                FontWeight = FontWeights.Bold,
                FontSize = 9,
                Foreground = System.Windows.Media.Brushes.Black,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 1)
            });

            // 2. Product Name
            sp.Children.Add(new TextBlock
            {
                Text = _product.Name,
                FontWeight = FontWeights.Bold,
                FontSize = 7.5,
                Foreground = System.Windows.Media.Brushes.Black,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxHeight = 11
            });

            // 3. SKU & Size Line
            string sizeStr = !string.IsNullOrWhiteSpace(CbSize?.Text) ? CbSize.Text.Trim() : (CbSize?.SelectedItem?.ToString() ?? (_product.Sizes?.FirstOrDefault() ?? "Free size"));
            var dockMeta = new DockPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 1, 0, 1) };
            dockMeta.Children.Add(new TextBlock
            {
                Text = $"SKU: {_product.Sku}",
                FontSize = 6.5,
                Foreground = System.Windows.Media.Brushes.Black,
                Margin = new Thickness(0, 0, 4, 0)
            });
            dockMeta.Children.Add(new TextBlock
            {
                Text = $"· Size: {sizeStr}",
                FontWeight = FontWeights.Bold,
                FontSize = 6.5,
                Foreground = System.Windows.Media.Brushes.Black
            });
            sp.Children.Add(dockMeta);

            // 4. Barcode Image
            string barcode = _product.DisplayBarcode;
            if (string.IsNullOrWhiteSpace(barcode))
            {
                barcode = BarcodeService.GenerateBarcode(_product.Category, _product.SourceType);
            }
            int bcWidth = 130;
            int bcHeight = 24;
            var img = new Image
            {
                Source = BarcodeService.GenerateBarcodeImage(barcode, width: bcWidth, height: bcHeight),
                Width = bcWidth,
                Height = bcHeight,
                Stretch = System.Windows.Media.Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 1, 0, 1)
            };
            sp.Children.Add(img);

            // 5. MRP & Taxes Line (compact so price is never clipped)
            var dockMrp = new DockPanel { HorizontalAlignment = HorizontalAlignment.Center, Width = 136 };
            var txtMrpLeft = new TextBlock
            {
                Text = "MRP (Incl. taxes):",
                FontSize = 6.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = System.Windows.Media.Brushes.Black,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(txtMrpLeft, Dock.Left);
            dockMrp.Children.Add(txtMrpLeft);

            string priceStr = _product.RetailPrice % 1 != 0 ? $"₹ {_product.RetailPrice:N2}" : $"₹ {_product.RetailPrice:N0}";
            var txtMrpVal = new TextBlock
            {
                Text = priceStr,
                FontWeight = FontWeights.Bold,
                FontSize = 8.5,
                Foreground = System.Windows.Media.Brushes.Black,
                HorizontalAlignment = HorizontalAlignment.Right,
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            dockMrp.Children.Add(txtMrpVal);
            sp.Children.Add(dockMrp);

            container.Child = sp;

            container.Measure(new Size(container.Width, container.Height));
            container.Arrange(new Rect(new Point(0, 0), new Size(container.Width, container.Height)));
            container.UpdateLayout();

            return container;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}