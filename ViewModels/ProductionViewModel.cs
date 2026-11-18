using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using AsiErp.Commands;
using AsiErp.Models;
using AsiErp.Services;
using Microsoft.Win32;

namespace AsiErp.ViewModels
{
    public class MonthProductionRow
    {
        public string MonthLabel { get; set; } = string.Empty;
        public int OrdersReceived { get; set; }
        public int OrdersCompleted { get; set; }
        public double UnitsProduced { get; set; }
        public string UnitsProducedFormatted => UnitsProduced.ToString("N0", CultureInfo.InvariantCulture);
        public int OnTime { get; set; }
        public int Late { get; set; }
        public string AvgDays { get; set; } = "—";
        public double MaterialCost { get; set; }
        public string MaterialCostFormatted => $"₹{MaterialCost:N0}";
        public bool IsTotalRow { get; set; }
        public FontWeight RowFontWeight => IsTotalRow ? FontWeights.Bold : FontWeights.Normal;
        public string RowBackground => IsTotalRow ? "#F1F5F9" : "Transparent";
    }

    public class CompletedByTypeRow
    {
        public string TypeKey { get; set; } = string.Empty;
        public string TypeLabel { get; set; } = string.Empty;
        public int OrdersCount { get; set; }
        public double UnitsCount { get; set; }
        public string UnitsFormatted => UnitsCount.ToString("N0", CultureInfo.InvariantCulture);
        public string BadgeBackground => TypeKey switch
        {
            "domestic" => "#DBEAFE",
            "export" => "#DCFCE7",
            _ => "#F1F5F9"
        };
        public string BadgeForeground => TypeKey switch
        {
            "domestic" => "#2563EB",
            "export" => "#16A34A",
            _ => "#475569"
        };
    }

    public class MaterialUsedRow
    {
        public string MaterialName { get; set; } = string.Empty;
        public string QuantityFormatted { get; set; } = string.Empty;
        public double Cost { get; set; }
        public string CostFormatted => $"₹{Cost:N0}";
    }

    public class ProductMadeRow
    {
        public string ProductName { get; set; } = string.Empty;
        public string UnitsFormatted { get; set; } = string.Empty;
        public int OrdersCount { get; set; }
    }

    public class ReportWorkOrderRow
    {
        public string OrderNo { get; set; } = string.Empty;
        public string Customer { get; set; } = "—";
        public string OrderType { get; set; } = "general";
        public string OrderTypeLabel => OrderType switch
        {
            "domestic" => "Domestic",
            "export" => "Export",
            _ => "General / Stock"
        };
        public string TypeBadgeBg => OrderType switch
        {
            "domestic" => "#DBEAFE",
            "export" => "#DCFCE7",
            _ => "#F1F5F9"
        };
        public string TypeBadgeFg => OrderType switch
        {
            "domestic" => "#2563EB",
            "export" => "#16A34A",
            _ => "#475569"
        };
        public string ProductsSummary { get; set; } = "—";
        public string OrderDate { get; set; } = string.Empty;
        public string DueDate { get; set; } = string.Empty;
        public string StartDate { get; set; } = "—";
        public string EndDate { get; set; } = "—";
        public string TimeTaken { get; set; } = "—";
        public string Result { get; set; } = "—";
        public string ResultBg { get; set; } = "#F1F5F9";
        public string ResultFg { get; set; } = "#475569";
    }

    public class ProductionViewModel : ViewModelBase
    {
        private readonly AppDataService _dataService;
        private readonly Action<string, object?> _openDialogAction;

        private string _selectedTab = "orders"; // "orders" or "report"
        private string _selectedTypeFilter = "all"; // all, general, domestic, export
        private string _searchQuery = string.Empty;

        // Date Range for Report
        private int _reportMonthOffset = 0;
        private DateTime? _reportFromDate;
        private DateTime? _reportToDate;
        private string _reportPeriodLabel = string.Empty;
        private bool _isCustomDateRange = false;

        // KPI stats for report
        private int _kpiOrdersReceived;
        private int _kpiOrdersStarted;
        private int _kpiOrdersCompleted;
        private string _kpiUnitsProduced = "0";
        private int _kpiCompletedOnTime;
        private int _kpiCompletedLate;
        private string _kpiAvgDays = "—";
        private string _kpiMaterialCost = "₹0";

        public ObservableCollection<WorkOrderDoc> FilteredOrders { get; } = new();
        public ObservableCollection<MonthProductionRow> MonthWiseRows { get; } = new();
        public ObservableCollection<CompletedByTypeRow> CompletedByTypeRows { get; } = new();
        public ObservableCollection<MaterialUsedRow> MaterialUsedRows { get; } = new();
        public ObservableCollection<ProductMadeRow> ProductsMadeRows { get; } = new();
        public ObservableCollection<ReportWorkOrderRow> ReportWorkOrders { get; } = new();

        public string SelectedTab
        {
            get => _selectedTab;
            set
            {
                if (SetField(ref _selectedTab, value))
                {
                    OnPropertyChanged(nameof(IsOrdersTabSelected));
                    OnPropertyChanged(nameof(IsReportTabSelected));
                    if (_selectedTab == "report")
                    {
                        ComputeReport();
                    }
                }
            }
        }

        public bool IsOrdersTabSelected => SelectedTab == "orders";
        public bool IsReportTabSelected => SelectedTab == "report";

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

        public int ReportMonthOffset
        {
            get => _reportMonthOffset;
            set
            {
                if (SetField(ref _reportMonthOffset, value))
                {
                    _reportFromDate = null;
                    _reportToDate = null;
                    _isCustomDateRange = false;
                    OnPropertyChanged(nameof(ReportFromDate));
                    OnPropertyChanged(nameof(ReportToDate));
                    OnPropertyChanged(nameof(IsCustomDateRange));
                    ComputeReport();
                }
            }
        }

        public DateTime? ReportFromDate
        {
            get => _reportFromDate;
            set
            {
                if (SetField(ref _reportFromDate, value))
                {
                    CheckCustomRange();
                }
            }
        }

        public DateTime? ReportToDate
        {
            get => _reportToDate;
            set
            {
                if (SetField(ref _reportToDate, value))
                {
                    CheckCustomRange();
                }
            }
        }

        private void CheckCustomRange()
        {
            if (_reportFromDate.HasValue && _reportToDate.HasValue)
            {
                _isCustomDateRange = true;
                OnPropertyChanged(nameof(IsCustomDateRange));
                ComputeReport();
            }
            else if (!_reportFromDate.HasValue && !_reportToDate.HasValue && _isCustomDateRange)
            {
                _isCustomDateRange = false;
                OnPropertyChanged(nameof(IsCustomDateRange));
                ComputeReport();
            }
        }

        public string ReportPeriodLabel
        {
            get => _reportPeriodLabel;
            private set => SetField(ref _reportPeriodLabel, value);
        }

        public bool IsCustomDateRange => _isCustomDateRange;
        public bool IsNotCurrentMonth => _reportMonthOffset != 0 || _isCustomDateRange;

        // KPI Stats
        public int KpiOrdersReceived { get => _kpiOrdersReceived; private set => SetField(ref _kpiOrdersReceived, value); }
        public int KpiOrdersStarted { get => _kpiOrdersStarted; private set => SetField(ref _kpiOrdersStarted, value); }
        public int KpiOrdersCompleted { get => _kpiOrdersCompleted; private set => SetField(ref _kpiOrdersCompleted, value); }
        public string KpiUnitsProduced { get => _kpiUnitsProduced; private set => SetField(ref _kpiUnitsProduced, value); }
        public int KpiCompletedOnTime { get => _kpiCompletedOnTime; private set => SetField(ref _kpiCompletedOnTime, value); }
        public int KpiCompletedLate { get => _kpiCompletedLate; private set => SetField(ref _kpiCompletedLate, value); }
        public string KpiAvgDays { get => _kpiAvgDays; private set => SetField(ref _kpiAvgDays, value); }
        public string KpiMaterialCost { get => _kpiMaterialCost; private set => SetField(ref _kpiMaterialCost, value); }

        // Work Order Tab Counts
        public int PendingCount => _dataService.WorkOrders.Count(o => string.Equals(o.Status, "Pending", StringComparison.OrdinalIgnoreCase));
        public int StartedCount => _dataService.WorkOrders.Count(o => string.Equals(o.Status, "Started", StringComparison.OrdinalIgnoreCase) || string.Equals(o.Status, "Cutting", StringComparison.OrdinalIgnoreCase));
        public int InProgressCount => _dataService.WorkOrders.Count(o => string.Equals(o.Status, "In Progress", StringComparison.OrdinalIgnoreCase)
            || (!string.Equals(o.Status, "Pending", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(o.Status, "Started", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(o.Status, "Cutting", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(o.Status, "Completed", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(o.Status, "Completed This Month", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(o.Status, "Overdue", StringComparison.OrdinalIgnoreCase)
                && !o.IsDone));
        public int CompletedCount => _dataService.WorkOrders.Count(o => o.IsDone || string.Equals(o.Status, "Completed", StringComparison.OrdinalIgnoreCase) || string.Equals(o.Status, "Completed This Month", StringComparison.OrdinalIgnoreCase));

        public int CompletedThisMonthCount
        {
            get
            {
                string currentMonthPrefix = DateTime.Now.ToString("yyyy-MM");
                return _dataService.WorkOrders.Count(o => (o.IsDone || string.Equals(o.Status, "Completed", StringComparison.OrdinalIgnoreCase) || string.Equals(o.Status, "Completed This Month", StringComparison.OrdinalIgnoreCase)) && (string.IsNullOrEmpty(o.EndDate) || o.EndDate.StartsWith(currentMonthPrefix)));
            }
        }

        public int OverdueCount => _dataService.WorkOrders.Count(o => o.IsOverdue || string.Equals(o.Status, "Overdue", StringComparison.OrdinalIgnoreCase));

        // Commands
        public RelayCommand SelectTabCommand { get; }
        public RelayCommand SetTypeFilterCommand { get; }
        public RelayCommand NewWorkOrderCommand { get; }
        public RelayCommand EditWorkOrderCommand { get; }
        public RelayCommand DeleteWorkOrderCommand { get; }

        public RelayCommand PrevMonthCommand { get; }
        public RelayCommand NextMonthCommand { get; }
        public RelayCommand CurrentMonthCommand { get; }
        public RelayCommand ClearCustomRangeCommand { get; }
        public RelayCommand DownloadMonthReportCommand { get; }
        public RelayCommand DownloadOrdersListCommand { get; }

        public ProductionViewModel(AppDataService dataService, Action<string, object?> openDialogAction)
        {
            _dataService = dataService;
            _openDialogAction = openDialogAction;

            SelectTabCommand = new RelayCommand(p => SelectedTab = p?.ToString() ?? "orders");
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

            PrevMonthCommand = new RelayCommand(_ => ReportMonthOffset--);
            NextMonthCommand = new RelayCommand(_ => ReportMonthOffset++);
            CurrentMonthCommand = new RelayCommand(_ => ReportMonthOffset = 0);
            ClearCustomRangeCommand = new RelayCommand(_ =>
            {
                _reportFromDate = null;
                _reportToDate = null;
                _isCustomDateRange = false;
                OnPropertyChanged(nameof(ReportFromDate));
                OnPropertyChanged(nameof(ReportToDate));
                OnPropertyChanged(nameof(IsCustomDateRange));
                ComputeReport();
            });

            DownloadMonthReportCommand = new RelayCommand(_ => ExportMonthWiseReport());
            DownloadOrdersListCommand = new RelayCommand(_ => ExportOrdersList());

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

            if (SelectedTab == "report")
            {
                ComputeReport();
            }
        }

        private void ApplyFilter()
        {
            FilteredOrders.Clear();
            string q = SearchQuery.Trim().ToLowerInvariant();

            foreach (var o in _dataService.WorkOrders)
            {
                bool matchType = SelectedTypeFilter == "all" || o.OrderType.Equals(SelectedTypeFilter, StringComparison.OrdinalIgnoreCase);
                bool matchQ = string.IsNullOrEmpty(q) || (o.OrderNo + " " + o.Customer + " " + o.ProductsSummary).ToLowerInvariant().Contains(q);

                if (matchType && matchQ)
                {
                    FilteredOrders.Add(o);
                }
            }
        }

        private (string from, string to, string label, bool custom) GetReportRange()
        {
            if (_isCustomDateRange && _reportFromDate.HasValue && _reportToDate.HasValue)
            {
                var f = _reportFromDate.Value <= _reportToDate.Value ? _reportFromDate.Value : _reportToDate.Value;
                var t = _reportFromDate.Value <= _reportToDate.Value ? _reportToDate.Value : _reportFromDate.Value;
                return (f.ToString("yyyy-MM-dd"), t.ToString("yyyy-MM-dd"), $"{f:dd MMM yyyy} – {t:dd MMM yyyy}", true);
            }

            var d = DateTime.Today.AddMonths(_reportMonthOffset);
            var firstDay = new DateTime(d.Year, d.Month, 1);
            var lastDay = new DateTime(d.Year, d.Month, DateTime.DaysInMonth(d.Year, d.Month));
            return (firstDay.ToString("yyyy-MM-dd"), lastDay.ToString("yyyy-MM-dd"), d.ToString("MMMM yyyy"), false);
        }

        private class PeriodStats
        {
            public List<WorkOrderDoc> Recv { get; set; } = new();
            public List<WorkOrderDoc> Done { get; set; } = new();
            public List<WorkOrderDoc> Started { get; set; } = new();
            public double Units { get; set; }
            public int OnTime { get; set; }
            public int Late { get; set; }
            public double AvgDays { get; set; }
            public double MaterialCost { get; set; }
        }

        private PeriodStats ComputePeriodStats(string fromIso, string toIso)
        {
            var orders = _dataService.WorkOrders;

            var recv = orders.Where(o => !string.IsNullOrEmpty(o.OrderDate) && string.Compare(o.OrderDate, fromIso) >= 0 && string.Compare(o.OrderDate, toIso) <= 0).ToList();
            var done = orders.Where(o => (o.IsDone || string.Equals(o.Status, "Completed", StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrEmpty(o.EndDate) && string.Compare(o.EndDate, fromIso) >= 0 && string.Compare(o.EndDate, toIso) <= 0).ToList();
            var started = orders.Where(o => !string.IsNullOrEmpty(o.StartDate) && string.Compare(o.StartDate, fromIso) >= 0 && string.Compare(o.StartDate, toIso) <= 0).ToList();

            double units = done.Sum(o => o.Items?.Sum(i => i.Qty) ?? 0);
            int onTime = done.Count(o => !string.IsNullOrEmpty(o.EndDate) && !string.IsNullOrEmpty(o.DueDate) && string.Compare(o.EndDate, o.DueDate) <= 0);
            int late = done.Count - onTime;

            var daysList = new List<int>();
            foreach (var o in done)
            {
                string startOrOrder = !string.IsNullOrEmpty(o.StartDate) ? o.StartDate : o.OrderDate;
                if (!string.IsNullOrEmpty(startOrOrder) && !string.IsNullOrEmpty(o.EndDate))
                {
                    daysList.Add(DaysBetween(startOrOrder, o.EndDate));
                }
            }
            double avgDays = daysList.Count > 0 ? daysList.Average() : 0;

            double matCost = 0;
            foreach (var o in done)
            {
                if (o.Materials != null)
                {
                    foreach (var c in o.Materials)
                    {
                        var rm = _dataService.RawMaterials.FirstOrDefault(r => (!string.IsNullOrEmpty(c.ItemId) && r.Id == c.ItemId) || (!string.IsNullOrEmpty(c.Name) && r.Name.Equals(c.Name, StringComparison.OrdinalIgnoreCase)));
                        double rate = rm?.Rate ?? 0;
                        matCost += rate * c.Qty;
                    }
                }
            }

            return new PeriodStats
            {
                Recv = recv,
                Done = done,
                Started = started,
                Units = units,
                OnTime = onTime,
                Late = late,
                AvgDays = avgDays,
                MaterialCost = matCost
            };
        }

        private static int DaysBetween(string d1, string d2)
        {
            if (DateTime.TryParse(d1, out var dt1) && DateTime.TryParse(d2, out var dt2))
            {
                return Math.Max(0, (int)Math.Round((dt2 - dt1).TotalDays));
            }
            return 0;
        }

        private static string FormatShort(double val)
        {
            if (val >= 10000000) return $"₹{(val / 10000000.0):0.##} Cr";
            if (val >= 100000) return $"₹{(val / 100000.0):0.##} L";
            if (val >= 1000) return $"₹{(val / 1000.0):0.##} k";
            return $"₹{val:N0}";
        }

        public void ComputeReport()
        {
            var range = GetReportRange();
            ReportPeriodLabel = range.label;
            OnPropertyChanged(nameof(IsNotCurrentMonth));

            var st = ComputePeriodStats(range.from, range.to);

            // KPI stats
            KpiOrdersReceived = st.Recv.Count;
            KpiOrdersStarted = st.Started.Count;
            KpiOrdersCompleted = st.Done.Count;
            KpiUnitsProduced = st.Units.ToString("N0", CultureInfo.InvariantCulture);
            KpiCompletedOnTime = st.OnTime;
            KpiCompletedLate = st.Late;
            KpiAvgDays = st.Done.Count > 0 ? st.AvgDays.ToString("0.0", CultureInfo.InvariantCulture) : "—";
            KpiMaterialCost = FormatShort(st.MaterialCost);

            // 1. Month-wise Report (Last 12 Months)
            MonthWiseRows.Clear();
            var monthsList = new List<(string label, string from, string to)>();
            var today = DateTime.Today;
            for (int i = 11; i >= 0; i--)
            {
                var dt = new DateTime(today.Year, today.Month, 1).AddMonths(-i);
                var f = dt.ToString("yyyy-MM-01");
                var t = new DateTime(dt.Year, dt.Month, DateTime.DaysInMonth(dt.Year, dt.Month)).ToString("yyyy-MM-dd");
                monthsList.Add((dt.ToString("MMM yy"), f, t));
            }

            int totR = 0, totD = 0, totO = 0, totL = 0;
            double totU = 0, totM = 0;

            foreach (var m in monthsList)
            {
                var ms = ComputePeriodStats(m.from, m.to);
                totR += ms.Recv.Count;
                totD += ms.Done.Count;
                totU += ms.Units;
                totO += ms.OnTime;
                totL += ms.Late;
                totM += ms.MaterialCost;

                MonthWiseRows.Add(new MonthProductionRow
                {
                    MonthLabel = m.label,
                    OrdersReceived = ms.Recv.Count,
                    OrdersCompleted = ms.Done.Count,
                    UnitsProduced = ms.Units,
                    OnTime = ms.OnTime,
                    Late = ms.Late,
                    AvgDays = ms.Done.Count > 0 ? ms.AvgDays.ToString("0.0", CultureInfo.InvariantCulture) : "—",
                    MaterialCost = ms.MaterialCost,
                    IsTotalRow = false
                });
            }

            MonthWiseRows.Add(new MonthProductionRow
            {
                MonthLabel = "Last 12 months",
                OrdersReceived = totR,
                OrdersCompleted = totD,
                UnitsProduced = totU,
                OnTime = totO,
                Late = totL,
                AvgDays = "—",
                MaterialCost = totM,
                IsTotalRow = true
            });

            // 2. Completed By Order Type
            CompletedByTypeRows.Clear();
            var types = new[] { ("general", "General / Stock"), ("domestic", "Domestic"), ("export", "Export") };
            foreach (var tp in types)
            {
                var typeOrders = st.Done.Where(x => string.Equals(x.OrderType, tp.Item1, StringComparison.OrdinalIgnoreCase)).ToList();
                double u = typeOrders.Sum(x => x.Items?.Sum(i => i.Qty) ?? 0);
                CompletedByTypeRows.Add(new CompletedByTypeRow
                {
                    TypeKey = tp.Item1,
                    TypeLabel = tp.Item2,
                    OrdersCount = typeOrders.Count,
                    UnitsCount = u
                });
            }

            // 3. Material Used
            MaterialUsedRows.Clear();
            var matDict = new Dictionary<string, (string name, string unit, double qty, double cost)>();
            foreach (var o in st.Done)
            {
                if (o.Materials != null)
                {
                    foreach (var c in o.Materials)
                    {
                        string key = !string.IsNullOrEmpty(c.ItemId) ? c.ItemId : c.Name;
                        if (string.IsNullOrEmpty(key)) continue;

                        var rm = _dataService.RawMaterials.FirstOrDefault(r => (!string.IsNullOrEmpty(c.ItemId) && r.Id == c.ItemId) || (!string.IsNullOrEmpty(c.Name) && r.Name.Equals(c.Name, StringComparison.OrdinalIgnoreCase)));
                        double rate = rm?.Rate ?? 0;
                        double cost = rate * c.Qty;
                        string unit = !string.IsNullOrEmpty(c.Unit) ? c.Unit : (rm?.Unit ?? "sq ft");

                        if (!matDict.ContainsKey(key))
                        {
                            matDict[key] = (c.Name, unit, c.Qty, cost);
                        }
                        else
                        {
                            var prev = matDict[key];
                            matDict[key] = (prev.name, prev.unit, prev.qty + c.Qty, prev.cost + cost);
                        }
                    }
                }
            }

            foreach (var item in matDict.Values.OrderByDescending(x => x.cost))
            {
                MaterialUsedRows.Add(new MaterialUsedRow
                {
                    MaterialName = item.name,
                    QuantityFormatted = $"{item.qty:N0} {item.unit}",
                    Cost = item.cost
                });
            }

            // 4. Products Made
            ProductsMadeRows.Clear();
            var prodDict = new Dictionary<string, (string name, string unit, double qty, HashSet<string> orderIds)>();
            foreach (var o in st.Done)
            {
                if (o.Items != null)
                {
                    foreach (var it in o.Items)
                    {
                        string pid = !string.IsNullOrEmpty(it.ProductId) ? it.ProductId : it.ProductName;
                        if (string.IsNullOrEmpty(pid)) continue;

                        if (!prodDict.ContainsKey(pid))
                        {
                            prodDict[pid] = (it.ProductName, it.Unit ?? "pair", it.Qty, new HashSet<string> { o.Id });
                        }
                        else
                        {
                            var prev = prodDict[pid];
                            prev.orderIds.Add(o.Id);
                            prodDict[pid] = (prev.name, prev.unit, prev.qty + it.Qty, prev.orderIds);
                        }
                    }
                }
            }

            foreach (var item in prodDict.Values.OrderByDescending(x => x.qty))
            {
                ProductsMadeRows.Add(new ProductMadeRow
                {
                    ProductName = item.name,
                    UnitsFormatted = $"{item.qty:N0} {item.unit}",
                    OrdersCount = item.orderIds.Count
                });
            }

            // 5. Work Orders in this Report
            ReportWorkOrders.Clear();
            var reportOrderIds = new HashSet<string>();
            var combinedOrders = new List<WorkOrderDoc>();

            foreach (var o in st.Recv)
            {
                if (reportOrderIds.Add(o.Id)) combinedOrders.Add(o);
            }
            foreach (var o in st.Done)
            {
                if (reportOrderIds.Add(o.Id)) combinedOrders.Add(o);
            }

            combinedOrders = combinedOrders.OrderByDescending(o => o.OrderDate).ToList();

            foreach (var o in combinedOrders)
            {
                string timeTaken = "—";
                if (!string.IsNullOrEmpty(o.EndDate))
                {
                    string startOrOrder = !string.IsNullOrEmpty(o.StartDate) ? o.StartDate : o.OrderDate;
                    if (!string.IsNullOrEmpty(startOrOrder))
                    {
                        timeTaken = $"{DaysBetween(startOrOrder, o.EndDate)} days";
                    }
                }

                string result = o.Status;
                string resultBg = "#F1F5F9";
                string resultFg = "#475569";

                if (o.IsDone || string.Equals(o.Status, "Completed", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(o.EndDate) && !string.IsNullOrEmpty(o.DueDate) && string.Compare(o.EndDate, o.DueDate) <= 0)
                    {
                        result = "On time";
                        resultBg = "#DCFCE7";
                        resultFg = "#16A34A";
                    }
                    else
                    {
                        result = "Late";
                        resultBg = "#FEE2E2";
                        resultFg = "#DC2626";
                    }
                }
                else if (string.Equals(o.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                {
                    result = "Pending";
                    resultBg = "#F1F5F9";
                    resultFg = "#64748B";
                }
                else
                {
                    result = o.Status;
                    resultBg = "#DBEAFE";
                    resultFg = "#2563EB";
                }

                string itemsSummary = "—";
                if (o.Items != null && o.Items.Count > 0)
                {
                    itemsSummary = string.Join("; ", o.Items.Select(i => $"{i.ProductName}{(string.IsNullOrEmpty(i.Size) ? "" : $" ({i.Size})")} × {i.Qty}"));
                }

                ReportWorkOrders.Add(new ReportWorkOrderRow
                {
                    OrderNo = o.OrderNo,
                    Customer = !string.IsNullOrEmpty(o.Customer) ? o.Customer : "—",
                    OrderType = o.OrderType,
                    ProductsSummary = itemsSummary,
                    OrderDate = FormatDateShort(o.OrderDate),
                    DueDate = FormatDateShort(o.DueDate),
                    StartDate = !string.IsNullOrEmpty(o.StartDate) ? FormatDateShort(o.StartDate) : "—",
                    EndDate = !string.IsNullOrEmpty(o.EndDate) ? FormatDateShort(o.EndDate) : "—",
                    TimeTaken = timeTaken,
                    Result = result,
                    ResultBg = resultBg,
                    ResultFg = resultFg
                });
            }
        }

        private static string FormatDateShort(string iso)
        {
            if (DateTime.TryParse(iso, out var d)) return d.ToString("dd MMM yyyy");
            return iso;
        }

        private void ExportMonthWiseReport()
        {
            try
            {
                var sfd = new SaveFileDialog
                {
                    Filter = "CSV File (*.csv)|*.csv",
                    FileName = "production-report-month-wise.csv"
                };

                if (sfd.ShowDialog() == true)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("\"Month\",\"Orders received\",\"Orders completed\",\"Units produced\",\"Completed on time\",\"Completed late\",\"Avg days to complete\",\"Material cost used (Rs)\"");

                    foreach (var r in MonthWiseRows)
                    {
                        sb.AppendLine($"\"{r.MonthLabel}\",\"{r.OrdersReceived}\",\"{r.OrdersCompleted}\",\"{r.UnitsProduced}\",\"{r.OnTime}\",\"{r.Late}\",\"{(r.AvgDays == "—" ? "" : r.AvgDays)}\",\"{r.MaterialCost:F2}\"");
                    }

                    File.WriteAllText(sfd.FileName, "\uFEFF" + sb.ToString(), Encoding.UTF8);
                    MessageBox.Show("Month-wise production report downloaded successfully!", "Export Report", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not export CSV:\n{ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportOrdersList()
        {
            try
            {
                var range = GetReportRange();
                var sfd = new SaveFileDialog
                {
                    Filter = "CSV File (*.csv)|*.csv",
                    FileName = $"production-orders_{range.from}_to_{range.to}.csv"
                };

                if (sfd.ShowDialog() == true)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("\"Order\",\"Customer\",\"Type\",\"Products\",\"Order date\",\"Due date\",\"Started\",\"Finished\",\"Days taken\",\"Status\",\"Result\"");

                    foreach (var r in ReportWorkOrders)
                    {
                        sb.AppendLine($"\"{r.OrderNo}\",\"{r.Customer}\",\"{r.OrderTypeLabel}\",\"{r.ProductsSummary}\",\"{r.OrderDate}\",\"{r.DueDate}\",\"{r.StartDate}\",\"{r.EndDate}\",\"{r.TimeTaken}\",\"{r.Result}\",\"{r.Result}\"");
                    }

                    File.WriteAllText(sfd.FileName, "\uFEFF" + sb.ToString(), Encoding.UTF8);
                    MessageBox.Show("Orders list downloaded successfully!", "Export Orders", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not export CSV:\n{ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
