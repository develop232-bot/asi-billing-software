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

        public string BuyerName { get => _buyerName; set => SetField(ref _buyerName, value); }
        public string ContactPerson { get => _contactPerson; set => SetField(ref _contactPerson, value); }
        public string PhoneNumber { get => _phoneNumber; set => SetField(ref _phoneNumber, value); }
        public DateTime InvoiceDate { get => _invoiceDate; set => SetField(ref _invoiceDate, value); }
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
                    if (value) PaidNow = 0;
                    else PaidNow = GrossAmountInr;
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

        public double SubtotalCurrency { get => _subtotalCurrency; private set => SetField(ref _subtotalCurrency, value); }
        public double GrossAmountCurrency { get => _grossAmountCurrency; private set => SetField(ref _grossAmountCurrency, value); }
        public double GrossAmountInr { get => _grossAmountInr; private set => SetField(ref _grossAmountInr, value); }

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

        private void UpdateDefaultCurrencyForCountry(string c)
        {
            switch (c)
            {
                case "UAE": Currency = "AED"; PortOfDischarge = "Jebel Ali Port"; break;
                case "Saudi Arabia": Currency = "SAR"; PortOfDischarge = "Dammam Port / Jeddah"; break;
                case "Qatar": Currency = "QAR"; PortOfDischarge = "Hamad Port, Doha"; break;
                case "Oman": Currency = "OMR"; PortOfDischarge = "Sohar Port"; break;
                case "Kuwait": Currency = "KWD"; PortOfDischarge = "Shuwaikh Port"; break;
                case "Bahrain": Currency = "BHD"; PortOfDischarge = "Khalifa Bin Salman Port"; break;
                case "United States": Currency = "USD"; PortOfDischarge = "New York / Los Angeles"; break;
                case "United Kingdom": Currency = "GBP"; PortOfDischarge = "Felixstowe / London Gateway"; break;
                case "Europe": Currency = "EUR"; PortOfDischarge = "Rotterdam / Hamburg"; break;
                default: Currency = "USD"; break;
            }
        }

        private void UpdateDefaultFxRate(string cur)
        {
            FxRate = cur switch
            {
                "AED" => 24.10,
                "SAR" => 23.50,
                "QAR" => 23.80,
                "USD" => 87.20,
                "EUR" => 93.50,
                "GBP" => 110.00,
                "OMR" => 226.00,
                "KWD" => 283.00,
                "BHD" => 231.00,
                _ => 87.20
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
            PortOfLoading = "Chennai Port";
            IsCredit = true;
            CreditDays = 45;

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

                if (!IsCredit)
                {
                    PaidNow = GrossAmountInr;
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
