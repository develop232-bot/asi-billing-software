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
    public partial class DomesticInvoiceWindow : Window
    {
        private readonly AppDataService _dataService;

        public List<ProductItem> AvailableProducts { get; }
        public ObservableCollection<SaleLine> Lines { get; } = new();

        public SaleDoc? CreatedSale { get; private set; }

        public DomesticInvoiceWindow()
        {
            InitializeComponent();
            _dataService = AppDataService.Instance;

            AvailableProducts = _dataService.Products.ToList();
            DgLines.ItemsSource = Lines;

            TxtInvoiceNo.Text = _dataService.NextDocNo("domestic", increment: false);
            DpDate.SelectedDate = DateTime.Now;

            // Autocomplete suggestions for domestic customer names
            var pastParties = _dataService.Sales
                .Where(s => s.Type == "domestic" && !string.IsNullOrWhiteSpace(s.Party))
                .Select(s => s.Party)
                .Distinct()
                .OrderBy(x => x)
                .ToList();
            CbParty.ItemsSource = pastParties;

            AddLine();
            UpdatePaymentDueDisplay();
        }

        private void AddLine()
        {
            var line = new SaleLine
            {
                Pid = string.Empty,
                Name = string.Empty,
                Hsn = string.Empty,
                Size = string.Empty,
                Qty = 1,
                Unit = "pair",
                Rate = 0,
                GstPercent = 5,
                DiscPercent = 0
            };
            line.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SaleLine.Rate))
                {
                    line.GstPercent = line.Rate <= 2500 ? 5 : 18;
                }
                Recalculate();
            };
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
                    line.Name = p.Name;
                    line.Hsn = p.Hsn;
                    line.Unit = p.Unit;
                    line.Rate = p.DomesticPrice > 0 ? p.DomesticPrice : (p.RetailPrice > 0 ? p.RetailPrice : 0);
                    line.Size = p.Sizes?.FirstOrDefault() ?? "—";
                    line.GstPercent = line.Rate <= 2500 ? 5 : 18;
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
                double subtotal = 0;
                double totalTax = 0;

                foreach (var l in Lines)
                {
                    double lineNet = Math.Round(l.Qty * l.Rate * (1.0 - l.DiscPercent / 100.0), 2);
                    double lineTax = Math.Round(lineNet * (l.GstPercent / 100.0), 2);
                    l.NetAmount = lineNet;
                    l.TaxAmount = lineTax;
                    l.GrossAmount = Math.Round(lineNet + lineTax, 2);

                    subtotal += lineNet;
                    totalTax += lineTax;
                }

                if (TxtSubtotal != null) TxtSubtotal.Text = $"₹{subtotal:N2}";
                double gross = Math.Round(subtotal + totalTax, 2);
                if (TxtGrossTotal != null) TxtGrossTotal.Text = $"₹{gross:N2}";

                bool isInter = CbTaxType?.SelectedIndex == 1;
                if (isInter)
                {
                    if (RowCgst != null) RowCgst.Visibility = Visibility.Collapsed;
                    if (RowSgst != null) RowSgst.Visibility = Visibility.Collapsed;
                    if (RowIgst != null)
                    {
                        RowIgst.Visibility = Visibility.Visible;
                        TxtIgst.Text = $"₹{totalTax:N2}";
                    }
                }
                else
                {
                    if (RowIgst != null) RowIgst.Visibility = Visibility.Collapsed;
                    if (RowCgst != null)
                    {
                        RowCgst.Visibility = Visibility.Visible;
                        TxtCgst.Text = $"₹{(totalTax / 2.0):N2}";
                    }
                    if (RowSgst != null)
                    {
                        RowSgst.Visibility = Visibility.Visible;
                        TxtSgst.Text = $"₹{(totalTax / 2.0):N2}";
                    }
                }

                if (RbCash?.IsChecked == true && TxtPaidNow != null)
                {
                    TxtPaidNow.Text = gross.ToString("F2");
                }
                UpdateBalance(gross);
                UpdatePaymentDueDisplay();
            }
            finally
            {
                _isRecalculating = false;
            }
        }

        private void UpdateBalance(double gross = -1)
        {
            if (gross < 0)
            {
                double.TryParse((TxtGrossTotal?.Text ?? "").Replace("₹", "").Replace(",", "").Trim(), out gross);
            }
            double.TryParse(TxtPaidNow?.Text, out double paid);
            double bal = Math.Max(0, gross - paid);
            if (TxtBalance != null) TxtBalance.Text = $"₹{bal:N2}";
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

        private void TaxType_Changed(object sender, SelectionChangedEventArgs e)
        {
            Recalculate();
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            string party = CbParty.Text.Trim();
            string phone = TxtPhone.Text.Trim();

            if (string.IsNullOrWhiteSpace(party))
            {
                MessageBox.Show("Please enter the customer / company name.", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
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

            double subtotal = validLines.Sum(l => l.NetAmount);
            double tax = validLines.Sum(l => l.TaxAmount);
            double gross = Math.Round(subtotal + tax, 2);

            bool isCredit = RbCredit.IsChecked == true;
            int.TryParse(TxtCreditDays.Text, out int creditDays);
            double.TryParse(TxtPaidNow.Text, out double paidNow);

            var sale = new SaleDoc
            {
                Id = "s_" + Guid.NewGuid().ToString("N")[..8],
                Type = "domestic",
                InvoiceNo = _dataService.NextDocNo("domestic", increment: true),
                Date = DpDate.SelectedDate?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd"),
                Party = party,
                ContactPerson = TxtPerson.Text.Trim(),
                Phone = phone,
                CustomerGstin = TxtGstin.Text.Trim(),
                VehicleNo = TxtVehicle.Text.Trim(),
                SaleType = isCredit ? "Credit" : "Cash",
                CreditDays = isCredit ? creditDays : 0,
                PaymentDueDate = isCredit ? (DpDate.SelectedDate ?? DateTime.Now).AddDays(creditDays).ToString("yyyy-MM-dd") : DpDate.SelectedDate?.ToString("yyyy-MM-dd") ?? "",
                TaxType = CbTaxType.SelectedIndex == 1 ? "inter" : "intra",
                NetAmount = Math.Round(subtotal, 2),
                TaxAmount = Math.Round(tax, 2),
                GrossAmount = Math.Round(gross, 2),
                PaidAmount = Math.Round(Math.Min(gross, paidNow), 2),
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
