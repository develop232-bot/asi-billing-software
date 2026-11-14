using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using AsiErp.Commands;
using AsiErp.Dialogs;
using AsiErp.Models;
using AsiErp.Services;

namespace AsiErp.ViewModels
{
    public class ProductItemEditorViewModel : ViewModelBase
    {
        private readonly AppDataService _dataService;
        private readonly Action<string> _navigateAction;
        private readonly Action<string, object?> _openDialogAction;

        private object? _existingItem;
        private bool _isEditMode;
        private string _returnRoute = "products";
        private string _title = "Add New Item";
        private string _itemType = "mfg"; // mfg, ready, raw, gen
        private string _name = "";
        private string _category = "Shoe";
        private string _unit = "pair";
        private string _sizes = "6,7,8,9,10,11";
        private string _sku = "";
        private string _barcode = "";
        private double _retailPrice = 0;
        private double _bulkPrice = 0;
        private double _domesticPrice = 0;
        private double _exportPrice = 0;
        private double _purchaseRate = 0;
        private double _gstPercent = 5;
        private double _stock = 0;
        private double _reorderLevel = 50;
        private string _hsnCode = "6403";
        private bool _isSaving;

        public string Title
        {
            get => _title;
            set => SetField(ref _title, value);
        }

        public string ReturnRoute
        {
            get => _returnRoute;
            set
            {
                if (SetField(ref _returnRoute, value))
                {
                    OnPropertyChanged(nameof(BackNavLabel));
                }
            }
        }

        public string BackNavLabel => _returnRoute switch
        {
            "sales:retail" or "pos" => "⬅ Back to Retail Billing",
            "sales" => "⬅ Back to Sales",
            "purchase" => "⬅ Back to Purchase",
            "dashboard" => "⬅ Back to Dashboard",
            var r when r.StartsWith("stock:") => "⬅ Back to Stock & Inventory",
            _ => "⬅ Back to Products"
        };

        public bool IsEditMode
        {
            get => _isEditMode;
            set
            {
                if (SetField(ref _isEditMode, value))
                {
                    OnPropertyChanged(nameof(CanChangeType));
                }
            }
        }

        public bool CanChangeType => !_isEditMode;

        public string ItemType
        {
            get => _itemType;
            set
            {
                if (SetField(ref _itemType, value))
                {
                    OnPropertyChanged(nameof(IsProductType));
                    OnPropertyChanged(nameof(IsRawOrGenType));
                    OnPropertyChanged(nameof(ProductVisibility));
                    OnPropertyChanged(nameof(RawOrGenVisibility));

                    if (!_isEditMode)
                    {
                        AutoSku();
                        AutoBarcode();
                    }
                }
            }
        }

        public bool IsProductType => _itemType == "mfg" || _itemType == "ready";
        public bool IsRawOrGenType => !IsProductType;
        public Visibility ProductVisibility => IsProductType ? Visibility.Visible : Visibility.Collapsed;
        public Visibility RawOrGenVisibility => IsRawOrGenType ? Visibility.Visible : Visibility.Collapsed;

        public string Name
        {
            get => _name;
            set => SetField(ref _name, value);
        }

        public string Category
        {
            get => _category;
            set
            {
                if (SetField(ref _category, value))
                {
                    if (!_isEditMode)
                    {
                        Sizes = value switch
                        {
                            "Shoe" => "6,7,8,9,10,11",
                            "Uniform" => "S,M,L,XL,XXL",
                            "Bag" => "Free Size",
                            _ => Sizes
                        };
                    }
                }
            }
        }

        public string Unit
        {
            get => _unit;
            set => SetField(ref _unit, value);
        }

        public string Sizes
        {
            get => _sizes;
            set => SetField(ref _sizes, value);
        }

        public string Sku
        {
            get => _sku;
            set => SetField(ref _sku, value);
        }

        public string Barcode
        {
            get => _barcode;
            set => SetField(ref _barcode, value);
        }

        public double Quantity
        {
            get => _stock;
            set
            {
                if (SetField(ref _stock, value))
                {
                    OnPropertyChanged(nameof(Stock));
                }
            }
        }

        public double CostPrice
        {
            get => _purchaseRate;
            set
            {
                if (SetField(ref _purchaseRate, value))
                {
                    OnPropertyChanged(nameof(PurchaseRate));
                }
            }
        }

        public double SellingPrice
        {
            get => _retailPrice;
            set
            {
                if (SetField(ref _retailPrice, value))
                {
                    OnPropertyChanged(nameof(RetailPrice));
                }
            }
        }

        public double LowAlertLevel
        {
            get => _reorderLevel;
            set
            {
                if (SetField(ref _reorderLevel, value))
                {
                    OnPropertyChanged(nameof(ReorderLevel));
                }
            }
        }

        private double _mediumAlertLevel = 100;
        public double MediumAlertLevel
        {
            get => _mediumAlertLevel;
            set => SetField(ref _mediumAlertLevel, value);
        }

        public double RetailPrice
        {
            get => _retailPrice;
            set => SetField(ref _retailPrice, value);
        }

        public double BulkPrice
        {
            get => _bulkPrice;
            set => SetField(ref _bulkPrice, value);
        }

        public double DomesticPrice
        {
            get => _domesticPrice;
            set => SetField(ref _domesticPrice, value);
        }

        public double ExportPrice
        {
            get => _exportPrice;
            set => SetField(ref _exportPrice, value);
        }

        public double PurchaseRate
        {
            get => _purchaseRate;
            set => SetField(ref _purchaseRate, value);
        }

        public double GstPercent
        {
            get => _gstPercent;
            set => SetField(ref _gstPercent, value);
        }

        public double Stock
        {
            get => _stock;
            set => SetField(ref _stock, value);
        }

        public double ReorderLevel
        {
            get => _reorderLevel;
            set => SetField(ref _reorderLevel, value);
        }

        public string HsnCode
        {
            get => _hsnCode;
            set => SetField(ref _hsnCode, value);
        }

        public bool IsSaving
        {
            get => _isSaving;
            set => SetField(ref _isSaving, value);
        }

        public ObservableCollection<string> Categories { get; } = new()
        {
            "Shoe",
            "Bag",
            "Uniform",
            "Accessory"
        };

        public ObservableCollection<string> Units { get; } = new()
        {
            "pair",
            "pcs",
            "set",
            "box",
            "meter",
            "sq.ft",
            "kg",
            "sheet",
            "roll",
            "dozen"
        };

        // Commands
        public RelayCommand BackCommand { get; }
        public RelayCommand AutoSkuCommand { get; }
        public RelayCommand AutoBarcodeCommand { get; }
        public RelayCommand SizePresetCommand { get; }
        public RelayCommand BulkUploadCommand { get; }
        public RelayCommand PrintBarcodeCommand { get; }
        public RelayCommand SaveCommand { get; }
        public RelayCommand CancelCommand { get; }

        public ProductItemEditorViewModel(
            AppDataService dataService,
            Action<string> navigateAction,
            Action<string, object?> openDialogAction)
        {
            _dataService = dataService;
            _navigateAction = navigateAction;
            _openDialogAction = openDialogAction;

            BackCommand = new RelayCommand(_ => NavigateBack());
            CancelCommand = new RelayCommand(_ => NavigateBack());
            AutoSkuCommand = new RelayCommand(_ => AutoSku());
            AutoBarcodeCommand = new RelayCommand(_ => AutoBarcode());
            SizePresetCommand = new RelayCommand(p =>
            {
                if (p is string preset) Sizes = preset;
            });
            BulkUploadCommand = new RelayCommand(_ => BulkUpload());
            PrintBarcodeCommand = new RelayCommand(_ => PrintBarcodePreview());
            SaveCommand = new RelayCommand(async _ => await SaveItemAsync());
        }

        public void LoadItem(string type = "mfg", object? existing = null, string returnRoute = "products")
        {
            _existingItem = existing;
            _returnRoute = string.IsNullOrWhiteSpace(returnRoute) ? "products" : returnRoute;
            OnPropertyChanged(nameof(ReturnRoute));
            OnPropertyChanged(nameof(BackNavLabel));

            _isEditMode = existing != null;
            OnPropertyChanged(nameof(IsEditMode));
            OnPropertyChanged(nameof(CanChangeType));

            _itemType = type;
            OnPropertyChanged(nameof(ItemType));
            OnPropertyChanged(nameof(IsProductType));
            OnPropertyChanged(nameof(IsRawOrGenType));
            OnPropertyChanged(nameof(ProductVisibility));
            OnPropertyChanged(nameof(RawOrGenVisibility));

            if (_isEditMode)
            {
                if (_existingItem is ProductItem p)
                {
                    _itemType = p.SourceType == "R" ? "ready" : "mfg";
                    Title = $"Edit Item: {p.Name}";
                    Name = p.Name;
                    Category = string.IsNullOrWhiteSpace(p.Category) ? "Shoe" : p.Category;
                    Unit = string.IsNullOrWhiteSpace(p.Unit) ? "pair" : p.Unit;
                    Sizes = string.Join(",", p.Sizes ?? new List<string>());
                    Sku = p.Sku ?? "";
                    Barcode = string.IsNullOrWhiteSpace(p.Barcode) ? _dataService.GetNextUniqueBarcode(p.SourceType == "R" ? "ready" : "mfg", p.Category, p.Id) : p.Barcode;
                    RetailPrice = p.RetailPrice;
                    CostPrice = p.Cost > 0 ? p.Cost : (p.RetailPrice > 0 ? p.RetailPrice * 0.7 : 0);
                    BulkPrice = p.BulkPrice;
                    DomesticPrice = p.DomesticPrice;
                    ExportPrice = p.ExportPrice;
                    Stock = p.Stock;
                    Quantity = p.RegisteredQty > 0 ? p.RegisteredQty : p.Stock;
                    ReorderLevel = p.ReorderLevel;
                    MediumAlertLevel = p.ReorderLevel * 2;
                    HsnCode = string.IsNullOrWhiteSpace(p.Hsn) ? "6403" : p.Hsn;
                }
                else if (_existingItem is RawMaterialItem r)
                {
                    _itemType = "raw";
                    Title = $"Edit Raw Material: {r.Name}";
                    Name = r.Name;
                    Unit = string.IsNullOrWhiteSpace(r.Unit) ? "meter" : r.Unit;
                    PurchaseRate = r.Rate;
                    CostPrice = r.Rate;
                    SellingPrice = r.Rate;
                    GstPercent = r.Gst;
                    Stock = r.Stock;
                    Quantity = r.RegisteredQty > 0 ? r.RegisteredQty : r.Stock;
                    ReorderLevel = r.ReorderLevel;
                    LowAlertLevel = r.ReorderLevel;
                    MediumAlertLevel = r.ReorderLevel * 2;
                    Sku = string.IsNullOrWhiteSpace(r.Sku) ? r.Id : r.Sku;
                    HsnCode = r.Hsn;
                    Barcode = string.IsNullOrWhiteSpace(r.Barcode) ? _dataService.GetNextUniqueBarcode("raw", "Raw Material", r.Id) : r.Barcode;
                }
                else if (_existingItem is GeneralItem g)
                {
                    _itemType = "gen";
                    Title = $"Edit Packaging Item: {g.Name}";
                    Name = g.Name;
                    Unit = string.IsNullOrWhiteSpace(g.Unit) ? "pcs" : g.Unit;
                    PurchaseRate = g.Rate;
                    CostPrice = g.Rate;
                    SellingPrice = g.Rate;
                    GstPercent = g.Gst;
                    Stock = g.Stock;
                    Quantity = g.RegisteredQty > 0 ? g.RegisteredQty : g.Stock;
                    ReorderLevel = g.ReorderLevel;
                    LowAlertLevel = g.ReorderLevel;
                    MediumAlertLevel = g.ReorderLevel * 2;
                    Sku = string.IsNullOrWhiteSpace(g.Sku) ? g.Id : g.Sku;
                    HsnCode = g.Hsn;
                    Barcode = string.IsNullOrWhiteSpace(g.Barcode) ? _dataService.GetNextUniqueBarcode("gen", "Packaging", g.Id) : g.Barcode;
                }

                OnPropertyChanged(nameof(ItemType));
                OnPropertyChanged(nameof(IsProductType));
                OnPropertyChanged(nameof(IsRawOrGenType));
                OnPropertyChanged(nameof(ProductVisibility));
                OnPropertyChanged(nameof(RawOrGenVisibility));
            }
            else
            {
                Title = type switch
                {
                    "ready" => "Add Readymade Product",
                    "raw" => "Add Raw Material",
                    "gen" => "Add General Packaging Item",
                    _ => "Add Finished Product"
                };

                Name = "";
                Category = "Shoe";
                Unit = (type == "raw" || type == "gen") ? "pcs" : "pair";
                Sizes = "6,7,8,9,10,11";
                RetailPrice = 0;
                BulkPrice = 0;
                DomesticPrice = 0;
                ExportPrice = 0;
                PurchaseRate = 0;
                GstPercent = 5;
                Stock = 0;
                ReorderLevel = 50;
                HsnCode = "6403";

                AutoSku();
                AutoBarcode();
            }
        }

        private void AutoSku()
        {
            if (_itemType == "raw")
            {
                int count = _dataService.RawMaterials.Count + 1;
                Sku = "ASI-RAW-" + count.ToString("D3");
            }
            else if (_itemType == "gen")
            {
                int count = _dataService.GeneralItems.Count + 1;
                Sku = "ASI-GEN-" + count.ToString("D3");
            }
            else
            {
                int nextNum = _dataService.Products.Count + 1;
                Sku = "ASI-" + (_itemType == "mfg" ? "MFG" : "RDY") + "-" + nextNum.ToString("D3");
            }
        }

        private void AutoBarcode()
        {
            Barcode = _dataService.GetNextUniqueBarcode(_itemType, Category);
        }

        private void NavigateBack()
        {
            string target = string.IsNullOrWhiteSpace(ReturnRoute) ? "products" : ReturnRoute;
            _navigateAction(target);
        }

        private void BulkUpload()
        {
            var dlg = new BulkProductImportWindow { Owner = Application.Current?.MainWindow };
            if (dlg.ShowDialog() == true)
            {
                NavigateBack();
            }
        }

        private void PrintBarcodePreview()
        {
            int copies = (int)Math.Max(1, Quantity);
            var tempProd = new ProductItem
            {
                Id = (_existingItem as ProductItem)?.Id ?? "p_preview",
                Name = string.IsNullOrWhiteSpace(Name) ? "Product Item" : Name.Trim(),
                Category = Category,
                Sku = string.IsNullOrWhiteSpace(Sku) ? "ASI-MFG-001" : Sku.Trim(),
                Barcode = string.IsNullOrWhiteSpace(Barcode) ? BarcodeService.GenerateBarcode() : Barcode.Trim(),
                RetailPrice = SellingPrice,
                Cost = CostPrice,
                Stock = Quantity,
                Sizes = (Sizes ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList(),
                SourceType = ItemType == "mfg" ? "M" : "R"
            };

            var dlg = new BarcodePrintWindow(tempProd, copies) { Owner = Application.Current?.MainWindow };
            dlg.ShowDialog();
        }

        private async Task SaveItemAsync()
        {
            string trimmedName = Name.Trim();
            if (string.IsNullOrWhiteSpace(trimmedName))
            {
                MessageBox.Show("Please enter the item name.", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsSaving = true;

            try
            {
                string currentId = (_existingItem as ProductItem)?.Id
                    ?? (_existingItem as RawMaterialItem)?.Id
                    ?? (_existingItem as GeneralItem)?.Id ?? "";

                var usedBarcodes = _dataService.GetAllUsedBarcodes(currentId);
                string candidateBarcode = (Barcode ?? "").Trim();
                if (string.IsNullOrWhiteSpace(candidateBarcode) || candidateBarcode == "—" || usedBarcodes.Contains(candidateBarcode))
                {
                    candidateBarcode = _dataService.GetNextUniqueBarcode(ItemType, Category, currentId);
                }

                if (ItemType == "raw")
                {
                    var rm = _existingItem as RawMaterialItem ?? new RawMaterialItem { Id = "r_" + Guid.NewGuid().ToString("N")[..8] };
                    rm.Name = trimmedName;
                    rm.Unit = string.IsNullOrWhiteSpace(Unit) ? "meter" : Unit.Trim();
                    rm.Rate = CostPrice > 0 ? CostPrice : PurchaseRate;
                    rm.Gst = GstPercent;
                    rm.Stock = Quantity;
                    rm.RegisteredQty = Quantity;
                    rm.ReorderLevel = LowAlertLevel;
                    rm.Hsn = string.IsNullOrWhiteSpace(HsnCode) ? "6406" : HsnCode.Trim();
                    rm.Sku = string.IsNullOrWhiteSpace(Sku)
                        ? "ASI-RAW-" + (_dataService.RawMaterials.Count + 1).ToString("D3")
                        : Sku.Trim();
                    rm.Barcode = candidateBarcode;

                    await _dataService.SaveRawMaterialAsync(rm);
                }
                else if (ItemType == "gen")
                {
                    var gi = _existingItem as GeneralItem ?? new GeneralItem { Id = "g_" + Guid.NewGuid().ToString("N")[..8] };
                    gi.Name = trimmedName;
                    gi.Unit = string.IsNullOrWhiteSpace(Unit) ? "pcs" : Unit.Trim();
                    gi.Rate = CostPrice > 0 ? CostPrice : PurchaseRate;
                    gi.Gst = GstPercent;
                    gi.Stock = Quantity;
                    gi.RegisteredQty = Quantity;
                    gi.ReorderLevel = LowAlertLevel;
                    gi.Hsn = string.IsNullOrWhiteSpace(HsnCode) ? "4819" : HsnCode.Trim();
                    gi.Sku = string.IsNullOrWhiteSpace(Sku)
                        ? "ASI-PKG-" + (_dataService.GeneralItems.Count + 1).ToString("D3")
                        : Sku.Trim();
                    gi.Barcode = candidateBarcode;

                    await _dataService.SaveGeneralItemAsync(gi);
                }
                else
                {
                    var prod = _existingItem as ProductItem ?? new ProductItem
                    {
                        Id = "p_" + Guid.NewGuid().ToString("N")[..8],
                    };

                    prod.Sku = string.IsNullOrWhiteSpace(Sku)
                        ? "ASI-" + (ItemType == "mfg" ? "MFG" : "RDY") + "-" + (_dataService.Products.Count + 1).ToString("D3")
                        : Sku.Trim();

                    prod.Barcode = candidateBarcode;

                    prod.Name = trimmedName;
                    prod.Category = string.IsNullOrWhiteSpace(Category) ? "Shoe" : Category;
                    prod.SourceType = ItemType == "mfg" ? "M" : "R";
                    prod.Unit = string.IsNullOrWhiteSpace(Unit) ? "pair" : Unit.Trim();
                    prod.Sizes = (Sizes ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
                    prod.Cost = CostPrice;
                    prod.RetailPrice = SellingPrice;
                    prod.BulkPrice = BulkPrice;
                    prod.DomesticPrice = DomesticPrice;
                    prod.ExportPrice = ExportPrice;
                    prod.Stock = Quantity;
                    prod.RegisteredQty = Quantity;
                    prod.ReorderLevel = LowAlertLevel;
                    prod.Hsn = string.IsNullOrWhiteSpace(HsnCode) ? "6403" : HsnCode.Trim();
                    prod.Gst = SellingPrice <= 2500 ? 5 : 18;
                    prod.Bom ??= new();

                    await _dataService.SaveProductAsync(prod);
                }

                NavigateBack();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save item: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsSaving = false;
            }
        }
    }
}
