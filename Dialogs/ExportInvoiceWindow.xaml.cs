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
        private readonly AppDataService _dataService = AppDataService.Instance;

        private static readonly Dictionary<string, (string Cur, double Fx)> CountryFx = new()
        {
            { "UAE", ("AED", 24.1) },
            { "Saudi Arabia", ("SAR", 23.6) },
            { "Qatar", ("QAR", 24.3) },
            { "Oman", ("OMR", 230.0) },
            { "Kuwait", ("KWD", 288.0) },
            { "Bahrain", ("BHD", 235.0) },
            { "Other (USD)", ("USD", 88.5) }
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

            AddLine();
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
                    LblCurTotal.Text = $"Total ({val.Cur}):";

                    // Update default rates
                    foreach (var l in Lines)
                    {
                        var p = AvailableProducts.FirstOrDefault(x => x.Id == l.Pid);
                        if (p != null)
                        {
                            l.Rate = Math.Round(p.ExportPrice * 88.5 / val.Fx, 2);
                        }
                    }
                    Recalculate();
                }
            }
        }

        private void Fx_TextChanged(object sender, TextChangedEventArgs e) => Recalculate();

        private void AddLine()
        {
            var first = AvailableProducts.FirstOrDefault();
            if (first != null)
            {
                double.TryParse(TxtFx.Text, out double fx);
                if (fx <= 0) fx = 24.1;

                double curRate = Math.Round(first.ExportPrice * 88.5 / fx, 2);

                Lines.Add(new SaleLine
                {
                    Pid = first.Id,
                    Name = first.Name,
                    Hsn = first.Hsn,
                    Size = first.Sizes?.FirstOrDefault() ?? "",
                    Qty = 100,
                    Unit = first.Unit,
                    Rate = curRate,
                    GstPercent = 0,
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
                    double.TryParse(TxtFx.Text, out double fx);
                    if (fx <= 0) fx = 24.1;

                    line.Name = p.Name;
                    line.Hsn = p.Hsn;
                    line.Unit = p.Unit;
                    line.Rate = Math.Round(p.ExportPrice * 88.5 / fx, 2);
                    line.Size = p.Sizes?.FirstOrDefault() ?? "";
                    line.GstPercent = 0;
                    Recalculate();
                }
            }
        }

        private void Recalculate()
        {
            double curTotal = 0;
            double.TryParse(TxtFx?.Text, out double fx);
            if (fx <= 0) fx = 1.0;

            foreach (var l in Lines)
            {
                double lineCur = l.Qty * l.Rate;
                l.GrossAmount = Math.Round(lineCur, 2);
                l.NetAmount = Math.Round(lineCur * fx, 2);
                curTotal += lineCur;
            }

            if (TxtCurTotal != null) TxtCurTotal.Text = $"{curTotal:N2}";
            if (TxtInrTotal != null) TxtInrTotal.Text = $"₹{(curTotal * fx):N2}";
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            string party = TxtParty.Text.Trim();
            if (string.IsNullOrWhiteSpace(party))
            {
                MessageBox.Show("Please enter the buyer company name.", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var validLines = Lines.Where(l => !string.IsNullOrEmpty(l.Pid) && l.Qty > 0).ToList();
            if (!validLines.Any())
            {
                MessageBox.Show("Please add at least one item with valid quantity.", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            double.TryParse(TxtFx.Text, out double fx);
            if (fx <= 0) fx = 1.0;

            double curTotal = validLines.Sum(l => l.Qty * l.Rate);
            double inrTotal = curTotal * fx;
            double.TryParse(TxtPaidNow.Text, out double paidCur);

            string country = (CbCountry.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "UAE";
            string inco = (CbInco.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "FOB";

            var sale = new SaleDoc
            {
                Id = "s_" + Guid.NewGuid().ToString("N")[..8],
                Type = "export",
                InvoiceNo = _dataService.NextDocNo("export", increment: true),
                Date = DpDate.SelectedDate?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd"),
                Party = party,
                Country = country,
                Currency = TxtCur.Text,
                FxRate = fx,
                Incoterm = inco,
                PortOfLoading = TxtPol.Text.Trim(),
                PortOfDischarge = TxtPod.Text.Trim(),
                SaleType = "Credit",
                CreditDays = 45,
                PaymentDueDate = (DpDate.SelectedDate ?? DateTime.Now).AddDays(45).ToString("yyyy-MM-dd"),
                NetAmount = Math.Round(inrTotal, 2),
                TaxAmount = 0,
                GrossAmount = Math.Round(inrTotal, 2),
                GrossAmountCurrency = Math.Round(curTotal, 2),
                PaidAmount = Math.Round(paidCur * fx, 2),
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
