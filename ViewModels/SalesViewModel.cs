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
    public class SalesViewModel : ViewModelBase
    {
        private readonly AppDataService _dataService;
        private readonly Action<string> _navigateAction;
        private readonly Action<string, object?> _openDialogAction;

        private string _searchQuery = string.Empty;
        private string _selectedChannel = "all"; // all, retail, domestic, export
        private string _selectedStatus = "all"; // all, Paid, Partial, Unpaid
        private DateTime _selectedMonth = DateTime.Now;
        private DateTime? _reportFromDate;
        private DateTime? _reportToDate;

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

        public DateTime SelectedMonth
        {
            get => _selectedMonth;
            set
            {
                if (SetField(ref _selectedMonth, value))
                {
                    OnPropertyChanged(nameof(SelectedMonthDisplay));
                    OnPropertyChanged(nameof(MonthRetailTotal));
                    OnPropertyChanged(nameof(MonthDomesticTotal));
                    OnPropertyChanged(nameof(MonthExportTotal));
                    OnPropertyChanged(nameof(MonthGrandTotal));
                }
            }
        }

        public string SelectedMonthDisplay => SelectedMonth.ToString("MMMM yyyy");

        public DateTime? ReportFromDate
        {
            get => _reportFromDate;
            set
            {
                if (SetField(ref _reportFromDate, value)) ApplyFilter();
            }
        }

        public DateTime? ReportToDate
        {
            get => _reportToDate;
            set
            {
                if (SetField(ref _reportToDate, value)) ApplyFilter();
            }
        }

        // ==========================================
        // PARTICULAR MONTH SALES SUMMARY AMOUNTS
        // ==========================================
        public double MonthRetailTotal => _dataService.Sales
            .Where(s => s.Type == "retail" && IsInSelectedMonth(s.Date))
            .Sum(s => s.GrossAmount);

        public double MonthDomesticTotal => _dataService.Sales
            .Where(s => s.Type == "domestic" && IsInSelectedMonth(s.Date))
            .Sum(s => s.GrossAmount);

        public double MonthExportTotal => _dataService.Sales
            .Where(s => s.Type == "export" && IsInSelectedMonth(s.Date))
            .Sum(s => s.GrossAmount);

        public double MonthGrandTotal => MonthRetailTotal + MonthDomesticTotal + MonthExportTotal;

        // Overall stats
        public double OutstandingTotal => _dataService.Sales.Where(s => s.Status != "Paid").Sum(s => s.BalanceAmount);
        public int OutstandingCount => _dataService.Sales.Count(s => s.Status != "Paid");
        public int TotalInvoicesCount => _dataService.Sales.Count;

        // Commands
        public RelayCommand SetChannelCommand { get; }
        public RelayCommand SetStatusCommand { get; }
        public RelayCommand NavigateCommand { get; }
        public RelayCommand OpenDialogCommand { get; }
        public RelayCommand ViewSaleCommand { get; }
        public RelayCommand ReceivePaymentCommand { get; }
        public RelayCommand PreviousMonthCommand { get; }
        public RelayCommand NextMonthCommand { get; }
        public RelayCommand CurrentMonthCommand { get; }
        public RelayCommand ResetFilterDatesCommand { get; }
        public RelayCommand ExportExcelCommand { get; }
        public RelayCommand ExportPdfCommand { get; }

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

            PreviousMonthCommand = new RelayCommand(_ => SelectedMonth = SelectedMonth.AddMonths(-1));
            NextMonthCommand = new RelayCommand(_ => SelectedMonth = SelectedMonth.AddMonths(1));
            CurrentMonthCommand = new RelayCommand(_ => SelectedMonth = DateTime.Now);

            ResetFilterDatesCommand = new RelayCommand(_ =>
            {
                ReportFromDate = null;
                ReportToDate = null;
                SelectedChannel = "all";
                SelectedStatus = "all";
                SearchQuery = string.Empty;
            });

            ExportExcelCommand = new RelayCommand(_ => ExportExcel());
            ExportPdfCommand = new RelayCommand(_ => ExportPdf());

            _dataService.DataChanged += RefreshData;
            RefreshData();
        }

        public void RefreshData()
        {
            ApplyFilter();
            OnPropertyChanged(nameof(MonthRetailTotal));
            OnPropertyChanged(nameof(MonthDomesticTotal));
            OnPropertyChanged(nameof(MonthExportTotal));
            OnPropertyChanged(nameof(MonthGrandTotal));
            OnPropertyChanged(nameof(OutstandingTotal));
            OnPropertyChanged(nameof(OutstandingCount));
            OnPropertyChanged(nameof(TotalInvoicesCount));
        }

        private bool IsInSelectedMonth(string? dateStr)
        {
            if (DateTime.TryParse(dateStr, out var dt))
            {
                return dt.Year == SelectedMonth.Year && dt.Month == SelectedMonth.Month;
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
            FilteredSales.Clear();
            string q = SearchQuery.Trim().ToLowerInvariant();

            foreach (var s in _dataService.Sales)
            {
                bool matchChan = SelectedChannel == "all" || s.Type.Equals(SelectedChannel, StringComparison.OrdinalIgnoreCase);
                bool matchStat = SelectedStatus == "all" || s.Status.Equals(SelectedStatus, StringComparison.OrdinalIgnoreCase);
                bool matchQ = string.IsNullOrEmpty(q) || (s.InvoiceNo + s.Party + s.Phone + s.ContactPerson + s.Event).ToLowerInvariant().Contains(q);

                bool matchDate = true;
                if (ReportFromDate.HasValue || ReportToDate.HasValue)
                {
                    var dt = ParseDate(s.Date);
                    if (ReportFromDate.HasValue && dt.Date < ReportFromDate.Value.Date) matchDate = false;
                    if (ReportToDate.HasValue && dt.Date > ReportToDate.Value.Date) matchDate = false;
                }

                if (matchChan && matchStat && matchQ && matchDate)
                {
                    FilteredSales.Add(s);
                }
            }
        }

        private void ExportExcel()
        {
            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export Sales Report to Excel / CSV",
                Filter = "CSV Files (*.csv)|*.csv",
                FileName = $"ASI_Sales_Report_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("AFRAH SAMIYA INDUSTRIES PVT LTD - SALES & BILLING REPORT");
                    sb.AppendLine($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    sb.AppendLine($"Filter Category: {SelectedChannel.ToUpper()}, Status: {SelectedStatus.ToUpper()}, From: {(ReportFromDate.HasValue ? ReportFromDate.Value.ToString("yyyy-MM-dd") : "All")} To: {(ReportToDate.HasValue ? ReportToDate.Value.ToString("yyyy-MM-dd") : "All")}");
                    sb.AppendLine();
                    sb.AppendLine("Date,Channel,Invoice No,Customer/Buyer Name,Contact No,Terms,Taxable Amount (Rs),GST Tax (Rs),Gross Amount (Rs),Paid Amount (Rs),Balance Due (Rs),Payment Status");

                    double totalNet = 0, totalTax = 0, totalGross = 0, totalPaid = 0, totalBal = 0;

                    foreach (var s in FilteredSales)
                    {
                        totalNet += s.NetAmount;
                        totalTax += s.TaxAmount;
                        totalGross += s.GrossAmount;
                        totalPaid += s.PaidAmount;
                        totalBal += s.BalanceAmount;

                        sb.AppendLine($"\"{s.Date}\",\"{s.ChannelLabel}\",\"{EscapeCsv(s.InvoiceNo)}\",\"{EscapeCsv(s.Party)}\",\"{EscapeCsv(s.Phone)}\",\"{EscapeCsv(s.SaleType)}\",{s.NetAmount:F2},{s.TaxAmount:F2},{s.GrossAmount:F2},{s.PaidAmount:F2},{s.BalanceAmount:F2},\"{EscapeCsv(s.Status)}\"");
                    }

                    sb.AppendLine($"\"TOTAL ({FilteredSales.Count} Records)\",\"\",\"\",\"\",\"\",,\"{totalNet:F2}\",\"{totalTax:F2}\",\"{totalGross:F2}\",\"{totalPaid:F2}\",\"{totalBal:F2}\",\"\"");

                    var utf8WithBom = new System.Text.UTF8Encoding(true);
                    System.IO.File.WriteAllText(sfd.FileName, sb.ToString(), utf8WithBom);

                    var res = MessageBox.Show($"Sales report CSV exported successfully to:\n{sfd.FileName}\n\nWould you like to open it now?",
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

                // Build a modern printable layout
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
                compStack.Children.Add(new TextBlock { Text = "Shoes & Uniforms ERP · Sales & Billing Custom Report", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)), Margin = new Thickness(0, 2, 0, 0) });
                compStack.Children.Add(new TextBlock { Text = $"Period: {(ReportFromDate.HasValue ? ReportFromDate.Value.ToString("dd-MMM-yyyy") : "Beginning")} to {(ReportToDate.HasValue ? ReportToDate.Value.ToString("dd-MMM-yyyy") : "Present")} | Channel: {SelectedChannel.ToUpper()}", FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)), Margin = new Thickness(0, 2, 0, 0) });
                headerGrid.Children.Add(compStack);
                mainStack.Children.Add(headerGrid);

                mainStack.Children.Add(new Separator { Background = new SolidColorBrush(Color.FromRgb(226, 232, 240)), Margin = new Thickness(0, 0, 0, 12) });

                // Table Header
                var thGrid = new Grid { Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)), Margin = new Thickness(0, 0, 0, 6) };
                thGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
                thGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
                thGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
                thGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                thGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(95) });
                thGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
                thGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(75) });

                void AddHeaderCell(string text, int col, TextAlignment align = TextAlignment.Left)
                {
                    var tb = new TextBlock { Text = text, FontWeight = FontWeights.Bold, FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59)), Padding = new Thickness(6, 6, 6, 6), TextAlignment = align };
                    Grid.SetColumn(tb, col);
                    thGrid.Children.Add(tb);
                }

                AddHeaderCell("DATE", 0);
                AddHeaderCell("CHANNEL", 1);
                AddHeaderCell("INVOICE", 2);
                AddHeaderCell("CUSTOMER / BUYER", 3);
                AddHeaderCell("CONTACT NO", 4);
                AddHeaderCell("AMOUNT (₹)", 5, TextAlignment.Right);
                AddHeaderCell("STATUS", 6, TextAlignment.Center);
                mainStack.Children.Add(thGrid);

                // Table Rows
                double totalGross = 0;
                int rowIdx = 0;
                foreach (var s in FilteredSales)
                {
                    totalGross += s.GrossAmount;
                    var trGrid = new Grid { Margin = new Thickness(0, 2, 0, 2), Background = rowIdx % 2 == 1 ? new SolidColorBrush(Color.FromRgb(248, 250, 252)) : Brushes.White };
                    trGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
                    trGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
                    trGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
                    trGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    trGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(95) });
                    trGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
                    trGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(75) });

                    void AddCell(string text, int col, bool bold = false, TextAlignment align = TextAlignment.Left)
                    {
                        var tb = new TextBlock { Text = text, FontSize = 9.5, Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)), Padding = new Thickness(6, 4, 6, 4), FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, TextAlignment = align };
                        Grid.SetColumn(tb, col);
                        trGrid.Children.Add(tb);
                    }

                    AddCell(s.Date, 0);
                    AddCell(s.ChannelLabel, 1);
                    AddCell(s.InvoiceNo, 2, true);
                    AddCell(s.Party, 3);
                    AddCell(s.Phone, 4);
                    AddCell($"₹{s.GrossAmount:N2}", 5, true, TextAlignment.Right);
                    AddCell(s.Status, 6, false, TextAlignment.Center);

                    mainStack.Children.Add(trGrid);
                    rowIdx++;
                }

                // Summary Total Footer
                var tfBorder = new Border { Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)), Padding = new Thickness(10, 8, 10, 8), Margin = new Thickness(0, 10, 0, 0), CornerRadius = new CornerRadius(4) };
                var tfDock = new DockPanel();
                tfDock.Children.Add(new TextBlock { Text = $"Total Invoices: {FilteredSales.Count}", FontWeight = FontWeights.Bold, FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59)) });
                var totalTb = new TextBlock { Text = $"Grand Total: ₹{totalGross:N2}", FontWeight = FontWeights.Bold, FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)), HorizontalAlignment = HorizontalAlignment.Right };
                tfDock.Children.Add(totalTb);
                tfBorder.Child = tfDock;
                mainStack.Children.Add(tfBorder);

                printArea.Child = mainStack;

                printArea.Measure(new Size(printArea.Width, double.PositiveInfinity));
                printArea.Arrange(new Rect(new Point(0, 0), printArea.DesiredSize));
                printArea.UpdateLayout();

                printDialog.PrintVisual(printArea, "ASI ERP Sales Report");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not print report: {ex.Message}", "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string EscapeCsv(string? val) => (val ?? string.Empty).Replace("\"", "\"\"");
    }
}
