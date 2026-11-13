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
        private DateTime _invoiceDate = DateTime.Now;
        private string _invoiceNo = string.Empty;
        private bool _isCredit = false;
        private int _creditDays = 30;
        private double _paidNow = 0;
        private bool _isInterState = false;

        private double _subtotal;
        private double _taxAmount;
        private double _grossAmount;

        public List<ProductItem> AvailableProducts { get; }
        public ObservableCollection<SaleLine> Lines { get; } = new();

        public string CustomerName { get => _customerName; set => SetField(ref _customerName, value); }
        public string ContactPerson { get => _contactPerson; set => SetField(ref _contactPerson, value); }
        public string PhoneNumber { get => _phoneNumber; set => SetField(ref _phoneNumber, value); }
        public string CustomerGstin { get => _customerGstin; set => SetField(ref _customerGstin, value); }
        public DateTime InvoiceDate { get => _invoiceDate; set => SetField(ref _invoiceDate, value); }
        public string InvoiceNo { get => _invoiceNo; set => SetField(ref _invoiceNo, value); }

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

        public bool IsInterState
        {
            get => _isInterState;
            set
            {
                if (SetField(ref _isInterState, value))
                {
                    OnPropertyChanged(nameof(TaxLabel));
                }
            }
        }

        public string TaxLabel => IsInterState ? "IGST (100% Inter-State):" : "CGST + SGST (Intra-State):";

        public double Subtotal { get => _subtotal; private set => SetField(ref _subtotal, value); }
        public double TaxAmount { get => _taxAmount; private set => SetField(ref _taxAmount, value); }
        public double GrossAmount { get => _grossAmount; private set => SetField(ref _grossAmount, value); }

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

            AvailableProducts = _dataService.Products.ToList();

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

            ResetForm();
        }

        public void ResetForm()
        {
            CustomerName = string.Empty;
            ContactPerson = string.Empty;
            PhoneNumber = string.Empty;
            CustomerGstin = string.Empty;
            InvoiceDate = DateTime.Now;
            InvoiceNo = _dataService.NextDocNo("domestic", increment: false);
            IsCredit = false;
            CreditDays = 30;
            IsInterState = false;

            Lines.Clear();
            AddLine();
            Recalculate();
        }

        public void AddLine()
        {
            var first = AvailableProducts.FirstOrDefault();
            var line = new SaleLine
            {
                Pid = first?.Id ?? "prd_1",
                Name = first?.FullDisplayName ?? "School Shoe – Black Lace-up (ASI-SH-001)",
                Hsn = first?.Hsn ?? "6403",
                Size = first?.Sizes?.FirstOrDefault() ?? "8",
                Qty = 10,
                Unit = first?.Unit ?? "pair",
                Rate = first?.DomesticPrice > 0 ? first.DomesticPrice : 310,
                GstPercent = first?.AutoGstRate > 0 ? first.AutoGstRate : 5,
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
                        if (p.AutoGstRate > 0) line.GstPercent = p.AutoGstRate;
                        if (p.Sizes != null && p.Sizes.Count > 0) line.Size = p.Sizes[0];
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
                SaleType = IsCredit ? "Credit" : "Cash",
                CreditDays = IsCredit ? CreditDays : 0,
                PaymentDueDate = IsCredit ? InvoiceDate.AddDays(CreditDays).ToString("yyyy-MM-dd") : InvoiceDate.ToString("yyyy-MM-dd"),
                TaxType = IsInterState ? "inter" : "intra",
                NetAmount = Math.Round(subtotal, 2),
                TaxAmount = Math.Round(tax, 2),
                GrossAmount = gross,
                PaidAmount = Math.Round(Math.Min(gross, PaidNow), 2),
                Lines = validLines
            };

            var recorded = await _dataService.RecordSaleAsync(sale);

            var printRes = MessageBox.Show($"Domestic Tax Invoice '{recorded.InvoiceNo}' saved successfully!\n\nWould you like to print the tax invoice now?",
                "Invoice Saved", MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (printRes == MessageBoxResult.Yes)
            {
                DocumentPrinter.PrintDirect(recorded, silent: false);
            }

            // Navigate back to sales register so they see their new invoice immediately
            _navigateAction("sales");
        }
    }
}
