using System;
using System.Collections.ObjectModel;
using System.Linq;
using AsiErp.Commands;
using AsiErp.Models;
using AsiErp.Services;

namespace AsiErp.ViewModels
{
    public class SalesViewModel : ViewModelBase
    {
        private readonly AppDataService _dataService;
        private readonly Action<string> _navigateAction;
        private readonly Action<string, object?> _openDialogAction;

        private string _searchQuery = string.Empty;
        private string _selectedChannel = "all"; // all, retail, domestic, export
        private string _selectedStatus = "all"; // all, Paid, Partial, Unpaid

        public ObservableCollection<SaleDoc> FilteredSales { get; } = new();

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetField(ref _searchQuery, value)) ApplyFilter();
            }
        }

        public string SelectedChannel
        {
            get => _selectedChannel;
            set
            {
                if (SetField(ref _selectedChannel, value)) ApplyFilter();
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

        public double BilledThisMonth
        {
            get
            {
                string f = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).ToString("yyyy-MM-dd");
                return _dataService.Sales.Where(s => string.Compare(s.Date, f) >= 0).Sum(s => s.GrossAmount);
            }
        }

        public double OutstandingTotal => _dataService.Sales.Where(s => s.Status != "Paid").Sum(s => s.BalanceAmount);
        public int OutstandingCount => _dataService.Sales.Count(s => s.Status != "Paid");
        public int TotalInvoicesCount => _dataService.Sales.Count;

        public int RetailCount => _dataService.Sales.Count(s => s.Type == "retail");
        public int DomesticCount => _dataService.Sales.Count(s => s.Type == "domestic");
        public int ExportCount => _dataService.Sales.Count(s => s.Type == "export");

        public RelayCommand SetChannelCommand { get; }
        public RelayCommand SetStatusCommand { get; }
        public RelayCommand NavigateCommand { get; }
        public RelayCommand OpenDialogCommand { get; }
        public RelayCommand ViewSaleCommand { get; }
        public RelayCommand ReceivePaymentCommand { get; }

        public SalesViewModel(AppDataService dataService, Action<string> navigateAction, Action<string, object?> openDialogAction)
        {
            _dataService = dataService;
            _navigateAction = navigateAction;
            _openDialogAction = openDialogAction;

            SetChannelCommand = new RelayCommand(p => SelectedChannel = p?.ToString() ?? "all");
            SetStatusCommand = new RelayCommand(p => SelectedStatus = p?.ToString() ?? "all");

            NavigateCommand = new RelayCommand(p =>
            {
                if (p is string r) _navigateAction(r);
            });

            OpenDialogCommand = new RelayCommand(p =>
            {
                if (p is string dlg) _openDialogAction(dlg, null);
            });

            ViewSaleCommand = new RelayCommand(p =>
            {
                if (p is SaleDoc sale) _openDialogAction("view_doc", sale);
            });

            ReceivePaymentCommand = new RelayCommand(p =>
            {
                if (p is SaleDoc sale) _openDialogAction("payment_sale", sale);
            });

            _dataService.DataChanged += RefreshData;
            RefreshData();
        }

        public void RefreshData()
        {
            ApplyFilter();
            OnPropertyChanged(nameof(BilledThisMonth));
            OnPropertyChanged(nameof(OutstandingTotal));
            OnPropertyChanged(nameof(OutstandingCount));
            OnPropertyChanged(nameof(TotalInvoicesCount));
            OnPropertyChanged(nameof(RetailCount));
            OnPropertyChanged(nameof(DomesticCount));
            OnPropertyChanged(nameof(ExportCount));
        }

        private void ApplyFilter()
        {
            FilteredSales.Clear();
            string q = SearchQuery.Trim().ToLowerInvariant();

            foreach (var s in _dataService.Sales)
            {
                bool matchChan = SelectedChannel == "all" || s.Type.Equals(SelectedChannel, StringComparison.OrdinalIgnoreCase);
                bool matchStat = SelectedStatus == "all" || s.Status.Equals(SelectedStatus, StringComparison.OrdinalIgnoreCase);
                bool matchQ = string.IsNullOrEmpty(q) || (s.InvoiceNo + s.Party + s.Event).ToLowerInvariant().Contains(q);

                if (matchChan && matchStat && matchQ)
                {
                    FilteredSales.Add(s);
                }
            }
        }
    }
}
