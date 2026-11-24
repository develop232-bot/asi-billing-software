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
    public class ExportInvoiceViewModel : ViewModelBase
    {
        private readonly AppDataService _dataService;
        private readonly Action<string> _navigateAction;
        private readonly Action<string, object?> _openDialogAction;

        private string _buyerName = string.Empty;
        private string _contactPerson = string.Empty;
        private string _phoneNumber = string.Empty;
        private DateTime _invoiceDate = DateTime.Now;
        private string _invoiceNo = string.Empty;
        private string _country = "UAE";
        private string _currency = "AED";
        private double _fxRate = 24.10;
        private string _incoterm = "FOB";
        private string _portOfLoading = "Chennai Port";
        private string _portOfDischarge = "Jebel Ali Port";

        private bool _isCredit = true;
        private int _creditDays = 45;
        private double _paidNow = 0;

        private double _subtotalCurrency;
        private double _grossAmountCurrency;
        private double _grossAmountInr;

        public List<ProductItem> AvailableProducts { get; }
        public ObservableCollection<SaleLine> Lines { get; } = new();
        public ObservableCollection<string> BuyerSuggestions { get; } = new();

        public string BuyerName { get => _buyerName; set => SetField(ref _buyerName, value); }
        public string ContactPerson { get => _contactPerson; set => SetField(ref _contactPerson, value); }
        public string PhoneNumber { get => _phoneNumber; set => SetField(ref _phoneNumber, value); }
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

        public string Country
        {
            get => _country;
            set
            {
                if (SetField(ref _country, value))
                {
                    UpdateDefaultCurrencyForCountry(value);
                }
            }
        }

        public string Currency
        {
            get => _currency;
            set
            {
                if (SetField(ref _currency, value))
                {
                    UpdateDefaultFxRate(value);
                    Recalculate();
                }
            }
        }

        public double FxRate
        {
            get => _fxRate;
            set
            {
                if (SetField(ref _fxRate, value))
                {
                    Recalculate();
                }
            }
        }

        public string Incoterm { get => _incoterm; set => SetField(ref _incoterm, value); }
        public string PortOfLoading { get => _portOfLoading; set => SetField(ref _portOfLoading, value); }
        public string PortOfDischarge { get => _portOfDischarge; set => SetField(ref _portOfDischarge, value); }

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
                    else PaidNow = GrossAmountCurrency;
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
                    OnPropertyChanged(nameof(BalanceAmountCurrency));
                    OnPropertyChanged(nameof(BalanceAmountInr));
                }
            }
        }

        public double SubtotalCurrency { get => _subtotalCurrency; private set => SetField(ref _subtotalCurrency, value); }
        public double GrossAmountCurrency { get => _grossAmountCurrency; private set => SetField(ref _grossAmountCurrency, value); }
        public double GrossAmountInr { get => _grossAmountInr; private set => SetField(ref _grossAmountInr, value); }
        public double BalanceAmountCurrency => Math.Max(0, Math.Round(GrossAmountCurrency - PaidNow, 2));
        public double BalanceAmountInr => Math.Max(0, Math.Round(GrossAmountInr - (PaidNow * FxRate), 2));
        public string PaymentDueDateDisplay => IsCredit ? $"{CreditDays} days · {InvoiceDate.AddDays(CreditDays):dd MMM yyyy}" : string.Empty;

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

        public ExportInvoiceViewModel(AppDataService dataService, Action<string> navigateAction, Action<string, object?> openDialogAction)
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
                    PaidNow = Math.Round(GrossAmountCurrency * (pct / 100.0), 2);
                }
            });

            ResetForm();
        }

        private void UpdateDefaultCurrencyForCountry(string c)
        {
            switch (c)
            {
                case "UAE": Currency = "AED"; FxRate = 24.10; PortOfDischarge = "Jebel Ali Port"; break;
                case "Saudi Arabia": Currency = "SAR"; FxRate = 23.60; PortOfDischarge = "Dammam / Jeddah Port"; break;
                case "Qatar": Currency = "QAR"; FxRate = 24.30; PortOfDischarge = "Hamad Port, Doha"; break;
                case "Oman": Currency = "OMR"; FxRate = 230.00; PortOfDischarge = "Sohar Port"; break;
                case "Kuwait": Currency = "KWD"; FxRate = 288.00; PortOfDischarge = "Shuwaikh Port"; break;
                case "Bahrain": Currency = "BHD"; FxRate = 235.00; PortOfDischarge = "Khalifa Bin Salman Port"; break;
                case "Other (USD)": Currency = "USD"; FxRate = 88.50; PortOfDischarge = "New York / Los Angeles"; break;
                default: Currency = "USD"; FxRate = 88.50; break;
            }
        }

        private void UpdateDefaultFxRate(string cur)
        {
            FxRate = cur switch
            {
                "AED" => 24.10,
                "SAR" => 23.60,
                "QAR" => 24.30,
                "OMR" => 230.00,
                "KWD" => 288.00,
                "BHD" => 235.00,
                "USD" => 88.50,
                _ => 88.50
            };
        }

        public void ResetForm()
        {
            BuyerName = string.Empty;
            ContactPerson = string.Empty;
            PhoneNumber = string.Empty;
            InvoiceDate = DateTime.Now;
            InvoiceNo = _dataService.NextDocNo("export", increment: false);
            Country = "UAE";
            Incoterm = "FOB";
            PortOfLoading = "Chennai";
            IsCredit = true;
            CreditDays = 45;
            ReceivedDate = DateTime.Now;
            PaymentMode = "Bank transfer";
            PaymentReference = string.Empty;

            BuyerSuggestions.Clear();
            var pastBuyers = _dataService.Sales
                .Where(s => s.Type == "export" && !string.IsNullOrWhiteSpace(s.Party))
                .Select(s => s.Party)
                .Distinct()
                .OrderBy(x => x);
            foreach (var b in pastBuyers) BuyerSuggestions.Add(b);

            Lines.Clear();
            AddLine();
            Recalculate();
        }

        public void AddLine()
        {
            double fx = FxRate > 0 ? FxRate : 24.10;
            var line = new SaleLine
            {
                Pid = string.Empty,
                Name = string.Empty,
                Hsn = string.Empty,
                Size = string.Empty,
                Qty = 1,
                Unit = "pair",
                Rate = 0,
                GstPercent = 0, // Zero-rated under LUT export
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
                        double price = 0;
                        if (p.ExportPrice > 0)
                        {
                            price = Currency == "USD" ? p.ExportPrice : Math.Round(p.ExportPrice * 88.50 / fx, 2);
                        }
                        else
                        {
                            double inr = p.DomesticPrice > 0 ? p.DomesticPrice : (p.RetailPrice > 0 ? p.RetailPrice : 300);
                            price = Math.Round(inr / fx, 2);
                        }
                        if (price > 0) line.Rate = price;
                        line.GstPercent = 0; // LUT export

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
                double subCur = 0;
                double fx = FxRate > 0 ? FxRate : 1.0;

                foreach (var l in Lines)
                {
                    double lineCur = Math.Round(l.Qty * l.Rate * (1.0 - l.DiscPercent / 100.0), 2);
                    l.NetAmount = Math.Round(lineCur * fx, 2);
                    l.TaxAmount = 0; // LUT Zero-rated
                    l.GrossAmount = l.NetAmount;

                    subCur += lineCur;
                }

                SubtotalCurrency = subCur;
                GrossAmountCurrency = subCur;
                GrossAmountInr = Math.Round(subCur * fx, 2);

                OnPropertyChanged(nameof(BalanceAmountCurrency));
                OnPropertyChanged(nameof(BalanceAmountInr));
                OnPropertyChanged(nameof(PaymentDueDateDisplay));

                if (!IsCredit)
                {
                    PaidNow = GrossAmountCurrency;
                }
            }
            finally
            {
                _isRecalculating = false;
            }
        }

        private async Task SaveInvoiceAsync()
        {
            string party = BuyerName.Trim();
            string phone = PhoneNumber.Trim();

            if (string.IsNullOrWhiteSpace(party))
            {
                MessageBox.Show("Please enter the buyer / company name.", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
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

            double subtotalInr = validLines.Sum(l => l.NetAmount);
            double grossInr = subtotalInr;

            var sale = new SaleDoc
            {
                Id = "s_" + Guid.NewGuid().ToString("N")[..8],
                Type = "export",
                InvoiceNo = _dataService.NextDocNo("export", increment: true),
                Date = InvoiceDate.ToString("yyyy-MM-dd"),
                Party = party,
                ContactPerson = ContactPerson.Trim(),
                Phone = phone,
                Country = Country,
                Currency = Currency,
                FxRate = FxRate,
                Incoterm = Incoterm,
                PortOfLoading = PortOfLoading,
                PortOfDischarge = PortOfDischarge,
                SaleType = IsCredit ? "Credit" : "Cash",
                CreditDays = IsCredit ? CreditDays : 0,
                PaymentDueDate = IsCredit ? InvoiceDate.AddDays(CreditDays).ToString("yyyy-MM-dd") : InvoiceDate.ToString("yyyy-MM-dd"),
                PaymentMode = PaymentMode,
                Event = PaymentReference.Trim(),
                TaxType = "export_lut",
                NetAmount = Math.Round(subtotalInr, 2),
                TaxAmount = 0,
                GrossAmount = Math.Round(grossInr, 2),
                GrossAmountCurrency = Math.Round(GrossAmountCurrency, 2),
                PaidAmount = Math.Round(Math.Min(grossInr, PaidNow), 2),
                Lines = validLines
            };

            var recorded = await _dataService.RecordSaleAsync(sale);

            var printRes = MessageBox.Show($"Export Commercial Invoice '{recorded.InvoiceNo}' saved successfully!\n\nWould you like to print / preview the invoice now?",
                "Invoice Saved", MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (printRes == MessageBoxResult.Yes)
            {
                DocumentPrinter.PrintDirect(recorded, silent: false);
            }

            _navigateAction("sales");
        }
    }
}
