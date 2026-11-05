using System;
using System.Collections.ObjectModel;
using System.Linq;
using AsiErp.Commands;
using AsiErp.Models;
using AsiErp.Services;

namespace AsiErp.ViewModels
{
    public class ProductionViewModel : ViewModelBase
    {
        private readonly AppDataService _dataService;
        private readonly Action<string, object?> _openDialogAction;

        private string _selectedTypeFilter = "all"; // all, general, domestic, export
        private string _searchQuery = string.Empty;

        public ObservableCollection<WorkOrderDoc> FilteredOrders { get; } = new();

        public string SelectedTypeFilter
        {
            get => _selectedTypeFilter;
            set
            {
                if (SetField(ref _selectedTypeFilter, value)) ApplyFilter();
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

        public int PendingCount => _dataService.WorkOrders.Count(o => o.Status == "Pending");
        public int InProgressCount => _dataService.WorkOrders.Count(o => o.Status != "Pending" && o.Status != "Completed");

        public int CompletedThisMonthCount
        {
            get
            {
                string f = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).ToString("yyyy-MM-dd");
                return _dataService.WorkOrders.Count(o => o.IsDone && string.Compare(o.EndDate, f) >= 0);
            }
        }

        public int OverdueCount => _dataService.WorkOrders.Count(o => o.IsOverdue);

        public RelayCommand SetTypeFilterCommand { get; }
        public RelayCommand NewWorkOrderCommand { get; }
        public RelayCommand EditWorkOrderCommand { get; }

        public ProductionViewModel(AppDataService dataService, Action<string, object?> openDialogAction)
        {
            _dataService = dataService;
            _openDialogAction = openDialogAction;

            SetTypeFilterCommand = new RelayCommand(p => SelectedTypeFilter = p?.ToString() ?? "all");
            NewWorkOrderCommand = new RelayCommand(_ => _openDialogAction("work_order", null));
            EditWorkOrderCommand = new RelayCommand(p =>
            {
                if (p is WorkOrderDoc doc) _openDialogAction("work_order", doc);
            });

            _dataService.DataChanged += RefreshData;
            RefreshData();
        }

        public void RefreshData()
        {
            ApplyFilter();
            OnPropertyChanged(nameof(PendingCount));
            OnPropertyChanged(nameof(InProgressCount));
            OnPropertyChanged(nameof(CompletedThisMonthCount));
            OnPropertyChanged(nameof(OverdueCount));
        }

        private void ApplyFilter()
        {
            FilteredOrders.Clear();
            string q = SearchQuery.Trim().ToLowerInvariant();

            foreach (var o in _dataService.WorkOrders)
            {
                bool matchType = SelectedTypeFilter == "all" || o.OrderType.Equals(SelectedTypeFilter, StringComparison.OrdinalIgnoreCase);
                bool matchQ = string.IsNullOrEmpty(q) || (o.OrderNo + o.Customer + o.ProductsSummary).ToLowerInvariant().Contains(q);

                if (matchType && matchQ)
                {
                    FilteredOrders.Add(o);
                }
            }
        }
    }
}
