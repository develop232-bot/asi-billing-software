using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
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

        private static string NormalizeStatus(string status) => status switch
        {
            "Cutting" => "Started",
            "Stitching" or "Lasting & pressing" or "Finishing" or "Packing" => "In Progress",
            _ => status
        };

        public int PendingCount => _dataService.WorkOrders.Count(o => string.Equals(NormalizeStatus(o.Status), "Pending", StringComparison.OrdinalIgnoreCase));
        public int StartedCount => _dataService.WorkOrders.Count(o => string.Equals(NormalizeStatus(o.Status), "Started", StringComparison.OrdinalIgnoreCase));
        public int InProgressCount => _dataService.WorkOrders.Count(o => string.Equals(NormalizeStatus(o.Status), "In Progress", StringComparison.OrdinalIgnoreCase));
        public int CompletedCount => _dataService.WorkOrders.Count(o => o.IsDone || string.Equals(NormalizeStatus(o.Status), "Completed", StringComparison.OrdinalIgnoreCase));

        public int CompletedThisMonthCount => CompletedCount;

        public int OverdueCount => _dataService.WorkOrders.Count(o => o.IsOverdue);

        public RelayCommand SetTypeFilterCommand { get; }
        public RelayCommand NewWorkOrderCommand { get; }
        public RelayCommand EditWorkOrderCommand { get; }
        public RelayCommand DeleteWorkOrderCommand { get; }

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
            DeleteWorkOrderCommand = new RelayCommand(async p =>
            {
                if (p is WorkOrderDoc doc)
                {
                    var res = MessageBox.Show(
                        $"Are you sure you want to permanently delete Work Order '{doc.OrderNo}'?",
                        "Delete Work Order",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                    if (res == MessageBoxResult.Yes)
                    {
                        await _dataService.DeleteWorkOrderAsync(doc.Id);
                        RefreshData();
                    }
                }
            });

            _dataService.DataChanged += RefreshData;
            RefreshData();
        }

        public void RefreshData()
        {
            ApplyFilter();
            OnPropertyChanged(nameof(PendingCount));
            OnPropertyChanged(nameof(StartedCount));
            OnPropertyChanged(nameof(InProgressCount));
            OnPropertyChanged(nameof(CompletedCount));
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
