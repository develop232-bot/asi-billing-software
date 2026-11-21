using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using AsiErp.Commands;
using AsiErp.Models;
using AsiErp.Services;

namespace AsiErp.ViewModels
{
    public class SourcingItemOption
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public double Rate { get; set; }
        public double SellingPrice { get; set; }
        public double Gst { get; set; }
        public double Stock { get; set; }

        public string DisplayText => string.IsNullOrWhiteSpace(Sku) ? Name : $"{Name} ({Sku})";
        public override string ToString() => Name;
    }

    public abstract class PurchaseEntryViewModelBase : ViewModelBase
    {
        protected readonly AppDataService _dataService;
        protected readonly Action<string> _navigateAction;
        protected readonly Action<string, object?> _openDialogAction;

        private string _vendorName = string.Empty;
        private string _contactPerson = string.Empty;
        private string _vendorPhone = string.Empty;
        private string _vendorInvoiceRef = "INV-1001";
        private DateTime _billDate = DateTime.Now;
        private string _billNo = string.Empty;
        private string _vendorGstin = string.Empty;

        private bool _isCredit = false;
        private int _creditDays = 30;
        private double _paidNow = 0;

        private double _subtotal;
        private double _taxAmount;
        private double _grossAmount;

        public abstract string Kind { get; }
        public abstract string Title { get; }
        public abstract string Subtitle { get; }
        public abstract string CategoryBadge { get; }
        public abstract string DefaultUnit { get; }

        public ObservableCollection<SourcingItemOption> AvailableItems { get; } = new();
        public ObservableCollection<PurchaseLine> Lines { get; } = new();
        public ObservableCollection<PurchaseDoc> RecentBills { get; } = new();

        private bool _isSaving;
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
        public string SaveButtonText => _isSaving ? "⏳ Saving..." : "Save Purchase Bill";
        public string TopSaveButtonText => _isSaving ? "⏳ Saving..." : "Save Bill";

        public Action<string, string>? ShowLoaderAction { get; set; }
        public Action? HideLoaderAction { get; set; }

        public string VendorName { get => _vendorName; set => SetField(ref _vendorName, value); }
        public string ContactPerson { get => _contactPerson; set => SetField(ref _contactPerson, value); }
        public string VendorPhone { get => _vendorPhone; set => SetField(ref _vendorPhone, value); }
        public string VendorInvoiceRef { get => _vendorInvoiceRef; set => SetField(ref _vendorInvoiceRef, value); }
        public DateTime BillDate { get => _billDate; set => SetField(ref _billDate, value); }
        public string BillNo { get => _billNo; set => SetField(ref _billNo, value); }
        public string VendorGstin { get => _vendorGstin; set => SetField(ref _vendorGstin, value); }

        public bool IsCredit
        {
            get => _isCredit;
            set
            {
                if (SetField(ref _isCredit, value))
                {
                    OnPropertyChanged(nameof(IsCash));
                    if (value) PaidNow = 0;
                    else PaidNow = GrossAmount;
                }
            }
        }

        public bool IsCash
        {
            get => !_isCredit;
            set
            {
                if (value) IsCredit = false;
            }
        }

        public int CreditDays { get => _creditDays; set => SetField(ref _creditDays, value); }
        public double PaidNow { get => _paidNow; set => SetField(ref _paidNow, value); }
        public double BalancePayable => Math.Max(0, Math.Round(GrossAmount - PaidNow, 2));

        public double Subtotal { get => _subtotal; protected set => SetField(ref _subtotal, value); }
        public double TaxAmount { get => _taxAmount; protected set => SetField(ref _taxAmount, value); }
        public double GrossAmount
        {
            get => _grossAmount;
            protected set
            {
                if (SetField(ref _grossAmount, value))
                {
                    OnPropertyChanged(nameof(BalancePayable));
                }
            }
        }

        public RelayCommand AddLineCommand { get; }
        public RelayCommand DeleteLineCommand { get; }
        public RelayCommand ResetFormCommand { get; }
        public RelayCommand SaveBillCommand { get; }
        public RelayCommand BackToPurchasesCommand { get; }
        public RelayCommand ViewBillCommand { get; }
        public RelayCommand PayBillCommand { get; }
        public RelayCommand DeleteBillCommand { get; }

        public PurchaseEntryViewModelBase(AppDataService dataService, Action<string> navigateAction, Action<string, object?> openDialogAction)
        {
            _dataService = dataService;
            _navigateAction = navigateAction;
            _openDialogAction = openDialogAction;

            AddLineCommand = new RelayCommand(_ => AddLine());
            DeleteLineCommand = new RelayCommand(p =>
            {
                if (p is PurchaseLine line)
                {
                    Lines.Remove(line);
                    if (!Lines.Any()) AddLine();
                    Recalculate();
                }
            });

            ResetFormCommand = new RelayCommand(_ => ResetForm());
            SaveBillCommand = new RelayCommand(async _ => await SaveBillAsync());
            BackToPurchasesCommand = new RelayCommand(_ => _navigateAction("pur"));

            ViewBillCommand = new RelayCommand(p =>
            {
                if (p is PurchaseDoc doc) _openDialogAction("view_purchase_doc", doc);
            });

            PayBillCommand = new RelayCommand(p =>
            {
                if (p is PurchaseDoc doc) _openDialogAction("payment_purchase", doc);
            });

            DeleteBillCommand = new RelayCommand(async p =>
            {
                if (p is PurchaseDoc doc)
                {
                    var res = MessageBox.Show(
                        $"Are you sure you want to delete Inward Bill '{doc.BillNo}' from '{doc.Party}'?",
                        "Delete Inward Bill",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                    if (res == MessageBoxResult.Yes)
                    {
                        await _dataService.DeletePurchaseAsync(doc.Id);
                        RefreshRecentBills();
                    }
                }
            });

            _dataService.DataChanged += () =>
            {
                ReloadAvailableItems();
                RefreshRecentBills();
            };
        }

        public abstract void ReloadAvailableItems();

        public virtual void ResetForm()
        {
            VendorName = string.Empty;
            ContactPerson = string.Empty;
            VendorPhone = string.Empty;
            VendorInvoiceRef = "INV-1001";
            BillDate = DateTime.Now;
            BillNo = _dataService.NextDocNo(Kind, increment: false);
            VendorGstin = string.Empty;
            IsCredit = false;
            CreditDays = 30;

            ReloadAvailableItems();
            RefreshRecentBills();

            Lines.Clear();
            AddLine();
            Recalculate();
        }

        public virtual void RefreshRecentBills()
        {
            RecentBills.Clear();
            var bills = _dataService.Purchases
                .Where(p => p.Kind.Equals(Kind, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(p => p.Date)
                .ThenByDescending(p => p.BillNo)
                .Take(25);

            foreach (var b in bills)
            {
                RecentBills.Add(b);
            }
        }

        public virtual void AddLine()
        {
            var line = new PurchaseLine
            {
                ItemId = string.Empty,
                Name = string.Empty,
                Unit = DefaultUnit,
                Qty = 1,
                Rate = 0,
                GstPercent = 0,
                Size = Kind == "ready" ? "Free" : "",
                SellingPrice = 0
            };
            line.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(PurchaseLine.ItemId))
                {
                    if (!string.IsNullOrWhiteSpace(line.ItemId) && string.IsNullOrWhiteSpace(line.Name))
                    {
                        var opt = AvailableItems.FirstOrDefault(x => x.Id == line.ItemId);
                        if (opt != null)
                        {
                            line.Name = opt.Name;
                            if (string.IsNullOrWhiteSpace(line.Unit)) line.Unit = !string.IsNullOrWhiteSpace(opt.Unit) ? opt.Unit : DefaultUnit;
                            if (string.IsNullOrWhiteSpace(line.Sku)) line.Sku = opt.Sku;
                            if (opt.SellingPrice > 0 && line.SellingPrice <= 0) line.SellingPrice = opt.SellingPrice;
                        }
                    }
                }
                Recalculate();
            };
            Lines.Add(line);
            Recalculate();
        }

        private bool _isRecalculating;
        public void Recalculate()
        {
            if (_isRecalculating) return;
            _isRecalculating = true;
            try
            {
                double sub = 0;
                double tax = 0;

                foreach (var l in Lines)
                {
                    l.RecalculateLine();
                    sub += l.NetAmount;
                    tax += l.TaxAmount;
                }

                Subtotal = Math.Round(sub, 2);
                TaxAmount = Math.Round(tax, 2);
                GrossAmount = Math.Round(sub + tax, 2);

                if (!IsCredit)
                {
                    PaidNow = GrossAmount;
                }
                OnPropertyChanged(nameof(BalancePayable));
            }
            finally
            {
                _isRecalculating = false;
            }
        }

        public virtual async Task SaveBillAsync()
        {
            if (IsSaving) return;

            string party = VendorName.Trim();
            if (string.IsNullOrWhiteSpace(party))
            {
                MessageBox.Show("Please enter the vendor / supplier name.", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var validLines = Lines.Where(l => !string.IsNullOrWhiteSpace(l.Name) && l.Qty > 0).ToList();
            if (!validLines.Any())
            {
                MessageBox.Show("Please add at least one item with a valid name and quantity.", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsSaving = true;
            ShowLoaderAction?.Invoke("Saving Purchase Bill...", $"Recording inward bill for {party} & updating inventory...");

            try
            {
                foreach (var l in validLines)
                {
                    string trimmedName = l.Name.Trim();
                    l.Name = trimmedName;

                    if (string.IsNullOrWhiteSpace(l.ItemId))
                    {
                        l.ItemId = "pur_it_" + Guid.NewGuid().ToString("N")[..8];
                    }
                    if (string.IsNullOrWhiteSpace(l.Unit))
                    {
                        l.Unit = Kind switch
                        {
                            "raw" => "sq ft",
                            "ready" => "pair",
                            _ => "pcs"
                        };
                    }
                    l.RecalculateLine();
                }

                double subtotal = validLines.Sum(l => l.NetAmount);
                double tax = validLines.Sum(l => l.TaxAmount);
                double gross = subtotal + tax;

                bool isCredit = IsCredit;
                int creditDays = isCredit ? CreditDays : 0;
                double paidNow = Math.Min(gross, Math.Max(0, PaidNow));

                var purchase = new PurchaseDoc
                {
                    Id = "b_" + Guid.NewGuid().ToString("N")[..8],
                    Kind = Kind,
                    BillNo = _dataService.NextDocNo(Kind, increment: true),
                    VendorBillRef = VendorInvoiceRef.Trim(),
                    Date = BillDate.ToString("yyyy-MM-dd"),
                    Party = party,
                    ContactPerson = ContactPerson.Trim(),
                    Phone = VendorPhone.Trim(),
                    SaleType = isCredit ? "Credit" : "Cash",
                    CreditDays = creditDays,
                    PaymentDueDate = isCredit ? BillDate.AddDays(creditDays).ToString("yyyy-MM-dd") : BillDate.ToString("yyyy-MM-dd"),
                    NetAmount = Math.Round(subtotal, 2),
                    TaxAmount = Math.Round(tax, 2),
                    GrossAmount = Math.Round(gross, 2),
                    PaidAmount = Math.Round(paidNow, 2),
                    Lines = validLines
                };

                await _dataService.RecordPurchaseAsync(purchase);

                HideLoaderAction?.Invoke();

                var askPrint = MessageBox.Show(
                    $"Purchase Bill '{purchase.BillNo}' recorded successfully!\n\nVendor: {purchase.Party}\nGross: ₹{purchase.GrossAmount:N2}\nStatus: {purchase.Status}\n\nWould you like to print / preview the inward bill voucher now?",
                    "Inward Bill Saved",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (askPrint == MessageBoxResult.Yes)
                {
                    _openDialogAction("view_purchase_doc", purchase);
                }

                ResetForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save purchase bill: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                HideLoaderAction?.Invoke();
                IsSaving = false;
            }
        }
    }
}
