using System;
using System.Collections.ObjectModel;
using System.Linq;
using AsiErp.Commands;
using AsiErp.Models;
using AsiErp.Services;

namespace AsiErp.ViewModels
{
    public class PurchaseViewModel : ViewModelBase
    {
        private readonly AppDataService _dataService;
        private readonly Action<string, object?> _openDialogAction;

        private string _activeKind = "raw"; // raw, ready, gen
        private string _selectedStatus = "all"; // all, Paid, Partial, Unpaid
        private string _searchQuery = string.Empty;

        public ObservableCollection<PurchaseDoc> FilteredPurchases { get; } = new();

        public string ActiveKind
        {
            get => _activeKind;
            set
            {
                if (SetField(ref _activeKind, value))
                {
                    ApplyFilter();
                    OnPropertyChanged(nameof(Title));
                    OnPropertyChanged(nameof(Subtitle));
                    OnPropertyChanged(nameof(BoughtThisMonth));
                    OnPropertyChanged(nameof(ToPayVendors));
                    OnPropertyChanged(nameof(OpenBillsCount));
                    OnPropertyChanged(nameof(BillsToDate));
                    OnPropertyChanged(nameof(UniqueVendorsCount));
                }
            }
        }

        public string SelectedStatus
        {
            get => _selectedStatus;
            set
            {
                if (SetField(ref _selectedStatus, value)) ApplyFilter();
            }
        }

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetField(ref _searchQuery, value)) ApplyFilter();
            }
        }

        public string Title => ActiveKind switch
        {
            "raw" => "Purchase · Raw Material",
            "ready" => "Purchase · Readymade Products",
            "gen" => "Purchase · General Packaging",
            _ => "Purchase Register"
        };

        public string Subtitle => ActiveKind switch
        {
            "raw" => "Raw materials purchased from tanneries, fabric mills, sole manufacturers and trim vendors.",
            "ready" => "Finished shoes and uniforms bought from vendors for trading or resale.",
            "gen" => "Boxes, plastic covers, hang tags, tape and packing supplies.",
            _ => "Vendor bills register."
        };

        public double BoughtThisMonth
        {
            get
            {
                string f = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).ToString("yyyy-MM-dd");
                return _dataService.Purchases
                    .Where(p => p.Kind == ActiveKind && string.Compare(p.Date, f) >= 0)
                    .Sum(p => p.GrossAmount);
            }
        }

        public double ToPayVendors => _dataService.Purchases
            .Where(p => p.Kind == ActiveKind && p.Status != "Paid")
            .Sum(p => p.BalanceAmount);

        public int OpenBillsCount => _dataService.Purchases
            .Count(p => p.Kind == ActiveKind && p.Status != "Paid");

        public int BillsToDate => _dataService.Purchases.Count(p => p.Kind == ActiveKind);

        public int UniqueVendorsCount => _dataService.Purchases
            .Where(p => p.Kind == ActiveKind)
            .Select(p => p.Party)
            .Distinct()
            .Count();

        public RelayCommand SetKindCommand { get; }
        public RelayCommand SetStatusCommand { get; }
        public RelayCommand NewPurchaseBillCommand { get; }
        public RelayCommand ViewPurchaseCommand { get; }
        public RelayCommand PayBillCommand { get; }
        public RelayCommand ExportCsvCommand { get; }

        public PurchaseViewModel(AppDataService dataService, Action<string, object?> openDialogAction, string initialKind = "raw")
        {
            _dataService = dataService;
            _openDialogAction = openDialogAction;
            _activeKind = initialKind;

            SetKindCommand = new RelayCommand(p => ActiveKind = p?.ToString() ?? "raw");
            SetStatusCommand = new RelayCommand(p => SelectedStatus = p?.ToString() ?? "all");

            NewPurchaseBillCommand = new RelayCommand(_ => _openDialogAction("purchase_bill", ActiveKind));

            ViewPurchaseCommand = new RelayCommand(p =>
            {
                if (p is PurchaseDoc doc) _openDialogAction("view_purchase_doc", doc);
            });

            PayBillCommand = new RelayCommand(p =>
            {
                if (p is PurchaseDoc doc) _openDialogAction("payment_purchase", doc);
            });

            ExportCsvCommand = new RelayCommand(_ => ExportCsv());

            _dataService.DataChanged += RefreshData;
            RefreshData();
        }

        public void RefreshData()
        {
            ApplyFilter();
            OnPropertyChanged(nameof(BoughtThisMonth));
            OnPropertyChanged(nameof(ToPayVendors));
            OnPropertyChanged(nameof(OpenBillsCount));
            OnPropertyChanged(nameof(BillsToDate));
            OnPropertyChanged(nameof(UniqueVendorsCount));
        }

        private void ApplyFilter()
        {
            FilteredPurchases.Clear();
            string q = SearchQuery.Trim().ToLowerInvariant();

            foreach (var b in _dataService.Purchases.Where(x => x.Kind == ActiveKind))
            {
                bool matchStat = SelectedStatus == "all" || b.Status.Equals(SelectedStatus, StringComparison.OrdinalIgnoreCase);
                bool matchQ = string.IsNullOrEmpty(q) || (b.BillNo + b.Party + b.VendorBillRef).ToLowerInvariant().Contains(q);

                if (matchStat && matchQ)
                {
                    FilteredPurchases.Add(b);
                }
            }
        }

        private void ExportCsv()
        {
            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export Purchase Bills to CSV",
                Filter = "CSV Files (*.csv)|*.csv",
                FileName = $"ASI_{ActiveKind.ToUpper()}_Purchases_{DateTime.Now:yyyyMMdd}.csv"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    var sb = new System.Text.StringBuilder();
                    // Professional CSV Header with proper column formatting
                    sb.AppendLine("Bill No,Bill Date,Category,Vendor Name,Vendor Bill Ref,Vendor Phone,Payment Terms,Taxable Net Amount (Rs),GST Input Tax (Rs),Total Bill Amount (Rs),Paid Amount (Rs),Balance Due (Rs),Status,Items Purchased");
                    
                    foreach (var b in FilteredPurchases)
                    {
                        string catName = b.Kind switch
                        {
                            "raw" => "Raw Material",
                            "ready" => "Readymade Product",
                            "gen" => "Packaging & General",
                            _ => b.Kind
                        };

                        string itemsSummary = b.Lines != null && b.Lines.Any()
                            ? string.Join("; ", b.Lines.Select(l => $"{l.Name} (Qty: {l.Qty} {l.Unit} @ Rs.{l.Rate:F2})"))
                            : "";

                        sb.AppendLine($"\"{EscapeCsv(b.BillNo)}\",\"{b.Date}\",\"{catName}\",\"{EscapeCsv(b.Party)}\",\"{EscapeCsv(b.VendorBillRef)}\",\"{EscapeCsv(b.Phone)}\",\"{EscapeCsv(b.SaleType)}\",{b.NetAmount:F2},{b.TaxAmount:F2},{b.GrossAmount:F2},{b.PaidAmount:F2},{b.BalanceAmount:F2},\"{EscapeCsv(b.Status)}\",\"{EscapeCsv(itemsSummary)}\"");
                    }

                    // Use UTF-8 with BOM so Excel opens it with correct formatting
                    var utf8WithBom = new System.Text.UTF8Encoding(true);
                    System.IO.File.WriteAllText(sfd.FileName, sb.ToString(), utf8WithBom);

                    var res = System.Windows.MessageBox.Show($"Purchase bills CSV exported successfully to:\n{sfd.FileName}\n\nWould you like to open the file now?",
                        "Export Complete", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Information);

                    if (res == System.Windows.MessageBoxResult.Yes)
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = sfd.FileName,
                            UseShellExecute = true
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Could not export CSV file: {ex.Message}", "Export Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        private static string EscapeCsv(string? val) => (val ?? string.Empty).Replace("\"", "\"\"");
    }
}
