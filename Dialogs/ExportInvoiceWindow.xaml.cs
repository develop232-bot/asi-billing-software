using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AsiErp.Models;
using AsiErp.Services;

namespace AsiErp.Dialogs
{
    public partial class ExportInvoiceWindow : Window
    {
        private readonly AppDataService _dataService;

        private static readonly Dictionary<string, (string Cur, double Fx, string Pod)> CountryFx = new()
        {
            { "UAE", ("AED", 24.10, "Jebel Ali Port") },
            { "Saudi Arabia", ("SAR", 23.60, "Dammam / Jeddah Port") },
            { "Qatar", ("QAR", 24.30, "Hamad Port, Doha") },
            { "Oman", ("OMR", 230.00, "Sohar Port") },
            { "Kuwait", ("KWD", 288.00, "Shuwaikh Port") },
            { "Bahrain", ("BHD", 235.00, "Khalifa Bin Salman Port") },
            { "Other (USD)", ("USD", 88.50, "New York / Los Angeles") }
        };

        public List<ProductItem> AvailableProducts { get; }
        public ObservableCollection<SaleLine> Lines { get; } = new();
        public SaleDoc? CreatedSale { get; private set; }

        public ExportInvoiceWindow()
        {
            InitializeComponent();
            _dataService = AppDataService.Instance;

            AvailableProducts = _dataService.Products.ToList();
            DgLines.ItemsSource = Lines;

            TxtInvoiceNo.Text = _dataService.NextDocNo("export", increment: false);
            DpDate.SelectedDate = DateTime.Now;

            // Autocomplete suggestions for export buyer names
            var pastBuyers = _dataService.Sales
                .Where(s => s.Type == "export" && !string.IsNullOrWhiteSpace(s.Party))
                .Select(s => s.Party)
                .Distinct()
                .OrderBy(x => x)
                .ToList();
            CbParty.ItemsSource = pastBuyers;

            AddLine();
            UpdatePaymentDueDisplay();
        }

        private void Country_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CbCountry?.SelectedItem is ComboBoxItem item && TxtCur != null && TxtFx != null)
            {
                string c = item.Content.ToString() ?? "UAE";
                if (CountryFx.TryGetValue(c, out var val))
                {
                    TxtCur.Text = val.Cur;
                    TxtFx.Text = val.Fx.ToString("F2");
                    if (TxtPod != null) TxtPod.Text = val.Pod;
                    if (LblFx != null) LblFx.Text = $"₹ per 1 {val.Cur} (edit for today's rate)";
                    if (LblSubtotal != null) LblSubtotal.Text = $"Subtotal ({val.Cur}):";
                    if (LblCurTotal != null) LblCurTotal.Text = $"Total ({val.Cur}):";
                    if (LblPaidNow != null) LblPaidNow.Text = $"Received now ({val.Cur}):";

                    // Update rates for selected currency
                    foreach (var l in Lines)
                    {
                        var p = AvailableProducts.FirstOrDefault(x => x.Id == l.Pid);
                        if (p != null)
                        {
                            l.Rate = p.ExportPrice > 0
                                ? (val.Cur == "USD" ? p.ExportPrice : Math.Round(p.ExportPrice * 88.50 / val.Fx, 2))
                                : Math.Round(p.DomesticPrice / val.Fx, 2);
                        }
                    }
                    Recalculate();
                }
            }
        }

        private void Fx_TextChanged(object sender, TextChangedEventArgs e) => Recalculate();

        private void AddLine()
        {
            double.TryParse(TxtFx?.Text, out double fx);
            if (fx <= 0) fx = 24.10;
            string cur = TxtCur?.Text ?? "AED";

            var line = new SaleLine
            {
                Pid = string.Empty,
                Name = string.Empty,
                Hsn = string.Empty,
                Size = string.Empty,
                Qty = 1,
                Unit = "pair",
                Rate = 0,
                GstPercent = 0,
                DiscPercent = 0
            };
            line.PropertyChanged += (s, e) => Recalculate();
            Lines.Add(line);
            Recalculate();
        }

        private void AddLine_Click(object sender, RoutedEventArgs e) => AddLine();

        private void DeleteLine_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is SaleLine line)
            {
                Lines.Remove(line);
                if (!Lines.Any()) AddLine();
                Recalculate();
            }
        }

        private void ProductSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cb && cb.DataContext is SaleLine line && cb.SelectedValue is string pid)
            {
                var p = AvailableProducts.FirstOrDefault(x => x.Id == pid);
                if (p != null)
                {
                    double.TryParse(TxtFx?.Text, out double fx);
                    if (fx <= 0) fx = 24.10;
                    string cur = TxtCur?.Text ?? "AED";

                    line.Name = p.Name;
                    line.Hsn = p.Hsn;
                    line.Unit = p.Unit;
                    line.Rate = p.ExportPrice > 0
                        ? (cur == "USD" ? p.ExportPrice : Math.Round(p.ExportPrice * 88.50 / fx, 2))
                        : Math.Round(p.DomesticPrice / fx, 2);
                    line.Size = p.Sizes?.FirstOrDefault() ?? "—";
                    line.GstPercent = 0;
                    Recalculate();
                }
            }
        }

        private bool _isRecalculating;
        private void Recalculate()
        {
            if (_isRecalculating) return;
            _isRecalculating = true;
            try
            {
                double curTotal = 0;
                double.TryParse(TxtFx?.Text, out double fx);
                if (fx <= 0) fx = 1.0;

                foreach (var l in Lines)
                {
                    double lineCur = Math.Round(l.Qty * l.Rate * (1.0 - l.DiscPercent / 100.0), 2);
                    l.NetAmount = lineCur;
                    l.TaxAmount = 0;
                    l.GrossAmount = lineCur;
                    curTotal += lineCur;
                }

                if (TxtCurSubtotal != null) TxtCurSubtotal.Text = $"{curTotal:N2}";
                if (TxtCurTotal != null) TxtCurTotal.Text = $"{curTotal:N2}";
                if (TxtInrTotal != null) TxtInrTotal.Text = $"₹{(curTotal * fx):N2}";

                if (RbCash?.IsChecked == true && TxtPaidNow != null)
                {
                    TxtPaidNow.Text = curTotal.ToString("F2");
                }
                UpdateBalance(curTotal);
                UpdatePaymentDueDisplay();
            }
            finally
            {
                _isRecalculating = false;
            }
        }

        private void UpdateBalance(double curTotal = -1)
        {
            if (curTotal < 0)
            {
                double.TryParse(TxtCurTotal?.Text?.Replace(",", "").Trim(), out curTotal);
            }
            double.TryParse(TxtPaidNow?.Text, out double paid);
            double bal = Math.Max(0, curTotal - paid);
            string cur = TxtCur?.Text ?? "AED";
            if (TxtBalance != null) TxtBalance.Text = $"{cur} {bal:N2}";
        }

        private void UpdatePaymentDueDisplay()
        {
            if (TxtPaymentDueDisplay == null) return;
            int.TryParse(TxtCreditDays?.Text, out int days);
            var dt = DpDate?.SelectedDate ?? DateTime.Now;
            TxtPaymentDueDisplay.Text = $"{days} days · {dt.AddDays(days):dd MMM yyyy}";
        }

        private void Terms_Changed(object sender, RoutedEventArgs e)
        {
            if (CreditWrap == null) return;
            bool isCredit = RbCredit.IsChecked == true;
            CreditWrap.Visibility = isCredit ? Visibility.Visible : Visibility.Collapsed;
            if (isCredit && TxtPaidNow != null) TxtPaidNow.Text = "0.00";
            else Recalculate();
            UpdatePaymentDueDisplay();
        }

        private void CreditDays_Changed(object sender, TextChangedEventArgs e) => UpdatePaymentDueDisplay();
        private void PaidNow_Changed(object sender, TextChangedEventArgs e) => UpdateBalance();

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            string party = CbParty.Text.Trim();
            string phone = TxtPhone.Text.Trim();

            if (string.IsNullOrWhiteSpace(party))
            {
                MessageBox.Show("Please enter the buyer company name.", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(phone))
            {
                MessageBox.Show("Please enter the contact phone number.", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var validLines = Lines.Where(l => !string.IsNullOrEmpty(l.Pid) && l.Qty > 0).ToList();
            if (!validLines.Any())
            {
                MessageBox.Show("Please add at least one item with valid quantity.", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Check stock warning
            foreach (var l in validLines)
            {
                var p = AvailableProducts.FirstOrDefault(x => x.Id == l.Pid);
                if (p != null && l.Qty > p.Stock)
                {
                    var proceed = MessageBox.Show($"Only {p.Stock} {p.Unit} of {p.Name} available in stock. Proceed anyway?", "Stock Notice", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (proceed != MessageBoxResult.Yes) return;
                }
            }

            double.TryParse(TxtFx.Text, out double fx);
            if (fx <= 0) fx = 1.0;

            double curTotal = validLines.Sum(l => Math.Round(l.Qty * l.Rate * (1.0 - l.DiscPercent / 100.0), 2));
            double inrTotal = Math.Round(curTotal * fx, 2);
            double.TryParse(TxtPaidNow.Text, out double paidCur);

            bool isCredit = RbCredit.IsChecked == true;
            int.TryParse(TxtCreditDays.Text, out int creditDays);
            string country = (CbCountry.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "UAE";
            string inco = (CbInco.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "FOB";

            var sale = new SaleDoc
            {
                Id = "s_" + Guid.NewGuid().ToString("N")[..8],
                Type = "export",
                InvoiceNo = _dataService.NextDocNo("export", increment: true),
                Date = DpDate.SelectedDate?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd"),
                Party = party,
                ContactPerson = TxtPerson.Text.Trim(),
                Phone = phone,
                Country = country,
                Currency = TxtCur.Text,
                FxRate = fx,
                Incoterm = inco,
                PortOfLoading = TxtPol.Text.Trim(),
                PortOfDischarge = TxtPod.Text.Trim(),
                SaleType = isCredit ? "Credit" : "Cash",
                CreditDays = isCredit ? creditDays : 0,
                PaymentDueDate = isCredit ? (DpDate.SelectedDate ?? DateTime.Now).AddDays(creditDays).ToString("yyyy-MM-dd") : DpDate.SelectedDate?.ToString("yyyy-MM-dd") ?? "",
                NetAmount = Math.Round(inrTotal, 2),
                TaxAmount = 0,
                GrossAmount = Math.Round(inrTotal, 2),
                GrossAmountCurrency = Math.Round(curTotal, 2),
                PaidAmount = Math.Round(Math.Min(inrTotal, paidCur * fx), 2),
                Lines = validLines
            };

            CreatedSale = await _dataService.RecordSaleAsync(sale);
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
