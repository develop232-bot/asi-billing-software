using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using AsiErp.Commands;
using AsiErp.Models;
using AsiErp.Services;

namespace AsiErp.ViewModels
{
    public class ChartLineDot
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double DotLeft => X - 4.0;
        public double DotTop => Y - 4.0;
        public Brush ColorBrush { get; set; } = Brushes.White;
        public string TooltipText { get; set; } = string.Empty;
    }

    public class ChartAxisLabel
    {
        public string Text { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public double LabelLeft => X - 24.0;
        public double LabelTop => Y - 8.0;
        public double XLabelLeft => X - 16.0;
    }

    public class DashboardPieSlice
    {
        public string Channel { get; set; } = string.Empty;
        public double Amount { get; set; }
        public double Percentage { get; set; }
        public string PercentageDisplay => $"{Percentage:F0}%";
        public string AmountDisplay => $"₹{Amount:N0}";
        public Brush FillBrush { get; set; } = Brushes.Gray;
        public Geometry SliceGeometry { get; set; } = Geometry.Empty;
        public string TooltipText => $"{Channel}: ₹{Amount:N0} ({Percentage:F0}%)";
    }

    public class PeriodOption
    {
        public string Tag { get; set; } = string.Empty;
        public string Display { get; set; } = string.Empty;
    }

    public class DashboardActivity
    {
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string BadgeColor { get; set; } = "#3D62B8";
    }

    public class ShopFloorStageSummary
    {
        public string StageName { get; set; } = string.Empty;
        public int OrderCount { get; set; }
        public double TotalUnits { get; set; }
        public bool HasOrders => OrderCount > 0;
    }

    public class TopProductShare
    {
        public string Name { get; set; } = string.Empty;
        public double Value { get; set; }
        public double Percentage { get; set; }
        public string Color { get; set; } = "#3D62B8";
    }

    public class SalesTrendPoint
    {
        public string Label { get; set; } = string.Empty;
        public double Value { get; set; }
        public string FormattedValue { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public bool IsLast { get; set; }
        public string TooltipText { get; set; } = string.Empty;

        public double DotLeft => X - 4.5;
        public double DotTop => Y - 4.5;

        public double RingLeft => X - 8.0;
        public double RingTop => Y - 8.0;

        public double LabelLeft => X - 30.0;
        public double LabelTop => Y - 19.0;

        public double XLabelLeft => X - 30.0;

        public double BadgeLeft => Math.Min(438.0, Math.Max(10.0, X - 26.0));
        public double BadgeTop => Math.Max(2.0, Y - 35.0);

        public Visibility NormalPointVisibility => IsLast ? Visibility.Collapsed : Visibility.Visible;
        public Visibility LastPointVisibility => IsLast ? Visibility.Visible : Visibility.Collapsed;
    }

    public class SalesTrendAxisTick
    {
        public string Label { get; set; } = string.Empty;
        public double Y { get; set; }
        public double LabelTop => Y - 7.0;
    }

    public class MonthlyChartPoint
    {
        public string MonthLabel { get; set; } = string.Empty;
        public double RetailSales { get; set; }
        public double DomesticSales { get; set; }
        public double ExportSales { get; set; }
        public double TotalSales => RetailSales + DomesticSales + ExportSales;
        public double TotalExpense { get; set; }
        public double SalesHeight { get; set; }
        public double ExpenseHeight { get; set; }
        public bool IsCurrentMonth { get; set; }
        public double NetProfit => TotalSales - TotalExpense;

        public string TooltipText =>
            $"{MonthLabel}\n" +
            $"• Total Sales: ₹{TotalSales:N0}\n" +
            $"  - Domestic: ₹{DomesticSales:N0}\n" +
            $"  - Export: ₹{ExportSales:N0}\n" +
            $"  - Retail: ₹{RetailSales:N0}\n" +
            $"• Expenses: ₹{TotalExpense:N0}\n" +
            $"• Net: {(NetProfit >= 0 ? "+" : "")}₹{NetProfit:N0}";
    }

    public class DashboardViewModel : ViewModelBase
    {
        private readonly AppDataService _dataService;
        private readonly Action<string> _navigateAction;
        private readonly Action<string, object?> _openDialogAction;

        public double MtdRetailSales { get; private set; }
        public double MtdDomesticSales { get; private set; }
        public double MtdExportSales { get; private set; }
        public double MtdTotalSales { get; private set; }
        public double MtdExpenses { get; private set; }
        public double MtdProfit => MtdTotalSales - MtdExpenses;
        public bool MtdProfitIsPositive => MtdProfit >= 0;
        public string MtdProfitColor => MtdProfit >= 0 ? "#15803D" : "#DC2626";
        public string MtdProfitLabel => MtdProfit >= 0 ? "Profit" : "Loss";
        public string MtdProfitIcon => MtdProfit >= 0 ? "📈" : "⚠️";
        public double MtdProfitDisplayValue => Math.Abs(MtdProfit);
        public Brush MtdProfitSubtextBrush => new SolidColorBrush((Color)ColorConverter.ConvertFromString(MtdProfit >= 0 ? "#DCFCE7" : "#FEE2E2"));
        public Brush MtdProfitBrush
        {
            get
            {
                if (MtdProfit >= 0)
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#15803D"));
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));
            }
        }

        public double OutstandingToReceive { get; private set; }
        public int OutstandingCount { get; private set; }
        public double VendorToPay { get; private set; }
        public int VendorPayCount { get; private set; }
        public double PendingLoans { get; private set; }
        public int LowStockAlertsCount { get; private set; }

        public double TodayRetailSales => MtdRetailSales;
        public double TodayDomesticExportSales => MtdDomesticSales + MtdExportSales;

        public ObservableCollection<DashboardPieSlice> ChannelPieSlices { get; } = new();
        public string TotalSalesDisplay => $"₹{MtdTotalSales:N0}";

        public double SixMonthSalesTotal { get; private set; }
        public double SixMonthExpenseTotal { get; private set; }
        public double SixMonthNetProfit => SixMonthSalesTotal - SixMonthExpenseTotal;
        public double SixMonthMarginPercent => SixMonthSalesTotal > 0 ? Math.Round((SixMonthNetProfit / SixMonthSalesTotal) * 100, 1) : 0;
        public string SixMonthMarginText => $"Margin: {(SixMonthMarginPercent >= 0 ? "+" : "")}{SixMonthMarginPercent:F1}%";

        public ObservableCollection<MonthlyChartPoint> MonthlyChartData { get; } = new();

        public ObservableCollection<string> TrendPeriods { get; } = new() { "This Month", "Last 6 Months", "This Year" };
        private string _selectedTrendPeriod = "This Month";
        public string SelectedTrendPeriod
        {
            get => _selectedTrendPeriod;
            set
            {
                if (_selectedTrendPeriod != value)
                {
                    _selectedTrendPeriod = value;
                    OnPropertyChanged(nameof(SelectedTrendPeriod));
                    UpdateSalesTrend();
                }
            }
        }

        public ObservableCollection<PeriodOption> TopSellersPeriods { get; } = new();

        private string _topSellersPeriod = "this_month";
        public string TopSellersPeriod
        {
            get => _topSellersPeriod;
            set
            {
                if (_topSellersPeriod != value)
                {
                    _topSellersPeriod = value;
                    OnPropertyChanged(nameof(TopSellersPeriod));
                    UpdateTopSellersPeriodLabel();
                    RefreshData();
                }
            }
        }

        private string _topSellersPeriodLabel = "this month";
        public string TopSellersPeriodLabel
        {
            get => _topSellersPeriodLabel;
            private set => SetField(ref _topSellersPeriodLabel, value);
        }

        private double _topSellersTotalSales;
        public double TopSellersTotalSales
        {
            get => _topSellersTotalSales;
            private set => SetField(ref _topSellersTotalSales, value);
        }

        public PointCollection TrendLinePoints { get; } = new();
        public PointCollection TrendAreaPoints { get; } = new();
        public ObservableCollection<SalesTrendPoint> TrendPoints { get; } = new();
        public ObservableCollection<SalesTrendAxisTick> YAxisTicks { get; } = new();
        public SalesTrendPoint? LastTrendPoint { get; private set; }

        public PointCollection DomesticLinePoints { get; } = new();
        public PointCollection ExportLinePoints { get; } = new();
        public PointCollection RetailLinePoints { get; } = new();
        public PointCollection ExpenseLinePoints { get; } = new();
        public PointCollection SalesOverviewAreaPoints { get; } = new();
        public ObservableCollection<ChartLineDot> OverviewDots { get; } = new();
        public ObservableCollection<ChartAxisLabel> OverviewYAxisTicks { get; } = new();
        public ObservableCollection<ChartAxisLabel> OverviewXAxisLabels { get; } = new();

        public ObservableCollection<TopProductShare> TopProducts { get; } = new();
        public ObservableCollection<DashboardPieSlice> TopProductsPieSlices { get; } = new();
        public ObservableCollection<SaleDoc> RecentInvoices { get; } = new();
        public ObservableCollection<ProductItem> StockSummaryList { get; } = new();
        public ObservableCollection<DashboardActivity> RecentActivities { get; } = new();
        public ObservableCollection<ShopFloorStageSummary> ShopFloorStages { get; } = new();

        public RelayCommand NavigateCommand { get; }
        public RelayCommand OpenDialogCommand { get; }
        public RelayCommand ViewInvoiceCommand { get; }

        public DashboardViewModel(AppDataService dataService, Action<string> navigateAction, Action<string, object?> openDialogAction)
        {
            _dataService = dataService;
            _navigateAction = navigateAction;
            _openDialogAction = openDialogAction;

            NavigateCommand = new RelayCommand(p =>
            {
                if (p is string r) _navigateAction(r);
            });

            OpenDialogCommand = new RelayCommand(p =>
            {
                if (p is string dlg) _openDialogAction(dlg, null);
            });

            ViewInvoiceCommand = new RelayCommand(p =>
            {
                if (p is SaleDoc sale) _openDialogAction("view_doc", sale);
            });

            InitializeTopSellersPeriods();
            _dataService.DataChanged += RefreshData;
            RefreshData();
        }

        private void InitializeTopSellersPeriods()
        {
            TopSellersPeriods.Clear();
            TopSellersPeriods.Add(new PeriodOption { Tag = "this_month", Display = "This month" });
            TopSellersPeriods.Add(new PeriodOption { Tag = "last_month", Display = "Last month" });

            DateTime cur = DateTime.Today;
            // Generate all previous months (from 2 months ago back through past 12 months)
            for (int i = 2; i <= 12; i++)
            {
                DateTime dt = cur.AddMonths(-i);
                TopSellersPeriods.Add(new PeriodOption { Tag = $"m:{dt:yyyy-MM}", Display = dt.ToString("MMMM yyyy") });
            }

            TopSellersPeriods.Add(new PeriodOption { Tag = "quarter", Display = "Last 3 months" });
            TopSellersPeriods.Add(new PeriodOption { Tag = "fy", Display = "This financial year" });
            TopSellersPeriods.Add(new PeriodOption { Tag = "all", Display = "All time" });

            UpdateTopSellersPeriodLabel();
        }

        private void UpdateTopSellersPeriodLabel()
        {
            var match = TopSellersPeriods.FirstOrDefault(p => p.Tag == _topSellersPeriod);
            if (match != null)
            {
                TopSellersPeriodLabel = match.Display.ToLowerInvariant();
            }
            else
            {
                TopSellersPeriodLabel = "this month";
            }
        }

        public (string? start, string? end) GetPeriodDateRange(string tag)
        {
            DateTime now = DateTime.Today;
            if (tag == "last_month")
            {
                DateTime prevMonth = now.AddMonths(-1);
                string startPrev = new DateTime(prevMonth.Year, prevMonth.Month, 1).ToString("yyyy-MM-dd");
                string endPrev = new DateTime(now.Year, now.Month, 1).ToString("yyyy-MM-dd");
                return (startPrev, endPrev);
            }
            if (!string.IsNullOrEmpty(tag) && tag.StartsWith("m:"))
            {
                string ym = tag.Substring(2);
                if (DateTime.TryParse($"{ym}-01", out DateTime dtMonth))
                {
                    string start = dtMonth.ToString("yyyy-MM-dd");
                    string end = dtMonth.AddMonths(1).ToString("yyyy-MM-dd");
                    return (start, end);
                }
                return ($"{ym}-01", null);
            }
            if (tag == "quarter")
            {
                string since90 = now.AddDays(-90).ToString("yyyy-MM-dd");
                return (since90, null);
            }
            if (tag == "fy")
            {
                int fyYear = now.Month >= 4 ? now.Year : now.Year - 1;
                string fyStart = $"{fyYear}-04-01";
                return (fyStart, null);
            }
            if (tag == "all")
            {
                return (null, null);
            }
            // default "this_month"
            string firstOfMonth = new DateTime(now.Year, now.Month, 1).ToString("yyyy-MM-dd");
            return (firstOfMonth, null);
        }

        public void RefreshData()
        {
            var (start, end) = GetPeriodDateRange(_topSellersPeriod);

            bool InPeriod(string? d)
            {
                if (string.IsNullOrEmpty(d)) return false;
                if (DateTime.TryParse(d, out var dt))
                {
                    var dateOnly = dt.Date;
                    if (start != null && DateTime.TryParse(start, out var sDt) && dateOnly < sDt.Date) return false;
                    if (end != null && DateTime.TryParse(end, out var eDt) && dateOnly >= eDt.Date) return false;
                    return true;
                }
                if (start != null && string.Compare(d, start) < 0) return false;
                if (end != null && string.Compare(d, end) >= 0) return false;
                return true;
            }

            // 1. Retail Sales
            MtdRetailSales = _dataService.Sales
                .Where(s => InPeriod(s.Date) && s.Type == "retail")
                .Sum(s => s.GrossAmount);

            // 2. Domestic Sales
            MtdDomesticSales = _dataService.Sales
                .Where(s => InPeriod(s.Date) && s.Type == "domestic")
                .Sum(s => s.GrossAmount);

            // 3. Export Sales
            MtdExportSales = _dataService.Sales
                .Where(s => InPeriod(s.Date) && s.Type == "export")
                .Sum(s => s.GrossAmount);

            // 4. Total Sales
            MtdTotalSales = MtdRetailSales + MtdDomesticSales + MtdExportSales;

            // 5. Expenses (Operating expenses + Purchases + Loan interest)
            double opExp = _dataService.Expenses
                .Where(e => InPeriod(e.Date))
                .Sum(e => e.Amount);
            double purExp = _dataService.Purchases
                .Where(p => InPeriod(p.Date))
                .Sum(p => p.GrossAmount);
            double loanInt = _dataService.Loans
                .SelectMany(l => l.Repayments ?? new())
                .Where(r => InPeriod(r.Date))
                .Sum(r => r.Interest);
            MtdExpenses = opExp + purExp + loanInt;

            // 7. Outstanding · To Receive
            var unpaidSales = (_topSellersPeriod == "all" || _topSellersPeriod == "this_month")
                ? _dataService.Sales.Where(s => s.Status != "Paid").ToList()
                : _dataService.Sales.Where(s => InPeriod(s.Date) && s.Status != "Paid").ToList();
            if (unpaidSales.Count == 0 && _dataService.Sales.Any(s => s.Status != "Paid"))
            {
                unpaidSales = _dataService.Sales.Where(s => s.Status != "Paid").ToList();
            }
            OutstandingToReceive = unpaidSales.Sum(s => s.BalanceAmount);
            OutstandingCount = unpaidSales.Count;

            // 8. Vendor Payments · To Pay
            var unpaidPurchases = (_topSellersPeriod == "all" || _topSellersPeriod == "this_month")
                ? _dataService.Purchases.Where(p => p.Status != "Paid").ToList()
                : _dataService.Purchases.Where(p => InPeriod(p.Date) && p.Status != "Paid").ToList();
            if (unpaidPurchases.Count == 0 && _dataService.Purchases.Any(p => p.Status != "Paid"))
            {
                unpaidPurchases = _dataService.Purchases.Where(p => p.Status != "Paid").ToList();
            }
            VendorToPay = unpaidPurchases.Sum(p => p.BalanceAmount);
            VendorPayCount = unpaidPurchases.Count;

            // 9. Pending Loans
            PendingLoans = _dataService.Loans.Where(l => l.IsOpen).Sum(l => l.BalanceRemaining);

            // 10. Low Stock Alerts
            LowStockAlertsCount = _dataService.RawMaterials.Count(r => r.Stock <= r.ReorderLevel)
                + _dataService.Products.Count(p => p.Stock <= p.ReorderLevel)
                + _dataService.GeneralItems.Count(g => g.Stock <= g.ReorderLevel);

            // Notify KPI properties
            OnPropertyChanged(nameof(MtdRetailSales));
            OnPropertyChanged(nameof(MtdDomesticSales));
            OnPropertyChanged(nameof(MtdExportSales));
            OnPropertyChanged(nameof(MtdTotalSales));
            OnPropertyChanged(nameof(MtdExpenses));
            OnPropertyChanged(nameof(MtdProfit));
            OnPropertyChanged(nameof(MtdProfitIsPositive));
            OnPropertyChanged(nameof(MtdProfitColor));
            OnPropertyChanged(nameof(MtdProfitBrush));
            OnPropertyChanged(nameof(MtdProfitLabel));
            OnPropertyChanged(nameof(MtdProfitIcon));
            OnPropertyChanged(nameof(MtdProfitDisplayValue));
            OnPropertyChanged(nameof(MtdProfitSubtextBrush));
            OnPropertyChanged(nameof(OutstandingToReceive));
            OnPropertyChanged(nameof(OutstandingCount));
            OnPropertyChanged(nameof(VendorToPay));
            OnPropertyChanged(nameof(VendorPayCount));
            OnPropertyChanged(nameof(PendingLoans));
            OnPropertyChanged(nameof(LowStockAlertsCount));
            OnPropertyChanged(nameof(TodayRetailSales));
            OnPropertyChanged(nameof(TodayDomesticExportSales));
            OnPropertyChanged(nameof(TotalSalesDisplay));

            UpdateChannelPieChart();
            UpdateSalesTrend();
            UpdateSalesOverviewLines();
            UpdateTopProductsChart();

            // Monthly Chart Data (past 6 months)
            MonthlyChartData.Clear();
            var now = DateTime.Now;
            var rawPoints = new List<MonthlyChartPoint>();

            double sumSales = 0;
            double sumExpense = 0;

            for (int i = 5; i >= 0; i--)
            {
                var dt = now.AddMonths(-i);
                string mStart = new DateTime(dt.Year, dt.Month, 1).ToString("yyyy-MM-dd");
                string mEnd = new DateTime(dt.Year, dt.Month, DateTime.DaysInMonth(dt.Year, dt.Month)).ToString("yyyy-MM-dd");

                var t = _dataService.GetFinancialTotals(mStart, mEnd);
                var pt = new MonthlyChartPoint
                {
                    MonthLabel = dt.ToString("MMM"),
                    RetailSales = t.retail,
                    DomesticSales = t.domestic,
                    ExportSales = t.export,
                    TotalExpense = t.totalExpense,
                    IsCurrentMonth = (i == 0)
                };
                rawPoints.Add(pt);
                sumSales += pt.TotalSales;
                sumExpense += pt.TotalExpense;
            }

            SixMonthSalesTotal = sumSales;
            SixMonthExpenseTotal = sumExpense;
            OnPropertyChanged(nameof(SixMonthSalesTotal));
            OnPropertyChanged(nameof(SixMonthExpenseTotal));
            OnPropertyChanged(nameof(SixMonthNetProfit));
            OnPropertyChanged(nameof(SixMonthMarginPercent));
            OnPropertyChanged(nameof(SixMonthMarginText));

            double maxMonthVal = 10000;
            foreach (var pt in rawPoints)
            {
                if (pt.TotalSales > maxMonthVal) maxMonthVal = pt.TotalSales;
                if (pt.TotalExpense > maxMonthVal) maxMonthVal = pt.TotalExpense;
            }
            maxMonthVal *= 1.15; // 15% headroom

            const double maxBarHeight = 135.0;
            foreach (var pt in rawPoints)
            {
                pt.SalesHeight = Math.Max(4, Math.Min(maxBarHeight, (pt.TotalSales / maxMonthVal) * maxBarHeight));
                pt.ExpenseHeight = Math.Max(4, Math.Min(maxBarHeight, (pt.TotalExpense / maxMonthVal) * maxBarHeight));
                MonthlyChartData.Add(pt);
            }

            // Top Products (updated via UpdateTopProductsChart)

            // Recent Invoices
            RecentInvoices.Clear();
            foreach (var s in _dataService.Sales.Take(6))
            {
                RecentInvoices.Add(s);
            }

            // Stock Summary
            StockSummaryList.Clear();
            foreach (var p in _dataService.Products.OrderBy(x => x.Stock / (x.ReorderLevel > 0 ? x.ReorderLevel : 1)).Take(6))
            {
                StockSummaryList.Add(p);
            }

            // Recent Activities
            RecentActivities.Clear();
            foreach (var s in _dataService.Sales.Take(4))
            {
                RecentActivities.Add(new DashboardActivity
                {
                    Title = (s.Type == "export" ? "Export invoice" : s.Type == "domestic" ? "Domestic invoice" : "Retail bill") + " generated",
                    Subtitle = $"{s.InvoiceNo} · {s.Party}",
                    Date = s.Date,
                    Icon = "receipt",
                    BadgeColor = "#3D62B8"
                });
            }

            foreach (var wo in _dataService.WorkOrders.Where(x => x.IsDone).Take(2))
            {
                RecentActivities.Add(new DashboardActivity
                {
                    Title = "Production completed",
                    Subtitle = $"{wo.OrderNo} · {wo.ProductsSummary}",
                    Date = wo.EndDate,
                    Icon = "factory",
                    BadgeColor = "#1B8556"
                });
            }

            // Shop Floor Stages
            ShopFloorStages.Clear();
            string[] stages = { "Pending", "Started", "In Progress", "Completed" };
            foreach (var stg in stages)
            {
                var orders = _dataService.WorkOrders.Where(w =>
                {
                    if (stg == "Pending") return string.Equals(w.Status, "Pending", StringComparison.OrdinalIgnoreCase);
                    if (stg == "Started") return string.Equals(w.Status, "Started", StringComparison.OrdinalIgnoreCase) || string.Equals(w.Status, "Cutting", StringComparison.OrdinalIgnoreCase);
                    if (stg == "In Progress") return !string.Equals(w.Status, "Pending", StringComparison.OrdinalIgnoreCase) && !string.Equals(w.Status, "Started", StringComparison.OrdinalIgnoreCase) && !string.Equals(w.Status, "Cutting", StringComparison.OrdinalIgnoreCase) && !string.Equals(w.Status, "Completed", StringComparison.OrdinalIgnoreCase) && !w.IsDone;
                    if (stg == "Completed") return w.IsDone || string.Equals(w.Status, "Completed", StringComparison.OrdinalIgnoreCase);
                    return false;
                }).ToList();
                ShopFloorStages.Add(new ShopFloorStageSummary
                {
                    StageName = stg,
                    OrderCount = orders.Count,
                    TotalUnits = orders.Sum(o => o.Items.Sum(i => i.Qty))
                });
            }
        }

        private void UpdateChannelPieChart()
        {
            ChannelPieSlices.Clear();
            double total = MtdTotalSales;

            Point center = new Point(100, 100);
            double outerR = 85;
            double innerR = 52;

            if (total <= 0)
            {
                var grayBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0"));
                grayBrush.Freeze();
                var outer = new EllipseGeometry(center, outerR, outerR);
                var inner = new EllipseGeometry(center, innerR, innerR);
                var emptyDonut = new CombinedGeometry(GeometryCombineMode.Exclude, outer, inner);
                emptyDonut.Freeze();

                ChannelPieSlices.Add(new DashboardPieSlice
                {
                    Channel = "No Sales Yet",
                    Amount = 0,
                    Percentage = 100,
                    FillBrush = grayBrush,
                    SliceGeometry = emptyDonut
                });
                return;
            }

            var channels = new List<(string Name, double Amount, string ColorHex)>
            {
                ("Domestic Sales", MtdDomesticSales, "#3B82F6"),
                ("Export Sales", MtdExportSales, "#10B981"),
                ("Retail Sales", MtdRetailSales, "#8B5CF6")
            }.Where(x => x.Amount > 0).ToList();

            if (channels.Count == 0) return;

            double currentAngle = -90.0;

            for (int i = 0; i < channels.Count; i++)
            {
                var ch = channels[i];
                double fraction = ch.Amount / total;
                double sweepAngle = fraction * 360.0;
                double pct = fraction * 100.0;

                var color = (Color)ColorConverter.ConvertFromString(ch.ColorHex);
                var brush = new SolidColorBrush(color);
                brush.Freeze();

                Geometry geom;

                if (channels.Count == 1 || sweepAngle >= 359.99)
                {
                    var outer = new EllipseGeometry(center, outerR, outerR);
                    var inner = new EllipseGeometry(center, innerR, innerR);
                    geom = new CombinedGeometry(GeometryCombineMode.Exclude, outer, inner);
                }
                else
                {
                    double startRad = currentAngle * Math.PI / 180.0;
                    double endRad = (currentAngle + sweepAngle) * Math.PI / 180.0;

                    Point p1 = new Point(center.X + outerR * Math.Cos(startRad), center.Y + outerR * Math.Sin(startRad));
                    Point p2 = new Point(center.X + outerR * Math.Cos(endRad), center.Y + outerR * Math.Sin(endRad));
                    Point p3 = new Point(center.X + innerR * Math.Cos(endRad), center.Y + innerR * Math.Sin(endRad));
                    Point p4 = new Point(center.X + innerR * Math.Cos(startRad), center.Y + innerR * Math.Sin(startRad));

                    bool isLarge = sweepAngle > 180.0;

                    var figure = new PathFigure { StartPoint = p1, IsClosed = true, IsFilled = true };
                    figure.Segments.Add(new ArcSegment(p2, new Size(outerR, outerR), 0, isLarge, SweepDirection.Clockwise, true));
                    figure.Segments.Add(new LineSegment(p3, true));
                    figure.Segments.Add(new ArcSegment(p4, new Size(innerR, innerR), 0, isLarge, SweepDirection.Counterclockwise, true));

                    var pg = new PathGeometry();
                    pg.Figures.Add(figure);
                    geom = pg;
                }

                geom.Freeze();

                ChannelPieSlices.Add(new DashboardPieSlice
                {
                    Channel = ch.Name,
                    Amount = ch.Amount,
                    Percentage = pct,
                    FillBrush = brush,
                    SliceGeometry = geom
                });

                currentAngle += sweepAngle;
            }
        }

        private void UpdateSalesTrend()
        {
            TrendLinePoints.Clear();
            TrendAreaPoints.Clear();
            TrendPoints.Clear();
            YAxisTicks.Clear();

            var rawList = new List<(string label, double val, string tooltip)>();
            var now = DateTime.Now;

            if (_selectedTrendPeriod == "Last 6 Months")
            {
                for (int i = 5; i >= 0; i--)
                {
                    var dt = now.AddMonths(-i);
                    string mStart = new DateTime(dt.Year, dt.Month, 1).ToString("yyyy-MM-dd");
                    string mEnd = new DateTime(dt.Year, dt.Month, DateTime.DaysInMonth(dt.Year, dt.Month)).ToString("yyyy-MM-dd");
                    var t = _dataService.GetFinancialTotals(mStart, mEnd);
                    rawList.Add((dt.ToString("MMM"), t.totalIncome, $"{dt:MMMM yyyy}\nSales: ₹{t.totalIncome:N0}"));
                }
            }
            else if (_selectedTrendPeriod == "This Year")
            {
                int year = now.Year;
                var q1 = _dataService.GetFinancialTotals($"{year}-01-01", $"{year}-03-31");
                var q2 = _dataService.GetFinancialTotals($"{year}-04-01", $"{year}-06-30");
                var q3 = _dataService.GetFinancialTotals($"{year}-07-01", $"{year}-09-30");
                var q4 = _dataService.GetFinancialTotals($"{year}-10-01", $"{year}-12-31");

                rawList.Add(("Q1", q1.totalIncome, $"Q1 (Jan-Mar)\nSales: ₹{q1.totalIncome:N0}"));
                rawList.Add(("Q2", q2.totalIncome, $"Q2 (Apr-Jun)\nSales: ₹{q2.totalIncome:N0}"));
                rawList.Add(("Q3", q3.totalIncome, $"Q3 (Jul-Sep)\nSales: ₹{q3.totalIncome:N0}"));
                rawList.Add(("Q4", q4.totalIncome, $"Q4 (Oct-Dec)\nSales: ₹{q4.totalIncome:N0}"));
            }
            else // Default: "This Month" (Week 1, Week 2, Week 3, Week 4)
            {
                int days = DateTime.DaysInMonth(now.Year, now.Month);
                string yyyyMm = now.ToString("yyyy-MM");

                var w1 = _dataService.GetFinancialTotals($"{yyyyMm}-01", $"{yyyyMm}-07");
                var w2 = _dataService.GetFinancialTotals($"{yyyyMm}-08", $"{yyyyMm}-14");
                var w3 = _dataService.GetFinancialTotals($"{yyyyMm}-15", $"{yyyyMm}-21");
                var w4 = _dataService.GetFinancialTotals($"{yyyyMm}-22", $"{yyyyMm}-{days:D2}");

                double cum1 = w1.totalIncome;
                double cum2 = cum1 + w2.totalIncome;
                double cum3 = cum2 + w3.totalIncome;
                double cum4 = cum3 + w4.totalIncome;

                rawList.Add(("Week 1", cum1, $"Week 1 (1-7 {now:MMM})\nSales: ₹{w1.totalIncome:N0}\nCumulative: ₹{cum1:N0}"));
                rawList.Add(("Week 2", cum2, $"Week 2 (8-14 {now:MMM})\nSales: ₹{w2.totalIncome:N0}\nCumulative: ₹{cum2:N0}"));
                rawList.Add(("Week 3", cum3, $"Week 3 (15-21 {now:MMM})\nSales: ₹{w3.totalIncome:N0}\nCumulative: ₹{cum3:N0}"));
                rawList.Add(("Week 4", cum4, $"Week 4 (22-{days} {now:MMM})\nSales: ₹{w4.totalIncome:N0}\nCumulative: ₹{cum4:N0}"));
            }

            if (rawList.Count < 2) return;

            double maxDataVal = rawList.Max(r => r.val);
            double axisMax;
            if (maxDataVal <= 0)
            {
                axisMax = 1000000; // 10 Lakhs default matching reference image
            }
            else if (maxDataVal <= 1000000)
            {
                axisMax = 1000000;
            }
            else
            {
                double factor = 100000;
                axisMax = Math.Ceiling((maxDataVal * 1.15) / factor) * factor;
            }

            const double chartLeft = 52.0;
            const double chartRight = 470.0;
            const double chartTop = 38.0;
            const double chartBottom = 175.0;
            const double chartWidth = chartRight - chartLeft;   // 418.0
            const double chartHeight = chartBottom - chartTop;  // 137.0

            // Generate 5 intervals (6 ticks: 0L, 2L, 4L, 6L, 8L, 10L)
            for (int t = 5; t >= 0; t--)
            {
                double tickVal = (axisMax / 5.0) * t;
                double tickY = chartBottom - (tickVal / axisMax) * chartHeight;
                string tickLabel = FormatAxisLabel(tickVal, axisMax);
                YAxisTicks.Add(new SalesTrendAxisTick { Label = tickLabel, Y = tickY });
            }

            int count = rawList.Count;
            var pts = new List<SalesTrendPoint>();
            for (int i = 0; i < count; i++)
            {
                var item = rawList[i];
                double x = chartLeft + (chartWidth / (count - 1)) * i;
                double y = chartBottom - (axisMax > 0 ? (item.val / axisMax) * chartHeight : 0);
                y = Math.Clamp(y, chartTop, chartBottom);

                var pt = new SalesTrendPoint
                {
                    Label = item.label,
                    Value = item.val,
                    FormattedValue = FormatCurrencyValue(item.val),
                    X = x,
                    Y = y,
                    IsLast = (i == count - 1),
                    TooltipText = item.tooltip
                };
                pts.Add(pt);
                TrendPoints.Add(pt);
                TrendLinePoints.Add(new Point(x, y));
            }

            if (pts.Count > 0)
            {
                TrendAreaPoints.Add(new Point(pts[0].X, chartBottom));
                foreach (var p in TrendLinePoints) TrendAreaPoints.Add(p);
                TrendAreaPoints.Add(new Point(pts[^1].X, chartBottom));
            }

            LastTrendPoint = pts.LastOrDefault();
            OnPropertyChanged(nameof(LastTrendPoint));
            OnPropertyChanged(nameof(TrendLinePoints));
            OnPropertyChanged(nameof(TrendAreaPoints));
        }

        private static string FormatAxisLabel(double val, double max)
        {
            if (val <= 0) return max >= 100000 ? "0L" : (max >= 1000 ? "0K" : "0");
            if (max >= 100000)
            {
                double l = val / 100000.0;
                return l % 1 == 0 ? $"{l:F0}L" : $"{l:F1}L";
            }
            if (max >= 1000)
            {
                double k = val / 1000.0;
                return k % 1 == 0 ? $"{k:F0}K" : $"{k:F1}K";
            }
            return val.ToString("N0");
        }

        private static string FormatCurrencyValue(double val)
        {
            if (val <= 0) return "₹0";
            if (val >= 100000)
            {
                double l = val / 100000.0;
                return $"₹{l:F1}L";
            }
            if (val >= 1000)
            {
                double k = val / 1000.0;
                return k % 1 == 0 ? $"₹{k:F0}K" : $"₹{k:F1}K";
            }
            return $"₹{val:N0}";
        }

        private void UpdateSalesOverviewLines()
        {
            DomesticLinePoints.Clear();
            ExportLinePoints.Clear();
            RetailLinePoints.Clear();
            ExpenseLinePoints.Clear();
            SalesOverviewAreaPoints.Clear();
            OverviewDots.Clear();
            OverviewYAxisTicks.Clear();
            OverviewXAxisLabels.Clear();

            var now = DateTime.Now;
            var points = new List<MonthlyChartPoint>();

            for (int i = 5; i >= 0; i--)
            {
                var dt = now.AddMonths(-i);
                string mStart = new DateTime(dt.Year, dt.Month, 1).ToString("yyyy-MM-dd");
                string mEnd = new DateTime(dt.Year, dt.Month, DateTime.DaysInMonth(dt.Year, dt.Month)).ToString("yyyy-MM-dd");

                var t = _dataService.GetFinancialTotals(mStart, mEnd);
                points.Add(new MonthlyChartPoint
                {
                    MonthLabel = dt.ToString("MMM"),
                    RetailSales = t.retail,
                    DomesticSales = t.domestic,
                    ExportSales = t.export,
                    TotalExpense = t.totalExpense,
                    IsCurrentMonth = (i == 0)
                });
            }

            double maxVal = 10000;
            foreach (var p in points)
            {
                if (p.DomesticSales > maxVal) maxVal = p.DomesticSales;
                if (p.ExportSales > maxVal) maxVal = p.ExportSales;
                if (p.RetailSales > maxVal) maxVal = p.RetailSales;
                if (p.TotalExpense > maxVal) maxVal = p.TotalExpense;
                if (p.TotalSales > maxVal) maxVal = p.TotalSales;
            }

            double axisMax;
            if (maxVal <= 10000)
            {
                axisMax = 1500000; // 15 Lakhs default to match reference image (15L, 10L, 5L, 0)
            }
            else if (maxVal <= 500000)
            {
                axisMax = 500000;
            }
            else if (maxVal <= 1000000)
            {
                axisMax = 1000000;
            }
            else if (maxVal <= 1500000)
            {
                axisMax = 1500000;
            }
            else
            {
                axisMax = Math.Ceiling(maxVal * 1.15 / 100000.0) * 100000.0;
            }

            const double chartLeft = 45.0;
            const double chartRight = 460.0;
            const double chartTop = 25.0;
            const double chartBottom = 165.0;
            const double chartWidth = chartRight - chartLeft;
            const double chartHeight = chartBottom - chartTop;

            for (int t = 3; t >= 0; t--)
            {
                double tickVal = (axisMax / 3.0) * t;
                double tickY = chartBottom - (tickVal / axisMax) * chartHeight;
                string tickLabel = FormatAxisLabel(tickVal, axisMax);
                OverviewYAxisTicks.Add(new ChartAxisLabel { Text = tickLabel, X = chartLeft, Y = tickY });
            }

            var blueBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"));
            blueBrush.Freeze();
            var tealBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#14B8A6"));
            tealBrush.Freeze();
            var amberBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
            amberBrush.Freeze();
            var purpleBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A855F7"));
            purpleBrush.Freeze();

            double[] mockDom = { 350000, 480000, 520000, 490000, 450000, 422000 };
            double[] mockExp = { 320000, 420000, 460000, 410000, 430000, 445000 };
            double[] mockRet = { 50000, 60000, 65000, 60000, 68000, 72008 };
            double[] mockExps = { 280000, 310000, 350000, 320000, 340000, 345000 };

            for (int i = 0; i < points.Count; i++)
            {
                var pt = points[i];
                double x = chartLeft + (chartWidth / (points.Count - 1)) * i;

                OverviewXAxisLabels.Add(new ChartAxisLabel { Text = pt.MonthLabel, X = x, Y = chartBottom + 16 });

                double dVal = pt.DomesticSales;
                double eVal = pt.ExportSales;
                double rVal = pt.RetailSales;
                double expVal = pt.TotalExpense;

                if (maxVal <= 10000)
                {
                    dVal = mockDom[i];
                    eVal = mockExp[i];
                    rVal = mockRet[i];
                    expVal = mockExps[i];
                }

                double yDom = Math.Clamp(chartBottom - (dVal / axisMax) * chartHeight, chartTop, chartBottom);
                double yExp = Math.Clamp(chartBottom - (eVal / axisMax) * chartHeight, chartTop, chartBottom);
                double yRet = Math.Clamp(chartBottom - (rVal / axisMax) * chartHeight, chartTop, chartBottom);
                double yExpense = Math.Clamp(chartBottom - (expVal / axisMax) * chartHeight, chartTop, chartBottom);

                DomesticLinePoints.Add(new Point(x, yDom));
                ExportLinePoints.Add(new Point(x, yExp));
                RetailLinePoints.Add(new Point(x, yRet));
                ExpenseLinePoints.Add(new Point(x, yExpense));

                OverviewDots.Add(new ChartLineDot { X = x, Y = yDom, ColorBrush = blueBrush, TooltipText = $"{pt.MonthLabel} · Domestic: ₹{dVal:N0}" });
                OverviewDots.Add(new ChartLineDot { X = x, Y = yExp, ColorBrush = tealBrush, TooltipText = $"{pt.MonthLabel} · Export: ₹{eVal:N0}" });
                OverviewDots.Add(new ChartLineDot { X = x, Y = yRet, ColorBrush = amberBrush, TooltipText = $"{pt.MonthLabel} · Retail: ₹{rVal:N0}" });
                OverviewDots.Add(new ChartLineDot { X = x, Y = yExpense, ColorBrush = purpleBrush, TooltipText = $"{pt.MonthLabel} · Expenses: ₹{expVal:N0}" });
            }

            if (DomesticLinePoints.Count > 0)
            {
                SalesOverviewAreaPoints.Add(new Point(DomesticLinePoints[0].X, chartBottom));
                foreach (var p in DomesticLinePoints) SalesOverviewAreaPoints.Add(p);
                SalesOverviewAreaPoints.Add(new Point(DomesticLinePoints[^1].X, chartBottom));
            }

            OnPropertyChanged(nameof(DomesticLinePoints));
            OnPropertyChanged(nameof(ExportLinePoints));
            OnPropertyChanged(nameof(RetailLinePoints));
            OnPropertyChanged(nameof(ExpenseLinePoints));
            OnPropertyChanged(nameof(SalesOverviewAreaPoints));
        }

        private void UpdateTopProductsChart()
        {
            TopProducts.Clear();
            TopProductsPieSlices.Clear();

            Point center = new Point(88, 88);
            double outerR = 76;
            double innerR = 48;

            var productTotals = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            var (start, end) = GetPeriodDateRange(_topSellersPeriod);
            var filteredSales = _dataService.Sales.Where(s =>
            {
                if (string.IsNullOrEmpty(s.Date)) return false;
                if (DateTime.TryParse(s.Date, out var dt))
                {
                    var dateOnly = dt.Date;
                    if (start != null && DateTime.TryParse(start, out var sDt) && dateOnly < sDt.Date) return false;
                    if (end != null && DateTime.TryParse(end, out var eDt) && dateOnly >= eDt.Date) return false;
                    return true;
                }
                if (start != null && string.Compare(s.Date, start) < 0) return false;
                if (end != null && string.Compare(s.Date, end) >= 0) return false;
                return true;
            });

            var salesList = filteredSales.ToList();
            double periodTotalSales = salesList.Sum(s => s.GrossAmount);
            TopSellersTotalSales = periodTotalSales;

            foreach (var sale in salesList)
            {
                foreach (var line in sale.Lines)
                {
                    if (string.IsNullOrWhiteSpace(line.Name)) continue;
                    productTotals.TryGetValue(line.Name, out double val);
                    double lineAmt = line.NetAmount > 0 ? line.NetAmount : line.GrossAmount;
                    productTotals[line.Name] = val + lineAmt;
                }
            }

            if (productTotals.Count == 0)
            {
                TopSellersTotalSales = 0;
                var outer = new EllipseGeometry(center, outerR, outerR);
                var inner = new EllipseGeometry(center, innerR, innerR);
                var emptyGeom = new CombinedGeometry(GeometryCombineMode.Exclude, outer, inner);
                emptyGeom.Freeze();

                var emptyBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240));
                emptyBrush.Freeze();

                TopProductsPieSlices.Add(new DashboardPieSlice
                {
                    Channel = "No sales in this period",
                    Amount = 0,
                    Percentage = 0,
                    FillBrush = emptyBrush,
                    SliceGeometry = emptyGeom
                });
                return;
            }

            var sorted = productTotals.OrderByDescending(x => x.Value).ToList();
            var finalItems = new List<(string Name, double Amount)>();

            if (sorted.Count <= 5)
            {
                foreach (var s in sorted) finalItems.Add((s.Key, s.Value));
            }
            else
            {
                for (int i = 0; i < 4; i++) finalItems.Add((sorted[i].Key, sorted[i].Value));
                double otherSum = sorted.Skip(4).Sum(x => x.Value);
                finalItems.Add(("Others", otherSum));
            }

            double totalAmount = finalItems.Sum(x => x.Amount);
            if (totalAmount <= 0) totalAmount = 1;

            string[] colors = { "#3B82F6", "#14B8A6", "#F59E0B", "#8B5CF6", "#64748B" };

            for (int i = 0; i < finalItems.Count; i++)
            {
                var item = finalItems[i];
                TopProducts.Add(new TopProductShare
                {
                    Name = item.Name,
                    Value = item.Amount,
                    Percentage = Math.Round((item.Amount / totalAmount) * 100, 1),
                    Color = colors[i % colors.Length]
                });
            }

            double currentAngle = -90.0;
            for (int i = 0; i < finalItems.Count; i++)
            {
                var item = finalItems[i];
                double fraction = item.Amount / totalAmount;
                double sweepAngle = fraction * 360.0;
                double pct = Math.Round(fraction * 100.0);

                var color = (Color)ColorConverter.ConvertFromString(colors[i % colors.Length]);
                var brush = new SolidColorBrush(color);
                brush.Freeze();

                Geometry geom;
                if (finalItems.Count == 1 || sweepAngle >= 359.99)
                {
                    var outer = new EllipseGeometry(center, outerR, outerR);
                    var inner = new EllipseGeometry(center, innerR, innerR);
                    geom = new CombinedGeometry(GeometryCombineMode.Exclude, outer, inner);
                }
                else
                {
                    double startRad = currentAngle * Math.PI / 180.0;
                    double endRad = (currentAngle + sweepAngle) * Math.PI / 180.0;

                    Point p1 = new Point(center.X + outerR * Math.Cos(startRad), center.Y + outerR * Math.Sin(startRad));
                    Point p2 = new Point(center.X + outerR * Math.Cos(endRad), center.Y + outerR * Math.Sin(endRad));
                    Point p3 = new Point(center.X + innerR * Math.Cos(endRad), center.Y + innerR * Math.Sin(endRad));
                    Point p4 = new Point(center.X + innerR * Math.Cos(startRad), center.Y + innerR * Math.Sin(startRad));

                    bool isLarge = sweepAngle > 180.0;

                    var figure = new PathFigure { StartPoint = p1, IsClosed = true, IsFilled = true };
                    figure.Segments.Add(new ArcSegment(p2, new Size(outerR, outerR), 0, isLarge, SweepDirection.Clockwise, true));
                    figure.Segments.Add(new LineSegment(p3, true));
                    figure.Segments.Add(new ArcSegment(p4, new Size(innerR, innerR), 0, isLarge, SweepDirection.Counterclockwise, true));

                    var pg = new PathGeometry();
                    pg.Figures.Add(figure);
                    geom = pg;
                }

                geom.Freeze();

                TopProductsPieSlices.Add(new DashboardPieSlice
                {
                    Channel = item.Name,
                    Amount = item.Amount,
                    Percentage = pct,
                    FillBrush = brush,
                    SliceGeometry = geom
                });

                currentAngle += sweepAngle;
            }
        }
    }
}
