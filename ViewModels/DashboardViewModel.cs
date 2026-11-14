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
    public class DashboardPieSlice
    {
        public string Channel { get; set; } = string.Empty;
        public double Amount { get; set; }
        public double Percentage { get; set; }
        public string PercentageDisplay => $"{Percentage:F1}%";
        public string AmountDisplay => $"₹{Amount:N0}";
        public Brush FillBrush { get; set; } = Brushes.Gray;
        public Geometry SliceGeometry { get; set; } = Geometry.Empty;
        public string TooltipText => $"{Channel}: ₹{Amount:N0} ({Percentage:F1}%)";
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
        public string MtdProfitColor => MtdProfit >= 0 ? "#10B981" : "#DC2626";
        public Brush MtdProfitBrush
        {
            get
            {
                var grad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
                if (MtdProfit >= 0)
                {
                    grad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#10B981"), 0));
                    grad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#047857"), 1));
                }
                else
                {
                    grad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#EF4444"), 0));
                    grad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#B91C1C"), 1));
                }
                return grad;
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

        public PointCollection TrendLinePoints { get; } = new();
        public PointCollection TrendAreaPoints { get; } = new();
        public ObservableCollection<SalesTrendPoint> TrendPoints { get; } = new();
        public ObservableCollection<SalesTrendAxisTick> YAxisTicks { get; } = new();
        public SalesTrendPoint? LastTrendPoint { get; private set; }
        public ObservableCollection<TopProductShare> TopProducts { get; } = new();
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

            _dataService.DataChanged += RefreshData;
            RefreshData();
        }

        public void RefreshData()
        {
            string firstOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).ToString("yyyy-MM-dd");

            // 1. Retail Sales MTD
            MtdRetailSales = _dataService.Sales
                .Where(s => string.Compare(s.Date, firstOfMonth) >= 0 && s.Type == "retail")
                .Sum(s => s.GrossAmount);

            // 2. Domestic Sales MTD
            MtdDomesticSales = _dataService.Sales
                .Where(s => string.Compare(s.Date, firstOfMonth) >= 0 && s.Type == "domestic")
                .Sum(s => s.GrossAmount);

            // 3. Export Sales MTD
            MtdExportSales = _dataService.Sales
                .Where(s => string.Compare(s.Date, firstOfMonth) >= 0 && s.Type == "export")
                .Sum(s => s.GrossAmount);

            // 4. Total Sales (MTD)
            MtdTotalSales = MtdRetailSales + MtdDomesticSales + MtdExportSales;

            // 5. Expenses · this month (Operating expenses + Purchases + Loan interest)
            double opExp = _dataService.Expenses
                .Where(e => string.Compare(e.Date, firstOfMonth) >= 0)
                .Sum(e => e.Amount);
            double purExp = _dataService.Purchases
                .Where(p => string.Compare(p.Date, firstOfMonth) >= 0)
                .Sum(p => p.GrossAmount);
            double loanInt = _dataService.Loans
                .SelectMany(l => l.Repayments ?? new())
                .Where(r => string.Compare(r.Date, firstOfMonth) >= 0)
                .Sum(r => r.Interest);
            MtdExpenses = opExp + purExp + loanInt;

            // 7. Outstanding · To Receive
            var unpaidSales = _dataService.Sales.Where(s => s.Status != "Paid").ToList();
            OutstandingToReceive = unpaidSales.Sum(s => s.BalanceAmount);
            OutstandingCount = unpaidSales.Count;

            // 8. Vendor Payments · To Pay
            var unpaidPurchases = _dataService.Purchases.Where(p => p.Status != "Paid").ToList();
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
            OnPropertyChanged(nameof(MtdProfitColor));
            OnPropertyChanged(nameof(MtdProfitBrush));
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

            // Top Products
            TopProducts.Clear();
            var productTotals = new Dictionary<string, double>();
            foreach (var sale in _dataService.Sales)
            {
                foreach (var line in sale.Lines)
                {
                    productTotals.TryGetValue(line.Name, out double val);
                    productTotals[line.Name] = val + line.NetAmount;
                }
            }

            double grandTotal = productTotals.Values.Sum();
            if (grandTotal <= 0) grandTotal = 1;

            string[] colors = { "#3D62B8", "#23968A", "#E5A62E", "#8B5CF6", "#C23A32", "#78839A" };
            int cIdx = 0;
            foreach (var kvp in productTotals.OrderByDescending(x => x.Value).Take(5))
            {
                TopProducts.Add(new TopProductShare
                {
                    Name = kvp.Key,
                    Value = kvp.Value,
                    Percentage = Math.Round((kvp.Value / grandTotal) * 100, 1),
                    Color = colors[cIdx % colors.Length]
                });
                cIdx++;
            }

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
                var orders = _dataService.WorkOrders.Where(w => w.Status == stg).ToList();
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
                int fyStartYear = now.Month >= 4 ? now.Year : now.Year - 1;
                var q1 = _dataService.GetFinancialTotals($"{fyStartYear}-04-01", $"{fyStartYear}-06-30");
                var q2 = _dataService.GetFinancialTotals($"{fyStartYear}-07-01", $"{fyStartYear}-09-30");
                var q3 = _dataService.GetFinancialTotals($"{fyStartYear}-10-01", $"{fyStartYear}-12-31");
                var q4 = _dataService.GetFinancialTotals($"{fyStartYear + 1}-01-01", $"{fyStartYear + 1}-03-31");

                rawList.Add(("Q1", q1.totalIncome, $"Q1 (Apr-Jun)\nSales: ₹{q1.totalIncome:N0}"));
                rawList.Add(("Q2", q2.totalIncome, $"Q2 (Jul-Sep)\nSales: ₹{q2.totalIncome:N0}"));
                rawList.Add(("Q3", q3.totalIncome, $"Q3 (Oct-Dec)\nSales: ₹{q3.totalIncome:N0}"));
                rawList.Add(("Q4", q4.totalIncome, $"Q4 (Jan-Mar)\nSales: ₹{q4.totalIncome:N0}"));
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
    }
}
