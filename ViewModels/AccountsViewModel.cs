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
    public class LineChartPoint
    {
        public string MonthLabel { get; set; } = string.Empty;
        public double X { get; set; }
        public double IncomeY { get; set; }
        public double ExpenseY { get; set; }
        public double Income { get; set; }
        public double Expense { get; set; }
        public string TooltipText => $"{MonthLabel}\nIncome: ₹{Income:N0}\nExpenses: ₹{Expense:N0}\nNet P&L: {(Income - Expense >= 0 ? "+" : "")}₹{(Income - Expense):N0}";
    }

    public class PieChartSlice
    {
        public string Category { get; set; } = string.Empty;
        public double Amount { get; set; }
        public double Percentage { get; set; }
        public string PercentageDisplay => $"{Percentage:F1}%";
        public string AmountDisplay => $"₹{Amount:N0}";
        public Brush FillBrush { get; set; } = Brushes.Gray;
        public Geometry SliceGeometry { get; set; } = Geometry.Empty;
        public string TooltipText => $"{Category}: ₹{Amount:N0} ({Percentage:F1}%)";
    }

    public class PlExpenseCategoryRow
    {
        public string Category { get; set; } = string.Empty;
        public double Amount { get; set; }
    }

    public class PlMonthSummaryRow
    {
        public string MonthLabel { get; set; } = string.Empty;
        public double Income { get; set; }
        public double Expenses { get; set; }
        public double ProfitLoss => Income - Expenses;
        public string MarginText => Income > 0 ? $"{(ProfitLoss / Income * 100):F1}%" : "—";
        public bool IsProfit => ProfitLoss >= 0;
    }

    public class AccountsViewModel : ViewModelBase
    {
        private readonly AppDataService _dataService;
        private readonly Action<string, object?> _openDialogAction;

        private string _activeTab = "dashboard"; // dashboard, income, exp, inv, loans, assets, pl, bs
        private string _selectedPeriod = "m0"; // m0, m1, q, fy, all
        private string _selectedIncomeChannel = "all";
        private string _selectedExpenseFreq = "all";
        private string _expenseSearchText = string.Empty;

        public string ExpenseSearchText
        {
            get => _expenseSearchText;
            set
            {
                if (SetField(ref _expenseSearchText, value)) ApplyExpenseFilter();
            }
        }

        // Income collections
        public ObservableCollection<SaleDoc> IncomeRows { get; } = new();

        // Expense collections
        public ObservableCollection<ExpenseItem> ExpenseRows { get; } = new();

        // Investment collections
        public ObservableCollection<InvestmentItem> InvestmentRows { get; } = new();

        // Loan collections
        public ObservableCollection<LoanItem> LoanRows { get; } = new();

        // Asset collections
        public ObservableCollection<AssetItem> AssetRows { get; } = new();

        // P&L collections
        public ObservableCollection<PlExpenseCategoryRow> PlExpenseCategories { get; } = new();
        public ObservableCollection<PlMonthSummaryRow> PlMonthBreakdowns { get; } = new();

        public string ActiveTab
        {
            get => _activeTab;
            set
            {
                if (SetField(ref _activeTab, value))
                {
                    OnPropertyChanged(nameof(ActiveTabTitle));
                    RefreshData();
                }
            }
        }

        public string ActiveTabTitle => ActiveTab switch
        {
            "dashboard" => "Finance & Accounts Dashboard",
            "income" => "Income",
            "exp" => "Expenses",
            "inv" => "Investments",
            "loans" => "Loans",
            "assets" => "Assets",
            "pl" => "Profit & Loss",
            "bs" => "Balance Sheet",
            _ => "Finance & Accounts"
        };

        public string SelectedPeriod
        {
            get => _selectedPeriod;
            set
            {
                if (SetField(ref _selectedPeriod, value)) RefreshData();
            }
        }

        public string SelectedIncomeChannel
        {
            get => _selectedIncomeChannel;
            set
            {
                if (SetField(ref _selectedIncomeChannel, value)) ApplyIncomeFilter();
            }
        }

        public string SelectedExpenseFreq
        {
            get => _selectedExpenseFreq;
            set
            {
                if (SetField(ref _selectedExpenseFreq, value)) ApplyExpenseFilter();
            }
        }

        // --- INCOME STATS ---
        public double IncomeTotal { get; private set; }
        public double DomesticIncome { get; private set; }
        public double ExportIncome { get; private set; }
        public double RetailIncome { get; private set; }

        // --- EXPENSE STATS ---
        public double EmployeeExpenseTotal { get; private set; }
        public double UtilityExpenseTotal { get; private set; }
        public double MiscExpenseTotal { get; private set; }
        public double PurchaseExpenseTotal { get; private set; }

        public double SalaryExpenseTotal => EmployeeExpenseTotal;
        public double UtilitiesExpenseTotal => UtilityExpenseTotal;
        public double YearlyExpenseTotal => MiscExpenseTotal;
        public double MiscellaneousExpenseTotal => MiscExpenseTotal;
        public double DailyExpenseTotal { get; private set; }
        public double MonthlyExpenseTotal { get; private set; }

        // --- INVESTMENT STATS ---
        public double TotalInvested { get; private set; }
        public double AllTimeProfit { get; private set; }
        public double RecoveryPercent { get; private set; }
        public int PreStartInvestmentCount { get; private set; }

        // --- LOAN STATS ---
        public double TotalLoansTaken { get; private set; }
        public double TotalLoanInterestPaid { get; private set; }
        public double TotalLoanPrincipalPaid { get; private set; }
        public double TotalLoansOutstanding { get; private set; }
        public int OpenLoansCount { get; private set; }
        public int LoansCount { get; private set; }

        // --- ASSET STATS ---
        public int AssetsCount { get; private set; }
        public double TotalAssetsCost { get; private set; }
        public double TotalAssetsDepreciation { get; private set; }
        public double TotalAssetsBookValue { get; private set; }

        // --- P&L STATS ---
        public double PlTotalIncome { get; private set; }
        public double PlTotalExpense { get; private set; }
        public double PlNetProfit { get; private set; }
        public double PlTotalSide => Math.Max(PlTotalIncome, PlTotalExpense);
        public bool PlIsProfit => PlNetProfit >= 0;
        public string PlPeriodLabel { get; private set; } = "This month";

        public string PlStatusWord => PlNetProfit >= 0 ? "PROFIT" : "LOSS";
        public string PlProfitLossColor => PlNetProfit >= 0 ? "#10B981" : "#DC2626";
        public string PlNetProfitDisplay => PlNetProfit >= 0 ? $"+₹{PlNetProfit:N0}" : $"-₹{Math.Abs(PlNetProfit):N0}";
        public Brush PlCardBrush
        {
            get
            {
                var grad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
                if (PlIsProfit)
                {
                    grad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#10B981"), 0));
                    grad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#059669"), 1));
                }
                else
                {
                    grad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#EF4444"), 0));
                    grad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#DC2626"), 1));
                }
                return grad;
            }
        }
        public string PlCardLabel => PlIsProfit ? "Profit" : "Loss";
        public string PlValueDisplay => $"₹{Math.Abs(PlNetProfit):N0}";

        public PointCollection IncomeLinePoints { get; } = new();
        public PointCollection ExpenseLinePoints { get; } = new();
        public PointCollection IncomeAreaPoints { get; } = new();
        public PointCollection ExpenseAreaPoints { get; } = new();
        public ObservableCollection<LineChartPoint> LineChartPoints { get; } = new();
        public string ChartYMaxLabel { get; private set; } = "₹0";
        public string ChartYMidLabel { get; private set; } = "₹0";
        public string ChartYZeroLabel => "₹0";

        public ObservableCollection<PieChartSlice> PieChartSlices { get; } = new();
        public bool HasExpenseSlices => PieChartSlices.Count > 0;
        public string TotalExpensesDisplay => $"₹{PlTotalExpense:N0}";

        // --- BALANCE SHEET STATS ---
        public double BsCap { get; private set; }
        public double BsProfit { get; private set; }
        public double BsStock { get; private set; }
        public double BsLoans { get; private set; }
        public double BsPay { get; private set; }
        public double BsGst { get; private set; }
        public double BsLiabilitiesTotal { get; private set; }

        public double BsFixedAssets { get; private set; }
        public double BsRecv { get; private set; }
        public double BsCash { get; private set; }
        public double BsAssetsTotal { get; private set; }

        public double BsDifference => Math.Abs(BsAssetsTotal - BsLiabilitiesTotal);
        public bool BsIsBalanced => BsDifference < 1.0;
        public string BsTodayDate => DateTime.Now.ToString("dd MMM yyyy");

        // --- DASHBOARD SHORTCUTS ---
        public double DashReceivables => BsRecv;
        public double DashPayables => BsPay;
        public double DashCashBank => BsCash;
        public double DashFixedAssets => BsFixedAssets;
        public double DashStockValue => BsStock;
        public double DashTotalLoans => BsLoans;
        public double DashCapital => BsCap;
        public double DashAllTimeProfit => BsProfit;

        // Commands
        public RelayCommand SetTabCommand { get; }
        public RelayCommand SetPeriodCommand { get; }
        public RelayCommand SetIncomeChannelCommand { get; }
        public RelayCommand SetExpenseFreqCommand { get; }

        public RelayCommand AddExpenseCommand { get; }
        public RelayCommand DeleteExpenseCommand { get; }

        public RelayCommand AddInvestmentCommand { get; }
        public RelayCommand DeleteInvestmentCommand { get; }

        public RelayCommand AddLoanCommand { get; }
        public RelayCommand RepayLoanCommand { get; }
        public RelayCommand DeleteLoanCommand { get; }

        public RelayCommand AddAssetCommand { get; }
        public RelayCommand DeleteAssetCommand { get; }

        public RelayCommand ViewDocCommand { get; }

        public AccountsViewModel(AppDataService dataService, Action<string, object?> openDialogAction, string initialTab = "dashboard")
        {
            _dataService = dataService;
            _openDialogAction = openDialogAction;
            _activeTab = initialTab;

            SetTabCommand = new RelayCommand(p => ActiveTab = p?.ToString() ?? "dashboard");
            SetPeriodCommand = new RelayCommand(p => SelectedPeriod = p?.ToString() ?? "m0");
            SetIncomeChannelCommand = new RelayCommand(p => SelectedIncomeChannel = p?.ToString() ?? "all");
            SetExpenseFreqCommand = new RelayCommand(p => SelectedExpenseFreq = p?.ToString() ?? "all");

            AddExpenseCommand = new RelayCommand(_ => _openDialogAction("expense_add", null));
            DeleteExpenseCommand = new RelayCommand(async p =>
            {
                if (p is ExpenseItem exp && !exp.IsAuto)
                {
                    await _dataService.DeleteExpenseAsync(exp.Id);
                }
            });

            AddInvestmentCommand = new RelayCommand(_ => _openDialogAction("investment_add", null));
            DeleteInvestmentCommand = new RelayCommand(async p =>
            {
                if (p is InvestmentItem inv)
                {
                    await _dataService.DeleteInvestmentAsync(inv.Id);
                }
            });

            AddLoanCommand = new RelayCommand(_ => _openDialogAction("loan_add", null));
            RepayLoanCommand = new RelayCommand(p =>
            {
                if (p is LoanItem loan) _openDialogAction("loan_repay", loan);
            });
            DeleteLoanCommand = new RelayCommand(async p =>
            {
                if (p is LoanItem loan)
                {
                    var res = MessageBox.Show($"Delete loan from '{loan.Lender}'?\nIt will also be removed from the balance sheet.",
                        "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (res == MessageBoxResult.Yes)
                    {
                        await _dataService.DeleteLoanAsync(loan.Id);
                    }
                }
            });

            AddAssetCommand = new RelayCommand(_ => _openDialogAction("asset_add", null));
            DeleteAssetCommand = new RelayCommand(async p =>
            {
                if (p is AssetItem asset)
                {
                    var res = MessageBox.Show($"Delete asset '{asset.Name}'?\nIt will also be removed from the balance sheet.",
                        "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (res == MessageBoxResult.Yes)
                    {
                        await _dataService.DeleteAssetAsync(asset.Id);
                    }
                }
            });

            ViewDocCommand = new RelayCommand(p =>
            {
                if (p is SaleDoc sale) _openDialogAction("view_doc", sale);
            });

            _dataService.DataChanged += RefreshData;
            RefreshData();
        }

        public (string from, string to, string label) GetDateRange(string periodKey)
        {
            var now = DateTime.Now;
            if (periodKey == "m0")
            {
                var f = new DateTime(now.Year, now.Month, 1);
                var t = now;
                return (f.ToString("yyyy-MM-dd"), t.ToString("yyyy-MM-dd"), "this month");
            }
            if (periodKey == "m1")
            {
                var prev = now.AddMonths(-1);
                var f = new DateTime(prev.Year, prev.Month, 1);
                var t = new DateTime(prev.Year, prev.Month, DateTime.DaysInMonth(prev.Year, prev.Month));
                return (f.ToString("yyyy-MM-dd"), t.ToString("yyyy-MM-dd"), "last month");
            }
            if (periodKey == "q")
            {
                var f = now.AddMonths(-3);
                return (f.ToString("yyyy-MM-dd"), now.ToString("yyyy-MM-dd"), "last 3 months");
            }
            if (periodKey == "fy")
            {
                int fyYr = now.Month >= 4 ? now.Year : now.Year - 1;
                return ($"{fyYr}-04-01", now.ToString("yyyy-MM-dd"), $"FY {fyYr}-{(fyYr + 1) % 100:D2}");
            }
            return ("0000-00-00", "9999-12-31", "all time");
        }

        public void RefreshData()
        {
            var range = GetDateRange(SelectedPeriod);
            PlPeriodLabel = range.label;

            // Compute Income
            var totals = _dataService.GetFinancialTotals(range.from, range.to);
            RetailIncome = totals.retail;
            DomesticIncome = totals.domestic;
            ExportIncome = totals.export;
            IncomeTotal = totals.totalIncome;

            OnPropertyChanged(nameof(IncomeTotal));
            OnPropertyChanged(nameof(DomesticIncome));
            OnPropertyChanged(nameof(ExportIncome));
            OnPropertyChanged(nameof(RetailIncome));

            ApplyIncomeFilter();

            // Compute Expenses
            var allExpenses = _dataService.GetAllExpenses()
                .Where(e => string.Compare(e.Date, range.from) >= 0 && string.Compare(e.Date, range.to) <= 0)
                .ToList();

            EmployeeExpenseTotal = allExpenses.Where(e => e.Group.Equals("Employee", StringComparison.OrdinalIgnoreCase)).Sum(e => e.Amount);
            UtilityExpenseTotal = allExpenses.Where(e => e.Group.Equals("Utility", StringComparison.OrdinalIgnoreCase)).Sum(e => e.Amount);
            MiscExpenseTotal = allExpenses.Where(e => e.Group.Equals("Misc Exp", StringComparison.OrdinalIgnoreCase) || e.Group.Equals("Misc", StringComparison.OrdinalIgnoreCase)).Sum(e => e.Amount);
            PurchaseExpenseTotal = allExpenses.Where(e => e.Group.Equals("Purchase", StringComparison.OrdinalIgnoreCase)).Sum(e => e.Amount);

            OnPropertyChanged(nameof(EmployeeExpenseTotal));
            OnPropertyChanged(nameof(UtilityExpenseTotal));
            OnPropertyChanged(nameof(MiscExpenseTotal));
            OnPropertyChanged(nameof(PurchaseExpenseTotal));
            OnPropertyChanged(nameof(SalaryExpenseTotal));
            OnPropertyChanged(nameof(UtilitiesExpenseTotal));
            OnPropertyChanged(nameof(YearlyExpenseTotal));
            OnPropertyChanged(nameof(MiscellaneousExpenseTotal));

            ApplyExpenseFilter();

            // Compute Investments
            InvestmentRows.Clear();
            foreach (var inv in _dataService.Investments.OrderByDescending(x => x.Date))
            {
                InvestmentRows.Add(inv);
            }
            TotalInvested = _dataService.Investments.Sum(x => x.Amount);
            PreStartInvestmentCount = _dataService.Investments.Count(x => x.IsPreStart);

            var allTimeTotals = _dataService.GetFinancialTotals("0000-00-00", "9999-12-31");
            AllTimeProfit = allTimeTotals.profit;
            RecoveryPercent = TotalInvested > 0 ? Math.Min(100, Math.Max(0, (AllTimeProfit / TotalInvested) * 100)) : 0;

            OnPropertyChanged(nameof(TotalInvested));
            OnPropertyChanged(nameof(AllTimeProfit));
            OnPropertyChanged(nameof(RecoveryPercent));
            OnPropertyChanged(nameof(PreStartInvestmentCount));

            // Compute Loans
            LoanRows.Clear();
            foreach (var l in _dataService.Loans.OrderByDescending(x => x.Date))
            {
                LoanRows.Add(l);
            }
            TotalLoansTaken = _dataService.Loans.Sum(l => l.Amount);
            TotalLoanInterestPaid = _dataService.Loans.Sum(l => l.TotalInterestPaid);
            TotalLoanPrincipalPaid = _dataService.Loans.Sum(l => l.TotalPrincipalPaid);
            TotalLoansOutstanding = _dataService.Loans.Sum(l => l.BalanceRemaining);
            OpenLoansCount = _dataService.Loans.Count(l => l.IsOpen);
            LoansCount = _dataService.Loans.Count;

            OnPropertyChanged(nameof(TotalLoansTaken));
            OnPropertyChanged(nameof(TotalLoanInterestPaid));
            OnPropertyChanged(nameof(TotalLoanPrincipalPaid));
            OnPropertyChanged(nameof(TotalLoansOutstanding));
            OnPropertyChanged(nameof(OpenLoansCount));
            OnPropertyChanged(nameof(LoansCount));

            // Compute Assets
            AssetRows.Clear();
            foreach (var a in _dataService.Assets.OrderByDescending(x => x.Date))
            {
                AssetRows.Add(a);
            }
            AssetsCount = _dataService.Assets.Count;
            TotalAssetsCost = _dataService.Assets.Sum(a => a.Cost);
            TotalAssetsDepreciation = _dataService.Assets.Sum(a => a.DepreciationSoFar);
            TotalAssetsBookValue = _dataService.Assets.Sum(a => a.BookValue);

            OnPropertyChanged(nameof(AssetsCount));
            OnPropertyChanged(nameof(TotalAssetsCost));
            OnPropertyChanged(nameof(TotalAssetsDepreciation));
            OnPropertyChanged(nameof(TotalAssetsBookValue));

            // Compute Balance Sheet metrics
            double loanIn = TotalLoansTaken;
            double prin = TotalLoanPrincipalPaid;
            double loanInt = TotalLoanInterestPaid;
            double cap = TotalInvested;

            double salesPaid = _dataService.Sales.Sum(s => s.PaidAmount);
            double purchasesPaid = _dataService.Purchases.Sum(p => p.PaidAmount);
            double expensesPaid = _dataService.Expenses.Where(e => !e.IsAuto).Sum(e => e.Amount);
            double assetsCost = TotalAssetsCost;

            BsCap = cap;
            BsLoans = Math.Max(0, loanIn - prin);
            BsPay = _dataService.Purchases.Sum(p => Math.Max(0, p.GrossAmount - p.PaidAmount));
            BsGst = _dataService.Sales.Sum(s => s.TaxAmount) - _dataService.Purchases.Sum(p => p.TaxAmount);

            // Stock in hand at cost
            double rawVal = _dataService.RawMaterials.Sum(r => Math.Max(0, r.Stock) * r.Rate);
            double genVal = _dataService.GeneralItems.Sum(g => Math.Max(0, g.Stock) * g.Rate);
            double prodVal = _dataService.Products.Sum(p => Math.Max(0, p.Stock) * (p.Cost > 0 ? p.Cost : p.DomesticPrice));
            BsStock = rawVal + genVal + prodVal;

            // Profit so far after loan interest and depreciation
            BsProfit = allTimeTotals.profit - loanInt - TotalAssetsDepreciation;

            BsLiabilitiesTotal = BsPay + BsLoans + BsGst + BsCap + BsProfit + BsStock;

            BsFixedAssets = TotalAssetsBookValue;
            BsRecv = _dataService.Sales.Sum(s => Math.Max(0, s.GrossAmount - s.PaidAmount));
            BsCash = cap + loanIn - prin - loanInt + salesPaid - purchasesPaid - expensesPaid - assetsCost;

            BsAssetsTotal = BsCash + BsStock + BsRecv + BsFixedAssets;

            OnPropertyChanged(nameof(BsCap));
            OnPropertyChanged(nameof(BsProfit));
            OnPropertyChanged(nameof(BsStock));
            OnPropertyChanged(nameof(BsLoans));
            OnPropertyChanged(nameof(BsPay));
            OnPropertyChanged(nameof(BsGst));
            OnPropertyChanged(nameof(BsLiabilitiesTotal));
            OnPropertyChanged(nameof(BsFixedAssets));
            OnPropertyChanged(nameof(BsRecv));
            OnPropertyChanged(nameof(BsCash));
            OnPropertyChanged(nameof(BsAssetsTotal));
            OnPropertyChanged(nameof(BsDifference));
            OnPropertyChanged(nameof(BsIsBalanced));
            OnPropertyChanged(nameof(BsTodayDate));

            // Dashboard
            OnPropertyChanged(nameof(DashReceivables));
            OnPropertyChanged(nameof(DashPayables));
            OnPropertyChanged(nameof(DashCashBank));
            OnPropertyChanged(nameof(DashFixedAssets));
            OnPropertyChanged(nameof(DashStockValue));
            OnPropertyChanged(nameof(DashTotalLoans));
            OnPropertyChanged(nameof(DashCapital));
            OnPropertyChanged(nameof(DashAllTimeProfit));

            // Compute P&L with extra financial items (Loan Interest & Asset Depreciation)
            var (fxInterest, fxDepr) = GetFinancialExtra(range.from, range.to);

            PlTotalIncome = totals.totalIncome;
            PlTotalExpense = totals.totalExpense + fxInterest + fxDepr;
            PlNetProfit = PlTotalIncome - PlTotalExpense;

            OnPropertyChanged(nameof(PlTotalIncome));
            OnPropertyChanged(nameof(PlTotalExpense));
            OnPropertyChanged(nameof(PlNetProfit));
            OnPropertyChanged(nameof(PlTotalSide));
            OnPropertyChanged(nameof(PlIsProfit));
            OnPropertyChanged(nameof(PlPeriodLabel));

            PlExpenseCategories.Clear();
            var expByCat = allExpenses.GroupBy(e => e.Category)
                .Select(g => new PlExpenseCategoryRow { Category = g.Key, Amount = g.Sum(x => x.Amount) })
                .ToList();

            if (fxInterest > 0.01)
            {
                expByCat.Add(new PlExpenseCategoryRow { Category = "Loan interest", Amount = fxInterest });
            }
            if (fxDepr > 0.01)
            {
                expByCat.Add(new PlExpenseCategoryRow { Category = "Depreciation of assets", Amount = fxDepr });
            }

            foreach (var r in expByCat.OrderByDescending(x => x.Amount)) PlExpenseCategories.Add(r);

            PlMonthBreakdowns.Clear();
            var now = DateTime.Now;
            for (int i = 5; i >= 0; i--)
            {
                var dt = now.AddMonths(-i);
                string mStart = new DateTime(dt.Year, dt.Month, 1).ToString("yyyy-MM-dd");
                string mEnd = new DateTime(dt.Year, dt.Month, DateTime.DaysInMonth(dt.Year, dt.Month)).ToString("yyyy-MM-dd");

                var mTotals = _dataService.GetFinancialTotals(mStart, mEnd);
                var (mInt, mDepr) = GetFinancialExtra(mStart, mEnd);
                double mExp = mTotals.totalExpense + mInt + mDepr;

                PlMonthBreakdowns.Add(new PlMonthSummaryRow
                {
                    MonthLabel = dt.ToString("MMM yyyy"),
                    Income = mTotals.totalIncome,
                    Expenses = mExp
                });
            }

            OnPropertyChanged(nameof(PlStatusWord));
            OnPropertyChanged(nameof(PlProfitLossColor));
            OnPropertyChanged(nameof(PlNetProfitDisplay));
            OnPropertyChanged(nameof(PlCardBrush));
            OnPropertyChanged(nameof(PlCardLabel));
            OnPropertyChanged(nameof(PlValueDisplay));
            UpdateLineChart();
            UpdatePieChart();
        }

        private void UpdateLineChart()
        {
            IncomeLinePoints.Clear();
            ExpenseLinePoints.Clear();
            IncomeAreaPoints.Clear();
            ExpenseAreaPoints.Clear();
            LineChartPoints.Clear();

            if (PlMonthBreakdowns.Count == 0) return;

            double maxVal = Math.Max(1000, PlMonthBreakdowns.Max(m => Math.Max(m.Income, m.Expenses)));
            double magnitude = Math.Pow(10, Math.Max(0, Math.Floor(Math.Log10(maxVal))));
            double normalized = maxVal / magnitude;
            double ceiling = Math.Ceiling(normalized) * magnitude;
            if (ceiling <= 0) ceiling = 1000;

            ChartYMaxLabel = FormatShortCurrency(ceiling);
            ChartYMidLabel = FormatShortCurrency(ceiling / 2);
            OnPropertyChanged(nameof(ChartYMaxLabel));
            OnPropertyChanged(nameof(ChartYMidLabel));

            double chartLeft = 50;
            double chartTop = 20;
            double chartWidth = 430;
            double chartHeight = 140;
            double baselineY = chartTop + chartHeight;

            int n = PlMonthBreakdowns.Count;
            double stepX = n > 1 ? chartWidth / (n - 1) : 0;

            for (int i = 0; i < n; i++)
            {
                var m = PlMonthBreakdowns[i];
                double x = chartLeft + i * stepX;
                double incRatio = Math.Clamp(m.Income / ceiling, 0.0, 1.0);
                double expRatio = Math.Clamp(m.Expenses / ceiling, 0.0, 1.0);

                double yInc = baselineY - (incRatio * chartHeight);
                double yExp = baselineY - (expRatio * chartHeight);

                IncomeLinePoints.Add(new Point(x, yInc));
                ExpenseLinePoints.Add(new Point(x, yExp));

                LineChartPoints.Add(new LineChartPoint
                {
                    MonthLabel = m.MonthLabel.Split(' ')[0],
                    X = x,
                    IncomeY = yInc,
                    ExpenseY = yExp,
                    Income = m.Income,
                    Expense = m.Expenses
                });
            }

            if (LineChartPoints.Count > 0)
            {
                IncomeAreaPoints.Add(new Point(LineChartPoints[0].X, baselineY));
                foreach (var p in IncomeLinePoints) IncomeAreaPoints.Add(p);
                IncomeAreaPoints.Add(new Point(LineChartPoints[^1].X, baselineY));

                ExpenseAreaPoints.Add(new Point(LineChartPoints[0].X, baselineY));
                foreach (var p in ExpenseLinePoints) ExpenseAreaPoints.Add(p);
                ExpenseAreaPoints.Add(new Point(LineChartPoints[^1].X, baselineY));
            }

            OnPropertyChanged(nameof(IncomeLinePoints));
            OnPropertyChanged(nameof(ExpenseLinePoints));
            OnPropertyChanged(nameof(IncomeAreaPoints));
            OnPropertyChanged(nameof(ExpenseAreaPoints));
        }

        private static string FormatShortCurrency(double val)
        {
            if (val >= 10000000) return $"₹{(val / 10000000):F1} Cr";
            if (val >= 100000) return $"₹{(val / 100000):F1} L";
            if (val >= 1000) return $"₹{(val / 1000):F1} k";
            return $"₹{val:F0}";
        }

        private void UpdatePieChart()
        {
            PieChartSlices.Clear();
            double total = PlExpenseCategories.Sum(x => x.Amount);

            string[] palette = new[]
            {
                "#3B82F6", "#10B981", "#F59E0B", "#EC4899",
                "#8B5CF6", "#06B6D4", "#6366F1", "#64748B"
            };

            if (total <= 0)
            {
                var grayBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0"));
                grayBrush.Freeze();
                var outer = new EllipseGeometry(new Point(100, 100), 85, 85);
                var inner = new EllipseGeometry(new Point(100, 100), 52, 52);
                var emptyDonut = new CombinedGeometry(GeometryCombineMode.Exclude, outer, inner);
                emptyDonut.Freeze();

                PieChartSlices.Add(new PieChartSlice
                {
                    Category = "No Expenses",
                    Amount = 0,
                    Percentage = 100,
                    FillBrush = grayBrush,
                    SliceGeometry = emptyDonut
                });

                OnPropertyChanged(nameof(HasExpenseSlices));
                OnPropertyChanged(nameof(TotalExpensesDisplay));
                return;
            }

            Point center = new Point(100, 100);
            double outerR = 85;
            double innerR = 52;
            double currentAngle = -90.0;

            var validCategories = PlExpenseCategories.Where(x => x.Amount > 0).ToList();

            for (int i = 0; i < validCategories.Count; i++)
            {
                var cat = validCategories[i];
                double fraction = cat.Amount / total;
                double sweepAngle = fraction * 360.0;
                double pct = fraction * 100.0;

                var color = (Color)ColorConverter.ConvertFromString(palette[i % palette.Length]);
                var brush = new SolidColorBrush(color);
                brush.Freeze();

                Geometry geom;

                if (validCategories.Count == 1 || sweepAngle >= 359.99)
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

                PieChartSlices.Add(new PieChartSlice
                {
                    Category = cat.Category,
                    Amount = cat.Amount,
                    Percentage = pct,
                    FillBrush = brush,
                    SliceGeometry = geom
                });

                currentAngle += sweepAngle;
            }

            OnPropertyChanged(nameof(HasExpenseSlices));
            OnPropertyChanged(nameof(TotalExpensesDisplay));
        }

        private (double interest, double depr) GetFinancialExtra(string fromDate, string toDate)
        {
            // Interest paid on loans in period
            double interest = _dataService.Loans
                .SelectMany(l => l.Repayments)
                .Where(r => string.Compare(r.Date, fromDate) >= 0 && string.Compare(r.Date, toDate) <= 0)
                .Sum(r => r.Interest);

            // Asset depreciation in period
            double depr = 0;
            string todayIso = DateTime.Today.ToString("yyyy-MM-dd");
            string toIso = string.Compare(toDate, todayIso) > 0 ? todayIso : toDate;

            DateTime? dtFrom = DateTime.TryParse(fromDate, out var dF) ? dF : null;
            DateTime? dtTo = DateTime.TryParse(toIso, out var dT) ? dT : null;

            if (dtTo.HasValue)
            {
                foreach (var a in _dataService.Assets)
                {
                    if (string.Compare(toIso, a.Date) < 0) continue;

                    double deprTo = CalculateDepreciationTo(a, dtTo.Value);
                    double deprFrom = 0;
                    if (dtFrom.HasValue && string.Compare(fromDate, "1900-01-01") > 0)
                    {
                        deprFrom = CalculateDepreciationTo(a, dtFrom.Value.AddDays(-1));
                    }

                    depr += Math.Max(0, deprTo - deprFrom);
                }
            }

            return (interest, depr);
        }

        private static double CalculateDepreciationTo(AssetItem a, DateTime targetDate)
        {
            if (DateTime.TryParse(a.Date, out var boughtDt))
            {
                double days = Math.Max(0, (targetDate.Date - boughtDt.Date).TotalDays);
                return Math.Min(a.Cost, a.Cost * (a.DepreciationRate / 100.0) * (days / 365.0));
            }
            return 0;
        }

        private void ApplyIncomeFilter()
        {
            IncomeRows.Clear();
            var range = GetDateRange(SelectedPeriod);

            foreach (var s in _dataService.Sales)
            {
                if (string.Compare(s.Date, range.from) >= 0 && string.Compare(s.Date, range.to) <= 0)
                {
                    if (SelectedIncomeChannel == "all" || s.Type.Equals(SelectedIncomeChannel, StringComparison.OrdinalIgnoreCase))
                    {
                        IncomeRows.Add(s);
                    }
                }
            }
        }

        private static bool IsSalary(ExpenseItem e) =>
            !e.IsAuto && (e.Category.IndexOf("Salary", StringComparison.OrdinalIgnoreCase) >= 0 ||
                          e.Description.IndexOf("Salary", StringComparison.OrdinalIgnoreCase) >= 0);

        private static bool IsPurchase(ExpenseItem e) =>
            e.IsAuto || e.Frequency.Equals("Purchase", StringComparison.OrdinalIgnoreCase) ||
            e.Category.IndexOf("Purchase", StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool IsYearly(ExpenseItem e) =>
            !e.IsAuto && !IsSalary(e) &&
            (e.Frequency.Equals("Yearly", StringComparison.OrdinalIgnoreCase) ||
             e.Category.IndexOf("Licence", StringComparison.OrdinalIgnoreCase) >= 0 ||
             e.Category.IndexOf("Insurance", StringComparison.OrdinalIgnoreCase) >= 0);

        private static bool IsUtilities(ExpenseItem e) =>
            !e.IsAuto && !IsSalary(e) && !IsYearly(e);

        private void ApplyExpenseFilter()
        {
            ExpenseRows.Clear();
            var range = GetDateRange(SelectedPeriod);
            string q = (ExpenseSearchText ?? "").Trim().ToLowerInvariant();

            foreach (var e in _dataService.GetAllExpenses())
            {
                if (string.Compare(e.Date, range.from) >= 0 && string.Compare(e.Date, range.to) <= 0)
                {
                    bool groupMatch = SelectedExpenseFreq.ToLowerInvariant() switch
                    {
                        "all" => true,
                        "employee" or "salaries" or "salary" => e.Group.Equals("Employee", StringComparison.OrdinalIgnoreCase),
                        "utility" or "utilities" => e.Group.Equals("Utility", StringComparison.OrdinalIgnoreCase),
                        "misc exp" or "misc" or "miscellaneous" => e.Group.Equals("Misc Exp", StringComparison.OrdinalIgnoreCase) || e.Group.Equals("Misc", StringComparison.OrdinalIgnoreCase),
                        "purchase" => e.Group.Equals("Purchase", StringComparison.OrdinalIgnoreCase),
                        _ => true
                    };

                    if (!groupMatch) continue;

                    if (!string.IsNullOrEmpty(q))
                    {
                        string haystack = $"{e.Group} {e.Category} {e.Description} {e.EmployeeName} {e.Position}".ToLowerInvariant();
                        if (!haystack.Contains(q)) continue;
                    }

                    ExpenseRows.Add(e);
                }
            }
        }
    }
}
