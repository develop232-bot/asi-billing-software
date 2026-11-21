using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using AsiErp.Commands;
using AsiErp.Models;
using AsiErp.Services;

namespace AsiErp.ViewModels
{
    public class UnifiedProductRow
    {
        public string Id { get; set; } = string.Empty;
        public string TypeKey { get; set; } = "mfg"; // raw, mfg, ready, gen
        public string Date { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public string Hsn { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public double CostPrice { get; set; }
        public double SellingPrice { get; set; }
        public double Gst { get; set; }
        public double Quantity { get; set; }

        public string DateDisplay => string.IsNullOrWhiteSpace(Date) ? DateTime.Now.ToString("yyyy-MM-dd") : Date;
        public string HsnDisplay => (!string.IsNullOrWhiteSpace(Hsn) && Hsn != "—" && Hsn != "-") ? Hsn : "—";
        public string BarcodeDisplay => (!string.IsNullOrWhiteSpace(Barcode) && Barcode != "—" && Barcode != "-") ? Barcode : "—";
        public string CostPriceDisplay => CostPrice > 0 ? $"₹{CostPrice:N0}" : "₹0";
        public string SellingPriceDisplay
        {
            get
            {
                if (TypeKey == "raw" || TypeKey == "gen") return "—";
                return SellingPrice > 0 ? $"₹{SellingPrice:N0}" : "₹0";
            }
        }
        public string GstDisplay => $"{Gst}%";
        public string QuantityDisplay => $"{Quantity:N0}";

        public bool CanPrintBarcode => !string.IsNullOrWhiteSpace(Barcode) && Barcode != "—" && Barcode != "-";

        public string TypeLabel => TypeKey switch
        {
            "raw" => "Raw Material",
            "mfg" => "Item",
            "ready" => "Readymade Product",
            "gen" => "General Supplies",
            _ => TypeKey
        };

        public string TypeColor => TypeKey switch
        {
            "raw" => "#0D9488",
            "mfg" => "#2563EB",
            "ready" => "#059669",
            "gen" => "#D97706",
            _ => "#64748B"
        };
    }

    public class ProductsViewModel : ViewModelBase
    {
        private readonly AppDataService _dataService;
        private readonly Action<string, object?> _openDialogAction;

        private string _activeFilter = "all"; // all, raw, mfg, ready, gen
        private string _searchQuery = string.Empty;
        private DateTime? _fromDate;
        private DateTime? _toDate;

        public ObservableCollection<UnifiedProductRow> FilteredRows { get; } = new();

        public string ActiveFilter
        {
            get => _activeFilter;
            set
            {
                if (SetField(ref _activeFilter, value)) ApplyFilter();
            }
        }

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetField(ref _searchQuery, value)) ApplyFilter();
            }
        }

        public DateTime? FromDate
        {
            get => _fromDate;
            set
            {
                if (SetField(ref _fromDate, value)) ApplyFilter();
            }
        }

        public DateTime? ToDate
        {
            get => _toDate;
            set
            {
                if (SetField(ref _toDate, value)) ApplyFilter();
            }
        }

        private UnifiedProductRow? _selectedRow;
        public UnifiedProductRow? SelectedRow
        {
            get => _selectedRow;
            set => SetField(ref _selectedRow, value);
        }

        private int _totalSkusCount;
        public int TotalSkusCount
        {
            get => _totalSkusCount;
            private set => SetField(ref _totalSkusCount, value);
        }

        private int _rawMaterialsCount;
        public int RawMaterialsCount
        {
            get => _rawMaterialsCount;
            private set => SetField(ref _rawMaterialsCount, value);
        }

        private int _finishedProductsCount;
        public int FinishedProductsCount
        {
            get => _finishedProductsCount;
            private set => SetField(ref _finishedProductsCount, value);
        }

        private int _readymadeProductsCount;
        public int ReadymadeProductsCount
        {
            get => _readymadeProductsCount;
            private set => SetField(ref _readymadeProductsCount, value);
        }

        private int _generalItemsCount;
        public int GeneralItemsCount
        {
            get => _generalItemsCount;
            private set => SetField(ref _generalItemsCount, value);
        }

        public RelayCommand SetFilterCommand { get; }
        public RelayCommand AddItemCommand { get; }
        public RelayCommand EditItemCommand { get; }
        public RelayCommand DeleteItemCommand { get; }
        public RelayCommand PrintBarcodeCommand { get; }
        public RelayCommand PrintBarcodeOptionsCommand { get; }
        public RelayCommand BulkImportCommand { get; }
        public RelayCommand ExportCsvCommand { get; }
        public RelayCommand DownloadFileCommand { get; }
        public RelayCommand ClearDateFilterCommand { get; }

        public ProductsViewModel(AppDataService dataService, Action<string, object?> openDialogAction)
        {
            _dataService = dataService;
            _openDialogAction = openDialogAction;

            SetFilterCommand = new RelayCommand(p => ActiveFilter = p?.ToString() ?? "all");
            ClearDateFilterCommand = new RelayCommand(_ =>
            {
                _fromDate = null;
                _toDate = null;
                OnPropertyChanged(nameof(FromDate));
                OnPropertyChanged(nameof(ToDate));
                ApplyFilter();
            });

            AddItemCommand = new RelayCommand(_ =>
            {
                string dlg = ActiveFilter switch
                {
                    "raw" => "product_edit_raw",
                    "ready" => "product_edit_ready",
                    "gen" => "product_edit_gen",
                    _ => "product_edit_mfg"
                };
                _openDialogAction(dlg, null);
            });

            BulkImportCommand = new RelayCommand(_ => _openDialogAction("bulk_csv_import", null));
            ExportCsvCommand = new RelayCommand(_ => DownloadFile());
            DownloadFileCommand = new RelayCommand(_ => DownloadFile());

            EditItemCommand = new RelayCommand(p =>
            {
                if (p is UnifiedProductRow row)
                {
                    string dlg = "product_edit_" + row.TypeKey;
                    object? target = row.TypeKey switch
                    {
                        "raw" => _dataService.RawMaterials.FirstOrDefault(x => x.Id == row.Id),
                        "gen" => _dataService.GeneralItems.FirstOrDefault(x => x.Id == row.Id),
                        _ => _dataService.Products.FirstOrDefault(x => x.Id == row.Id)
                    };
                    _openDialogAction(dlg, target);
                }
            });

            DeleteItemCommand = new RelayCommand(async p =>
            {
                if (p is UnifiedProductRow row)
                {
                    var res = MessageBox.Show(
                        $"Are you sure you want to permanently delete '{row.Name}' ({row.Sku})?",
                        "Delete Item",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                    if (res == MessageBoxResult.Yes)
                    {
                        if (row.TypeKey == "raw") await _dataService.DeleteRawMaterialAsync(row.Id);
                        else if (row.TypeKey == "gen") await _dataService.DeleteGeneralItemAsync(row.Id);
                        else await _dataService.DeleteProductAsync(row.Id);
                        RefreshData();
                    }
                }
            });

            PrintBarcodeCommand = new RelayCommand(p =>
            {
                if (p is UnifiedProductRow row)
                {
                    var prod = _dataService.Products.FirstOrDefault(x => x.Id == row.Id);
                    if (prod == null)
                    {
                        prod = new ProductItem
                        {
                            Id = row.Id,
                            Name = row.Name,
                            Sku = row.Sku,
                            Barcode = row.Barcode,
                            Unit = row.Unit,
                            RetailPrice = row.SellingPrice,
                            Cost = row.CostPrice,
                            Stock = row.Quantity,
                            SourceType = row.TypeKey == "raw" ? "RAW" : (row.TypeKey == "gen" ? "PKG" : "R"),
                            Category = row.TypeLabel
                        };
                    }
                    _openDialogAction("print_barcode", prod);
                }
            });

            PrintBarcodeOptionsCommand = new RelayCommand(_ =>
            {
                _openDialogAction("print_barcode_options", SelectedRow);
            });

            _dataService.DataChanged += RefreshData;
            RefreshData();
        }

        public async void RefreshData()
        {
            _dataService.EnsureUniqueBarcodesAcrossCatalog();
            await _dataService.CleanAndFixCorruptedSkusAsync();
            ApplyFilter();
        }

        private bool MatchDate(string? dateStr)
        {
            if (!FromDate.HasValue && !ToDate.HasValue) return true;
            if (string.IsNullOrWhiteSpace(dateStr)) return true;
            if (DateTime.TryParse(dateStr, out var d))
            {
                if (FromDate.HasValue && d.Date < FromDate.Value.Date) return false;
                if (ToDate.HasValue && d.Date > ToDate.Value.Date) return false;
                return true;
            }
            return true;
        }

        private void ApplyFilter()
        {
            FilteredRows.Clear();
            string q = SearchQuery.Trim().ToLowerInvariant();

            var all = new List<UnifiedProductRow>();

            // Strictly filter out any purchase records: purchases belong ONLY to Main and Purchase Dashboards
            var purchaseItemIds = new HashSet<string>(
                _dataService.Purchases.SelectMany(p => p.Lines ?? new())
                                      .Select(l => l.ItemId)
                                      .Where(id => !string.IsNullOrWhiteSpace(id)),
                StringComparer.OrdinalIgnoreCase);

            bool IsPurchaseItem(string? id, string? sku)
            {
                if (string.IsNullOrWhiteSpace(id)) return false;
                if (id.StartsWith("pur_", StringComparison.OrdinalIgnoreCase) || id.StartsWith("pur_it_", StringComparison.OrdinalIgnoreCase)) return true;
                if (purchaseItemIds.Contains(id)) return true;
                if (!string.IsNullOrWhiteSpace(sku) && (sku.StartsWith("pur_", StringComparison.OrdinalIgnoreCase) || sku.Contains("pur_it_"))) return true;
                return false;
            }

            // Products (Finished and Readymade)
            foreach (var p in _dataService.Products)
            {
                if (IsPurchaseItem(p.Id, p.Sku)) continue;
                bool isMfg = p.IsManufactured;
                all.Add(new UnifiedProductRow
                {
                    Id = p.Id,
                    TypeKey = isMfg ? "mfg" : "ready",
                    Date = string.IsNullOrWhiteSpace(p.CreatedAt) ? DateTime.Now.ToString("yyyy-MM-dd") : p.CreatedAt,
                    Name = p.Name,
                    Unit = string.IsNullOrWhiteSpace(p.Unit) ? "pair" : p.Unit,
                    Sku = !string.IsNullOrWhiteSpace(p.Sku) ? p.Sku : _dataService.GetNextSku(p.IsManufactured ? "mfg" : "ready"),
                    Hsn = p.Hsn ?? "",
                    Barcode = p.Barcode ?? "",
                    CostPrice = p.Cost > 0 ? p.Cost : (p.RetailPrice * 0.7),
                    SellingPrice = p.RetailPrice,
                    Gst = p.AutoGstRate,
                    Quantity = p.Stock
                });
            }

            // Raw Materials (pure product catalog only, excluding purchase entries)
            foreach (var r in _dataService.RawMaterials)
            {
                if (IsPurchaseItem(r.Id, r.Sku)) continue;
                all.Add(new UnifiedProductRow
                {
                    Id = r.Id,
                    TypeKey = "raw",
                    Date = string.IsNullOrWhiteSpace(r.CreatedAt) ? DateTime.Now.ToString("yyyy-MM-dd") : r.CreatedAt,
                    Name = r.Name,
                    Unit = string.IsNullOrWhiteSpace(r.Unit) ? "meter" : r.Unit,
                    Sku = !string.IsNullOrWhiteSpace(r.Sku) && !r.Sku.Contains("pur_it_") ? r.Sku : _dataService.GetNextSku("raw"),
                    Hsn = r.Hsn ?? "",
                    Barcode = r.Barcode ?? "",
                    CostPrice = r.Rate,
                    SellingPrice = 0,
                    Gst = r.Gst,
                    Quantity = r.Stock
                });
            }

            // General Items (pure product catalog only, excluding purchase entries)
            foreach (var g in _dataService.GeneralItems)
            {
                if (IsPurchaseItem(g.Id, g.Sku)) continue;
                all.Add(new UnifiedProductRow
                {
                    Id = g.Id,
                    TypeKey = "gen",
                    Date = string.IsNullOrWhiteSpace(g.CreatedAt) ? DateTime.Now.ToString("yyyy-MM-dd") : g.CreatedAt,
                    Name = g.Name,
                    Unit = string.IsNullOrWhiteSpace(g.Unit) ? "pcs" : g.Unit,
                    Sku = !string.IsNullOrWhiteSpace(g.Sku) && !g.Sku.Contains("pur_it_") ? g.Sku : _dataService.GetNextSku("gen"),
                    Hsn = g.Hsn ?? "",
                    Barcode = g.Barcode ?? "",
                    CostPrice = g.Rate,
                    SellingPrice = 0,
                    Gst = g.Gst,
                    Quantity = g.Stock
                });
            }

            // Filter all candidates by the date range
            var dateFiltered = all.Where(r => MatchDate(r.Date)).ToList();

            // Counts matching date filter
            int rawCount = dateFiltered.Count(x => x.TypeKey == "raw");
            int mfgCount = dateFiltered.Count(x => x.TypeKey == "mfg");
            int readyCount = dateFiltered.Count(x => x.TypeKey == "ready");
            int genCount = dateFiltered.Count(x => x.TypeKey == "gen");

            if (ActiveFilter == "all")
            {
                RawMaterialsCount = rawCount;
                FinishedProductsCount = mfgCount;
                ReadymadeProductsCount = readyCount;
                GeneralItemsCount = genCount;
                TotalSkusCount = rawCount + mfgCount + readyCount + genCount;
            }
            else if (ActiveFilter == "raw")
            {
                RawMaterialsCount = rawCount;
                FinishedProductsCount = 0;
                ReadymadeProductsCount = 0;
                GeneralItemsCount = 0;
                TotalSkusCount = rawCount;
            }
            else if (ActiveFilter == "mfg")
            {
                RawMaterialsCount = 0;
                FinishedProductsCount = mfgCount;
                ReadymadeProductsCount = 0;
                GeneralItemsCount = 0;
                TotalSkusCount = mfgCount;
            }
            else if (ActiveFilter == "ready")
            {
                RawMaterialsCount = 0;
                FinishedProductsCount = 0;
                ReadymadeProductsCount = readyCount;
                GeneralItemsCount = 0;
                TotalSkusCount = readyCount;
            }
            else if (ActiveFilter == "gen")
            {
                RawMaterialsCount = 0;
                FinishedProductsCount = 0;
                ReadymadeProductsCount = 0;
                GeneralItemsCount = genCount;
                TotalSkusCount = genCount;
            }

            int ParseSkuNum(string? sku)
            {
                if (string.IsNullOrWhiteSpace(sku)) return int.MaxValue;
                var match = System.Text.RegularExpressions.Regex.Match(sku, @"\d+");
                return match.Success && int.TryParse(match.Value, out int n) ? n : int.MaxValue;
            }

            var sortedCandidates = dateFiltered
                .OrderBy(r => r.TypeKey switch { "raw" => 1, "mfg" => 2, "ready" => 3, "gen" => 4, _ => 5 })
                .ThenBy(r => ParseSkuNum(r.Sku))
                .ThenBy(r => r.Name)
                .ToList();

            // Populate FilteredRows by ActiveFilter and SearchQuery
            foreach (var row in sortedCandidates)
            {
                bool matchType = ActiveFilter == "all" || row.TypeKey == ActiveFilter;
                bool matchQ = string.IsNullOrEmpty(q) ||
                              (row.Name + " " + row.Sku + " " + row.Barcode + " " + row.Unit + " " + row.TypeLabel).ToLowerInvariant().Contains(q);

                if (matchType && matchQ)
                {
                    FilteredRows.Add(row);
                }
            }
        }

        private void DownloadFile()
        {
            var format = AsiErp.Dialogs.DownloadFormatDialog.PromptFormat(System.Windows.Application.Current?.MainWindow, "Products Report");
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
                compStack.Children.Add(new System.Windows.Controls.TextBlock { Text = "G-TRACK ERP · PRODUCTS & ITEMS REPORT", FontSize = 16, FontWeight = System.Windows.FontWeights.Bold, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 23, 42)) });
                compStack.Children.Add(new System.Windows.Controls.TextBlock { Text = $"Category: {ActiveFilter.ToUpper()} | Total Items: {FilteredRows.Count}", FontSize = 11, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 116, 139)), Margin = new System.Windows.Thickness(0, 2, 0, 0) });
                mainStack.Children.Add(compStack);

                mainStack.Children.Add(new System.Windows.Controls.Separator { Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(226, 232, 240)), Margin = new System.Windows.Thickness(0, 0, 0, 12) });

                var thGrid = new System.Windows.Controls.Grid { Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(241, 245, 249)), Margin = new System.Windows.Thickness(0, 0, 0, 6) };
                thGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
                thGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(90) });
                thGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(80) });
                thGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(60) });
                thGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(80) });
                thGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(80) });
                thGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(65) });

                void AddHeaderCell(string text, int col, System.Windows.TextAlignment align = System.Windows.TextAlignment.Left)
                {
                    var tb = new System.Windows.Controls.TextBlock { Text = text, FontWeight = System.Windows.FontWeights.Bold, FontSize = 10, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 41, 59)), Padding = new System.Windows.Thickness(6), TextAlignment = align };
                    System.Windows.Controls.Grid.SetColumn(tb, col);
                    thGrid.Children.Add(tb);
                }

                AddHeaderCell("ITEM DESCRIPTION", 0);
                AddHeaderCell("TYPE", 1);
                AddHeaderCell("SKU", 2);
                AddHeaderCell("UNIT", 3);
                AddHeaderCell("COST (₹)", 4, System.Windows.TextAlignment.Right);
                AddHeaderCell("SELL (₹)", 5, System.Windows.TextAlignment.Right);
                AddHeaderCell("STOCK", 6, System.Windows.TextAlignment.Right);
                mainStack.Children.Add(thGrid);

                int rowIdx = 0;
                foreach (var r in FilteredRows)
                {
                    var trGrid = new System.Windows.Controls.Grid { Margin = new System.Windows.Thickness(0, 2, 0, 2), Background = rowIdx % 2 == 1 ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 250, 252)) : System.Windows.Media.Brushes.White };
                    trGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
                    trGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(90) });
                    trGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(80) });
                    trGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(60) });
                    trGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(80) });
                    trGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(80) });
                    trGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(65) });

                    void AddCell(string text, int col, bool bold = false, System.Windows.TextAlignment align = System.Windows.TextAlignment.Left)
                    {
                        var tb = new System.Windows.Controls.TextBlock { Text = text, FontSize = 9.5, FontWeight = bold ? System.Windows.FontWeights.Bold : System.Windows.FontWeights.Normal, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 23, 42)), Padding = new System.Windows.Thickness(6, 4, 6, 4), TextAlignment = align };
                        System.Windows.Controls.Grid.SetColumn(tb, col);
                        trGrid.Children.Add(tb);
                    }

                    AddCell(r.Name, 0, true);
                    AddCell(r.TypeLabel, 1);
                    AddCell(r.Sku, 2);
                    AddCell(r.Unit, 3);
                    AddCell(r.CostPriceDisplay, 4, false, System.Windows.TextAlignment.Right);
                    AddCell(r.SellingPriceDisplay, 5, false, System.Windows.TextAlignment.Right);
                    AddCell(r.QuantityDisplay, 6, true, System.Windows.TextAlignment.Right);

                    mainStack.Children.Add(trGrid);
                    rowIdx++;
                }

                printArea.Child = mainStack;
                printArea.Measure(new System.Windows.Size(printDialog.PrintableAreaWidth, double.PositiveInfinity));
                printArea.Arrange(new System.Windows.Rect(new System.Windows.Point(0, 0), printArea.DesiredSize));

                printDialog.PrintVisual(printArea, "Products Report");
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
                Title = "Export Products Catalog to CSV",
                Filter = "CSV Files (*.csv)|*.csv",
                FileName = $"ASI_{ActiveFilter.ToUpper()}_Catalog_{DateTime.Now:yyyyMMdd}.csv"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    var sb = new System.Text.StringBuilder();

                    sb.AppendLine("Date,Item Description,Unit of Measurement,Type,SKU,Barcode,Cost Price,Selling Price,GST,Quantity");
                    foreach (var r in FilteredRows)
                    {
                        sb.AppendLine($"\"{EscapeCsv(r.DateDisplay)}\",\"{EscapeCsv(r.Name)}\",\"{EscapeCsv(r.Unit)}\",\"{EscapeCsv(r.TypeLabel)}\",\"{EscapeCsv(r.Sku)}\",\"{EscapeCsv(r.Barcode)}\",{r.CostPrice},{r.SellingPrice},{r.Gst},{r.Quantity}");
                    }

                    System.IO.File.WriteAllText(sfd.FileName, sb.ToString(), System.Text.Encoding.UTF8);

                    var res = System.Windows.MessageBox.Show($"CSV exported successfully to:\n{sfd.FileName}\n\nWould you like to open the CSV file now?",
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

        private static string EscapeCsv(string val)
        {
            if (string.IsNullOrEmpty(val)) return "";
            return val.Replace("\"", "\"\"");
        }
    }
}
