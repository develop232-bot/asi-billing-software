using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
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

    public class PosViewModel : ViewModelBase
    {
        private readonly AppDataService _dataService;
        private readonly Action<string, object?> _openDialogAction;
        private readonly Action<string>? _navigateAction;

        private string _searchQuery = string.Empty;
        private string _scannedBarcode = string.Empty;
        private string _selectedCategory = "all";
        private ProductItem? _quickAddItem;
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

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetField(ref _searchQuery, value)) ApplyProductFilter();
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

        public ProductItem? QuickAddItem
        {
            get => _quickAddItem;
            set => SetField(ref _quickAddItem, value);
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

        public PosViewModel(AppDataService dataService, Action<string, object?> openDialogAction, Action<string>? navigateAction = null)
        {
            _dataService = dataService;
            _openDialogAction = openDialogAction;
            _navigateAction = navigateAction;

            BackToSalesCommand = new RelayCommand(_ => _navigateAction?.Invoke("sales"));

            SetCategoryCommand = new RelayCommand(p => SelectedCategory = p?.ToString() ?? "all");

            QuickAddCommand = new RelayCommand(_ =>
            {
                if (QuickAddItem != null)
                {
                    AddProductToCart(QuickAddItem, QuickAddQty);
                    QuickAddQty = 1;
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

            _dataService.DataChanged += LoadData;
            LoadData();
        }

        public void LoadData()
        {
            AvailableProducts.Clear();
            foreach (var p in _dataService.Products.Where(x => x.Stock > 0))
                AvailableProducts.Add(p);

            ApplyProductFilter();

            RecentBills.Clear();
            foreach (var s in _dataService.Sales.Where(x => x.Type == "retail").Take(12))
                RecentBills.Add(s);

            OnPropertyChanged(nameof(NextBillNo));
        }

        private void ApplyProductFilter()
        {
            FilteredProducts.Clear();
            string q = SearchQuery.Trim().ToLowerInvariant();

            foreach (var p in _dataService.Products)
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
            if (prod.Stock <= 0)
            {
                MessageBox.Show($"{prod.Name} is out of stock.", "Out of Stock", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string defaultSize = prod.Sizes?.FirstOrDefault() ?? "";
            var existing = CartItems.FirstOrDefault(x => x.Product.Id == prod.Id && x.SelectedSize == defaultSize);

            if (existing != null)
            {
                if (existing.Qty + qty <= prod.Stock)
                {
                    existing.Qty += qty;
                }
                else
                {
                    MessageBox.Show($"Only {prod.Stock} {prod.Unit} of {prod.Name} in stock.", "Stock Limit", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                int addQty = Math.Min(qty, (int)prod.Stock);
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

            // Clear cart
            CartItems.Clear();
            DiscountPercent = 0;
            CustomerName = string.Empty;
            CustomerPhone = string.Empty;
            RecalculateCart();

            // Directly print without preview window
            DocumentPrinter.PrintDirect(newSale, silent: true);
        }
    }
}