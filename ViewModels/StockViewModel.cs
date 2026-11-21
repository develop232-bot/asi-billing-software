using System;
using System.Collections.ObjectModel;
using System.Linq;
using AsiErp.Commands;
using System.Windows.Threading;
using AsiErp.Models;
using AsiErp.Services;

namespace AsiErp.ViewModels
{
    public class StockDisplayItem
    {
        public string Id { get; set; } = string.Empty;
        public string Kind { get; set; } = "raw"; // raw, gen, prod
        public string Name { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public double Stock { get; set; }
        public double ReorderLevel { get; set; }
        public double RateOrCost { get; set; }
        public double TotalValue => Stock * RateOrCost;
        public object? RawItem { get; set; }

        public string StatusLevel
        {
            get
            {
                // Below 10 -> Red
                if (Stock < 10 || LevelPercent < 10) return "out";
                // Medium -> Amber / Orange
                if (Stock <= ReorderLevel || LevelPercent < 50) return "low";
                // Above average -> Green
                return "ok";
            }
        }

        public string StatusLevelText
        {
            get
            {
                if (Stock <= 0) return "Out";
                if (Stock < 10 || LevelPercent < 10) return "Low";
                if (Stock <= ReorderLevel || LevelPercent < 50) return "Medium";
                return "In stock";
            }
        }

        public double LevelPercent
        {
            get
            {
                double target = Math.Max(ReorderLevel, 1) * 2.0;
                return Math.Min(100, Math.Max(0, (Stock / target) * 100));
            }
        }

        public double RemainingPercent => Math.Max(0, 100.0 - LevelPercent);
        public System.Windows.GridLength LevelStarLength => new System.Windows.GridLength(Math.Max(0.001, LevelPercent), System.Windows.GridUnitType.Star);
        public System.Windows.GridLength RemainingStarLength => new System.Windows.GridLength(Math.Max(0.001, RemainingPercent), System.Windows.GridUnitType.Star);

        public double LevelMeterWidth
        {
            get
            {
                if (Stock <= 0) return 6;
                return Math.Max(8, Math.Min(100, LevelPercent));
            }
        }
    }

    public class StockViewModel : ViewModelBase
    {
        private readonly AppDataService _dataService;
        private readonly Action<string, object?> _openDialogAction;

        private string _activeTab = "raw"; // raw, fg, ready, gen
        private string _searchQuery = string.Empty;

        public ObservableCollection<StockDisplayItem> Items { get; } = new();

        public string ActiveTab
        {
            get => _activeTab;
            set
            {
                if (SetField(ref _activeTab, value))
                {
                    ApplyFilter();
                    OnPropertyChanged(nameof(Title));
                    OnPropertyChanged(nameof(Subtitle));
                    OnPropertyChanged(nameof(AddButtonText));
                    OnPropertyChanged(nameof(BuyButtonText));
                    OnPropertyChanged(nameof(TotalValue));
                    OnPropertyChanged(nameof(LowStockCount));
                    OnPropertyChanged(nameof(TotalUnitsOnHand));
                }
            }
        }

        private DispatcherTimer? _searchDebounceTimer;
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetField(ref _searchQuery, value))
                {
                    DebounceAction(ref _searchDebounceTimer, ApplyFilter, 200);
                }
            }
        }

        public string Title => ActiveTab switch
        {
            "raw" => "Raw Material Stock",
            "fg" => "Finished Products",
            "ready" => "Readymade Stock",
            "gen" => "General Supplies",
            _ => "Stock"
        };

        public string Subtitle => ActiveTab switch
        {
            "raw" => "Leather, soles, fabrics, thread and other material used in production.",
            "fg" => "Shoes and uniforms made in our own factory, ready to sell.",
            "ready" => "Readymade items bought from vendors for resale.",
            "gen" => "Shoe boxes, uniform covers, tissue paper, ASI logo bags, single-shoe covers and other packing items.",
            _ => "Stock and inventory management"
        };

        public string AddButtonText => ActiveTab switch
        {
            "raw" => "+ Add Raw Material",
            "gen" => "+ Add General Item",
            "ready" => "+ Add Readymade",
            _ => "+ Add Product"
        };

        public string BuyButtonText => ActiveTab switch
        {
            "raw" => "Buy Raw Material",
            "gen" => "Buy General Items",
            "ready" => "Buy Readymade Goods",
            _ => "New Work Order"
        };

        public double TotalValue => Items.Sum(x => x.TotalValue);
        public int LowStockCount => Items.Count(x => x.StatusLevel != "ok");
        public double TotalUnitsOnHand => Items.Sum(x => x.Stock);

        public RelayCommand SetTabCommand { get; }
        public RelayCommand AdjustStockCommand { get; }
        public RelayCommand EditItemCommand { get; }
        public RelayCommand AddItemCommand { get; }
        public RelayCommand BuyItemCommand { get; }
        public RelayCommand ExportCsvCommand { get; }
        public RelayCommand DownloadFileCommand { get; }

        public StockViewModel(AppDataService dataService, Action<string, object?> openDialogAction, string initialTab = "raw")
        {
            _dataService = dataService;
            _openDialogAction = openDialogAction;
            _activeTab = initialTab;

            SetTabCommand = new RelayCommand(p => ActiveTab = p?.ToString() ?? "raw");

            AdjustStockCommand = new RelayCommand(p =>
            {
                if (p is StockDisplayItem item) _openDialogAction("stock_adjust", item);
            });

            EditItemCommand = new RelayCommand(p =>
            {
                if (p is StockDisplayItem item)
                {
                    string dlgType = item.Kind switch
                    {
                        "raw" => "product_edit_raw",
                        "gen" => "product_edit_gen",
                        "ready" => "product_edit_ready",
                        _ => "product_edit_mfg"
                    };

                    object? raw = item.RawItem;
                    if (raw == null)
                    {
                        raw = item.Kind switch
                        {
                            "raw" => _dataService.RawMaterials.FirstOrDefault(x => x.Id == item.Id),
                            "gen" => _dataService.GeneralItems.FirstOrDefault(x => x.Id == item.Id),
                            _ => _dataService.Products.FirstOrDefault(x => x.Id == item.Id)
                        };
                    }

                    _openDialogAction(dlgType, raw);
                }
            });

            AddItemCommand = new RelayCommand(_ =>
            {
                string dlgType = ActiveTab switch
                {
                    "raw" => "product_edit_raw",
                    "gen" => "product_edit_gen",
                    "ready" => "product_edit_ready",
                    _ => "product_edit_mfg"
                };
                _openDialogAction(dlgType, null);
            });

            BuyItemCommand = new RelayCommand(_ =>
            {
                if (ActiveTab == "fg")
                {
                    _openDialogAction("work_order", null);
                    return;
                }
                string kind = ActiveTab switch
                {
                    "raw" => "raw",
                    "ready" => "ready",
                    "gen" => "gen",
                    _ => "raw"
                };
                _openDialogAction("purchase_bill", kind);
            });

            ExportCsvCommand = new RelayCommand(_ => DownloadFile());
            DownloadFileCommand = new RelayCommand(_ => DownloadFile());

            _dataService.DataChanged += () =>
            {
                if (!IsActive) { IsStale = true; return; }
                RefreshData();
            };
            RefreshData();
        }

        public override void RefreshData()
        {
            ApplyFilter();
            OnPropertyChanged(nameof(TotalValue));
            OnPropertyChanged(nameof(LowStockCount));
            OnPropertyChanged(nameof(TotalUnitsOnHand));
        }

        private void ApplyFilter()
        {
            Items.Clear();
            string q = SearchQuery.Trim().ToLowerInvariant();

            var list = new System.Collections.Generic.List<StockDisplayItem>();

            if (ActiveTab == "raw")
            {
                var grouped = _dataService.RawMaterials
                    .Where(r => !r.Id.StartsWith("pur_it_") && !r.Id.StartsWith("pur_") && !string.IsNullOrWhiteSpace(r.Name) && r.Name.Trim() != "—" && r.Name.Trim() != "-")
                    .GroupBy(r => !string.IsNullOrWhiteSpace(r.Sku) ? r.Sku.Trim() : r.Name.Trim(), StringComparer.OrdinalIgnoreCase);

                list = grouped.Select(grp =>
                {
                    var primary = grp.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Name)) ?? grp.OrderByDescending(x => x.CreatedAt).First();
                    if (string.IsNullOrWhiteSpace(primary.Name)) return null;
                    double totalStock = Math.Round(grp.Sum(x => x.Stock), 2);
                    return new StockDisplayItem
                    {
                        Id = primary.Id,
                        Kind = "raw",
                        Name = primary.Name,
                        Sku = !string.IsNullOrWhiteSpace(primary.Sku) ? primary.Sku : primary.Name,
                        Subtitle = $"Rate: ₹{primary.Rate:N0} / {primary.Unit} · GST {primary.Gst}%",
                        Unit = primary.Unit,
                        Stock = totalStock,
                        ReorderLevel = primary.ReorderLevel,
                        RateOrCost = primary.Rate,
                        RawItem = primary
                    };
                }).Where(it => it != null).Select(it => it!).ToList();
            }
            else if (ActiveTab == "gen")
            {
                var grouped = _dataService.GeneralItems
                    .Where(g => !string.IsNullOrWhiteSpace(g.Name) && g.Name.Trim() != "—" && g.Name.Trim() != "-")
                    .GroupBy(g => !string.IsNullOrWhiteSpace(g.Sku) ? g.Sku.Trim() : g.Name.Trim(), StringComparer.OrdinalIgnoreCase);

                list = grouped.Select(grp =>
                {
                    var primary = grp.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Name)) ?? grp.OrderByDescending(x => x.CreatedAt).First();
                    if (string.IsNullOrWhiteSpace(primary.Name)) return null;
                    double totalStock = Math.Round(grp.Sum(x => x.Stock), 2);
                    return new StockDisplayItem
                    {
                        Id = primary.Id,
                        Kind = "gen",
                        Name = primary.Name,
                        Sku = !string.IsNullOrWhiteSpace(primary.Sku) ? primary.Sku : primary.Name,
                        Subtitle = $"Rate: ₹{primary.Rate:N0} / {primary.Unit} · GST {primary.Gst}%",
                        Unit = primary.Unit,
                        Stock = totalStock,
                        ReorderLevel = primary.ReorderLevel,
                        RateOrCost = primary.Rate,
                        RawItem = primary
                    };
                }).Where(it => it != null).Select(it => it!).ToList();
            }
            else if (ActiveTab == "fg")
            {
                var grouped = _dataService.Products
                    .Where(p => p.IsManufactured && !string.IsNullOrWhiteSpace(p.Name) && p.Name.Trim() != "—" && p.Name.Trim() != "-")
                    .GroupBy(p => !string.IsNullOrWhiteSpace(p.Sku) ? p.Sku.Trim() : p.Name.Trim(), StringComparer.OrdinalIgnoreCase);

                list = grouped.Select(grp =>
                {
                    var primary = grp.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Name)) ?? grp.OrderByDescending(x => x.CreatedAt).First();
                    if (string.IsNullOrWhiteSpace(primary.Name)) return null;
                    double totalStock = Math.Round(grp.Sum(x => x.Stock), 2);
                    return new StockDisplayItem
                    {
                        Id = primary.Id,
                        Kind = "mfg",
                        Name = primary.Name,
                        Sku = primary.Sku,
                        Subtitle = $"{primary.Category} · Sizes: {primary.SizesDisplay}",
                        Unit = primary.Unit,
                        Stock = totalStock,
                        ReorderLevel = primary.ReorderLevel,
                        RateOrCost = primary.EffectiveCost,
                        RawItem = primary
                    };
                }).Where(it => it != null).Select(it => it!).ToList();
            }
            else if (ActiveTab == "ready")
            {
                var grouped = _dataService.Products
                    .Where(p => p.IsReadymade && !string.IsNullOrWhiteSpace(p.Name) && p.Name.Trim() != "—" && p.Name.Trim() != "-")
                    .GroupBy(p => !string.IsNullOrWhiteSpace(p.Sku) ? p.Sku.Trim() : p.Name.Trim(), StringComparer.OrdinalIgnoreCase);

                list = grouped.Select(grp =>
                {
                    var primary = grp.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Name)) ?? grp.OrderByDescending(x => x.CreatedAt).First();
                    if (string.IsNullOrWhiteSpace(primary.Name)) return null;
                    double totalStock = Math.Round(grp.Sum(x => x.Stock), 2);
                    return new StockDisplayItem
                    {
                        Id = primary.Id,
                        Kind = "ready",
                        Name = primary.Name,
                        Sku = primary.Sku,
                        Subtitle = $"{primary.Category} · Sizes: {primary.SizesDisplay}",
                        Unit = primary.Unit,
                        Stock = totalStock,
                        ReorderLevel = primary.ReorderLevel,
                        RateOrCost = primary.Cost,
                        RawItem = primary
                    };
                }).Where(it => it != null).Select(it => it!).ToList();
            }

            int ParseSkuNum(string? sku)
            {
                if (string.IsNullOrWhiteSpace(sku)) return int.MaxValue;
                var match = System.Text.RegularExpressions.Regex.Match(sku, @"\d+");
                return match.Success && int.TryParse(match.Value, out int n) ? n : int.MaxValue;
            }

            list = list.OrderBy(x => ParseSkuNum(x.Sku)).ThenBy(x => x.Name).ToList();

            foreach (var it in list)
            {
                if (string.IsNullOrEmpty(q) || (it.Name + " " + it.Sku + " " + it.Subtitle).ToLowerInvariant().Contains(q))
                {
                    Items.Add(it);
                }
            }
        }

        private void DownloadFile()
        {
            var format = AsiErp.Dialogs.DownloadFormatDialog.PromptFormat(System.Windows.Application.Current?.MainWindow, $"{Title} Report");
            if (format == AsiErp.Dialogs.DownloadFormat.Pdf)
            {
                ExportPdf();
            }
            else if (format == AsiErp.Dialogs.DownloadFormat.Csv)
            {
                ExportCsv();
            }
        }

        private void ExportPdf()
        {
            try
            {
                var printDialog = new System.Windows.Controls.PrintDialog();
                if (printDialog.ShowDialog() != true) return;

                var printArea = new System.Windows.Controls.Border
                {
                    Background = System.Windows.Media.Brushes.White,
                    Padding = new System.Windows.Thickness(36),
                    Width = printDialog.PrintableAreaWidth > 0 ? printDialog.PrintableAreaWidth : 790
                };

                var mainStack = new System.Windows.Controls.StackPanel();

                var compStack = new System.Windows.Controls.StackPanel { Margin = new System.Windows.Thickness(0, 0, 0, 14) };
                compStack.Children.Add(new System.Windows.Controls.TextBlock { Text = "G-TRACK ERP · INVENTORY & STOCK REPORT", FontSize = 16, FontWeight = System.Windows.FontWeights.Bold, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 23, 42)) });
                compStack.Children.Add(new System.Windows.Controls.TextBlock { Text = $"{Title} ({Subtitle}) | Total Items: {Items.Count} | Total Value: ₹{TotalValue:N0}", FontSize = 11, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 116, 139)), Margin = new System.Windows.Thickness(0, 2, 0, 0) });
                mainStack.Children.Add(compStack);

                mainStack.Children.Add(new System.Windows.Controls.Separator { Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(226, 232, 240)), Margin = new System.Windows.Thickness(0, 0, 0, 12) });

                var thGrid = new System.Windows.Controls.Grid { Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(241, 245, 249)), Margin = new System.Windows.Thickness(0, 0, 0, 6) };
                thGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
                thGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(100) });
                thGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(65) });
                thGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(80) });
                thGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(90) });
                thGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(95) });
                thGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(80) });

                void AddHeaderCell(string text, int col, System.Windows.TextAlignment align = System.Windows.TextAlignment.Left)
                {
                    var tb = new System.Windows.Controls.TextBlock { Text = text, FontWeight = System.Windows.FontWeights.Bold, FontSize = 10, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 41, 59)), Padding = new System.Windows.Thickness(6), TextAlignment = align };
                    System.Windows.Controls.Grid.SetColumn(tb, col);
                    thGrid.Children.Add(tb);
                }

                AddHeaderCell("ITEM DESCRIPTION", 0);
                AddHeaderCell("SKU", 1);
                AddHeaderCell("UNIT", 2);
                AddHeaderCell("STOCK", 3, System.Windows.TextAlignment.Right);
                AddHeaderCell("COST (₹)", 4, System.Windows.TextAlignment.Right);
                AddHeaderCell("VALUE (₹)", 5, System.Windows.TextAlignment.Right);
                AddHeaderCell("STATUS", 6, System.Windows.TextAlignment.Center);
                mainStack.Children.Add(thGrid);

                int rowIdx = 0;
                foreach (var it in Items)
                {
                    var trGrid = new System.Windows.Controls.Grid { Margin = new System.Windows.Thickness(0, 2, 0, 2), Background = rowIdx % 2 == 1 ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 250, 252)) : System.Windows.Media.Brushes.White };
                    trGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
                    trGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(100) });
                    trGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(65) });
                    trGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(80) });
                    trGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(90) });
                    trGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(95) });
                    trGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(80) });

                    void AddCell(string text, int col, bool bold = false, System.Windows.TextAlignment align = System.Windows.TextAlignment.Left)
                    {
                        var tb = new System.Windows.Controls.TextBlock { Text = text, FontSize = 9.5, FontWeight = bold ? System.Windows.FontWeights.Bold : System.Windows.FontWeights.Normal, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 23, 42)), Padding = new System.Windows.Thickness(6, 4, 6, 4), TextAlignment = align };
                        System.Windows.Controls.Grid.SetColumn(tb, col);
                        trGrid.Children.Add(tb);
                    }

                    AddCell(it.Name, 0, true);
                    AddCell(it.Sku, 1);
                    AddCell(it.Unit, 2);
                    AddCell($"{it.Stock:N0}", 3, true, System.Windows.TextAlignment.Right);
                    AddCell($"₹{it.RateOrCost:N2}", 4, false, System.Windows.TextAlignment.Right);
                    AddCell($"₹{it.TotalValue:N2}", 5, true, System.Windows.TextAlignment.Right);
                    AddCell(it.StatusLevelText, 6, false, System.Windows.TextAlignment.Center);

                    mainStack.Children.Add(trGrid);
                    rowIdx++;
                }

                printArea.Child = mainStack;
                printArea.Measure(new System.Windows.Size(printDialog.PrintableAreaWidth, double.PositiveInfinity));
                printArea.Arrange(new System.Windows.Rect(new System.Windows.Point(0, 0), printArea.DesiredSize));

                printDialog.PrintVisual(printArea, $"{Title} Report");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Could not print/export PDF: {ex.Message}", "Print Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void ExportCsv()
        {
            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export Inventory Stock to CSV",
                Filter = "CSV Files (*.csv)|*.csv",
                FileName = $"ASI_{ActiveTab.ToUpper()}_Stock_{DateTime.Now:yyyyMMdd}.csv"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("ItemName,Details,Unit,CurrentStock,ReorderAlertLevel,CostOrRate,TotalValueINR,Status");
                    foreach (var it in Items)
                    {
                        sb.AppendLine($"\"{EscapeCsv(it.Name)}\",\"{EscapeCsv(it.Subtitle)}\",\"{EscapeCsv(it.Unit)}\",{it.Stock},{it.ReorderLevel},{it.RateOrCost},{it.TotalValue},\"{EscapeCsv(it.StatusLevelText)}\"");
                    }

                    System.IO.File.WriteAllText(sfd.FileName, sb.ToString(), System.Text.Encoding.UTF8);

                    var res = System.Windows.MessageBox.Show($"Stock CSV exported successfully to:\n{sfd.FileName}\n\nWould you like to open the file now?",
                        "Export Complete", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Information);

                    if (res == System.Windows.MessageBoxResult.Yes)
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = sfd.FileName,
                            UseShellExecute = true
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Could not export CSV file: {ex.Message}", "Export Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        private static string EscapeCsv(string? val) => (val ?? string.Empty).Replace("\"", "\"\"");
    }
}
