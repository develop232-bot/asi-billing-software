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
        private string _title = "Add Products";
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
                        Title = "Add Products";
                        AutoSku();
                        if (_itemType == "ready")
                        {
                            AutoBarcode();
                        }
                        else
                        {
                            Barcode = "";
                            HsnCode = "";
                        }
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

        private double _originalQuantity;
        private string _originalDate = "";

        public bool IsInwardNoticeVisible => _isEditMode && Quantity > _originalQuantity;

        public string InwardNoticeText => $"📅 Adding +{(Quantity - _originalQuantity):N0} stock creates a new entry for today ({DateTime.Now:yyyy-MM-dd}) in Products, while Inventory consolidates total stock ({Quantity:N0}) under SKU {Sku}.";

        public double Quantity
        {
            get => _stock;
            set
            {
                if (SetField(ref _stock, value))
                {
                    OnPropertyChanged(nameof(Stock));
                    OnPropertyChanged(nameof(IsInwardNoticeVisible));
                    OnPropertyChanged(nameof(InwardNoticeText));
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
                    _originalDate = p.CreatedAt;
                    _itemType = p.SourceType == "R" ? "ready" : "mfg";
                    Title = $"Edit Product: {p.Name}";
                    Name = p.Name;
                    Category = string.IsNullOrWhiteSpace(p.Category) ? "Shoe" : p.Category;
                    Unit = string.IsNullOrWhiteSpace(p.Unit) ? "pair" : p.Unit;
                    Sizes = string.Join(",", p.Sizes ?? new List<string>());
                    Sku = p.Sku ?? "";
                    Barcode = (string.IsNullOrWhiteSpace(p.Barcode) || p.Barcode == "—" || p.Barcode == "-") ? "" : p.Barcode;
                    RetailPrice = p.RetailPrice;
                    CostPrice = p.Cost > 0 ? p.Cost : (p.RetailPrice > 0 ? p.RetailPrice * 0.7 : 0);
                    BulkPrice = p.BulkPrice;
                    DomesticPrice = p.DomesticPrice;
                    ExportPrice = p.ExportPrice;

                    double totalStock = _dataService.Products
                        .Where(x => (!string.IsNullOrWhiteSpace(p.Sku) && x.Sku == p.Sku) ||
                                    (string.IsNullOrWhiteSpace(p.Sku) && x.Name.Equals(p.Name, StringComparison.OrdinalIgnoreCase)))
                        .Sum(x => x.Stock);
                    if (totalStock <= 0) totalStock = p.Stock;

                    Stock = totalStock;
                    Quantity = totalStock;
                    _originalQuantity = totalStock;
                    ReorderLevel = p.ReorderLevel;
                    MediumAlertLevel = p.ReorderLevel * 2;
                    HsnCode = (string.IsNullOrWhiteSpace(p.Hsn) || p.Hsn == "—" || p.Hsn == "-") ? "" : p.Hsn;
                }
                else if (_existingItem is RawMaterialItem r)
                {
                    _originalDate = r.CreatedAt;
                    _itemType = "raw";
                    Title = $"Edit Product: {r.Name}";
                    Name = r.Name;
                    Unit = string.IsNullOrWhiteSpace(r.Unit) ? "sq ft" : r.Unit;
                    PurchaseRate = r.Rate;
                    CostPrice = r.Rate;
                    SellingPrice = 0;
                    GstPercent = r.Gst;

                    double totalStock = _dataService.RawMaterials
                        .Where(x => (!string.IsNullOrWhiteSpace(r.Sku) && x.Sku == r.Sku) ||
                                    (string.IsNullOrWhiteSpace(r.Sku) && x.Name.Equals(r.Name, StringComparison.OrdinalIgnoreCase)))
                        .Sum(x => x.Stock);
                    if (totalStock <= 0) totalStock = r.Stock;

                    Stock = totalStock;
                    Quantity = totalStock;
                    _originalQuantity = totalStock;
                    ReorderLevel = r.ReorderLevel;
                    LowAlertLevel = r.ReorderLevel;
                    MediumAlertLevel = r.ReorderLevel * 2;
                    Sku = string.IsNullOrWhiteSpace(r.Sku) ? r.Id : r.Sku;
                    HsnCode = (string.IsNullOrWhiteSpace(r.Hsn) || r.Hsn == "—" || r.Hsn == "-") ? "" : r.Hsn;
                    Barcode = (string.IsNullOrWhiteSpace(r.Barcode) || r.Barcode == "—" || r.Barcode == "-") ? "" : r.Barcode;
                }
                else if (_existingItem is GeneralItem g)
                {
                    _originalDate = g.CreatedAt;
                    _itemType = "gen";
                    Title = $"Edit Product: {g.Name}";
                    Name = g.Name;
                    Unit = string.IsNullOrWhiteSpace(g.Unit) ? "pcs" : g.Unit;
                    PurchaseRate = g.Rate;
                    CostPrice = g.Rate;
                    SellingPrice = 0;
                    GstPercent = g.Gst;

                    double totalStock = _dataService.GeneralItems
                        .Where(x => (!string.IsNullOrWhiteSpace(g.Sku) && x.Sku == g.Sku) ||
                                    (string.IsNullOrWhiteSpace(g.Sku) && x.Name.Equals(g.Name, StringComparison.OrdinalIgnoreCase)))
                        .Sum(x => x.Stock);
                    if (totalStock <= 0) totalStock = g.Stock;

                    Stock = totalStock;
                    Quantity = totalStock;
                    _originalQuantity = totalStock;
                    ReorderLevel = g.ReorderLevel;
                    LowAlertLevel = g.ReorderLevel;
                    MediumAlertLevel = g.ReorderLevel * 2;
                    Sku = string.IsNullOrWhiteSpace(g.Sku) ? g.Id : g.Sku;
                    HsnCode = (string.IsNullOrWhiteSpace(g.Hsn) || g.Hsn == "—" || g.Hsn == "-") ? "" : g.Hsn;
                    Barcode = (string.IsNullOrWhiteSpace(g.Barcode) || g.Barcode == "—" || g.Barcode == "-") ? "" : g.Barcode;
                }

                if (string.IsNullOrWhiteSpace(_originalDate)) _originalDate = DateTime.Now.ToString("yyyy-MM-dd");
                OnPropertyChanged(nameof(IsInwardNoticeVisible));
                OnPropertyChanged(nameof(InwardNoticeText));

                OnPropertyChanged(nameof(ItemType));
                OnPropertyChanged(nameof(IsProductType));
                OnPropertyChanged(nameof(IsRawOrGenType));
                OnPropertyChanged(nameof(ProductVisibility));
                OnPropertyChanged(nameof(RawOrGenVisibility));
            }
            else
            {
                Title = "Add Products";

                Name = "";
                Category = "Shoe";
                Unit = type == "raw" ? "sq ft" : (type == "gen" ? "pcs" : "pair");
                Sizes = "6,7,8,9,10,11";
                RetailPrice = 0;
                BulkPrice = 0;
                DomesticPrice = 0;
                ExportPrice = 0;
                PurchaseRate = 0;
                CostPrice = 0;
                SellingPrice = 0;
                GstPercent = 5;
                Stock = 0;
                Quantity = 0;
                ReorderLevel = 50;
                LowAlertLevel = 50;
                MediumAlertLevel = 100;
                HsnCode = "";
                Barcode = "";

                AutoSku();
            }
        }

        private void AutoSku()
        {
            Sku = _dataService.GetNextSku(_itemType);
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
                    ?? (_existingItem as GeneralItem)?.Id
                    ?? (_existingItem as UnifiedProductRow)?.Id ?? "";

                string cleanHsn = (HsnCode ?? "").Trim();
                if (cleanHsn == "—" || cleanHsn == "-") cleanHsn = "";

                string cleanBarcode = (Barcode ?? "").Trim();
                if (cleanBarcode == "—" || cleanBarcode == "-") cleanBarcode = "";

                if (!string.IsNullOrWhiteSpace(cleanBarcode))
                {
                    var usedBarcodes = _dataService.GetAllUsedBarcodes(currentId);
                    if (usedBarcodes.Contains(cleanBarcode))
                    {
                        MessageBox.Show($"The barcode '{cleanBarcode}' is already assigned to another item. Please enter a unique barcode or leave it blank.", "Duplicate Barcode", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                string today = DateTime.Now.ToString("yyyy-MM-dd");
                bool isStockAddition = _isEditMode && Quantity > _originalQuantity;
                double addedQty = isStockAddition ? Math.Round(Quantity - _originalQuantity, 2) : 0;

                if (ItemType == "raw")
                {
                    var rm = (!string.IsNullOrEmpty(currentId) ? _dataService.RawMaterials.FirstOrDefault(x => x.Id == currentId) : null)
                        ?? (_existingItem as RawMaterialItem)
                        ?? new RawMaterialItem { Id = !string.IsNullOrEmpty(currentId) ? currentId : "r_" + Guid.NewGuid().ToString("N")[..8] };

                    string cleanSku = !string.IsNullOrWhiteSpace(Sku) ? Sku.Trim() : (!string.IsNullOrWhiteSpace(rm.Sku) ? rm.Sku.Trim() : _dataService.GetNextSku("raw"));
                    rm.Sku = cleanSku;
                    rm.Name = trimmedName;
                    rm.Unit = string.IsNullOrWhiteSpace(Unit) ? "sq ft" : Unit.Trim();
                    rm.Rate = CostPrice > 0 ? CostPrice : PurchaseRate;
                    rm.Gst = GstPercent;
                    rm.ReorderLevel = LowAlertLevel > 0 ? LowAlertLevel : ReorderLevel;
                    rm.Hsn = cleanHsn;
                    rm.Barcode = cleanBarcode;

                    if (isStockAddition)
                    {
                        await _dataService.SaveRawMaterialAsync(rm);

                        var otherEntries = _dataService.RawMaterials
                            .Where(x => x.Id != rm.Id && !string.IsNullOrWhiteSpace(x.Sku) && x.Sku.Equals(cleanSku, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                        foreach (var other in otherEntries)
                        {
                            other.Name = trimmedName;
                            other.Unit = rm.Unit;
                            other.Rate = rm.Rate;
                            other.Gst = rm.Gst;
                            other.ReorderLevel = rm.ReorderLevel;
                            other.Hsn = cleanHsn;
                            other.Barcode = cleanBarcode;
                            await _dataService.SaveRawMaterialAsync(other);
                        }

                        var newRm = new RawMaterialItem
                        {
                            Id = "r_" + Guid.NewGuid().ToString("N")[..8],
                            CreatedAt = today,
                            Name = trimmedName,
                            Unit = rm.Unit,
                            Rate = rm.Rate,
                            Gst = rm.Gst,
                            Stock = addedQty,
                            RegisteredQty = addedQty,
                            ReorderLevel = rm.ReorderLevel,
                            Hsn = cleanHsn,
                            Barcode = cleanBarcode,
                            Sku = cleanSku
                        };
                        await _dataService.SaveRawMaterialAsync(newRm);
                    }
                    else
                    {
                        rm.Stock = Quantity;
                        rm.RegisteredQty = Quantity;
                        if (!_isEditMode) rm.CreatedAt = today;
                        await _dataService.SaveRawMaterialAsync(rm);
                    }
                }
                else if (ItemType == "gen")
                {
                    var gi = (!string.IsNullOrEmpty(currentId) ? _dataService.GeneralItems.FirstOrDefault(x => x.Id == currentId) : null)
                        ?? (_existingItem as GeneralItem)
                        ?? new GeneralItem { Id = !string.IsNullOrEmpty(currentId) ? currentId : "gen_" + Guid.NewGuid().ToString("N")[..8] };

                    string cleanSku = !string.IsNullOrWhiteSpace(Sku) ? Sku.Trim() : (!string.IsNullOrWhiteSpace(gi.Sku) ? gi.Sku.Trim() : _dataService.GetNextSku("gen"));
                    gi.Sku = cleanSku;
                    gi.Name = trimmedName;
                    gi.Unit = string.IsNullOrWhiteSpace(Unit) ? "pcs" : Unit.Trim();
                    gi.Rate = CostPrice > 0 ? CostPrice : PurchaseRate;
                    gi.Gst = GstPercent;
                    gi.ReorderLevel = LowAlertLevel > 0 ? LowAlertLevel : ReorderLevel;
                    gi.Hsn = cleanHsn;
                    gi.Barcode = cleanBarcode;

                    if (isStockAddition)
                    {
                        await _dataService.SaveGeneralItemAsync(gi);

                        var otherEntries = _dataService.GeneralItems
                            .Where(x => x.Id != gi.Id && !string.IsNullOrWhiteSpace(x.Sku) && x.Sku.Equals(cleanSku, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                        foreach (var other in otherEntries)
                        {
                            other.Name = trimmedName;
                            other.Unit = gi.Unit;
                            other.Rate = gi.Rate;
                            other.Gst = gi.Gst;
                            other.ReorderLevel = gi.ReorderLevel;
                            other.Hsn = cleanHsn;
                            other.Barcode = cleanBarcode;
                            await _dataService.SaveGeneralItemAsync(other);
                        }

                        var newGen = new GeneralItem
                        {
                            Id = "gen_" + Guid.NewGuid().ToString("N")[..8],
                            CreatedAt = today,
                            Name = trimmedName,
                            Unit = gi.Unit,
                            Rate = gi.Rate,
                            Gst = gi.Gst,
                            Stock = addedQty,
                            RegisteredQty = addedQty,
                            ReorderLevel = gi.ReorderLevel,
                            Hsn = cleanHsn,
                            Barcode = cleanBarcode,
                            Sku = cleanSku
                        };
                        await _dataService.SaveGeneralItemAsync(newGen);
                    }
                    else
                    {
                        gi.Stock = Quantity;
                        gi.RegisteredQty = Quantity;
                        if (!_isEditMode) gi.CreatedAt = today;
                        await _dataService.SaveGeneralItemAsync(gi);
                    }
                }
                else
                {
                    var prod = (!string.IsNullOrEmpty(currentId) ? _dataService.Products.FirstOrDefault(x => x.Id == currentId) : null)
                        ?? (_existingItem as ProductItem)
                        ?? new ProductItem { Id = !string.IsNullOrEmpty(currentId) ? currentId : "p_" + Guid.NewGuid().ToString("N")[..8] };

                    string cleanSku = !string.IsNullOrWhiteSpace(Sku) ? Sku.Trim() : (!string.IsNullOrWhiteSpace(prod.Sku) ? prod.Sku.Trim() : _dataService.GetNextSku(ItemType == "mfg" ? "mfg" : "ready"));
                    prod.Sku = cleanSku;
                    prod.Barcode = cleanBarcode;
                    prod.Hsn = cleanHsn;
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
                    prod.ReorderLevel = LowAlertLevel > 0 ? LowAlertLevel : ReorderLevel;
                    prod.Gst = SellingPrice <= 2500 ? 5 : 18;
                    prod.Bom ??= new();

                    if (isStockAddition)
                    {
                        await _dataService.SaveProductAsync(prod);

                        var otherEntries = _dataService.Products
                            .Where(x => x.Id != prod.Id && !string.IsNullOrWhiteSpace(x.Sku) && x.Sku.Equals(cleanSku, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                        foreach (var other in otherEntries)
                        {
                            other.Name = trimmedName;
                            other.Category = prod.Category;
                            other.SourceType = prod.SourceType;
                            other.Unit = prod.Unit;
                            other.Sizes = new List<string>(prod.Sizes ?? new List<string>());
                            other.Cost = CostPrice;
                            other.RetailPrice = SellingPrice;
                            other.BulkPrice = BulkPrice;
                            other.DomesticPrice = DomesticPrice;
                            other.ExportPrice = ExportPrice;
                            other.ReorderLevel = prod.ReorderLevel;
                            other.Gst = prod.Gst;
                            other.Hsn = cleanHsn;
                            other.Barcode = cleanBarcode;
                            await _dataService.SaveProductAsync(other);
                        }

                        var newProd = new ProductItem
                        {
                            Id = "p_" + Guid.NewGuid().ToString("N")[..8],
                            CreatedAt = today,
                            Name = trimmedName,
                            Category = prod.Category,
                            SourceType = prod.SourceType,
                            Unit = prod.Unit,
                            Sizes = new List<string>(prod.Sizes ?? new List<string>()),
                            Cost = CostPrice,
                            RetailPrice = SellingPrice,
                            BulkPrice = BulkPrice,
                            DomesticPrice = DomesticPrice,
                            ExportPrice = ExportPrice,
                            Stock = addedQty,
                            RegisteredQty = addedQty,
                            ReorderLevel = prod.ReorderLevel,
                            Gst = prod.Gst,
                            Hsn = cleanHsn,
                            Barcode = cleanBarcode,
                            Sku = cleanSku,
                            Bom = prod.Bom ?? new()
                        };
                        await _dataService.SaveProductAsync(newProd);
                    }
                    else
                    {
                        prod.Stock = Quantity;
                        prod.RegisteredQty = Quantity;
                        if (!_isEditMode) prod.CreatedAt = today;
                        await _dataService.SaveProductAsync(prod);
                    }
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
