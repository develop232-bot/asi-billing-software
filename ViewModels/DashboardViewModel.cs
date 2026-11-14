using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AsiErp.Commands;
using AsiErp.Models;
using AsiErp.Services;

namespace AsiErp.ViewModels
{
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

        public double TodayRetailSales { get; private set; }
        public double TodayDomesticExportSales { get; private set; }
        public double MtdTotalSales { get; private set; }
        public double OutstandingToReceive { get; private set; }
        public int OutstandingCount { get; private set; }
        public double VendorToPay { get; private set; }
        public int VendorPayCount { get; private set; }
        public int LowStockAlertsCount { get; private set; }

        public double SixMonthSalesTotal { get; private set; }
        public double SixMonthExpenseTotal { get; private set; }
        public double SixMonthNetProfit => SixMonthSalesTotal - SixMonthExpenseTotal;
        public double SixMonthMarginPercent => SixMonthSalesTotal > 0 ? Math.Round((SixMonthNetProfit / SixMonthSalesTotal) * 100, 1) : 0;
        public string SixMonthMarginText => $"Margin: {(SixMonthMarginPercent >= 0 ? "+" : "")}{SixMonthMarginPercent:F1}%";

        public ObservableCollection<MonthlyChartPoint> MonthlyChartData { get; } = new();
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
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            string firstOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).ToString("yyyy-MM-dd");

            // Today's Sales
            TodayRetailSales = _dataService.Sales
                .Where(s => s.Date == today && s.Type == "retail")
                .Sum(s => s.GrossAmount);

            TodayDomesticExportSales = _dataService.Sales
                .Where(s => s.Date == today && (s.Type == "domestic" || s.Type == "export"))
                .Sum(s => s.GrossAmount);

            // MTD Sales
            MtdTotalSales = _dataService.Sales
                .Where(s => string.Compare(s.Date, firstOfMonth) >= 0)
                .Sum(s => s.NetAmount);

            // Outstanding
            var unpaidSales = _dataService.Sales.Where(s => s.Status != "Paid").ToList();
            OutstandingToReceive = unpaidSales.Sum(s => s.BalanceAmount);
            OutstandingCount = unpaidSales.Count;

            // Vendor to Pay
            var unpaidPurchases = _dataService.Purchases.Where(p => p.Status != "Paid").ToList();
            VendorToPay = unpaidPurchases.Sum(p => p.BalanceAmount);
            VendorPayCount = unpaidPurchases.Count;

            // Low Stock
            LowStockAlertsCount = _dataService.RawMaterials.Count(r => r.Stock <= r.ReorderLevel)
                + _dataService.Products.Count(p => p.Stock <= p.ReorderLevel)
                + _dataService.GeneralItems.Count(g => g.Stock <= g.ReorderLevel);

            // Notify KPI properties
            OnPropertyChanged(nameof(TodayRetailSales));
            OnPropertyChanged(nameof(TodayDomesticExportSales));
            OnPropertyChanged(nameof(MtdTotalSales));
            OnPropertyChanged(nameof(OutstandingToReceive));
            OnPropertyChanged(nameof(OutstandingCount));
            OnPropertyChanged(nameof(VendorToPay));
            OnPropertyChanged(nameof(VendorPayCount));
            OnPropertyChanged(nameof(LowStockAlertsCount));

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
    }
}
