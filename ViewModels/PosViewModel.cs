using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using AsiErp.Commands;
using AsiErp.Models;
using AsiErp.Services;

namespace AsiErp.ViewModels
{
    public class PosCartItem : ViewModelBase
    {
        private double _qty = 1;
        private string _selectedSize = string.Empty;

        public ProductItem Product { get; }

        public string SelectedSize
        {
            get => _selectedSize;
            set => SetField(ref _selectedSize, value);
        }

        public double Qty
        {
            get => _qty;
            set
            {
                if (SetField(ref _qty, value))
                {
                    OnPropertyChanged(nameof(UnitPrice));
                    OnPropertyChanged(nameof(LineTotal));
                    OnPropertyChanged(nameof(IsBulkRateApplied));
                }
            }
        }

        public bool IsBulkRateApplied => Qty >= 10;

        public double UnitPrice => IsBulkRateApplied && Product.BulkPrice > 0 ? Product.BulkPrice : Product.RetailPrice;

        public double LineTotal => Qty * UnitPrice;

        public PosCartItem(ProductItem product, string size = "", double qty = 1)
        {
            Product = product;
            _selectedSize = !string.IsNullOrEmpty(size) ? size : (product.Sizes?.FirstOrDefault() ?? "");
            _qty = qty;
        }
    }

    public class GsConsumptionItem : ViewModelBase
    {
        private double _qty = 1;

        public GeneralItem Item { get; }
        public string Name { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public string Unit { get; set; } = "pcs";
        public double CurrentStock { get; set; }

        public double Qty
        {
            get => _qty;
            set
            {
                if (SetField(ref _qty, value))
                {
                    OnPropertyChanged(nameof(RemainingStock));
                }
            }
        }

        public double RemainingStock => Math.Max(0, Math.Round(CurrentStock - Qty, 2));

        public GsConsumptionItem(GeneralItem item, string size, double qty = 1)
        {
            Item = item;
            Name = item.Name;
            Size = !string.IsNullOrWhiteSpace(size) ? size : (!string.IsNullOrWhiteSpace(item.Size) ? item.Size : "—");
            Unit = !string.IsNullOrWhiteSpace(item.Unit) ? item.Unit : "pcs";
            CurrentStock = item.Stock;
            _qty = qty;
        }
    }

    public class PosViewModel : ViewModelBase
    {
        private readonly AppDataService _dataService;
        private readonly Action<string, object?> _openDialogAction;
        private readonly Action<string>? _navigateAction;

        private string _searchQuery = string.Empty;
        private string _scannedBarcode = string.Empty;
        private string _selectedCategory = "all";
        private ProductItem? _quickAddItem;
        private string _quickAddText = string.Empty;
        private bool _isFilteringQuickAdd = false;
        private readonly List<ProductItem> _allAvailableProducts = new();
        private int _quickAddQty = 1;

        private double _discountPercent = 0;
        private string _paymentMode = "Cash"; // Cash, UPI, Card
        private string _soldAtLocation = "Showroom";
        private string _customerName = string.Empty;
        private string _customerPhone = string.Empty;

        public ObservableCollection<ProductItem> FilteredProducts { get; } = new();
        public ObservableCollection<ProductItem> AvailableProducts { get; } = new();
        public ObservableCollection<PosCartItem> CartItems { get; } = new();
        public ObservableCollection<SaleDoc> RecentBills { get; } = new();

        // --- GENERAL STOCK (PACKAGING CONSUMPTION) ---
        private readonly List<GeneralItem> _allGsItems = new();
        public ObservableCollection<GeneralItem> AvailableGsItems { get; } = new();
        public ObservableCollection<string> AvailableGsSizes { get; } = new();
        public ObservableCollection<GsConsumptionItem> GsConsumptionItems { get; } = new();

        public bool HasGsConsumptionItems => GsConsumptionItems.Count > 0;

        private GeneralItem? _selectedGsItem;
        private string _gsItemText = string.Empty;
        private string _selectedGsSize = string.Empty;
        private double _gsQty = 1;
        private bool _isFilteringGs = false;

        public GeneralItem? SelectedGsItem
        {
            get => _selectedGsItem;
            set
            {
                if (SetField(ref _selectedGsItem, value))
                {
                    if (value != null && !_isFilteringGs)
                    {
                        _isFilteringGs = true;
                        try
                        {
                            GsItemText = value.Name;
                        }
                        finally
                        {
                            _isFilteringGs = false;
                        }

                        AvailableGsSizes.Clear();
                        var matchingSizes = _allGsItems
                            .Where(g => g.Name.Equals(value.Name, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(g.Size) && g.Size != "—" && g.Size != "-")
                            .Select(g => g.Size.Trim())
                            .Distinct()
                            .ToList();

                        if (matchingSizes.Count > 0)
                        {
                            foreach (var s in matchingSizes) AvailableGsSizes.Add(s);
                            SelectedGsSize = !string.IsNullOrWhiteSpace(value.Size) && value.Size != "—" && value.Size != "-" ? value.Size.Trim() : matchingSizes[0];
                        }
                        else
                        {
                            string defSize = !string.IsNullOrWhiteSpace(value.Size) && value.Size != "—" && value.Size != "-" ? value.Size.Trim() : "Standard";
                            AvailableGsSizes.Add(defSize);
                            SelectedGsSize = defSize;
                        }
                    }
                }
            }
        }

        public string GsItemText
        {
            get => _gsItemText;
            set
            {
                if (SetField(ref _gsItemText, value))
                {
                    if (!_isFilteringGs)
                    {
                        FilterGs(value);
                    }
                }
            }
        }

        public string SelectedGsSize
        {
            get => _selectedGsSize;
            set => SetField(ref _selectedGsSize, value);
        }

        public double GsQty
        {
            get => _gsQty;
            set => SetField(ref _gsQty, value);
        }

        public void FilterGs(string? query)
        {
            _isFilteringGs = true;
            try
            {
                string q = (query ?? string.Empty).Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(q))
                {
                    ResetGsFilter();
                    return;
                }

                var matches = _allGsItems.Where(g =>
                    g.Name.ToLowerInvariant().Contains(q) ||
                    (!string.IsNullOrEmpty(g.Size) && g.Size.ToLowerInvariant().Contains(q)) ||
                    (!string.IsNullOrEmpty(g.Sku) && g.Sku.ToLowerInvariant().Contains(q)) ||
                    (!string.IsNullOrEmpty(g.Barcode) && g.Barcode.ToLowerInvariant().Contains(q))
                ).ToList();

                AvailableGsItems.Clear();
                foreach (var g in matches)
                {
                    AvailableGsItems.Add(g);
                }
            }
            finally
            {
                _isFilteringGs = false;
            }
        }

        public void ResetGsFilter()
        {
            _isFilteringGs = true;
            try
            {
                AvailableGsItems.Clear();
                foreach (var g in _allGsItems)
                {
                    AvailableGsItems.Add(g);
                }
            }
            finally
            {
                _isFilteringGs = false;
            }
        }

        public void LoadGeneralItems()
        {
            _allGsItems.Clear();
            AvailableGsItems.Clear();

            var items = _dataService.GeneralItems
                .Where(g => !string.IsNullOrWhiteSpace(g.Name) && g.Name.Trim() != "—" && g.Name.Trim() != "-")
                .OrderBy(g => g.Name)
                .ToList();

            foreach (var g in items)
            {
                _allGsItems.Add(g);
                AvailableGsItems.Add(g);
            }

            if (SelectedGsItem != null)
            {
                var refreshed = _allGsItems.FirstOrDefault(x => x.Id == SelectedGsItem.Id);
                if (refreshed != null) SelectedGsItem = refreshed;
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
                    DebounceAction(ref _searchDebounceTimer, ApplyProductFilter, 200);
                }
            }
        }

        public string ScannedBarcode
        {
            get => _scannedBarcode;
            set => SetField(ref _scannedBarcode, value);
        }

        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetField(ref _selectedCategory, value)) ApplyProductFilter();
            }
        }

        public string QuickAddText
        {
            get => _quickAddText;
            set
            {
                if (SetField(ref _quickAddText, value))
                {
                    if (!_isFilteringQuickAdd)
                    {
                        FilterQuickAdd(value);
                    }
                }
            }
        }

        public ProductItem? QuickAddItem
        {
            get => _quickAddItem;
            set
            {
                if (SetField(ref _quickAddItem, value))
                {
                    if (value != null && !_isFilteringQuickAdd)
                    {
                        _isFilteringQuickAdd = true;
                        try
                        {
                            QuickAddText = value.Name;
                        }
                        finally
                        {
                            _isFilteringQuickAdd = false;
                        }
                    }
                }
            }
        }

        public void FilterQuickAdd(string? query)
        {
            _isFilteringQuickAdd = true;
            try
            {
                string q = (query ?? string.Empty).Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(q))
                {
                    ResetQuickAddFilter();
                    return;
                }

                var matches = _allAvailableProducts.Where(p =>
                    p.Name.ToLowerInvariant().Contains(q) ||
                    p.Category.ToLowerInvariant().Contains(q) ||
                    p.DisplayBarcode.ToLowerInvariant().Contains(q) ||
                    p.Sku.ToLowerInvariant().Contains(q)
                ).ToList();

                AvailableProducts.Clear();
                foreach (var p in matches)
                {
                    AvailableProducts.Add(p);
                }

                var exact = matches.FirstOrDefault(p =>
                    p.DisplayBarcode.Equals(q, StringComparison.OrdinalIgnoreCase) ||
                    p.Sku.Equals(q, StringComparison.OrdinalIgnoreCase) ||
                    p.Name.Equals(q, StringComparison.OrdinalIgnoreCase)
                );
                _quickAddItem = exact ?? matches.FirstOrDefault();
                OnPropertyChanged(nameof(QuickAddItem));
            }
            finally
            {
                _isFilteringQuickAdd = false;
            }
        }

        public void ResetQuickAddFilter()
        {
            AvailableProducts.Clear();
            foreach (var p in _allAvailableProducts)
            {
                AvailableProducts.Add(p);
            }
        }

        public int QuickAddQty
        {
            get => _quickAddQty;
            set => SetField(ref _quickAddQty, value);
        }

        public double DiscountPercent
        {
            get => _discountPercent;
            set
            {
                if (SetField(ref _discountPercent, Math.Max(0, Math.Min(100, value)))) RecalculateCart();
            }
        }

        public string PaymentMode
        {
            get => _paymentMode;
            set => SetField(ref _paymentMode, value);
        }

        public string SoldAtLocation
        {
            get => _soldAtLocation;
            set => SetField(ref _soldAtLocation, value);
        }

        public string CustomerName
        {
            get => _customerName;
            set => SetField(ref _customerName, value);
        }

        public string CustomerPhone
        {
            get => _customerPhone;
            set => SetField(ref _customerPhone, value);
        }

        public double Subtotal => CartItems.Sum(x => x.LineTotal);
        public double DiscountAmount => Subtotal * (DiscountPercent / 100.0);
        public double GrandTotal => Math.Max(0, Subtotal - DiscountAmount);

        public double GstIncluded
        {
            get
            {
                double gst = 0;
                foreach (var ci in CartItems)
                {
                    double lineGross = ci.LineTotal * (1.0 - DiscountPercent / 100.0);
                    double rate = ci.Product.AutoGstRate;
                    double net = lineGross / (1.0 + rate / 100.0);
                    gst += (lineGross - net);
                }
                return Math.Round(gst, 2);
            }
        }

        public string NextBillNo => _dataService.NextDocNo("retail", increment: false);

        public Action<string, string>? ShowLoaderAction { get; set; }
        public Action? HideLoaderAction { get; set; }

        private bool _isProcessingSale;
        public bool IsProcessingSale
        {
            get => _isProcessingSale;
            set
            {
                if (SetField(ref _isProcessingSale, value))
                {
                    OnPropertyChanged(nameof(CanCompleteSale));
                    OnPropertyChanged(nameof(CompleteSaleButtonText));
                }
            }
        }

        public bool CanCompleteSale => !_isProcessingSale;
        public string CompleteSaleButtonText => _isProcessingSale ? "Processing Sale & Printing..." : "Complete Sale & Print Bill";

        public RelayCommand SetCategoryCommand { get; }
        public RelayCommand QuickAddCommand { get; }
        public RelayCommand ScanBarcodeCommand { get; }
        public RelayCommand AddToCartCommand { get; }
        public RelayCommand IncrementQtyCommand { get; }
        public RelayCommand DecrementQtyCommand { get; }
        public RelayCommand RemoveItemCommand { get; }
        public RelayCommand SetPaymentModeCommand { get; }
        public RelayCommand CompleteSaleCommand { get; }
        public RelayCommand ViewBillCommand { get; }
        public RelayCommand AddNewProductCommand { get; }
        public RelayCommand BackToSalesCommand { get; }
        public RelayCommand AddGsItemCommand { get; }
        public RelayCommand RemoveGsItemCommand { get; }
        public RelayCommand SubmitGsConsumptionCommand { get; }

        public PosViewModel(AppDataService dataService, Action<string, object?> openDialogAction, Action<string>? navigateAction = null)
        {
            _dataService = dataService;
            _openDialogAction = openDialogAction;
            _navigateAction = navigateAction;

            BackToSalesCommand = new RelayCommand(_ => _navigateAction?.Invoke("sales"));

            SetCategoryCommand = new RelayCommand(p => SelectedCategory = p?.ToString() ?? "all");

            GsConsumptionItems.CollectionChanged += (s, e) => OnPropertyChanged(nameof(HasGsConsumptionItems));

            AddGsItemCommand = new RelayCommand(_ => AddGsItem());
            RemoveGsItemCommand = new RelayCommand(p =>
            {
                if (p is GsConsumptionItem ci)
                {
                    GsConsumptionItems.Remove(ci);
                    OnPropertyChanged(nameof(HasGsConsumptionItems));
                }
            });
            SubmitGsConsumptionCommand = new RelayCommand(async _ => await SubmitGsConsumptionAsync());

            QuickAddCommand = new RelayCommand(_ =>
            {
                var item = QuickAddItem;
                if (item == null && !string.IsNullOrWhiteSpace(QuickAddText))
                {
                    string q = QuickAddText.Trim().ToLowerInvariant();
                    item = _allAvailableProducts.FirstOrDefault(p =>
                        p.DisplayBarcode.Equals(q, StringComparison.OrdinalIgnoreCase) ||
                        p.Sku.Equals(q, StringComparison.OrdinalIgnoreCase) ||
                        p.Name.Equals(q, StringComparison.OrdinalIgnoreCase)
                    ) ?? _allAvailableProducts.FirstOrDefault(p =>
                        p.Name.ToLowerInvariant().Contains(q) ||
                        p.Category.ToLowerInvariant().Contains(q) ||
                        p.DisplayBarcode.Contains(q) ||
                        p.Sku.ToLowerInvariant().Contains(q)
                    );
                }

                if (item != null)
                {
                    AddProductToCart(item, QuickAddQty);
                    QuickAddQty = 1;
                    _isFilteringQuickAdd = true;
                    try
                    {
                        QuickAddItem = null;
                        QuickAddText = string.Empty;
                    }
                    finally
                    {
                        _isFilteringQuickAdd = false;
                    }
                    ResetQuickAddFilter();
                }
                else
                {
                    MessageBox.Show("Please select an item from the list or enter a matching product name, SKU, or barcode.", "Product Not Found", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            });

            ScanBarcodeCommand = new RelayCommand(_ => ScanBarcode());
            AddNewProductCommand = new RelayCommand(_ => _openDialogAction("product_edit_mfg", null));

            AddToCartCommand = new RelayCommand(p =>
            {
                if (p is ProductItem prod) AddProductToCart(prod);
            });

            SetPaymentModeCommand = new RelayCommand(p =>
            {
                if (p is string mode) PaymentMode = mode;
            });

            IncrementQtyCommand = new RelayCommand(p =>
            {
                if (p is PosCartItem item)
                {
                    if (item.Qty < item.Product.Stock)
                    {
                        item.Qty++;
                        RecalculateCart();
                    }
                    else
                    {
                        MessageBox.Show($"Only {item.Product.Stock} {item.Product.Unit} of {item.Product.Name} in stock.", "Stock Limit", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            });

            DecrementQtyCommand = new RelayCommand(p =>
            {
                if (p is PosCartItem item)
                {
                    item.Qty--;
                    if (item.Qty <= 0) CartItems.Remove(item);
                    RecalculateCart();
                }
            });

            RemoveItemCommand = new RelayCommand(p =>
            {
                if (p is PosCartItem item)
                {
                    CartItems.Remove(item);
                    RecalculateCart();
                }
            });

            CompleteSaleCommand = new RelayCommand(_ => CompleteSale());

            ViewBillCommand = new RelayCommand(p =>
            {
                if (p is SaleDoc sale) _openDialogAction("view_doc", sale);
            });

            _dataService.DataChanged += () =>
            {
                if (!IsActive) { IsStale = true; return; }
                LoadData();
            };
            LoadData();
        }

        public override void OnActivated()
        {
            base.OnActivated();
            if (IsStale)
            {
                IsStale = false;
                LoadData();
            }
        }

        public void LoadData()
        {
            _allAvailableProducts.Clear();
            AvailableProducts.Clear();
            foreach (var p in _dataService.GetGroupedAvailableProducts().Where(x => x.Stock > 0))
            {
                _allAvailableProducts.Add(p);
                AvailableProducts.Add(p);
            }

            ApplyProductFilter();

            RecentBills.Clear();
            foreach (var s in _dataService.Sales.Where(x => x.Type == "retail").Take(12))
                RecentBills.Add(s);

            OnPropertyChanged(nameof(NextBillNo));

            LoadGeneralItems();
        }

        private void ApplyProductFilter()
        {
            FilteredProducts.Clear();
            string q = SearchQuery.Trim().ToLowerInvariant();

            foreach (var p in _dataService.GetGroupedAvailableProducts())
            {
                bool matchesCat = SelectedCategory == "all" || p.Category.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase);
                bool matchesQ = string.IsNullOrEmpty(q) || (p.Name + " " + p.Sku + " " + (p.Barcode ?? "")).ToLowerInvariant().Contains(q);

                if (matchesCat && matchesQ) FilteredProducts.Add(p);
            }
        }

        private void ScanBarcode()
        {
            ScanBarcodeDirect(ScannedBarcode);
        }

        public bool ScanBarcodeDirect(string? code)
        {
            if (string.IsNullOrWhiteSpace(code)) return false;
            code = code.Trim();

            var prod = BarcodeService.FindProductByBarcodeOrSku(code, _dataService.Products);
            if (prod != null)
            {
                try { System.Media.SystemSounds.Beep.Play(); } catch { }
                AddProductToCart(prod);
                ScannedBarcode = string.Empty;
                return true;
            }
            else
            {
                try { System.Media.SystemSounds.Hand.Play(); } catch { }
                MessageBox.Show($"No product found with barcode or SKU '{code}'.\n\nPlease ensure the product is registered with this barcode in the Products catalog.", "Item Not Found", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }
        }

        public void AddProductToCart(ProductItem prod, int qty = 1)
        {
            double availableStock = _dataService.Products
                .Where(p => (!string.IsNullOrWhiteSpace(prod.Sku) && p.Sku.Equals(prod.Sku, StringComparison.OrdinalIgnoreCase)) ||
                            (p.Name.Equals(prod.Name, StringComparison.OrdinalIgnoreCase) && string.Equals(p.SizesDisplay, prod.SizesDisplay, StringComparison.OrdinalIgnoreCase)))
                .Sum(p => p.Stock);

            if (availableStock <= 0)
            {
                MessageBox.Show($"{prod.Name} is out of stock.", "Out of Stock", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string defaultSize = prod.Sizes?.FirstOrDefault() ?? "";
            var existing = CartItems.FirstOrDefault(x => (x.Product.Id == prod.Id || (!string.IsNullOrWhiteSpace(prod.Sku) && x.Product.Sku.Equals(prod.Sku, StringComparison.OrdinalIgnoreCase))) && x.SelectedSize == defaultSize);

            if (existing != null)
            {
                if (existing.Qty + qty <= availableStock)
                {
                    existing.Qty += qty;
                }
                else
                {
                    MessageBox.Show($"Only {availableStock} {prod.Unit} of {prod.Name} in stock.", "Stock Limit", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                int addQty = Math.Min(qty, (int)availableStock);
                CartItems.Add(new PosCartItem(prod, defaultSize, addQty));
            }

            RecalculateCart();
        }

        private void RecalculateCart()
        {
            OnPropertyChanged(nameof(Subtotal));
            OnPropertyChanged(nameof(DiscountAmount));
            OnPropertyChanged(nameof(GrandTotal));
            OnPropertyChanged(nameof(GstIncluded));
        }

        private async void CompleteSale()
        {
            if (IsProcessingSale) return;

            if (!CartItems.Any())
            {
                MessageBox.Show("Please add at least one item to the cart.", "Cart Empty", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(CustomerName))
            {
                MessageBox.Show("Please enter the customer name.", "Required Field", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(CustomerPhone))
            {
                MessageBox.Show("Please enter the customer phone number.", "Required Field", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsProcessingSale = true;
            ShowLoaderAction?.Invoke("Completing Sale & Printing...", "Recording retail sale, deducting inventory & generating bill...");

            try
            {
                // Yield to UI to allow overlay to appear immediately
                await Task.Delay(80);

                var saleLines = CartItems.Select(ci =>
                {
                    double lineGross = ci.LineTotal * (1.0 - DiscountPercent / 100.0);
                    double gstRate = ci.Product.AutoGstRate;
                    double net = lineGross / (1.0 + gstRate / 100.0);
                    double tax = lineGross - net;

                    return new SaleLine
                    {
                        Pid = ci.Product.Id,
                        Name = ci.Product.Name,
                        Hsn = ci.Product.Hsn,
                        Size = ci.SelectedSize,
                        Qty = ci.Qty,
                        Unit = ci.Product.Unit,
                        Rate = ci.UnitPrice,
                        DiscPercent = DiscountPercent,
                        GstPercent = gstRate,
                        NetAmount = Math.Round(net, 2),
                        TaxAmount = Math.Round(tax, 2),
                        GrossAmount = Math.Round(lineGross, 2)
                    };
                }).ToList();

                double totalNet = saleLines.Sum(l => l.NetAmount);
                double totalTax = saleLines.Sum(l => l.TaxAmount);
                double totalGross = GrandTotal;

                var newSale = new SaleDoc
                {
                    Id = "s_" + Guid.NewGuid().ToString("N")[..8],
                    Type = "retail",
                    InvoiceNo = _dataService.NextDocNo("retail", increment: true),
                    Date = DateTime.Now.ToString("yyyy-MM-dd"),
                    Party = CustomerName.Trim(),
                    Phone = CustomerPhone.Trim(),
                    Event = SoldAtLocation,
                    PaymentMode = PaymentMode,
                    SaleType = "Cash",
                    CreditDays = 0,
                    PaymentDueDate = DateTime.Now.ToString("yyyy-MM-dd"),
                    DiscountPercent = DiscountPercent,
                    NetAmount = Math.Round(totalNet, 2),
                    TaxAmount = Math.Round(totalTax, 2),
                    GrossAmount = Math.Round(totalGross, 2),
                    PaidAmount = Math.Round(totalGross, 2),
                    Lines = saleLines
                };

                await _dataService.RecordSaleAsync(newSale);

                // Deduct any General Stock (packaging) items that were added for this sale
                if (GsConsumptionItems.Any())
                {
                    foreach (var ci in GsConsumptionItems.ToList())
                    {
                        double remainingToDeduct = ci.Qty;
                        var matches = _dataService.GeneralItems
                            .Where(g => g.Id == ci.Item.Id ||
                                       (g.Name.Equals(ci.Name, StringComparison.OrdinalIgnoreCase) &&
                                        (string.Equals(g.Size ?? "", ci.Size, StringComparison.OrdinalIgnoreCase) || ci.Size == "—" || ci.Size == "Standard")))
                            .ToList();

                        if (matches.Count == 0)
                        {
                            matches = _dataService.GeneralItems.Where(g => g.Name.Equals(ci.Name, StringComparison.OrdinalIgnoreCase)).ToList();
                        }

                        foreach (var g in matches)
                        {
                            if (remainingToDeduct <= 0) break;
                            double deduct = Math.Min(g.Stock, remainingToDeduct);
                            if (deduct <= 0) deduct = remainingToDeduct;

                            if (g.RegisteredQty <= 0 && g.Stock > 0) g.RegisteredQty = g.Stock;
                            g.Stock = Math.Max(0, Math.Round(g.Stock - deduct, 2));
                            remainingToDeduct -= deduct;
                            await _dataService.SaveGeneralItemAsync(g);
                        }
                    }
                    GsConsumptionItems.Clear();
                    OnPropertyChanged(nameof(HasGsConsumptionItems));
                    LoadGeneralItems();
                }

                // Clear cart
                CartItems.Clear();
                DiscountPercent = 0;
                CustomerName = string.Empty;
                CustomerPhone = string.Empty;
                RecalculateCart();

                // Directly print without preview window
                DocumentPrinter.PrintDirect(newSale, silent: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to complete sale: {ex.Message}", "Sale Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                HideLoaderAction?.Invoke();
                IsProcessingSale = false;
            }
        }

        private void AddGsItem()
        {
            var item = SelectedGsItem;
            if (item == null && !string.IsNullOrWhiteSpace(GsItemText))
            {
                item = _allGsItems.FirstOrDefault(g => g.Name.Equals(GsItemText.Trim(), StringComparison.OrdinalIgnoreCase))
                    ?? _allGsItems.FirstOrDefault(g => g.Name.Contains(GsItemText.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (item == null)
            {
                MessageBox.Show("Please select or type a valid General Stock (GS) item.", "Item Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (GsQty <= 0)
            {
                MessageBox.Show("Please enter a valid quantity greater than 0.", "Invalid Quantity", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string size = !string.IsNullOrWhiteSpace(SelectedGsSize) ? SelectedGsSize.Trim() : (!string.IsNullOrWhiteSpace(item.Size) && item.Size != "—" && item.Size != "-" ? item.Size.Trim() : "Standard");

            var specific = _allGsItems.FirstOrDefault(g => g.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase) && string.Equals(g.Size ?? "", size, StringComparison.OrdinalIgnoreCase)) ?? item;

            var existing = GsConsumptionItems.FirstOrDefault(x => x.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase) && string.Equals(x.Size, size, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.Qty += GsQty;
            }
            else
            {
                GsConsumptionItems.Add(new GsConsumptionItem(specific, size, GsQty));
            }

            GsQty = 1;
            GsItemText = string.Empty;
            SelectedGsItem = null;
            SelectedGsSize = string.Empty;
            ResetGsFilter();
            OnPropertyChanged(nameof(HasGsConsumptionItems));
        }

        private async Task SubmitGsConsumptionAsync()
        {
            if (!GsConsumptionItems.Any())
            {
                MessageBox.Show("No general stock items added to deduct. Please add items using the form above.", "List Empty", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                ShowLoaderAction?.Invoke("Deducting Packaging Stock...", "Updating packaging and general supplies in live database...");
                await Task.Delay(80);

                var deductedDetails = new List<string>();

                foreach (var ci in GsConsumptionItems.ToList())
                {
                    double remainingToDeduct = ci.Qty;

                    var matches = _dataService.GeneralItems
                        .Where(g => g.Id == ci.Item.Id ||
                                   (g.Name.Equals(ci.Name, StringComparison.OrdinalIgnoreCase) &&
                                    (string.Equals(g.Size ?? "", ci.Size, StringComparison.OrdinalIgnoreCase) || ci.Size == "—" || ci.Size == "Standard")))
                        .ToList();

                    if (matches.Count == 0)
                    {
                        matches = _dataService.GeneralItems.Where(g => g.Name.Equals(ci.Name, StringComparison.OrdinalIgnoreCase)).ToList();
                    }

                    foreach (var g in matches)
                    {
                        if (remainingToDeduct <= 0) break;
                        double deduct = Math.Min(g.Stock, remainingToDeduct);
                        if (deduct <= 0) deduct = remainingToDeduct;

                        if (g.RegisteredQty <= 0 && g.Stock > 0) g.RegisteredQty = g.Stock;
                        g.Stock = Math.Max(0, Math.Round(g.Stock - deduct, 2));
                        remainingToDeduct -= deduct;
                        await _dataService.SaveGeneralItemAsync(g);
                    }

                    deductedDetails.Add($"{ci.Name} ({ci.Size}) : -{ci.Qty} {ci.Unit}");
                }

                GsConsumptionItems.Clear();
                OnPropertyChanged(nameof(HasGsConsumptionItems));
                LoadGeneralItems();

                HideLoaderAction?.Invoke();

                MessageBox.Show(
                    $"General stock deducted from inventory successfully:\n\n• {string.Join("\n• ", deductedDetails)}",
                    "GS Stock Deducted",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            finally
            {
                HideLoaderAction?.Invoke();
            }
        }
    }
}