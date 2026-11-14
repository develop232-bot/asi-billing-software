using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using AsiErp.Commands;
using AsiErp.Models;
using AsiErp.Services;

namespace AsiErp.ViewModels
{
    public class PurchaseViewModel : ViewModelBase
    {
        private readonly AppDataService _dataService;
        private readonly Action<string, object?> _openDialogAction;

        private string _activeKind = "all"; // all, raw, ready, gen
        private string _selectedStatus = "all"; // all, Paid, Partial, Unpaid
        private string _searchQuery = string.Empty;
        private DateTime _selectedMonth = DateTime.Now;
        private DateTime? _reportFromDate;
        private DateTime? _reportToDate;
        private bool _isSyncingDates = false;

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

        public DateTime SelectedMonth
        {
            get => _selectedMonth;
            set
            {
                if (SetField(ref _selectedMonth, value))
                {
                    if (!_isSyncingDates)
                    {
                        _isSyncingDates = true;
                        try
                        {
                            _reportFromDate = new DateTime(value.Year, value.Month, 1);
                            _reportToDate = new DateTime(value.Year, value.Month, DateTime.DaysInMonth(value.Year, value.Month));
                            OnPropertyChanged(nameof(ReportFromDate));
                            OnPropertyChanged(nameof(ReportToDate));
                        }
                        finally
                        {
                            _isSyncingDates = false;
                        }
                    }
                    NotifySummaryPeriodChanged();
                    ApplyFilter();
                }
            }
        }

        public DateTime? SummaryFromDate
        {
            get => _reportFromDate;
            set => ReportFromDate = value;
        }

        public DateTime? SummaryToDate
        {
            get => _reportToDate;
            set => ReportToDate = value;
        }

        public string SelectedMonthDisplay
        {
            get
            {
                if (ReportFromDate.HasValue && ReportToDate.HasValue)
                {
                    if (ReportFromDate.Value.Day == 1 &&
                        ReportToDate.Value.Day == DateTime.DaysInMonth(ReportFromDate.Value.Year, ReportFromDate.Value.Month) &&
                        ReportFromDate.Value.Year == ReportToDate.Value.Year &&
                        ReportFromDate.Value.Month == ReportToDate.Value.Month)
                    {
                        return ReportFromDate.Value.ToString("MMMM yyyy");
                    }
                    return $"{ReportFromDate.Value:dd MMM yyyy} - {ReportToDate.Value:dd MMM yyyy}";
                }
                if (ReportFromDate.HasValue)
                {
                    return $"From {ReportFromDate.Value:dd MMM yyyy}";
                }
                if (ReportToDate.HasValue)
                {
                    return $"Up to {ReportToDate.Value:dd MMM yyyy}";
                }
                return SelectedMonth.ToString("MMMM yyyy");
            }
        }

        private void NotifySummaryPeriodChanged()
        {
            OnPropertyChanged(nameof(SelectedMonthDisplay));
            OnPropertyChanged(nameof(MonthRawTotal));
            OnPropertyChanged(nameof(MonthReadyTotal));
            OnPropertyChanged(nameof(MonthGenTotal));
            OnPropertyChanged(nameof(MonthGrandTotal));
        }

        public DateTime? ReportFromDate
        {
            get => _reportFromDate;
            set
            {
                if (SetField(ref _reportFromDate, value))
                {
                    if (!_isSyncingDates && value.HasValue)
                    {
                        _isSyncingDates = true;
                        try
                        {
                            _selectedMonth = value.Value;
                            OnPropertyChanged(nameof(SelectedMonth));
                        }
                        finally
                        {
                            _isSyncingDates = false;
                        }
                    }
                    NotifySummaryPeriodChanged();
                    ApplyFilter();
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
                    if (!_isSyncingDates && value.HasValue && !ReportFromDate.HasValue)
                    {
                        _isSyncingDates = true;
                        try
                        {
                            _selectedMonth = value.Value;
                            OnPropertyChanged(nameof(SelectedMonth));
                        }
                        finally
                        {
                            _isSyncingDates = false;
                        }
                    }
                    NotifySummaryPeriodChanged();
                    ApplyFilter();
                }
            }
        }

        public string Title => ActiveKind switch
        {
            "raw" => "Purchase · Raw Material",
            "ready" => "Purchase · Ready Made Products",
            "gen" => "Purchase · General Packaging",
            _ => "Purchase & Sourcing"
        };

        public string Subtitle => ActiveKind switch
        {
            "raw" => "Raw materials purchased from tanneries, fabric mills, sole manufacturers and trim vendors.",
            "ready" => "Finished shoes and uniforms sourced from suppliers for trading or resale.",
            "gen" => "Boxes, plastic covers, hang tags, packaging tape and general factory supplies.",
            _ => "Vendor bills register across Raw Material, Ready Made Products & General Packaging."
        };

        // ==========================================
        // PARTICULAR PERIOD / MONTH PURCHASE SUMMARY AMOUNTS
        // ==========================================
        public double MonthRawTotal => _dataService.Purchases
            .Where(p => p.Kind == "raw" && IsInSelectedPeriod(p.Date))
            .Sum(p => p.GrossAmount);

        public double MonthReadyTotal => _dataService.Purchases
            .Where(p => p.Kind == "ready" && IsInSelectedPeriod(p.Date))
            .Sum(p => p.GrossAmount);

        public double MonthGenTotal => _dataService.Purchases
            .Where(p => p.Kind == "gen" && IsInSelectedPeriod(p.Date))
            .Sum(p => p.GrossAmount);

        public double MonthGrandTotal => MonthRawTotal + MonthReadyTotal + MonthGenTotal;

        // Overall stats
        public double ToPayVendors => _dataService.Purchases
            .Where(p => (ActiveKind == "all" || p.Kind == ActiveKind) && p.Status != "Paid")
            .Sum(p => p.BalanceAmount);

        public int OpenBillsCount => _dataService.Purchases
            .Count(p => (ActiveKind == "all" || p.Kind == ActiveKind) && p.Status != "Paid");

        public int BillsToDate => _dataService.Purchases.Count(p => ActiveKind == "all" || p.Kind == ActiveKind);

        public int UniqueVendorsCount => _dataService.Purchases
            .Where(p => ActiveKind == "all" || p.Kind == ActiveKind)
            .Select(p => p.Party)
            .Distinct()
            .Count();

        // Commands
        public RelayCommand SetKindCommand { get; }
        public RelayCommand SetStatusCommand { get; }
        public RelayCommand NewPurchaseBillCommand { get; }
        public RelayCommand NewPurchaseWithKindCommand { get; }
        public RelayCommand ViewPurchaseCommand { get; }
        public RelayCommand PayBillCommand { get; }
        public RelayCommand PreviousMonthCommand { get; }
        public RelayCommand NextMonthCommand { get; }
        public RelayCommand CurrentMonthCommand { get; }
        public RelayCommand ResetSummaryDatesCommand { get; }
        public RelayCommand ResetFilterDatesCommand { get; }
        public RelayCommand ExportExcelCommand { get; }
        public RelayCommand ExportPdfCommand { get; }

        public PurchaseViewModel(AppDataService dataService, Action<string, object?> openDialogAction, string initialKind = "all")
        {
            _dataService = dataService;
            _openDialogAction = openDialogAction;
            _activeKind = initialKind;

            _reportFromDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            _reportToDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month));

            SetKindCommand = new RelayCommand(p => ActiveKind = p?.ToString() ?? "all");
            SetStatusCommand = new RelayCommand(p => SelectedStatus = p?.ToString() ?? "all");

            NewPurchaseBillCommand = new RelayCommand(_ =>
            {
                string kind = ActiveKind == "all" ? "raw" : ActiveKind;
                _openDialogAction("purchase_bill", kind);
            });

            NewPurchaseWithKindCommand = new RelayCommand(p =>
            {
                string kind = p?.ToString() ?? "raw";
                _openDialogAction("purchase_bill", kind);
            });

            ViewPurchaseCommand = new RelayCommand(p =>
            {
                if (p is PurchaseDoc doc) _openDialogAction("view_purchase_doc", doc);
            });

            PayBillCommand = new RelayCommand(p =>
            {
                if (p is PurchaseDoc doc) _openDialogAction("payment_purchase", doc);
            });

            PreviousMonthCommand = new RelayCommand(_ =>
            {
                SelectedMonth = SelectedMonth.AddMonths(-1);
            });
            NextMonthCommand = new RelayCommand(_ =>
            {
                SelectedMonth = SelectedMonth.AddMonths(1);
            });
            CurrentMonthCommand = new RelayCommand(_ =>
            {
                SelectedMonth = DateTime.Now;
            });
            ResetSummaryDatesCommand = new RelayCommand(_ =>
            {
                SelectedMonth = DateTime.Now;
            });

            ResetFilterDatesCommand = new RelayCommand(_ =>
            {
                _selectedMonth = DateTime.Now;
                _reportFromDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                _reportToDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month));
                OnPropertyChanged(nameof(SelectedMonth));
                OnPropertyChanged(nameof(ReportFromDate));
                OnPropertyChanged(nameof(ReportToDate));
                ActiveKind = "all";
                SelectedStatus = "all";
                SearchQuery = string.Empty;
                NotifySummaryPeriodChanged();
                ApplyFilter();
            });

            ExportExcelCommand = new RelayCommand(_ => ExportExcel());
            ExportPdfCommand = new RelayCommand(_ => ExportPdf());

            _dataService.DataChanged += RefreshData;
            RefreshData();
        }

        public void RefreshData()
        {
            ApplyFilter();
            OnPropertyChanged(nameof(MonthRawTotal));
            OnPropertyChanged(nameof(MonthReadyTotal));
            OnPropertyChanged(nameof(MonthGenTotal));
            OnPropertyChanged(nameof(MonthGrandTotal));
            OnPropertyChanged(nameof(ToPayVendors));
            OnPropertyChanged(nameof(OpenBillsCount));
            OnPropertyChanged(nameof(BillsToDate));
            OnPropertyChanged(nameof(UniqueVendorsCount));
        }

        private bool IsInSelectedPeriod(string? dateStr)
        {
            if (DateTime.TryParse(dateStr, out var dt))
            {
                var dateOnly = dt.Date;
                if (ReportFromDate.HasValue && dateOnly < ReportFromDate.Value.Date) return false;
                if (ReportToDate.HasValue && dateOnly > ReportToDate.Value.Date) return false;
                if (!ReportFromDate.HasValue && !ReportToDate.HasValue)
                {
                    return dt.Year == SelectedMonth.Year && dt.Month == SelectedMonth.Month;
                }
                return true;
            }
            return false;
        }

        private static DateTime ParseDate(string? dateStr)
        {
            if (DateTime.TryParse(dateStr, out var dt)) return dt;
            return DateTime.MinValue;
        }

        private void ApplyFilter()
        {
            FilteredPurchases.Clear();
            string q = (SearchQuery ?? string.Empty).Trim().ToLowerInvariant();
            var tokens = string.IsNullOrEmpty(q) ? Array.Empty<string>() : q.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var b in _dataService.Purchases)
            {
                bool matchKind = ActiveKind == "all" || b.Kind.Equals(ActiveKind, StringComparison.OrdinalIgnoreCase);
                bool matchStat = SelectedStatus == "all" || b.Status.Equals(SelectedStatus, StringComparison.OrdinalIgnoreCase);

                bool matchDate = true;
                if (ReportFromDate.HasValue || ReportToDate.HasValue)
                {
                    var dt = ParseDate(b.Date);
                    if (ReportFromDate.HasValue && dt.Date < ReportFromDate.Value.Date) matchDate = false;
                    if (ReportToDate.HasValue && dt.Date > ReportToDate.Value.Date) matchDate = false;
                }

                bool matchQ = true;
                if (tokens.Length > 0)
                {
                    var sb = new System.Text.StringBuilder();
                    sb.Append(b.BillNo).Append(' ');
                    sb.Append(b.Party).Append(' ');
                    sb.Append(b.Phone).Append(' ');
                    sb.Append(b.VendorBillRef).Append(' ');
                    sb.Append(b.Kind).Append(' ');
                    sb.Append(b.KindLabel).Append(' ');
                    sb.Append(b.Status).Append(' ');
                    sb.Append(b.SaleType).Append(' ');
                    sb.Append(b.Date).Append(' ');
                    if (DateTime.TryParse(b.Date, out var bdt))
                    {
                        sb.Append(bdt.ToString("dd MMM yyyy")).Append(' ');
                        sb.Append(bdt.ToString("dd MMM yy")).Append(' ');
                        sb.Append(bdt.ToString("MMMM yyyy")).Append(' ');
                    }
                    sb.Append(b.GrossAmount.ToString("0.##")).Append(' ');
                    sb.Append(b.GrossAmount.ToString("N2")).Append(' ');
                    sb.Append(b.GrossAmount.ToString("N0")).Append(' ');
                    sb.Append(b.BalanceAmount.ToString("0.##")).Append(' ');

                    if (b.Lines != null)
                    {
                        foreach (var line in b.Lines)
                        {
                            sb.Append(line.Name).Append(' ');
                            sb.Append(line.ItemId).Append(' ');
                            sb.Append(line.Unit).Append(' ');
                        }
                    }

                    string combined = sb.ToString().ToLowerInvariant();
                    matchQ = tokens.All(token => combined.Contains(token));
                }

                if (matchKind && matchStat && matchQ && matchDate)
                {
                    FilteredPurchases.Add(b);
                }
            }
        }

        private void ExportExcel()
        {
            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export Purchase Report to Excel / CSV",
                Filter = "CSV Files (*.csv)|*.csv",
                FileName = $"ASI_Purchase_Report_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("AFRAH SAMIYA INDUSTRIES PVT LTD - PURCHASE & SOURCING REPORT");
                    sb.AppendLine($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    sb.AppendLine($"Category: {ActiveKind.ToUpper()}, Status: {SelectedStatus.ToUpper()}, From: {(ReportFromDate.HasValue ? ReportFromDate.Value.ToString("yyyy-MM-dd") : "All")} To: {(ReportToDate.HasValue ? ReportToDate.Value.ToString("yyyy-MM-dd") : "All")}");
                    sb.AppendLine();
                    sb.AppendLine("Date,Channel,Invoice No,Vendor Name,Contact No,Amount (Rs),Vendor Ref,Terms,Taxable Amount (Rs),GST Tax (Rs),Paid Amount (Rs),Balance Due (Rs),Payment Status,Items Purchased");

                    double totalNet = 0, totalTax = 0, totalGross = 0, totalPaid = 0, totalBal = 0;

                    foreach (var b in FilteredPurchases)
                    {
                        totalNet += b.NetAmount;
                        totalTax += b.TaxAmount;
                        totalGross += b.GrossAmount;
                        totalPaid += b.PaidAmount;
                        totalBal += b.BalanceAmount;

                        string itemsSummary = b.Lines != null && b.Lines.Any()
                            ? string.Join("; ", b.Lines.Select(l => $"{l.Name} (Qty: {l.Qty} {l.Unit} @ Rs.{l.Rate:F2})"))
                            : "";

                        sb.AppendLine($"\"{b.Date}\",\"{b.KindLabel}\",\"{EscapeCsv(b.BillNo)}\",\"{EscapeCsv(b.Party)}\",\"{EscapeCsv(b.Phone)}\",{b.GrossAmount:F2},\"{EscapeCsv(b.VendorBillRef)}\",\"{EscapeCsv(b.SaleType)}\",{b.NetAmount:F2},{b.TaxAmount:F2},{b.PaidAmount:F2},{b.BalanceAmount:F2},\"{EscapeCsv(b.Status)}\",\"{EscapeCsv(itemsSummary)}\"");
                    }

                    sb.AppendLine($"\"TOTAL ({FilteredPurchases.Count} Records)\",\"\",\"\",\"\",\"\",\"{totalGross:F2}\",\"\",,\"{totalNet:F2}\",\"{totalTax:F2}\",\"{totalPaid:F2}\",\"{totalBal:F2}\",\"\",");

                    var utf8WithBom = new System.Text.UTF8Encoding(true);
                    System.IO.File.WriteAllText(sfd.FileName, sb.ToString(), utf8WithBom);

                    var res = MessageBox.Show($"Purchase report CSV exported successfully to:\n{sfd.FileName}\n\nWould you like to open it now?",
                        "Export Complete", MessageBoxButton.YesNo, MessageBoxImage.Information);

                    if (res == MessageBoxResult.Yes)
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
                    MessageBox.Show($"Could not export CSV file: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExportPdf()
        {
            try
            {
                var printDialog = new PrintDialog();
                if (printDialog.ShowDialog() != true) return;

                var printArea = new Border
                {
                    Background = Brushes.White,
                    Padding = new Thickness(36),
                    Width = printDialog.PrintableAreaWidth > 0 ? printDialog.PrintableAreaWidth : 790
                };

                var mainStack = new StackPanel();

                // Company Header
                var headerGrid = new DockPanel { Margin = new Thickness(0, 0, 0, 16) };
                var compStack = new StackPanel();
                compStack.Children.Add(new TextBlock { Text = "AFRAH SAMIYA INDUSTRIES PVT LTD", FontSize = 16, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)) });
                compStack.Children.Add(new TextBlock { Text = "Shoes & Uniforms ERP · Purchase & Sourcing Custom Report", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)), Margin = new Thickness(0, 2, 0, 0) });
                compStack.Children.Add(new TextBlock { Text = $"Period: {(ReportFromDate.HasValue ? ReportFromDate.Value.ToString("dd-MMM-yyyy") : "Beginning")} to {(ReportToDate.HasValue ? ReportToDate.Value.ToString("dd-MMM-yyyy") : "Present")} | Category: {ActiveKind.ToUpper()}", FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)), Margin = new Thickness(0, 2, 0, 0) });
                headerGrid.Children.Add(compStack);
                mainStack.Children.Add(headerGrid);

                mainStack.Children.Add(new Separator { Background = new SolidColorBrush(Color.FromRgb(226, 232, 240)), Margin = new Thickness(0, 0, 0, 12) });

                // Table Header
                var thGrid = new Grid { Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)), Margin = new Thickness(0, 0, 0, 6) };
                thGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
                thGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
                thGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
                thGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                thGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(95) });
                thGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
                thGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });

                void AddHeaderCell(string text, int col, TextAlignment align = TextAlignment.Left)
                {
                    var tb = new TextBlock { Text = text, FontWeight = FontWeights.Bold, FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59)), Padding = new Thickness(6, 6, 6, 6), TextAlignment = align };
                    Grid.SetColumn(tb, col);
                    thGrid.Children.Add(tb);
                }

                AddHeaderCell("DATE", 0);
                AddHeaderCell("CHANNEL", 1);
                AddHeaderCell("INVOICE", 2);
                AddHeaderCell("VENDOR", 3);
                AddHeaderCell("CONTACT NO", 4);
                AddHeaderCell("AMOUNT (₹)", 5, TextAlignment.Right);
                AddHeaderCell("STATUS", 6, TextAlignment.Center);
                mainStack.Children.Add(thGrid);

                // Table Rows
                double totalGross = 0;
                int rowIdx = 0;
                foreach (var b in FilteredPurchases)
                {
                    totalGross += b.GrossAmount;
                    var trGrid = new Grid { Margin = new Thickness(0, 2, 0, 2), Background = rowIdx % 2 == 1 ? new SolidColorBrush(Color.FromRgb(248, 250, 252)) : Brushes.White };
                    trGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
                    trGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
                    trGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
                    trGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    trGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(95) });
                    trGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
                    trGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });

                    void AddCell(string text, int col, bool bold = false, TextAlignment align = TextAlignment.Left)
                    {
                        var tb = new TextBlock { Text = text, FontSize = 9.5, Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)), Padding = new Thickness(6, 4, 6, 4), FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, TextAlignment = align };
                        Grid.SetColumn(tb, col);
                        trGrid.Children.Add(tb);
                    }

                    AddCell(b.Date, 0);
                    AddCell(b.KindLabel, 1);
                    AddCell(b.BillNo, 2, true);
                    AddCell(b.Party, 3);
                    AddCell(b.Phone, 4);
                    AddCell($"₹{b.GrossAmount:N2}", 5, true, TextAlignment.Right);
                    AddCell(b.Status, 6, false, TextAlignment.Center);

                    mainStack.Children.Add(trGrid);
                    rowIdx++;
                }

                // Summary Total Footer
                var tfBorder = new Border { Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)), Padding = new Thickness(10, 8, 10, 8), Margin = new Thickness(0, 10, 0, 0), CornerRadius = new CornerRadius(4) };
                var tfDock = new DockPanel();
                tfDock.Children.Add(new TextBlock { Text = $"Total Bills: {FilteredPurchases.Count}", FontWeight = FontWeights.Bold, FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59)) });
                var totalTb = new TextBlock { Text = $"Grand Total: ₹{totalGross:N2}", FontWeight = FontWeights.Bold, FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)), HorizontalAlignment = HorizontalAlignment.Right };
                tfDock.Children.Add(totalTb);
                tfBorder.Child = tfDock;
                mainStack.Children.Add(tfBorder);

                printArea.Child = mainStack;

                printArea.Measure(new Size(printArea.Width, double.PositiveInfinity));
                printArea.Arrange(new Rect(new Point(0, 0), printArea.DesiredSize));
                printArea.UpdateLayout();

                printDialog.PrintVisual(printArea, "ASI ERP Purchase Report");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not print report: {ex.Message}", "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string EscapeCsv(string? val) => (val ?? string.Empty).Replace("\"", "\"\"");
    }
}
