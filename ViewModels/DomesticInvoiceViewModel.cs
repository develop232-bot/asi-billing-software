using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using AsiErp.Commands;
using AsiErp.Models;
using AsiErp.Services;

namespace AsiErp.ViewModels
{
    public class DomesticInvoiceViewModel : ViewModelBase
    {
        private readonly AppDataService _dataService;
        private readonly Action<string> _navigateAction;
        private readonly Action<string, object?> _openDialogAction;

        private string _customerName = string.Empty;
        private string _contactPerson = string.Empty;
        private string _phoneNumber = string.Empty;
        private string _customerGstin = string.Empty;
        private string _vehicleNo = string.Empty;
        private DateTime _invoiceDate = DateTime.Now;
        private string _invoiceNo = string.Empty;
        private bool _isCredit = false;
        private int _creditDays = 30;
        private double _paidNow = 0;
        private bool _isInterState = false;

        private double _subtotal;
        private double _taxAmount;
        private double _grossAmount;

        public Action<string, string>? ShowLoaderAction { get; set; }
        public Action? HideLoaderAction { get; set; }

        public List<ProductItem> AvailableProducts { get; }
        public ObservableCollection<SaleLine> Lines { get; } = new();
        public ObservableCollection<string> PartySuggestions { get; } = new();

        public string CustomerName { get => _customerName; set => SetField(ref _customerName, value); }
        public string ContactPerson { get => _contactPerson; set => SetField(ref _contactPerson, value); }
        public string PhoneNumber { get => _phoneNumber; set => SetField(ref _phoneNumber, value); }
        public string CustomerGstin { get => _customerGstin; set => SetField(ref _customerGstin, value); }
        public string VehicleNo { get => _vehicleNo; set => SetField(ref _vehicleNo, value); }
        public DateTime InvoiceDate
        {
            get => _invoiceDate;
            set
            {
                if (SetField(ref _invoiceDate, value))
                {
                    OnPropertyChanged(nameof(PaymentDueDateDisplay));
                }
            }
        }
        public string InvoiceNo { get => _invoiceNo; set => SetField(ref _invoiceNo, value); }

        public bool IsCredit
        {
            get => _isCredit;
            set
            {
                if (SetField(ref _isCredit, value))
                {
                    OnPropertyChanged(nameof(IsCash));
                    OnPropertyChanged(nameof(PaymentDueDateDisplay));
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

        public int CreditDays
        {
            get => _creditDays;
            set
            {
                if (SetField(ref _creditDays, value))
                {
                    OnPropertyChanged(nameof(PaymentDueDateDisplay));
                }
            }
        }
        public double PaidNow
        {
            get => _paidNow;
            set
            {
                if (SetField(ref _paidNow, value))
                {
                    OnPropertyChanged(nameof(BalanceAmount));
                }
            }
        }

        public bool IsInterState
        {
            get => _isInterState;
            set
            {
                if (SetField(ref _isInterState, value))
                {
                    OnPropertyChanged(nameof(TaxLabel));
                    OnPropertyChanged(nameof(CgstAmount));
                    OnPropertyChanged(nameof(SgstAmount));
                    OnPropertyChanged(nameof(IgstAmount));
                }
            }
        }

        public string TaxLabel => IsInterState ? "IGST (Other State):" : "CGST + SGST (Within State):";
        public double CgstAmount => IsInterState ? 0 : Math.Round(TaxAmount / 2.0, 2);
        public double SgstAmount => IsInterState ? 0 : Math.Round(TaxAmount / 2.0, 2);
        public double IgstAmount => IsInterState ? TaxAmount : 0;
        public double BalanceAmount => Math.Max(0, Math.Round(GrossAmount - PaidNow, 2));
        public string PaymentDueDateDisplay => IsCredit ? $"{CreditDays} days · {InvoiceDate.AddDays(CreditDays):dd MMM yyyy}" : string.Empty;

        public double Subtotal { get => _subtotal; private set => SetField(ref _subtotal, value); }
        public double TaxAmount { get => _taxAmount; private set => SetField(ref _taxAmount, value); }
        public double GrossAmount { get => _grossAmount; private set => SetField(ref _grossAmount, value); }

        private DateTime _receivedDate = DateTime.Now;
        public DateTime ReceivedDate
        {
            get => _receivedDate;
            set => SetField(ref _receivedDate, value);
        }

        private string _paymentMode = "Bank transfer";
        public string PaymentMode
        {
            get => _paymentMode;
            set => SetField(ref _paymentMode, value);
        }

        public List<string> AvailablePaymentModes { get; } = new() { "Bank transfer", "Cash", "UPI", "Cheque" };

        private string _paymentReference = string.Empty;
        public string PaymentReference
        {
            get => _paymentReference;
            set => SetField(ref _paymentReference, value);
        }

        public RelayCommand QuickFillCommand { get; }
        public RelayCommand AddLineCommand { get; }
        public RelayCommand DeleteLineCommand { get; }
        public RelayCommand SaveInvoiceCommand { get; }
        public RelayCommand BackToSalesCommand { get; }
        public RelayCommand ResetFormCommand { get; }

        public DomesticInvoiceViewModel(AppDataService dataService, Action<string> navigateAction, Action<string, object?> openDialogAction)
        {
            _dataService = dataService;
            _navigateAction = navigateAction;
            _openDialogAction = openDialogAction;

            AvailableProducts = _dataService.GetGroupedAvailableProducts();

            AddLineCommand = new RelayCommand(_ => AddLine());
            DeleteLineCommand = new RelayCommand(p =>
            {
                if (p is SaleLine line)
                {
                    Lines.Remove(line);
                    if (!Lines.Any()) AddLine();
                    Recalculate();
                }
            });

            SaveInvoiceCommand = new RelayCommand(async _ => await SaveInvoiceAsync());
            BackToSalesCommand = new RelayCommand(_ => _navigateAction("sales"));
            ResetFormCommand = new RelayCommand(_ => ResetForm());
            QuickFillCommand = new RelayCommand(p =>
            {
                if (double.TryParse(p?.ToString(), out double pct))
                {
                    PaidNow = Math.Round(GrossAmount * (pct / 100.0), 2);
                }
            });

            ResetForm();
        }

        public void ResetForm()
        {
            CustomerName = string.Empty;
            ContactPerson = string.Empty;
            PhoneNumber = string.Empty;
            CustomerGstin = string.Empty;
            VehicleNo = string.Empty;
            InvoiceDate = DateTime.Now;
            InvoiceNo = _dataService.NextDocNo("domestic", increment: false);
            IsCredit = false;
            CreditDays = 30;
            IsInterState = false;
            ReceivedDate = DateTime.Now;
            PaymentMode = "Bank transfer";
            PaymentReference = string.Empty;

            PartySuggestions.Clear();
            var pastParties = _dataService.Sales
                .Where(s => s.Type == "domestic" && !string.IsNullOrWhiteSpace(s.Party))
                .Select(s => s.Party)
                .Distinct()
                .OrderBy(x => x);
            foreach (var p in pastParties) PartySuggestions.Add(p);

            Lines.Clear();
            AddLine();
            Recalculate();
        }

        public void AddLine()
        {
            var line = new SaleLine
            {
                Pid = string.Empty,
                Name = string.Empty,
                Hsn = string.Empty,
                Size = string.Empty,
                Qty = 1,
                Unit = "pair",
                Rate = 0,
                GstPercent = 5,
                DiscPercent = 0
            };
            line.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SaleLine.Pid))
                {
                    var p = AvailableProducts.FirstOrDefault(x => x.Id == line.Pid);
                    if (p != null)
                    {
                        line.Name = p.FullDisplayName;
                        line.Hsn = p.Hsn;
                        line.Unit = p.Unit;
                        if (p.DomesticPrice > 0) line.Rate = p.DomesticPrice;
                        else if (p.RetailPrice > 0) line.Rate = p.RetailPrice;

                        line.GstPercent = line.Rate <= 2500 ? 5 : 18;

                        if (p.Sizes != null && p.Sizes.Count > 0)
                        {
                            line.AvailableSizes = p.Sizes.ToList();
                            line.Size = p.Sizes[0];
                        }
                        else
                        {
                            line.AvailableSizes = new List<string>();
                            line.Size = "—";
                        }
                    }
                }
                else if (e.PropertyName == nameof(SaleLine.Rate))
                {
                    // Auto GST rule: 5% up to ₹2500, 18% above
                    line.GstPercent = line.Rate <= 2500 ? 5 : 18;
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
                    double net = Math.Round(l.Qty * l.Rate * (1.0 - l.DiscPercent / 100.0), 2);
                    double t = Math.Round(net * (l.GstPercent / 100.0), 2);
                    l.NetAmount = net;
                    l.TaxAmount = t;
                    l.GrossAmount = Math.Round(net + t, 2);

                    sub += net;
                    tax += t;
                }

                Subtotal = sub;
                TaxAmount = tax;
                GrossAmount = Math.Round(sub + tax, 2);

                OnPropertyChanged(nameof(CgstAmount));
                OnPropertyChanged(nameof(SgstAmount));
                OnPropertyChanged(nameof(IgstAmount));
                OnPropertyChanged(nameof(BalanceAmount));
                OnPropertyChanged(nameof(PaymentDueDateDisplay));

                if (!IsCredit)
                {
                    PaidNow = GrossAmount;
                }
            }
            finally
            {
                _isRecalculating = false;
            }
        }

        private async Task SaveInvoiceAsync()
        {
            string party = CustomerName.Trim();
            string phone = PhoneNumber.Trim();

            if (string.IsNullOrWhiteSpace(party))
            {
                MessageBox.Show("Please enter the customer / company name.", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(phone))
            {
                MessageBox.Show("Please enter the contact phone number.", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var validLines = Lines.Where(l => !string.IsNullOrEmpty(l.Pid) && l.Qty > 0).ToList();
            if (!validLines.Any())
            {
                MessageBox.Show("Please add at least one item with valid quantity.", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Check inventory stock warning
            foreach (var l in validLines)
            {
                var p = AvailableProducts.FirstOrDefault(x => x.Id == l.Pid);
                if (p != null && l.Qty > p.Stock)
                {
                    var proceed = MessageBox.Show($"Only {p.Stock} {p.Unit} of {p.Name} currently in stock. Proceed with order anyway?",
                        "Stock Notice", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (proceed != MessageBoxResult.Yes) return;
                }
            }

            double subtotal = validLines.Sum(l => l.NetAmount);
            double tax = validLines.Sum(l => l.TaxAmount);
            double gross = Math.Round(subtotal + tax, 2);

            var sale = new SaleDoc
            {
                Id = "s_" + Guid.NewGuid().ToString("N")[..8],
                Type = "domestic",
                InvoiceNo = _dataService.NextDocNo("domestic", increment: true),
                Date = InvoiceDate.ToString("yyyy-MM-dd"),
                Party = party,
                ContactPerson = ContactPerson.Trim(),
                Phone = phone,
                CustomerGstin = CustomerGstin.Trim(),
                VehicleNo = VehicleNo.Trim(),
                SaleType = IsCredit ? "Credit" : "Cash",
                CreditDays = IsCredit ? CreditDays : 0,
                PaymentDueDate = IsCredit ? InvoiceDate.AddDays(CreditDays).ToString("yyyy-MM-dd") : InvoiceDate.ToString("yyyy-MM-dd"),
                PaymentMode = PaymentMode,
                Event = PaymentReference.Trim(),
                TaxType = IsInterState ? "inter" : "intra",
                NetAmount = Math.Round(subtotal, 2),
                TaxAmount = Math.Round(tax, 2),
                GrossAmount = gross,
                PaidAmount = Math.Round(Math.Min(gross, PaidNow), 2),
                Lines = validLines
            };

            try
            {
                ShowLoaderAction?.Invoke("Saving Domestic Invoice...", $"Recording invoice for '{party}' and updating inventory...");
                await Task.Delay(80);

                var recorded = await _dataService.RecordSaleAsync(sale);
                HideLoaderAction?.Invoke();

                var printRes = MessageBox.Show($"Domestic Tax Invoice '{recorded.InvoiceNo}' saved successfully!\n\nWould you like to print the tax invoice now?",
                    "Invoice Saved", MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (printRes == MessageBoxResult.Yes)
                {
                    DocumentPrinter.PrintDirect(recorded, silent: false);
                }

                // Navigate back to sales register so they see their new invoice immediately
                _navigateAction("sales");
            }
            finally
            {
                HideLoaderAction?.Invoke();
            }
        }
    }
}
