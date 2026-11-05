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
        private readonly AppDataService _dataService = AppDataService.Instance;

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

            // Add an initial empty line
            AddLine();
        }

        private void AddLine()
        {
            var first = AvailableProducts.FirstOrDefault();
            if (first != null)
            {
                Lines.Add(new SaleLine
                {
                    Pid = first.Id,
                    Name = first.Name,
                    Hsn = first.Hsn,
                    Size = first.Sizes?.FirstOrDefault() ?? "",
                    Qty = 10,
                    Unit = first.Unit,
                    Rate = first.DomesticPrice,
                    GstPercent = first.AutoGstRate,
                    DiscPercent = 0
                });
                Recalculate();
            }
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
                    line.Rate = p.DomesticPrice;
                    line.Size = p.Sizes?.FirstOrDefault() ?? "";
                    line.GstPercent = p.AutoGstRate;
                    Recalculate();
                }
            }
        }

        private void Recalculate()
        {
            double subtotal = 0;
            double totalTax = 0;

            foreach (var l in Lines)
            {
                double lineNet = l.Qty * l.Rate * (1.0 - l.DiscPercent / 100.0);
                double lineTax = lineNet * (l.GstPercent / 100.0);
                l.NetAmount = Math.Round(lineNet, 2);
                l.TaxAmount = Math.Round(lineTax, 2);
                l.GrossAmount = Math.Round(lineNet + lineTax, 2);

                subtotal += lineNet;
                totalTax += lineTax;
            }

            TxtSubtotal.Text = $"₹{subtotal:N2}";
            TxtTaxAmount.Text = $"₹{totalTax:N2}";
            TxtGrossTotal.Text = $"₹{(subtotal + totalTax):N2}";

            if (RbCash.IsChecked == true)
            {
                TxtPaidNow.Text = (subtotal + totalTax).ToString("F2");
            }
        }

        private void Terms_Changed(object sender, RoutedEventArgs e)
        {
            if (CreditWrap == null) return;
            bool isCredit = RbCredit.IsChecked == true;
            CreditWrap.Visibility = isCredit ? Visibility.Visible : Visibility.Collapsed;
            if (isCredit) TxtPaidNow.Text = "0.00";
            else Recalculate();
        }

        private void CreditDays_Changed(object sender, TextChangedEventArgs e) { }
        private void PaidNow_Changed(object sender, TextChangedEventArgs e) { }

        private void TaxType_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (LblTax == null) return;
            bool isInter = CbTaxType.SelectedIndex == 1;
            LblTax.Text = isInter ? "IGST:" : "CGST + SGST:";
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            string party = TxtParty.Text.Trim();
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

            // Check stock
            foreach (var l in validLines)
            {
                var p = AvailableProducts.FirstOrDefault(x => x.Id == l.Pid);
                if (p != null && l.Qty > p.Stock)
                {
                    MessageBox.Show($"Only {p.Stock} {p.Unit} of {p.Name} available in stock.", "Stock Limit", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            double subtotal = validLines.Sum(l => l.NetAmount);
            double tax = validLines.Sum(l => l.TaxAmount);
            double gross = subtotal + tax;

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
