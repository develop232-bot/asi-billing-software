using System;
using System.Collections.Generic;
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
        public string HsnDisplay => !string.IsNullOrWhiteSpace(Hsn) ? Hsn : "—";
        public string CostPriceDisplay => CostPrice > 0 ? $"₹{CostPrice:N0}" : "₹0";
        public string SellingPriceDisplay => SellingPrice > 0 ? $"₹{SellingPrice:N0}" : "₹0";
        public string GstDisplay => $"{Gst}%";
        public string QuantityDisplay => $"{Quantity:N0}";

        public bool CanPrintBarcode => TypeKey == "mfg" || TypeKey == "ready";

        public string TypeLabel => TypeKey switch
        {
            "raw" => "Raw Material",
            "mfg" => "Finished Product",
            "ready" => "Readymade Product",
            "gen" => "General Packaging",
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
        public RelayCommand PrintBarcodeCommand { get; }
        public RelayCommand PrintBarcodeOptionsCommand { get; }
        public RelayCommand BulkImportCommand { get; }
        public RelayCommand ExportCsvCommand { get; }
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

            // Products (Finished and Readymade)
            foreach (var p in _dataService.Products)
            {
                bool isMfg = p.IsManufactured;
                all.Add(new UnifiedProductRow
                {
                    Id = p.Id,
                    TypeKey = isMfg ? "mfg" : "ready",
                    Date = string.IsNullOrWhiteSpace(p.CreatedAt) ? DateTime.Now.ToString("yyyy-MM-dd") : p.CreatedAt,
                    Name = p.Name,
                    Unit = string.IsNullOrWhiteSpace(p.Unit) ? "pair" : p.Unit,
                    Sku = p.Sku,
                    Hsn = string.IsNullOrWhiteSpace(p.Hsn) ? "6403" : p.Hsn,
                    Barcode = p.DisplayBarcode,
                    CostPrice = p.Cost > 0 ? p.Cost : (p.RetailPrice * 0.7),
                    SellingPrice = p.RetailPrice,
                    Gst = p.AutoGstRate,
                    Quantity = p.Stock
                });
            }

            // Raw Materials
            foreach (var r in _dataService.RawMaterials)
            {
                all.Add(new UnifiedProductRow
                {
                    Id = r.Id,
                    TypeKey = "raw",
                    Date = string.IsNullOrWhiteSpace(r.CreatedAt) ? DateTime.Now.ToString("yyyy-MM-dd") : r.CreatedAt,
                    Name = r.Name,
                    Unit = string.IsNullOrWhiteSpace(r.Unit) ? "meter" : r.Unit,
                    Sku = string.IsNullOrWhiteSpace(r.Sku) ? r.Id : r.Sku,
                    Hsn = r.Hsn,
                    Barcode = "—",
                    CostPrice = r.Rate,
                    SellingPrice = r.Rate,
                    Gst = r.Gst,
                    Quantity = r.Stock
                });
            }

            // General Items
            foreach (var g in _dataService.GeneralItems)
            {
                all.Add(new UnifiedProductRow
                {
                    Id = g.Id,
                    TypeKey = "gen",
                    Date = string.IsNullOrWhiteSpace(g.CreatedAt) ? DateTime.Now.ToString("yyyy-MM-dd") : g.CreatedAt,
                    Name = g.Name,
                    Unit = string.IsNullOrWhiteSpace(g.Unit) ? "pcs" : g.Unit,
                    Sku = string.IsNullOrWhiteSpace(g.Sku) ? g.Id : g.Sku,
                    Hsn = g.Hsn,
                    Barcode = "—",
                    CostPrice = g.Rate,
                    SellingPrice = g.Rate,
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

            // Populate FilteredRows by ActiveFilter and SearchQuery
            foreach (var row in dateFiltered)
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
