using System;
using System.Collections.ObjectModel;
using System.Linq;
using AsiErp.Commands;
using AsiErp.Models;
using AsiErp.Services;

namespace AsiErp.ViewModels
{
    public class UnifiedProductRow
    {
        public string Id { get; set; } = string.Empty;
        public string TypeKey { get; set; } = "mfg"; // mfg, ready, raw, gen
        public string Name { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public string PricingDisplay { get; set; } = string.Empty;
        public string GstDisplay { get; set; } = string.Empty;
        public string StockDisplay { get; set; } = string.Empty;

        public bool CanPrintBarcode => TypeKey == "mfg" || TypeKey == "ready";

        public string TypeLabel => TypeKey switch
        {
            "mfg" => "Finished product",
            "ready" => "Readymade product",
            "raw" => "Raw material",
            "gen" => "General",
            _ => TypeKey
        };

        public string TypeColor => TypeKey switch
        {
            "mfg" => "#3D62B8",
            "ready" => "#1B8556",
            _ => "#78839A"
        };
    }

    public class ProductsViewModel : ViewModelBase
    {
        private readonly AppDataService _dataService;
        private readonly Action<string, object?> _openDialogAction;

        private string _activeFilter = "all"; // all, mfg, ready, raw, gen
        private string _searchQuery = string.Empty;

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

        private UnifiedProductRow? _selectedRow;
        public UnifiedProductRow? SelectedRow
        {
            get => _selectedRow;
            set => SetField(ref _selectedRow, value);
        }

        public int SaleProductsCount => _dataService.Products.Count;
        public int RawMaterialsCount => _dataService.RawMaterials.Count;
        public int GeneralItemsCount => _dataService.GeneralItems.Count;
        public int TotalSkusCount => SaleProductsCount + RawMaterialsCount + GeneralItemsCount;

        public RelayCommand SetFilterCommand { get; }
        public RelayCommand AddItemCommand { get; }
        public RelayCommand EditItemCommand { get; }
        public RelayCommand PrintBarcodeCommand { get; }
        public RelayCommand PrintBarcodeOptionsCommand { get; }
        public RelayCommand BulkImportCommand { get; }
        public RelayCommand ExportCsvCommand { get; }

        public ProductsViewModel(AppDataService dataService, Action<string, object?> openDialogAction)
        {
            _dataService = dataService;
            _openDialogAction = openDialogAction;

            SetFilterCommand = new RelayCommand(p => ActiveFilter = p?.ToString() ?? "all");
            AddItemCommand = new RelayCommand(_ =>
            {
                string dlg = ActiveFilter switch
                {
                    "ready" => "product_edit_ready",
                    "raw" => "product_edit_raw",
                    "gen" => "product_edit_gen",
                    _ => "product_edit_mfg"
                };
                _openDialogAction(dlg, null);
            });
            BulkImportCommand = new RelayCommand(_ => _openDialogAction("bulk_csv_import", null));
            ExportCsvCommand = new RelayCommand(_ => ExportCsv());

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

            PrintBarcodeCommand = new RelayCommand(p =>
            {
                if (p is UnifiedProductRow row)
                {
                    var prod = _dataService.Products.FirstOrDefault(x => x.Id == row.Id);
                    if (prod != null)
                    {
                        _openDialogAction("print_barcode", prod);
                    }
                }
            });

            PrintBarcodeOptionsCommand = new RelayCommand(_ =>
            {
                _openDialogAction("print_barcode_options", SelectedRow);
            });

            _dataService.DataChanged += RefreshData;
            RefreshData();
        }

        public void RefreshData()
        {
            ApplyFilter();
            OnPropertyChanged(nameof(SaleProductsCount));
            OnPropertyChanged(nameof(RawMaterialsCount));
            OnPropertyChanged(nameof(GeneralItemsCount));
            OnPropertyChanged(nameof(TotalSkusCount));
        }

        private void ApplyFilter()
        {
            FilteredRows.Clear();
            string q = SearchQuery.Trim().ToLowerInvariant();

            var all = new System.Collections.Generic.List<UnifiedProductRow>();

            // Products (Finished and Readymade)
            foreach (var p in _dataService.Products)
            {
                all.Add(new UnifiedProductRow
                {
                    Id = p.Id,
                    TypeKey = p.IsManufactured ? "mfg" : "ready",
                    Name = p.Name,
                    Barcode = p.DisplayBarcode,
                    Subtitle = $"{p.Sku} · {p.Category} · Barcode: {p.DisplayBarcode} · Sizes: {p.SizesDisplay}",
                    PricingDisplay = $"Retail: ₹{p.RetailPrice:N0} · Dom: ₹{p.DomesticPrice:N0} · Exp: ${p.ExportPrice:F2}",
                    GstDisplay = $"Auto ({p.AutoGstRate}%)",
                    StockDisplay = $"{p.Stock:N0} {p.Unit}"
                });
            }

            // Raw Materials
            foreach (var r in _dataService.RawMaterials)
            {
                all.Add(new UnifiedProductRow
                {
                    Id = r.Id,
                    TypeKey = "raw",
                    Name = r.Name,
                    Barcode = "—",
                    Subtitle = r.Unit,
                    PricingDisplay = $"₹{r.Rate:N0} / {r.Unit}",
                    GstDisplay = $"{r.Gst}%",
                    StockDisplay = $"{r.Stock:N0} {r.Unit}"
                });
            }

            // General Items
            foreach (var g in _dataService.GeneralItems)
            {
                all.Add(new UnifiedProductRow
                {
                    Id = g.Id,
                    TypeKey = "gen",
                    Name = g.Name,
                    Barcode = "—",
                    Subtitle = g.Unit,
                    PricingDisplay = $"₹{g.Rate:N0} / {g.Unit}",
                    GstDisplay = $"{g.Gst}%",
                    StockDisplay = $"{g.Stock:N0} {g.Unit}"
                });
            }

            foreach (var row in all)
            {
                bool matchType = ActiveFilter == "all" || row.TypeKey == ActiveFilter;
                bool matchQ = string.IsNullOrEmpty(q) || (row.Name + " " + row.Subtitle + " " + row.Barcode).ToLowerInvariant().Contains(q);

                if (matchType && matchQ)
                {
                    FilteredRows.Add(row);
                }
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

                    if (ActiveFilter == "raw")
                    {
                        sb.AppendLine("Name,Unit,Stock,ReorderLevel,Rate,Gst");
                        foreach (var r in _dataService.RawMaterials)
                        {
                            sb.AppendLine($"\"{EscapeCsv(r.Name)}\",\"{EscapeCsv(r.Unit)}\",{r.Stock},{r.ReorderLevel},{r.Rate},{r.Gst}");
                        }
                    }
                    else if (ActiveFilter == "gen")
                    {
                        sb.AppendLine("Name,Unit,Stock,ReorderLevel,Rate,Gst");
                        foreach (var g in _dataService.GeneralItems)
                        {
                            sb.AppendLine($"\"{EscapeCsv(g.Name)}\",\"{EscapeCsv(g.Unit)}\",{g.Stock},{g.ReorderLevel},{g.Rate},{g.Gst}");
                        }
                    }
                    else if (ActiveFilter == "mfg" || ActiveFilter == "ready")
                    {
                        bool isReady = ActiveFilter == "ready";
                        var list = _dataService.Products.Where(p => isReady ? p.IsReadymade : p.IsManufactured).ToList();
                        sb.AppendLine("Name,SKU,Category,Subcategory,Unit,Sizes,Cost,RetailPrice,WholesalePrice,Stock,ReorderLevel,Barcode");
                        foreach (var p in list)
                        {
                            string sizes = string.Join(",", p.Sizes ?? new System.Collections.Generic.List<string>());
                            sb.AppendLine($"\"{EscapeCsv(p.Name)}\",\"{EscapeCsv(p.Sku)}\",\"{EscapeCsv(p.Category)}\",\"Formal\",\"{EscapeCsv(p.Unit)}\",\"{EscapeCsv(sizes)}\",{p.Cost},{p.RetailPrice},{p.BulkPrice},{p.Stock},{p.ReorderLevel},\"{EscapeCsv(p.Barcode)}\"");
                        }
                    }
                    else
                    {
                        // All items unified export matching standard bulk format
                        sb.AppendLine("ItemType,Name,SKU,Barcode,Category,Unit,Sizes,CostPrice,RetailMRP,WholesalePrice,DomesticPrice,ExportPriceUSD,Stock,ReorderLevel,GST");
                        foreach (var p in _dataService.Products)
                        {
                            string type = p.IsReadymade ? "Readymade" : "Manufactured";
                            string sizes = string.Join(",", p.Sizes ?? new System.Collections.Generic.List<string>());
                            sb.AppendLine($"\"{type}\",\"{EscapeCsv(p.Name)}\",\"{EscapeCsv(p.Sku)}\",\"{EscapeCsv(p.Barcode)}\",\"{EscapeCsv(p.Category)}\",\"{EscapeCsv(p.Unit)}\",\"{EscapeCsv(sizes)}\",{p.Cost},{p.RetailPrice},{p.BulkPrice},{p.DomesticPrice},{p.ExportPrice},{p.Stock},{p.ReorderLevel},{p.Gst}");
                        }
                        foreach (var r in _dataService.RawMaterials)
                        {
                            sb.AppendLine($"\"Raw Material\",\"{EscapeCsv(r.Name)}\",\"\",\"\",\"Material\",\"{EscapeCsv(r.Unit)}\",\"\",{r.Rate},0,0,0,0,{r.Stock},{r.ReorderLevel},{r.Gst}");
                        }
                        foreach (var g in _dataService.GeneralItems)
                        {
                            sb.AppendLine($"\"General Packaging\",\"{EscapeCsv(g.Name)}\",\"\",\"\",\"Packaging\",\"{EscapeCsv(g.Unit)}\",\"\",{g.Rate},0,0,0,0,{g.Stock},{g.ReorderLevel},{g.Gst}");
                        }
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

        private static string EscapeCsv(string? val) => (val ?? string.Empty).Replace("\"", "\"\"");
    }
}
