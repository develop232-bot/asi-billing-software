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
    public class ItemSuggestionOption
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty; // mfg, ready, raw, gen
        public string Subtitle { get; set; } = string.Empty;
        public string BadgeText { get; set; } = string.Empty;
        public string BadgeBg { get; set; } = "#EEF2FF";
        public string BadgeFg { get; set; } = "#4338CA";
        public object OriginalItem { get; set; } = null!;
    }

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
        private double _gstPercent = 0;
        private double _stock = 0;
        private double _reorderLevel = 0;
        private string _hsnCode = "";
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

        private bool _suppressSuggestions;
        private bool _isSuggestionsOpen;
        private bool _isExplicitEditFromTable;
        private bool _noticeIsNewVariant;
        private string _existingRecordNotice = string.Empty;

        public ObservableCollection<ItemSuggestionOption> FilteredSuggestions { get; } = new();

        public bool IsSuggestionsOpen
        {
            get => _isSuggestionsOpen;
            set
            {
                if (SetField(ref _isSuggestionsOpen, value))
                {
                    OnPropertyChanged(nameof(IsProductSuggestionsOpen));
                    OnPropertyChanged(nameof(IsRawSuggestionsOpen));
                }
            }
        }

        public bool IsProductSuggestionsOpen
        {
            get => _isSuggestionsOpen && IsProductType;
            set => IsSuggestionsOpen = value;
        }

        public bool IsRawSuggestionsOpen
        {
            get => _isSuggestionsOpen && IsRawOrGenType;
            set => IsSuggestionsOpen = value;
        }

        public bool NoticeIsNewVariant
        {
            get => _noticeIsNewVariant;
            set
            {
                if (SetField(ref _noticeIsNewVariant, value))
                {
                    OnPropertyChanged(nameof(NoticeBg));
                    OnPropertyChanged(nameof(NoticeBorder));
                    OnPropertyChanged(nameof(NoticeFg));
                    OnPropertyChanged(nameof(NoticeTitleFg));
                    OnPropertyChanged(nameof(NoticeIcon));
                    OnPropertyChanged(nameof(NoticePrefix));
                    OnPropertyChanged(nameof(NoticeBtnText));
                }
            }
        }

        public string ExistingRecordNotice
        {
            get => _existingRecordNotice;
            set
            {
                if (SetField(ref _existingRecordNotice, value))
                {
                    OnPropertyChanged(nameof(IsNoticeVisible));
                }
            }
        }

        public bool IsNoticeVisible => !string.IsNullOrWhiteSpace(ExistingRecordNotice);
        public string NoticeBg => NoticeIsNewVariant ? "#F0FDF4" : "#EFF6FF";
        public string NoticeBorder => NoticeIsNewVariant ? "#BBF7D0" : "#BFDBFE";
        public string NoticeFg => NoticeIsNewVariant ? "#15803D" : "#1D4ED8";
        public string NoticeTitleFg => NoticeIsNewVariant ? "#166534" : "#1E40AF";
        public string NoticeIcon => NoticeIsNewVariant ? "✨ " : "✏ ";
        public string NoticePrefix => NoticeIsNewVariant ? "Same Product SKU Inherited: " : "Editing Existing Record: ";
        public string NoticeBtnText => NoticeIsNewVariant ? "Clear / New SKU" : "Clear / Add New Instead";

        public string Name
        {
            get => _name;
            set
            {
                if (SetField(ref _name, value))
                {
                    if (!_suppressSuggestions && !_isExplicitEditFromTable)
                    {
                        UpdateSuggestions(value);
                        CheckNameAndSizeMatch();
                    }
                }
            }
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
            set
            {
                if (SetField(ref _sizes, value))
                {
                    if (!_suppressSuggestions && !_isExplicitEditFromTable)
                    {
                        CheckNameAndSizeMatch();
                    }
                }
            }
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
        private double _originalCostPrice;
        private string _originalDate = "";

        public string QuantityFieldLabel => _isEditMode ? $"Add Quantity (Stock on Hand: {_originalQuantity:N0}) *" : "Quantity *";

        public bool IsInwardNoticeVisible => _isEditMode && Quantity > 0;

        public string InwardNoticeText => $"✓ Adding +{Quantity:N0} will increase total stock from {_originalQuantity:N0} to {(_originalQuantity + Quantity):N0} under SKU {Sku}.";

        public string CostPriceFieldLabel => "Cost Price (₹) *";

        public bool IsInwardCostNoticeVisible => false;

        public string InwardCostNoticeText => "";

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
                    OnPropertyChanged(nameof(IsInwardCostNoticeVisible));
                    OnPropertyChanged(nameof(InwardCostNoticeText));
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

        private double _mediumAlertLevel = 0;
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

        public Action<string, string>? ShowLoaderAction { get; set; }
        public Action? HideLoaderAction { get; set; }

        public bool IsSaving
        {
            get => _isSaving;
            set
            {
                if (SetField(ref _isSaving, value))
                {
                    OnPropertyChanged(nameof(CanSave));
                    OnPropertyChanged(nameof(SaveButtonText));
                    OnPropertyChanged(nameof(TopSaveButtonText));
                }
            }
        }
        public bool CanSave => !_isSaving;
        public string SaveButtonText => _isSaving ? "⏳ Saving..." : (_isEditMode ? "Update Product" : "Save Product");
        public string TopSaveButtonText => _isSaving ? "⏳ Saving..." : (_isEditMode ? "Update" : "Save");

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
        public RelayCommand SelectSuggestionCommand { get; }
        public RelayCommand ResetToNewCommand { get; }

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
            SelectSuggestionCommand = new RelayCommand(p =>
            {
                if (p is ItemSuggestionOption opt) SelectSuggestion(opt);
            });
            ResetToNewCommand = new RelayCommand(_ => ResetToNew());
        }

        public void LoadItem(string type = "mfg", object? existing = null, string returnRoute = "products")
        {
            _existingItem = existing;
            _isExplicitEditFromTable = existing != null;
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
                    Title = $"Edit Product: {p.Name} ({p.SizesDisplay})";
                    Name = p.Name;
                    Category = string.IsNullOrWhiteSpace(p.Category) ? "Shoe" : p.Category;
                    Unit = string.IsNullOrWhiteSpace(p.Unit) ? "pair" : p.Unit;
                    Sizes = string.Join(",", p.Sizes ?? new List<string>());
                    Sku = p.Sku ?? "";
                    Barcode = (string.IsNullOrWhiteSpace(p.Barcode) || p.Barcode == "—" || p.Barcode == "-") ? "" : p.Barcode;
                    RetailPrice = p.RetailPrice;
                    SellingPrice = p.RetailPrice;
                    _originalCostPrice = p.Cost > 0 ? p.Cost : (p.RetailPrice > 0 ? p.RetailPrice * 0.7 : 0);
                    CostPrice = _originalCostPrice;
                    PurchaseRate = 0;
                    BulkPrice = p.BulkPrice;
                    DomesticPrice = p.DomesticPrice;
                    ExportPrice = p.ExportPrice;

                    double totalStock = _dataService.Products
                        .Where(x => ((!string.IsNullOrWhiteSpace(p.Sku) && x.Sku == p.Sku) ||
                                    (string.IsNullOrWhiteSpace(p.Sku) && x.Name.Equals(p.Name, StringComparison.OrdinalIgnoreCase))) &&
                                    string.Equals(x.SizesDisplay, p.SizesDisplay, StringComparison.OrdinalIgnoreCase))
                        .Sum(x => x.Stock);
                    if (totalStock <= 0) totalStock = p.Stock;

                    Stock = totalStock;
                    Quantity = 0;
                    _originalQuantity = totalStock;
                    ReorderLevel = p.ReorderLevel;
                    LowAlertLevel = p.ReorderLevel;
                    MediumAlertLevel = p.ReorderLevel * 2;
                    HsnCode = (string.IsNullOrWhiteSpace(p.Hsn) || p.Hsn == "—" || p.Hsn == "-") ? "" : p.Hsn;
                    GstPercent = p.Gst;
                    ExistingRecordNotice = $"{p.Name} · Size {p.SizesDisplay} (SKU: {p.Sku}) · Current Stock: {totalStock} {Unit} · Cost: ₹{_originalCostPrice:N0}";
                    NoticeIsNewVariant = false;
                }
                else if (_existingItem is RawMaterialItem r)
                {
                    _originalDate = r.CreatedAt;
                    _itemType = "raw";
                    Title = string.IsNullOrWhiteSpace(r.Size) ? $"Edit Product: {r.Name}" : $"Edit Product: {r.Name} ({r.Size})";
                    Name = r.Name;
                    Sizes = r.Size ?? "";
                    Unit = string.IsNullOrWhiteSpace(r.Unit) ? "sq ft" : r.Unit;
                    _originalCostPrice = r.Rate;
                    CostPrice = _originalCostPrice;
                    PurchaseRate = 0;
                    SellingPrice = 0;
                    GstPercent = r.Gst;

                    double totalStock = _dataService.RawMaterials
                        .Where(x => ((!string.IsNullOrWhiteSpace(r.Sku) && x.Sku == r.Sku) ||
                                    (string.IsNullOrWhiteSpace(r.Sku) && x.Name.Equals(r.Name, StringComparison.OrdinalIgnoreCase))) &&
                                    string.Equals(x.Size ?? "", r.Size ?? "", StringComparison.OrdinalIgnoreCase))
                        .Sum(x => x.Stock);
                    if (totalStock <= 0) totalStock = r.Stock;

                    Stock = totalStock;
                    Quantity = 0;
                    _originalQuantity = totalStock;
                    ReorderLevel = r.ReorderLevel;
                    LowAlertLevel = r.ReorderLevel;
                    MediumAlertLevel = r.ReorderLevel * 2;
                    Sku = string.IsNullOrWhiteSpace(r.Sku) ? r.Id : r.Sku;
                    HsnCode = (string.IsNullOrWhiteSpace(r.Hsn) || r.Hsn == "—" || r.Hsn == "-") ? "" : r.Hsn;
                    Barcode = (string.IsNullOrWhiteSpace(r.Barcode) || r.Barcode == "—" || r.Barcode == "-") ? "" : r.Barcode;
                    ExistingRecordNotice = $"{r.Name} · Size {r.SizeDisplay} (SKU: {r.Sku}) · Current Stock: {totalStock} {Unit} · Cost: ₹{_originalCostPrice:N0}";
                    NoticeIsNewVariant = false;
                }
                else if (_existingItem is GeneralItem g)
                {
                    _originalDate = g.CreatedAt;
                    _itemType = "gen";
                    Title = string.IsNullOrWhiteSpace(g.Size) ? $"Edit Product: {g.Name}" : $"Edit Product: {g.Name} ({g.Size})";
                    Name = g.Name;
                    Sizes = g.Size ?? "";
                    Unit = string.IsNullOrWhiteSpace(g.Unit) ? "pcs" : g.Unit;
                    _originalCostPrice = g.Rate;
                    CostPrice = _originalCostPrice;
                    PurchaseRate = 0;
                    SellingPrice = 0;
                    GstPercent = g.Gst;

                    double totalStock = _dataService.GeneralItems
                        .Where(x => ((!string.IsNullOrWhiteSpace(g.Sku) && x.Sku == g.Sku) ||
                                    (string.IsNullOrWhiteSpace(g.Sku) && x.Name.Equals(g.Name, StringComparison.OrdinalIgnoreCase))) &&
                                    string.Equals(x.Size ?? "", g.Size ?? "", StringComparison.OrdinalIgnoreCase))
                        .Sum(x => x.Stock);
                    if (totalStock <= 0) totalStock = g.Stock;

                    Stock = totalStock;
                    Quantity = 0;
                    _originalQuantity = totalStock;
                    ReorderLevel = g.ReorderLevel;
                    LowAlertLevel = g.ReorderLevel;
                    MediumAlertLevel = g.ReorderLevel * 2;
                    Sku = string.IsNullOrWhiteSpace(g.Sku) ? g.Id : g.Sku;
                    HsnCode = (string.IsNullOrWhiteSpace(g.Hsn) || g.Hsn == "—" || g.Hsn == "-") ? "" : g.Hsn;
                    Barcode = (string.IsNullOrWhiteSpace(g.Barcode) || g.Barcode == "—" || g.Barcode == "-") ? "" : g.Barcode;
                    ExistingRecordNotice = $"{g.Name} · Size {g.SizeDisplay} (SKU: {g.Sku}) · Current Stock: {totalStock} {Unit} · Cost: ₹{_originalCostPrice:N0}";
                    NoticeIsNewVariant = false;
                }

                if (string.IsNullOrWhiteSpace(_originalDate)) _originalDate = DateTime.Now.ToString("yyyy-MM-dd");
                OnPropertyChanged(nameof(IsInwardNoticeVisible));
                OnPropertyChanged(nameof(InwardNoticeText));
                OnPropertyChanged(nameof(QuantityFieldLabel));
                OnPropertyChanged(nameof(CostPriceFieldLabel));
                OnPropertyChanged(nameof(IsInwardCostNoticeVisible));
                OnPropertyChanged(nameof(InwardCostNoticeText));

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
                _originalCostPrice = 0;
                SellingPrice = 0;
                GstPercent = 0;
                Stock = 0;
                Quantity = 0;
                _originalQuantity = 0;
                ReorderLevel = 0;
                LowAlertLevel = 0;
                MediumAlertLevel = 0;
                HsnCode = "";
                Barcode = "";
                ExistingRecordNotice = "";
                NoticeIsNewVariant = false;

                OnPropertyChanged(nameof(QuantityFieldLabel));
                OnPropertyChanged(nameof(CostPriceFieldLabel));
                OnPropertyChanged(nameof(IsInwardNoticeVisible));
                OnPropertyChanged(nameof(InwardNoticeText));
                OnPropertyChanged(nameof(IsInwardCostNoticeVisible));
                OnPropertyChanged(nameof(InwardCostNoticeText));
                OnPropertyChanged(nameof(IsInwardNoticeVisible));
                OnPropertyChanged(nameof(InwardNoticeText));

                AutoSku();
            }

            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(SaveButtonText));
            OnPropertyChanged(nameof(TopSaveButtonText));
            OnPropertyChanged(nameof(ExistingRecordNotice));
            OnPropertyChanged(nameof(CanChangeType));
            OnPropertyChanged(nameof(IsSuggestionsOpen));
            OnPropertyChanged(nameof(IsProductSuggestionsOpen));
            OnPropertyChanged(nameof(IsRawSuggestionsOpen));
        }

        public void UpdateSuggestions(string? query)
        {
            FilteredSuggestions.Clear();
            if (string.IsNullOrWhiteSpace(query))
            {
                IsSuggestionsOpen = false;
                return;
            }

            string q = query.Trim();
            if (q.Length < 1)
            {
                IsSuggestionsOpen = false;
                return;
            }

            var list = new List<ItemSuggestionOption>();

            if (IsProductType)
            {
                var prods = _dataService.Products
                    .Where(p => !string.IsNullOrWhiteSpace(p.Name) && p.Name.Trim() != "—" && p.Name.Trim() != "-")
                    .Where(p => p.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                                (!string.IsNullOrWhiteSpace(p.Sku) && p.Sku.Contains(q, StringComparison.OrdinalIgnoreCase)))
                    .GroupBy(p => !string.IsNullOrWhiteSpace(p.Sku) ? p.Sku.Trim() : p.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .OrderByDescending(p => ItemType == "ready" ? (p.IsReadymade ? 1 : 0) : (!p.IsReadymade ? 1 : 0))
                    .Take(8);

                foreach (var p in prods)
                {
                    bool isReady = p.IsReadymade;
                    list.Add(new ItemSuggestionOption
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Sku = p.Sku ?? "",
                        Category = p.Category ?? "",
                        Unit = p.Unit ?? "pair",
                        ItemType = isReady ? "ready" : "mfg",
                        Subtitle = $"SKU: {p.Sku} · Stock: {p.Stock} {p.Unit} · Cost: ₹{p.Cost:N0} · Price: ₹{p.RetailPrice:N0}",
                        BadgeText = isReady ? "Readymade Stock" : "Finished Product",
                        BadgeBg = isReady ? "#ECFDF5" : "#EFF6FF",
                        BadgeFg = isReady ? "#059669" : "#2563EB",
                        OriginalItem = p
                    });
                }
            }
            else
            {
                if (ItemType == "raw")
                {
                    var rms = _dataService.RawMaterials
                        .Where(r => !string.IsNullOrWhiteSpace(r.Name) && r.Name.Trim() != "—" && r.Name.Trim() != "-")
                        .Where(r => r.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                                    (!string.IsNullOrWhiteSpace(r.Sku) && r.Sku.Contains(q, StringComparison.OrdinalIgnoreCase)))
                        .GroupBy(r => !string.IsNullOrWhiteSpace(r.Sku) ? r.Sku.Trim() : r.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                        .Select(g => g.First())
                        .Take(8);

                    foreach (var r in rms)
                    {
                        list.Add(new ItemSuggestionOption
                        {
                            Id = r.Id,
                            Name = r.Name,
                            Sku = r.Sku ?? "",
                            Category = "Raw Material",
                            Unit = r.Unit ?? "sq ft",
                            ItemType = "raw",
                            Subtitle = $"SKU: {r.Sku} · Stock: {r.Stock} {r.Unit} · Rate: ₹{r.Rate:N0}",
                            BadgeText = "Raw Material Stock",
                            BadgeBg = "#F0FDFA",
                            BadgeFg = "#0D9488",
                            OriginalItem = r
                        });
                    }
                }
                else
                {
                    var gens = _dataService.GeneralItems
                        .Where(g => !string.IsNullOrWhiteSpace(g.Name) && g.Name.Trim() != "—" && g.Name.Trim() != "-")
                        .Where(g => g.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                                    (!string.IsNullOrWhiteSpace(g.Sku) && g.Sku.Contains(q, StringComparison.OrdinalIgnoreCase)))
                        .GroupBy(g => !string.IsNullOrWhiteSpace(g.Sku) ? g.Sku.Trim() : g.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                        .Select(g => g.First())
                        .Take(8);

                    foreach (var g in gens)
                    {
                        list.Add(new ItemSuggestionOption
                        {
                            Id = g.Id,
                            Name = g.Name,
                            Sku = g.Sku ?? "",
                            Category = "General Supplies",
                            Unit = g.Unit ?? "pcs",
                            ItemType = "gen",
                            Subtitle = $"SKU: {g.Sku} · Stock: {g.Stock} {g.Unit} · Rate: ₹{g.Rate:N0}",
                            BadgeText = "General Supplies",
                            BadgeBg = "#FFFBEB",
                            BadgeFg = "#D97706",
                            OriginalItem = g
                        });
                    }
                }
            }

            foreach (var item in list)
            {
                FilteredSuggestions.Add(item);
            }

            IsSuggestionsOpen = FilteredSuggestions.Count > 0;
        }

        public void CheckNameAndSizeMatch()
        {
            if (_isExplicitEditFromTable) return;

            string trimmedName = (Name ?? "").Trim();
            if (string.IsNullOrWhiteSpace(trimmedName))
            {
                _isEditMode = false;
                _existingItem = null;
                ExistingRecordNotice = "";
                NoticeIsNewVariant = false;
                Title = "Add Products";
                OnPropertyChanged(nameof(IsEditMode));
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(SaveButtonText));
                OnPropertyChanged(nameof(TopSaveButtonText));
                return;
            }

            string enteredSize = (Sizes ?? "").Trim();

            if (IsProductType)
            {
                // Normalize entered sizes
                var enteredSizes = (Sizes ?? "")
                    .Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .OrderBy(s => s)
                    .ToList();
                string enteredNorm = string.Join(",", enteredSizes);

                // Find all existing products with the same item name (case-insensitive)
                var prodsWithName = _dataService.Products
                    .Where(p => string.Equals(p.Name?.Trim(), trimmedName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (prodsWithName.Count > 0)
                {
                    // Re-use the existing SKU based on item name!
                    var first = prodsWithName[0];
                    if (!string.IsNullOrWhiteSpace(first.Sku))
                    {
                        Sku = first.Sku;
                    }

                    // If user hasn't customized fields, auto-fill from the existing product
                    if (string.IsNullOrWhiteSpace(Category) || Category == "Shoe")
                    {
                        if (!string.IsNullOrWhiteSpace(first.Category)) Category = first.Category;
                    }
                    if (string.IsNullOrWhiteSpace(Unit) || Unit == "pair")
                    {
                        if (!string.IsNullOrWhiteSpace(first.Unit)) Unit = first.Unit;
                    }
                    if (string.IsNullOrWhiteSpace(HsnCode))
                    {
                        HsnCode = first.Hsn;
                    }
                    if (SellingPrice <= 0 && first.RetailPrice > 0)
                    {
                        SellingPrice = first.RetailPrice;
                        RetailPrice = first.RetailPrice;
                    }
                    if (CostPrice <= 0 && first.Cost > 0)
                    {
                        CostPrice = first.Cost;
                    }
                    if (LowAlertLevel <= 0 && first.ReorderLevel > 0)
                    {
                        LowAlertLevel = first.ReorderLevel;
                        MediumAlertLevel = first.ReorderLevel * 2;
                    }
                    GstPercent = first.Gst;

                    // Check if an existing product record has BOTH the same name AND the same size
                    ProductItem? exactMatch = null;
                    if (enteredSizes.Count > 0)
                    {
                        exactMatch = prodsWithName.FirstOrDefault(p =>
                        {
                            var pSizes = (p.Sizes ?? new List<string>())
                                .Select(s => s.Trim())
                                .Where(s => !string.IsNullOrEmpty(s))
                                .OrderBy(s => s)
                                .ToList();
                            string pNorm = string.Join(",", pSizes);
                            return string.Equals(pNorm, enteredNorm, StringComparison.OrdinalIgnoreCase);
                        });
                    }

                    if (exactMatch != null)
                    {
                        // Exact same product and size found -> this is an edit of that existing record
                        _isEditMode = true;
                        _existingItem = exactMatch;
                        _itemType = exactMatch.SourceType == "R" ? "ready" : "mfg";
                        OnPropertyChanged(nameof(ItemType));
                        OnPropertyChanged(nameof(IsProductType));
                        OnPropertyChanged(nameof(IsRawOrGenType));
                        OnPropertyChanged(nameof(ProductVisibility));
                        OnPropertyChanged(nameof(RawOrGenVisibility));
                        OnPropertyChanged(nameof(CanChangeType));

                        Category = string.IsNullOrWhiteSpace(exactMatch.Category) ? "Shoe" : exactMatch.Category;
                        Unit = string.IsNullOrWhiteSpace(exactMatch.Unit) ? "pair" : exactMatch.Unit;
                        Sku = exactMatch.Sku ?? "";
                        Barcode = (string.IsNullOrWhiteSpace(exactMatch.Barcode) || exactMatch.Barcode == "—" || exactMatch.Barcode == "-") ? "" : exactMatch.Barcode;
                        RetailPrice = exactMatch.RetailPrice;
                        SellingPrice = exactMatch.RetailPrice;
                        _originalCostPrice = exactMatch.Cost;
                        CostPrice = _originalCostPrice;
                        PurchaseRate = 0;
                        BulkPrice = exactMatch.BulkPrice;
                        DomesticPrice = exactMatch.DomesticPrice;
                        ExportPrice = exactMatch.ExportPrice;
                        Stock = exactMatch.Stock;
                        _originalQuantity = exactMatch.Stock;
                        Quantity = 0;
                        LowAlertLevel = exactMatch.ReorderLevel;
                        MediumAlertLevel = exactMatch.ReorderLevel * 2;
                        HsnCode = (string.IsNullOrWhiteSpace(exactMatch.Hsn) || exactMatch.Hsn == "—" || exactMatch.Hsn == "-") ? "" : exactMatch.Hsn;
                        GstPercent = exactMatch.Gst;

                        ExistingRecordNotice = $"{exactMatch.Name} · Size {exactMatch.SizesDisplay} (SKU: {exactMatch.Sku}) · Current Stock: {exactMatch.Stock} {Unit} · Cost: ₹{_originalCostPrice:N0}";
                        NoticeIsNewVariant = false;
                        Title = $"Edit Product: {exactMatch.Name} ({exactMatch.SizesDisplay})";
                        OnPropertyChanged(nameof(QuantityFieldLabel));
                        OnPropertyChanged(nameof(CostPriceFieldLabel));
                        OnPropertyChanged(nameof(IsInwardNoticeVisible));
                        OnPropertyChanged(nameof(InwardNoticeText));
                        OnPropertyChanged(nameof(IsInwardCostNoticeVisible));
                        OnPropertyChanged(nameof(InwardCostNoticeText));
                    }
                    else
                    {
                        // Same item name but DIFFERENT size -> Create NEW record with the SAME SKU!
                        _isEditMode = false;
                        _existingItem = null;
                        _originalCostPrice = 0;
                        CostPrice = first.Cost;
                        PurchaseRate = first.Cost;
                        OnPropertyChanged(nameof(CostPriceFieldLabel));
                        OnPropertyChanged(nameof(IsInwardCostNoticeVisible));
                        OnPropertyChanged(nameof(InwardCostNoticeText));
                        string sizeLabel = enteredSizes.Count > 0 ? $"Size {string.Join(",", enteredSizes)}" : "New Size";
                        ExistingRecordNotice = $"New variant ({sizeLabel}) for '{first.Name}' (SKU: {first.Sku})";
                        NoticeIsNewVariant = true;
                        Title = "Add Products";
                    }
                }
                else
                {
                    _isEditMode = false;
                    _existingItem = null;
                    _originalCostPrice = 0;
                    CostPrice = 0;
                    PurchaseRate = 0;
                    OnPropertyChanged(nameof(CostPriceFieldLabel));
                    OnPropertyChanged(nameof(IsInwardCostNoticeVisible));
                    OnPropertyChanged(nameof(InwardCostNoticeText));
                    ExistingRecordNotice = "";
                    NoticeIsNewVariant = false;
                    Title = "Add Products";
                }
            }
            else if (ItemType == "raw")
            {
                var rmsWithName = _dataService.RawMaterials
                    .Where(r => string.Equals(r.Name?.Trim(), trimmedName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (rmsWithName.Count > 0)
                {
                    var first = rmsWithName[0];
                    if (!string.IsNullOrWhiteSpace(first.Sku)) Sku = first.Sku;
                    if (string.IsNullOrWhiteSpace(Unit)) Unit = first.Unit;
                    if (string.IsNullOrWhiteSpace(HsnCode)) HsnCode = first.Hsn;
                    if (CostPrice <= 0 && first.Rate > 0) CostPrice = first.Rate;
                    if (LowAlertLevel <= 0 && first.ReorderLevel > 0)
                    {
                        LowAlertLevel = first.ReorderLevel;
                        MediumAlertLevel = first.ReorderLevel * 2;
                    }
                    GstPercent = first.Gst;

                    RawMaterialItem? exactMatch = null;
                    if (!string.IsNullOrEmpty(enteredSize))
                    {
                        exactMatch = rmsWithName.FirstOrDefault(r => string.Equals((r.Size ?? "").Trim(), enteredSize, StringComparison.OrdinalIgnoreCase));
                    }

                    if (exactMatch != null)
                    {
                        _isEditMode = true;
                        _existingItem = exactMatch;
                        _itemType = "raw";
                        OnPropertyChanged(nameof(ItemType));
                        OnPropertyChanged(nameof(IsProductType));
                        OnPropertyChanged(nameof(IsRawOrGenType));
                        OnPropertyChanged(nameof(ProductVisibility));
                        OnPropertyChanged(nameof(RawOrGenVisibility));
                        OnPropertyChanged(nameof(CanChangeType));

                        Unit = string.IsNullOrWhiteSpace(exactMatch.Unit) ? "sq ft" : exactMatch.Unit;
                        Sku = exactMatch.Sku ?? "";
                        Barcode = (string.IsNullOrWhiteSpace(exactMatch.Barcode) || exactMatch.Barcode == "—" || exactMatch.Barcode == "-") ? "" : exactMatch.Barcode;
                        _originalCostPrice = exactMatch.Rate;
                        CostPrice = _originalCostPrice;
                        PurchaseRate = 0;
                        SellingPrice = 0;
                        Stock = exactMatch.Stock;
                        _originalQuantity = exactMatch.Stock;
                        Quantity = 0;
                        LowAlertLevel = exactMatch.ReorderLevel;
                        MediumAlertLevel = exactMatch.ReorderLevel * 2;
                        HsnCode = (string.IsNullOrWhiteSpace(exactMatch.Hsn) || exactMatch.Hsn == "—" || exactMatch.Hsn == "-") ? "" : exactMatch.Hsn;
                        GstPercent = exactMatch.Gst;

                        ExistingRecordNotice = $"{exactMatch.Name} · Size {exactMatch.SizeDisplay} (SKU: {exactMatch.Sku}) · Current Stock: {exactMatch.Stock} {Unit} · Cost: ₹{_originalCostPrice:N0}";
                        NoticeIsNewVariant = false;
                        Title = $"Edit Product: {exactMatch.Name} ({exactMatch.SizeDisplay})";
                        OnPropertyChanged(nameof(QuantityFieldLabel));
                        OnPropertyChanged(nameof(CostPriceFieldLabel));
                        OnPropertyChanged(nameof(IsInwardNoticeVisible));
                        OnPropertyChanged(nameof(InwardNoticeText));
                        OnPropertyChanged(nameof(IsInwardCostNoticeVisible));
                        OnPropertyChanged(nameof(InwardCostNoticeText));
                    }
                    else
                    {
                        _isEditMode = false;
                        _existingItem = null;
                        _originalCostPrice = 0;
                        CostPrice = first.Rate;
                        PurchaseRate = first.Rate;
                        OnPropertyChanged(nameof(CostPriceFieldLabel));
                        OnPropertyChanged(nameof(IsInwardCostNoticeVisible));
                        OnPropertyChanged(nameof(InwardCostNoticeText));
                        string sizeLabel = !string.IsNullOrEmpty(enteredSize) ? $"Size {enteredSize}" : "New Size";
                        ExistingRecordNotice = $"New variant ({sizeLabel}) for '{first.Name}' (SKU: {first.Sku})";
                        NoticeIsNewVariant = true;
                        Title = "Add Products";
                    }
                }
                else
                {
                    _isEditMode = false;
                    _existingItem = null;
                    _originalCostPrice = 0;
                    CostPrice = 0;
                    PurchaseRate = 0;
                    OnPropertyChanged(nameof(CostPriceFieldLabel));
                    OnPropertyChanged(nameof(IsInwardCostNoticeVisible));
                    OnPropertyChanged(nameof(InwardCostNoticeText));
                    ExistingRecordNotice = "";
                    NoticeIsNewVariant = false;
                    Title = "Add Products";
                }
            }
            else if (ItemType == "gen")
            {
                var gensWithName = _dataService.GeneralItems
                    .Where(g => string.Equals(g.Name?.Trim(), trimmedName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (gensWithName.Count > 0)
                {
                    var first = gensWithName[0];
                    if (!string.IsNullOrWhiteSpace(first.Sku)) Sku = first.Sku;
                    if (string.IsNullOrWhiteSpace(Unit)) Unit = first.Unit;
                    if (string.IsNullOrWhiteSpace(HsnCode)) HsnCode = first.Hsn;
                    if (CostPrice <= 0 && first.Rate > 0) CostPrice = first.Rate;
                    if (LowAlertLevel <= 0 && first.ReorderLevel > 0)
                    {
                        LowAlertLevel = first.ReorderLevel;
                        MediumAlertLevel = first.ReorderLevel * 2;
                    }
                    GstPercent = first.Gst;

                    GeneralItem? exactMatch = null;
                    if (!string.IsNullOrEmpty(enteredSize))
                    {
                        exactMatch = gensWithName.FirstOrDefault(g => string.Equals((g.Size ?? "").Trim(), enteredSize, StringComparison.OrdinalIgnoreCase));
                    }

                    if (exactMatch != null)
                    {
                        _isEditMode = true;
                        _existingItem = exactMatch;
                        _itemType = "gen";
                        OnPropertyChanged(nameof(ItemType));
                        OnPropertyChanged(nameof(IsProductType));
                        OnPropertyChanged(nameof(IsRawOrGenType));
                        OnPropertyChanged(nameof(ProductVisibility));
                        OnPropertyChanged(nameof(RawOrGenVisibility));
                        OnPropertyChanged(nameof(CanChangeType));

                        Unit = string.IsNullOrWhiteSpace(exactMatch.Unit) ? "pcs" : exactMatch.Unit;
                        Sku = exactMatch.Sku ?? "";
                        Barcode = (string.IsNullOrWhiteSpace(exactMatch.Barcode) || exactMatch.Barcode == "—" || exactMatch.Barcode == "-") ? "" : exactMatch.Barcode;
                        _originalCostPrice = exactMatch.Rate;
                        CostPrice = _originalCostPrice;
                        PurchaseRate = 0;
                        SellingPrice = 0;
                        Stock = exactMatch.Stock;
                        _originalQuantity = exactMatch.Stock;
                        Quantity = 0;
                        LowAlertLevel = exactMatch.ReorderLevel;
                        MediumAlertLevel = exactMatch.ReorderLevel * 2;
                        HsnCode = (string.IsNullOrWhiteSpace(exactMatch.Hsn) || exactMatch.Hsn == "—" || exactMatch.Hsn == "-") ? "" : exactMatch.Hsn;
                        GstPercent = exactMatch.Gst;

                        ExistingRecordNotice = $"{exactMatch.Name} · Size {exactMatch.SizeDisplay} (SKU: {exactMatch.Sku}) · Current Stock: {exactMatch.Stock} {Unit} · Cost: ₹{_originalCostPrice:N0}";
                        NoticeIsNewVariant = false;
                        Title = $"Edit Product: {exactMatch.Name} ({exactMatch.SizeDisplay})";
                        OnPropertyChanged(nameof(QuantityFieldLabel));
                        OnPropertyChanged(nameof(CostPriceFieldLabel));
                        OnPropertyChanged(nameof(IsInwardNoticeVisible));
                        OnPropertyChanged(nameof(InwardNoticeText));
                        OnPropertyChanged(nameof(IsInwardCostNoticeVisible));
                        OnPropertyChanged(nameof(InwardCostNoticeText));
                    }
                    else
                    {
                        _isEditMode = false;
                        _existingItem = null;
                        _originalCostPrice = 0;
                        CostPrice = first.Rate;
                        PurchaseRate = first.Rate;
                        OnPropertyChanged(nameof(CostPriceFieldLabel));
                        OnPropertyChanged(nameof(IsInwardCostNoticeVisible));
                        OnPropertyChanged(nameof(InwardCostNoticeText));
                        string sizeLabel = !string.IsNullOrEmpty(enteredSize) ? $"Size {enteredSize}" : "New Size";
                        ExistingRecordNotice = $"New variant ({sizeLabel}) for '{first.Name}' (SKU: {first.Sku})";
                        NoticeIsNewVariant = true;
                        Title = "Add Products";
                    }
                }
                else
                {
                    _isEditMode = false;
                    _existingItem = null;
                    _originalCostPrice = 0;
                    CostPrice = 0;
                    PurchaseRate = 0;
                    OnPropertyChanged(nameof(CostPriceFieldLabel));
                    OnPropertyChanged(nameof(IsInwardCostNoticeVisible));
                    OnPropertyChanged(nameof(InwardCostNoticeText));
                    ExistingRecordNotice = "";
                    NoticeIsNewVariant = false;
                    Title = "Add Products";
                }
            }

            OnPropertyChanged(nameof(IsEditMode));
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(SaveButtonText));
            OnPropertyChanged(nameof(TopSaveButtonText));
        }

        public void SelectSuggestion(ItemSuggestionOption? suggestion)
        {
            if (suggestion == null) return;

            _suppressSuggestions = true;
            IsSuggestionsOpen = false;
            FilteredSuggestions.Clear();

            if (suggestion.OriginalItem is ProductItem p)
            {
                _itemType = p.SourceType == "R" ? "ready" : "mfg";
                Name = p.Name;
                Category = string.IsNullOrWhiteSpace(p.Category) ? "Shoe" : p.Category;
                Unit = string.IsNullOrWhiteSpace(p.Unit) ? "pair" : p.Unit;
                Sizes = string.Join(",", p.Sizes ?? new List<string>());
                Sku = p.Sku ?? "";
                Barcode = (string.IsNullOrWhiteSpace(p.Barcode) || p.Barcode == "—" || p.Barcode == "-") ? "" : p.Barcode;
                RetailPrice = p.RetailPrice;
                SellingPrice = p.RetailPrice;
                CostPrice = p.Cost > 0 ? p.Cost : (p.RetailPrice > 0 ? p.RetailPrice * 0.7 : 0);
                BulkPrice = p.BulkPrice;
                DomesticPrice = p.DomesticPrice;
                ExportPrice = p.ExportPrice;
                Stock = p.Stock;
                Quantity = p.Stock;
                _originalQuantity = p.Stock;
                ReorderLevel = p.ReorderLevel;
                LowAlertLevel = p.ReorderLevel;
                MediumAlertLevel = p.ReorderLevel * 2;
                HsnCode = (string.IsNullOrWhiteSpace(p.Hsn) || p.Hsn == "—" || p.Hsn == "-") ? "" : p.Hsn;
                GstPercent = p.Gst;

                CheckNameAndSizeMatch();
            }
            else if (suggestion.OriginalItem is RawMaterialItem r)
            {
                _itemType = "raw";
                Name = r.Name;
                Unit = string.IsNullOrWhiteSpace(r.Unit) ? "sq ft" : r.Unit;
                Sizes = r.Size ?? "";
                Sku = r.Sku ?? "";
                Barcode = (string.IsNullOrWhiteSpace(r.Barcode) || r.Barcode == "—" || r.Barcode == "-") ? "" : r.Barcode;
                CostPrice = r.Rate;
                PurchaseRate = r.Rate;
                SellingPrice = 0;
                Stock = r.Stock;
                Quantity = r.Stock;
                _originalQuantity = r.Stock;
                ReorderLevel = r.ReorderLevel;
                LowAlertLevel = r.ReorderLevel;
                MediumAlertLevel = r.ReorderLevel * 2;
                HsnCode = (string.IsNullOrWhiteSpace(r.Hsn) || r.Hsn == "—" || r.Hsn == "-") ? "" : r.Hsn;
                GstPercent = r.Gst;

                CheckNameAndSizeMatch();
            }
            else if (suggestion.OriginalItem is GeneralItem g)
            {
                _itemType = "gen";
                Name = g.Name;
                Unit = string.IsNullOrWhiteSpace(g.Unit) ? "pcs" : g.Unit;
                Sizes = g.Size ?? "";
                Sku = g.Sku ?? "";
                Barcode = (string.IsNullOrWhiteSpace(g.Barcode) || g.Barcode == "—" || g.Barcode == "-") ? "" : g.Barcode;
                CostPrice = g.Rate;
                PurchaseRate = g.Rate;
                SellingPrice = 0;
                Stock = g.Stock;
                Quantity = g.Stock;
                _originalQuantity = g.Stock;
                ReorderLevel = g.ReorderLevel;
                LowAlertLevel = g.ReorderLevel;
                MediumAlertLevel = g.ReorderLevel * 2;
                HsnCode = (string.IsNullOrWhiteSpace(g.Hsn) || g.Hsn == "—" || g.Hsn == "-") ? "" : g.Hsn;
                GstPercent = g.Gst;

                CheckNameAndSizeMatch();
            }
            else
            {
                LoadItem(suggestion.ItemType, suggestion.OriginalItem, ReturnRoute);
            }

            _suppressSuggestions = false;
        }

        public void ResetToNew()
        {
            _suppressSuggestions = true;
            IsSuggestionsOpen = false;
            FilteredSuggestions.Clear();

            _isExplicitEditFromTable = false;
            LoadItem(_itemType, null, ReturnRoute);

            _suppressSuggestions = false;
        }

        public void TrySelectExactMatch(string? typedName)
        {
            if (_isEditMode || string.IsNullOrWhiteSpace(typedName)) return;

            string name = typedName.Trim();
            if (IsProductType)
            {
                CheckNameAndSizeMatch();
            }
            else if (ItemType == "raw")
            {
                var match = _dataService.RawMaterials.FirstOrDefault(r =>
                    string.Equals(r.Name.Trim(), name, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    SelectSuggestion(new ItemSuggestionOption
                    {
                        ItemType = "raw",
                        OriginalItem = match
                    });
                }
            }
            else if (ItemType == "gen")
            {
                var match = _dataService.GeneralItems.FirstOrDefault(g =>
                    string.Equals(g.Name.Trim(), name, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    SelectSuggestion(new ItemSuggestionOption
                    {
                        ItemType = "gen",
                        OriginalItem = match
                    });
                }
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
            ShowLoaderAction?.Invoke(_isEditMode ? "Updating Product..." : "Saving Product...", $"Saving '{trimmedName}' to live database...");

            try
            {
                string currentId = _isEditMode ? ((_existingItem as ProductItem)?.Id
                    ?? (_existingItem as RawMaterialItem)?.Id
                    ?? (_existingItem as GeneralItem)?.Id
                    ?? (_existingItem as UnifiedProductRow)?.Id ?? "") : "";

                string cleanHsn = (HsnCode ?? "").Trim();
                if (cleanHsn == "—" || cleanHsn == "-") cleanHsn = "";

                string cleanBarcode = (Barcode ?? "").Trim();
                if (cleanBarcode == "—" || cleanBarcode == "-") cleanBarcode = "";

                if (string.IsNullOrWhiteSpace(cleanBarcode) && (ItemType == "ready" || ItemType == "mfg"))
                {
                    cleanBarcode = _dataService.GetNextUniqueBarcode(ItemType, Category);
                    Barcode = cleanBarcode;
                }
                else if (!string.IsNullOrWhiteSpace(cleanBarcode))
                {
                    var usedBarcodes = _dataService.GetAllUsedBarcodes(currentId, Sku);
                    if (usedBarcodes.Contains(cleanBarcode))
                    {
                        if (!_isEditMode)
                        {
                            cleanBarcode = _dataService.GetNextUniqueBarcode(ItemType, Category, null, Sku);
                            Barcode = cleanBarcode;
                        }
                        else
                        {
                            MessageBox.Show($"The barcode '{cleanBarcode}' is already assigned to another item. Please enter a unique barcode or leave it blank.", "Duplicate Barcode", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                    }
                }

                string today = DateTime.Now.ToString("yyyy-MM-dd");
                double addedQty = _isEditMode ? Math.Max(0, Quantity) : 0;
                bool isStockAddition = _isEditMode && addedQty > 0;

                // Cost price is unlocked: new inward additions use the entered CostPrice on the new record while keeping older records intact
                double finalCostPrice = CostPrice;

                if (ItemType == "raw")
                {
                    string cleanSize = (Sizes ?? "").Trim();
                    var rm = (!string.IsNullOrEmpty(currentId) ? _dataService.RawMaterials.FirstOrDefault(x => x.Id == currentId) : null)
                        ?? (_existingItem as RawMaterialItem)
                        ?? new RawMaterialItem { Id = !string.IsNullOrEmpty(currentId) ? currentId : "r_" + Guid.NewGuid().ToString("N")[..8] };

                    string cleanSku = !string.IsNullOrWhiteSpace(Sku) ? Sku.Trim() : (!string.IsNullOrWhiteSpace(rm.Sku) ? rm.Sku.Trim() : _dataService.GetNextSku("raw"));
                    rm.Sku = cleanSku;
                    rm.Name = trimmedName;
                    rm.Size = cleanSize;
                    rm.Unit = string.IsNullOrWhiteSpace(Unit) ? "sq ft" : Unit.Trim();
                    rm.Rate = isStockAddition ? _originalCostPrice : finalCostPrice;
                    rm.Gst = GstPercent;
                    rm.ReorderLevel = LowAlertLevel;
                    rm.Hsn = cleanHsn;
                    rm.Barcode = cleanBarcode;

                    if (_isEditMode)
                    {
                        // 1. Update existing rm attributes (leaving its original stock & registeredQty intact)
                        await _dataService.SaveRawMaterialAsync(rm);

                        // 2. If quantity was added, create a separate record for this inward addition with today's date!
                        if (addedQty > 0)
                        {
                            var additionRecord = new RawMaterialItem
                            {
                                Id = "r_" + Guid.NewGuid().ToString("N")[..8],
                                CreatedAt = today,
                                Name = trimmedName,
                                Sku = cleanSku,
                                Barcode = cleanBarcode,
                                Hsn = cleanHsn,
                                Size = cleanSize,
                                Unit = rm.Unit,
                                Rate = finalCostPrice,
                                Gst = GstPercent,
                                ReorderLevel = LowAlertLevel,
                                Stock = addedQty,
                                RegisteredQty = addedQty
                            };
                            await _dataService.SaveRawMaterialAsync(additionRecord);
                        }

                        var otherEntries = _dataService.RawMaterials
                            .Where(x => x.Id != rm.Id && !string.IsNullOrWhiteSpace(x.Sku) && x.Sku.Equals(cleanSku, StringComparison.OrdinalIgnoreCase) && string.Equals(x.Size ?? "", cleanSize, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                        foreach (var other in otherEntries)
                        {
                            other.Name = trimmedName;
                            other.Size = cleanSize;
                            other.Unit = rm.Unit;
                            other.Gst = rm.Gst;
                            other.ReorderLevel = rm.ReorderLevel;
                            other.Hsn = cleanHsn;
                            other.Barcode = cleanBarcode;
                            await _dataService.SaveRawMaterialAsync(other);
                        }
                    }
                    else
                    {
                        rm.Stock = Quantity;
                        rm.RegisteredQty = Quantity;
                        rm.CreatedAt = today;
                        await _dataService.SaveRawMaterialAsync(rm);
                    }
                }
                else if (ItemType == "gen")
                {
                    string cleanSize = (Sizes ?? "").Trim();
                    var gi = (!string.IsNullOrEmpty(currentId) ? _dataService.GeneralItems.FirstOrDefault(x => x.Id == currentId) : null)
                        ?? (_existingItem as GeneralItem)
                        ?? new GeneralItem { Id = !string.IsNullOrEmpty(currentId) ? currentId : "gen_" + Guid.NewGuid().ToString("N")[..8] };

                    string cleanSku = !string.IsNullOrWhiteSpace(Sku) ? Sku.Trim() : (!string.IsNullOrWhiteSpace(gi.Sku) ? gi.Sku.Trim() : _dataService.GetNextSku("gen"));
                    gi.Sku = cleanSku;
                    gi.Name = trimmedName;
                    gi.Size = cleanSize;
                    gi.Unit = string.IsNullOrWhiteSpace(Unit) ? "pcs" : Unit.Trim();
                    gi.Rate = isStockAddition ? _originalCostPrice : finalCostPrice;
                    gi.Gst = GstPercent;
                    gi.ReorderLevel = LowAlertLevel;
                    gi.Hsn = cleanHsn;
                    gi.Barcode = cleanBarcode;

                    if (_isEditMode)
                    {
                        // 1. Update existing gi attributes (leaving its original stock & registeredQty intact)
                        await _dataService.SaveGeneralItemAsync(gi);

                        // 2. If quantity was added, create a separate record for this inward addition with today's date!
                        if (addedQty > 0)
                        {
                            var additionRecord = new GeneralItem
                            {
                                Id = "gen_" + Guid.NewGuid().ToString("N")[..8],
                                CreatedAt = today,
                                Name = trimmedName,
                                Sku = cleanSku,
                                Barcode = cleanBarcode,
                                Hsn = cleanHsn,
                                Size = cleanSize,
                                Unit = gi.Unit,
                                Rate = finalCostPrice,
                                Gst = GstPercent,
                                ReorderLevel = LowAlertLevel,
                                Stock = addedQty,
                                RegisteredQty = addedQty
                            };
                            await _dataService.SaveGeneralItemAsync(additionRecord);
                        }

                        var otherEntries = _dataService.GeneralItems
                            .Where(x => x.Id != gi.Id && !string.IsNullOrWhiteSpace(x.Sku) && x.Sku.Equals(cleanSku, StringComparison.OrdinalIgnoreCase) && string.Equals(x.Size ?? "", cleanSize, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                        foreach (var other in otherEntries)
                        {
                            other.Name = trimmedName;
                            other.Size = cleanSize;
                            other.Unit = gi.Unit;
                            other.Gst = gi.Gst;
                            other.ReorderLevel = gi.ReorderLevel;
                            other.Hsn = cleanHsn;
                            other.Barcode = cleanBarcode;
                            await _dataService.SaveGeneralItemAsync(other);
                        }
                    }
                    else
                    {
                        gi.Stock = Quantity;
                        gi.RegisteredQty = Quantity;
                        gi.CreatedAt = today;
                        await _dataService.SaveGeneralItemAsync(gi);
                    }
                }
                else
                {
                    string cleanSku = !string.IsNullOrWhiteSpace(Sku) ? Sku.Trim() : _dataService.GetNextSku(ItemType == "mfg" ? "mfg" : "ready");

                    if (!_isEditMode || string.IsNullOrEmpty(currentId))
                    {
                        // Create NEW product record (either completely new product, or new size variant with same SKU)
                        var newProd = new ProductItem
                        {
                            Id = "p_" + Guid.NewGuid().ToString("N")[..8],
                            CreatedAt = today,
                            Name = trimmedName,
                            Sku = cleanSku, // Inherits same SKU based on item name!
                            Barcode = cleanBarcode,
                            Hsn = cleanHsn,
                            Category = string.IsNullOrWhiteSpace(Category) ? "Shoe" : Category,
                            SourceType = ItemType == "mfg" ? "M" : "R",
                            Unit = string.IsNullOrWhiteSpace(Unit) ? "pair" : Unit.Trim(),
                            Sizes = (Sizes ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList(),
                            Cost = CostPrice,
                            RetailPrice = SellingPrice,
                            BulkPrice = BulkPrice,
                            DomesticPrice = DomesticPrice,
                            ExportPrice = ExportPrice,
                            Stock = Quantity,
                            RegisteredQty = Quantity,
                            ReorderLevel = LowAlertLevel,
                            Gst = GstPercent,
                            Bom = new()
                        };
                        await _dataService.SaveProductAsync(newProd);
                    }
                    else
                    {
                        var prod = (!string.IsNullOrEmpty(currentId) ? _dataService.Products.FirstOrDefault(x => x.Id == currentId) : null)
                            ?? (_existingItem as ProductItem)
                            ?? new ProductItem { Id = currentId };

                        prod.Sku = cleanSku;
                        prod.Barcode = cleanBarcode;
                        prod.Hsn = cleanHsn;
                        prod.Name = trimmedName;
                        prod.Category = string.IsNullOrWhiteSpace(Category) ? "Shoe" : Category;
                        prod.SourceType = ItemType == "mfg" ? "M" : "R";
                        prod.Unit = string.IsNullOrWhiteSpace(Unit) ? "pair" : Unit.Trim();
                        prod.Sizes = (Sizes ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
                        prod.Cost = isStockAddition ? _originalCostPrice : finalCostPrice;
                        prod.RetailPrice = SellingPrice;
                        prod.BulkPrice = BulkPrice;
                        prod.DomesticPrice = DomesticPrice;
                        prod.ExportPrice = ExportPrice;
                        prod.ReorderLevel = LowAlertLevel;
                        prod.Gst = GstPercent;

                        // 1. Update existing prod attributes (leaving its original stock & registeredQty intact)
                        await _dataService.SaveProductAsync(prod);

                        // 2. If quantity was added, create a separate record for this inward addition with today's date!
                        if (addedQty > 0)
                        {
                            var additionRecord = new ProductItem
                            {
                                Id = "p_" + Guid.NewGuid().ToString("N")[..8],
                                CreatedAt = today,
                                Name = trimmedName,
                                Sku = cleanSku,
                                Barcode = cleanBarcode,
                                Hsn = cleanHsn,
                                Category = prod.Category,
                                SourceType = prod.SourceType,
                                Unit = prod.Unit,
                                Sizes = prod.Sizes != null ? new List<string>(prod.Sizes) : new List<string>(),
                                Cost = finalCostPrice,
                                RetailPrice = SellingPrice,
                                BulkPrice = BulkPrice,
                                DomesticPrice = DomesticPrice,
                                ExportPrice = ExportPrice,
                                ReorderLevel = LowAlertLevel,
                                Gst = GstPercent,
                                Stock = addedQty,
                                RegisteredQty = addedQty,
                                Bom = new()
                            };
                            await _dataService.SaveProductAsync(additionRecord);
                        }
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
                HideLoaderAction?.Invoke();
                IsSaving = false;
            }
        }
    }
}
