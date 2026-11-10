using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AsiErp.Commands;
using AsiErp.Models;
using AsiErp.Services;

namespace AsiErp.ViewModels
{
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

        private string _activeTab = "income"; // income, exp, inv, pl
        private string _selectedPeriod = "m0"; // m0, m1, q, fy, all
        private string _selectedIncomeChannel = "all";
        private string _selectedExpenseFreq = "all";

        // Income collections
        public ObservableCollection<SaleDoc> IncomeRows { get; } = new();

        // Expense collections
        public ObservableCollection<ExpenseItem> ExpenseRows { get; } = new();

        // Investment collections
        public ObservableCollection<InvestmentItem> InvestmentRows { get; } = new();

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
            "income" => "Income",
            "exp" => "Expenses",
            "inv" => "Investments",
            "pl" => "Profit & Loss",
            _ => "Accounts"
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
        public double SalaryExpenseTotal { get; private set; }
        public double UtilitiesExpenseTotal { get; private set; }
        public double YearlyExpenseTotal { get; private set; }
        public double MiscellaneousExpenseTotal => YearlyExpenseTotal;
        public double PurchaseExpenseTotal { get; private set; }
        public double DailyExpenseTotal { get; private set; }
        public double MonthlyExpenseTotal { get; private set; }

        // --- INVESTMENT STATS ---
        public double TotalInvested { get; private set; }
        public double AllTimeProfit { get; private set; }
        public double RecoveryPercent { get; private set; }
        public int PreStartInvestmentCount { get; private set; }

        // --- P&L STATS ---
        public double PlTotalIncome { get; private set; }
        public double PlTotalExpense { get; private set; }
        public double PlNetProfit { get; private set; }
        public double PlTotalSide => Math.Max(PlTotalIncome, PlTotalExpense);
        public bool PlIsProfit => PlNetProfit >= 0;
        public string PlPeriodLabel { get; private set; } = "This month";

        public RelayCommand SetTabCommand { get; }
        public RelayCommand SetPeriodCommand { get; }
        public RelayCommand SetIncomeChannelCommand { get; }
        public RelayCommand SetExpenseFreqCommand { get; }
        public RelayCommand AddExpenseCommand { get; }
        public RelayCommand DeleteExpenseCommand { get; }
        public RelayCommand AddInvestmentCommand { get; }
        public RelayCommand DeleteInvestmentCommand { get; }
        public RelayCommand ViewDocCommand { get; }

        public AccountsViewModel(AppDataService dataService, Action<string, object?> openDialogAction, string initialTab = "income")
        {
            _dataService = dataService;
            _openDialogAction = openDialogAction;
            _activeTab = initialTab;

            SetTabCommand = new RelayCommand(p => ActiveTab = p?.ToString() ?? "income");
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

            SalaryExpenseTotal = allExpenses.Where(IsSalary).Sum(e => e.Amount);
            UtilitiesExpenseTotal = allExpenses.Where(IsUtilities).Sum(e => e.Amount);
            YearlyExpenseTotal = allExpenses.Where(IsYearly).Sum(e => e.Amount);
            PurchaseExpenseTotal = allExpenses.Where(IsPurchase).Sum(e => e.Amount);
            DailyExpenseTotal = allExpenses.Where(e => e.Frequency == "Daily").Sum(e => e.Amount);
            MonthlyExpenseTotal = allExpenses.Where(e => e.Frequency == "Monthly").Sum(e => e.Amount);

            OnPropertyChanged(nameof(SalaryExpenseTotal));
            OnPropertyChanged(nameof(UtilitiesExpenseTotal));
            OnPropertyChanged(nameof(YearlyExpenseTotal));
            OnPropertyChanged(nameof(MiscellaneousExpenseTotal));
            OnPropertyChanged(nameof(PurchaseExpenseTotal));
            OnPropertyChanged(nameof(DailyExpenseTotal));
            OnPropertyChanged(nameof(MonthlyExpenseTotal));

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

            // Compute P&L
            PlTotalIncome = totals.totalIncome;
            PlTotalExpense = totals.totalExpense;
            PlNetProfit = totals.profit;

            OnPropertyChanged(nameof(PlTotalIncome));
            OnPropertyChanged(nameof(PlTotalExpense));
            OnPropertyChanged(nameof(PlNetProfit));
            OnPropertyChanged(nameof(PlTotalSide));
            OnPropertyChanged(nameof(PlIsProfit));
            OnPropertyChanged(nameof(PlPeriodLabel));

            PlExpenseCategories.Clear();
            var expByCat = allExpenses.GroupBy(e => e.Category)
                .Select(g => new PlExpenseCategoryRow { Category = g.Key, Amount = g.Sum(x => x.Amount) })
                .OrderByDescending(x => x.Amount);

            foreach (var r in expByCat) PlExpenseCategories.Add(r);

            PlMonthBreakdowns.Clear();
            var now = DateTime.Now;
            for (int i = 5; i >= 0; i--)
            {
                var dt = now.AddMonths(-i);
                string mStart = new DateTime(dt.Year, dt.Month, 1).ToString("yyyy-MM-dd");
                string mEnd = new DateTime(dt.Year, dt.Month, DateTime.DaysInMonth(dt.Year, dt.Month)).ToString("yyyy-MM-dd");

                var mTotals = _dataService.GetFinancialTotals(mStart, mEnd);
                PlMonthBreakdowns.Add(new PlMonthSummaryRow
                {
                    MonthLabel = dt.ToString("MMM yyyy"),
                    Income = mTotals.totalIncome,
                    Expenses = mTotals.totalExpense
                });
            }
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

            foreach (var e in _dataService.GetAllExpenses())
            {
                if (string.Compare(e.Date, range.from) >= 0 && string.Compare(e.Date, range.to) <= 0)
                {
                    bool match = SelectedExpenseFreq.ToLowerInvariant() switch
                    {
                        "salary" or "salaries" => IsSalary(e),
                        "utilities" => IsUtilities(e),
                        "yearly" or "misc" or "miscellaneous" => IsYearly(e),
                        "purchase" => IsPurchase(e),
                        "daily" => e.Frequency.Equals("Daily", StringComparison.OrdinalIgnoreCase),
                        "monthly" => e.Frequency.Equals("Monthly", StringComparison.OrdinalIgnoreCase),
                        _ => true
                    };

                    if (match)
                    {
                        ExpenseRows.Add(e);
                    }
                }
            }
        }
    }
}
