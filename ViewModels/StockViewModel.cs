using System;
using System.Collections.ObjectModel;
using System.Linq;
using AsiErp.Commands;
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
                    OnPropertyChanged(nameof(TotalValue));
                    OnPropertyChanged(nameof(LowStockCount));
                    OnPropertyChanged(nameof(TotalUnitsOnHand));
                }
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

        public string Title => ActiveTab switch
        {
            "raw" => "Raw Material Stock",
            "fg" => "Finished Products",
            "ready" => "Readymade Stock",
            "gen" => "Packaging & General",
            _ => "Stock"
        };

        public string Subtitle => string.Empty;

        public double TotalValue => Items.Sum(x => x.TotalValue);
        public int LowStockCount => Items.Count(x => x.StatusLevel != "ok");
        public double TotalUnitsOnHand => Items.Sum(x => x.Stock);

        public RelayCommand SetTabCommand { get; }
        public RelayCommand AdjustStockCommand { get; }
        public RelayCommand EditItemCommand { get; }
        public RelayCommand AddItemCommand { get; }
        public RelayCommand BuyItemCommand { get; }
        public RelayCommand ExportCsvCommand { get; }

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
                string kind = ActiveTab switch
                {
                    "raw" => "raw",
                    "ready" => "ready",
                    "gen" => "gen",
                    _ => "raw"
                };
                _openDialogAction("purchase_bill", kind);
            });

            ExportCsvCommand = new RelayCommand(_ => ExportCsv());

            _dataService.DataChanged += RefreshData;
            RefreshData();
        }

        public void RefreshData()
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
                list = _dataService.RawMaterials.Select(r => new StockDisplayItem
                {
                    Id = r.Id,
                    Kind = "raw",
                    Name = r.Name,
                    Sku = !string.IsNullOrWhiteSpace(r.Sku) ? r.Sku : $"ASI-RAW-{r.Id.Replace("r", "").PadLeft(3, '0')}",
                    Subtitle = $"Rate: ₹{r.Rate:N0} / {r.Unit} · GST {r.Gst}%",
                    Unit = r.Unit,
                    Stock = r.Stock,
                    ReorderLevel = r.ReorderLevel,
                    RateOrCost = r.Rate,
                    RawItem = r
                }).ToList();
            }
            else if (ActiveTab == "gen")
            {
                list = _dataService.GeneralItems.Select(g => new StockDisplayItem
                {
                    Id = g.Id,
                    Kind = "gen",
                    Name = g.Name,
                    Sku = !string.IsNullOrWhiteSpace(g.Sku) ? g.Sku : $"ASI-PKG-{g.Id.Replace("g", "").PadLeft(3, '0')}",
                    Subtitle = $"Rate: ₹{g.Rate:N0} / {g.Unit} · GST {g.Gst}%",
                    Unit = g.Unit,
                    Stock = g.Stock,
                    ReorderLevel = g.ReorderLevel,
                    RateOrCost = g.Rate,
                    RawItem = g
                }).ToList();
            }
            else if (ActiveTab == "fg")
            {
                list = _dataService.Products.Where(p => p.IsManufactured).Select(p => new StockDisplayItem
                {
                    Id = p.Id,
                    Kind = "prod",
                    Name = p.Name,
                    Sku = p.Sku,
                    Subtitle = $"{p.Category} · Sizes: {p.SizesDisplay}",
                    Unit = p.Unit,
                    Stock = p.Stock,
                    ReorderLevel = p.ReorderLevel,
                    RateOrCost = p.EffectiveCost,
                    RawItem = p
                }).ToList();
            }
            else if (ActiveTab == "ready")
            {
                list = _dataService.Products.Where(p => p.IsReadymade).Select(p => new StockDisplayItem
                {
                    Id = p.Id,
                    Kind = "prod",
                    Name = p.Name,
                    Sku = p.Sku,
                    Subtitle = $"{p.Category} · Sizes: {p.SizesDisplay}",
                    Unit = p.Unit,
                    Stock = p.Stock,
                    ReorderLevel = p.ReorderLevel,
                    RateOrCost = p.Cost,
                    RawItem = p
                }).ToList();
            }

            foreach (var it in list)
            {
                if (string.IsNullOrEmpty(q) || (it.Name + " " + it.Sku + " " + it.Subtitle).ToLowerInvariant().Contains(q))
                {
                    Items.Add(it);
                }
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
