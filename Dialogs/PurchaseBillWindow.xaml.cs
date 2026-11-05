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
    public class PurchaseItemOption
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public double Rate { get; set; }
        public double Gst { get; set; }
    }

    public partial class PurchaseBillWindow : Window
    {
        private readonly AppDataService _dataService = AppDataService.Instance;
        private string _kind = "raw";

        public ObservableCollection<PurchaseItemOption> AvailableItems { get; } = new();
        public ObservableCollection<PurchaseLine> Lines { get; } = new();

        public PurchaseDoc? CreatedPurchase { get; private set; }

        public PurchaseBillWindow(string initialKind = "raw")
        {
            _kind = initialKind;
            InitializeComponent();

            DgLines.ItemsSource = Lines;
            DpDate.SelectedDate = DateTime.Now;

            // Set initial dropdown
            foreach (ComboBoxItem item in CbKind.Items)
            {
                if (item.Tag?.ToString() == _kind)
                {
                    CbKind.SelectedItem = item;
                    break;
                }
            }

            ReloadAvailableItems();
            UpdateDocNo();
            AddLine();
        }

        private void UpdateDocNo()
        {
            if (_dataService == null || TxtBillNo == null || TxtTitle == null) return;
            TxtBillNo.Text = _dataService.NextDocNo(_kind, increment: false);
            TxtTitle.Text = $"New Purchase Bill ({_kind.ToUpper()})";
        }

        private void Kind_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_dataService == null) return;
            if (CbKind?.SelectedItem is ComboBoxItem item && item.Tag is string k)
            {
                _kind = k;
                ReloadAvailableItems();
                UpdateDocNo();
                Lines.Clear();
                AddLine();
            }
        }

        private void ReloadAvailableItems()
        {
            if (_dataService == null) return;
            AvailableItems.Clear();
            if (_kind == "raw" && _dataService.RawMaterials != null)
            {
                foreach (var r in _dataService.RawMaterials)
                    AvailableItems.Add(new PurchaseItemOption { Id = r.Id, Name = r.Name, Unit = r.Unit, Rate = r.Rate, Gst = r.Gst });
            }
            else if (_kind == "ready" && _dataService.Products != null)
            {
                var prods = _dataService.Products.Where(x => x.IsReadymade).ToList();
                // If no items specifically tagged as readymade, include all products so the user can purchase any catalog shoe/uniform
                if (!prods.Any())
                {
                    prods = _dataService.Products.ToList();
                }
                foreach (var p in prods)
                {
                    double buyRate = p.Cost > 0 ? p.Cost : (p.BulkPrice > 0 ? p.BulkPrice : (p.RetailPrice > 0 ? p.RetailPrice * 0.6 : 100));
                    AvailableItems.Add(new PurchaseItemOption
                    {
                        Id = p.Id,
                        Name = string.IsNullOrWhiteSpace(p.Sku) ? p.Name : $"{p.Name} ({p.Sku})",
                        Unit = string.IsNullOrWhiteSpace(p.Unit) ? "pair" : p.Unit,
                        Rate = Math.Round(buyRate, 2),
                        Gst = p.Gst > 0 ? p.Gst : 5
                    });
                }
            }
            else if (_kind == "gen" && _dataService.GeneralItems != null)
            {
                foreach (var g in _dataService.GeneralItems)
                    AvailableItems.Add(new PurchaseItemOption { Id = g.Id, Name = g.Name, Unit = g.Unit, Rate = g.Rate, Gst = g.Gst });
            }
        }

        private void AddLine()
        {
            var first = AvailableItems.FirstOrDefault();
            string defaultUnit = _kind == "raw" ? "sq.ft" : (_kind == "gen" ? "pcs" : "pair");
            string defaultName = _kind == "ready" ? "Readymade Footwear / Apparel" : (_kind == "raw" ? "Raw Material Item" : "Packaging Supply");

            Lines.Add(new PurchaseLine
            {
                ItemId = first?.Id ?? "pur_item_" + Guid.NewGuid().ToString("N")[..6],
                Name = first?.Name ?? defaultName,
                Unit = !string.IsNullOrWhiteSpace(first?.Unit) ? first.Unit : defaultUnit,
                Qty = 10,
                Rate = first != null && first.Rate > 0 ? first.Rate : 100,
                GstPercent = first != null && first.Gst > 0 ? first.Gst : 5
            });
            Recalculate();
        }

        private void AddLine_Click(object sender, RoutedEventArgs e) => AddLine();

        private void DeleteLine_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is PurchaseLine line)
            {
                Lines.Remove(line);
                if (!Lines.Any()) AddLine();
                Recalculate();
            }
        }

        private void ItemSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cb && cb.DataContext is PurchaseLine line)
            {
                string? selectedId = cb.SelectedValue as string;
                if (string.IsNullOrEmpty(selectedId) && cb.SelectedItem is PurchaseItemOption opt)
                {
                    selectedId = opt.Id;
                }

                if (!string.IsNullOrEmpty(selectedId))
                {
                    var it = AvailableItems.FirstOrDefault(x => x.Id == selectedId);
                    if (it != null)
                    {
                        line.ItemId = it.Id;
                        line.Name = it.Name;
                        line.Unit = it.Unit;
                        line.Rate = it.Rate;
                        line.GstPercent = it.Gst;
                        Recalculate();
                        return;
                    }
                }

                if (!string.IsNullOrWhiteSpace(cb.Text))
                {
                    line.Name = cb.Text.Trim();
                }
            }
        }

        private void Recalculate()
        {
            double subtotal = 0;
            double totalTax = 0;

            foreach (var l in Lines)
            {
                double lineNet = l.Qty * l.Rate;
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

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            string party = TxtParty.Text.Trim();
            if (string.IsNullOrWhiteSpace(party))
            {
                MessageBox.Show("Please enter the vendor name.", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            foreach (var l in Lines)
            {
                if (string.IsNullOrWhiteSpace(l.ItemId))
                    l.ItemId = "pur_it_" + Guid.NewGuid().ToString("N")[..8];
            }

            var validLines = Lines.Where(l => !string.IsNullOrWhiteSpace(l.Name) && l.Qty > 0).ToList();
            if (!validLines.Any())
            {
                MessageBox.Show("Please add at least one item with valid quantity.", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            double subtotal = validLines.Sum(l => l.NetAmount);
            double tax = validLines.Sum(l => l.TaxAmount);
            double gross = subtotal + tax;

            bool isCredit = RbCredit.IsChecked == true;
            int.TryParse(TxtCreditDays.Text, out int creditDays);
            double.TryParse(TxtPaidNow.Text, out double paidNow);

            var purchase = new PurchaseDoc
            {
                Id = "b_" + Guid.NewGuid().ToString("N")[..8],
                Kind = _kind,
                BillNo = _dataService.NextDocNo(_kind, increment: true),
                VendorBillRef = TxtRef.Text.Trim(),
                Date = DpDate.SelectedDate?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd"),
                Party = party,
                Phone = TxtPhone.Text.Trim(),
                SaleType = isCredit ? "Credit" : "Cash",
                CreditDays = isCredit ? creditDays : 0,
                PaymentDueDate = isCredit ? (DpDate.SelectedDate ?? DateTime.Now).AddDays(creditDays).ToString("yyyy-MM-dd") : DpDate.SelectedDate?.ToString("yyyy-MM-dd") ?? "",
                NetAmount = Math.Round(subtotal, 2),
                TaxAmount = Math.Round(tax, 2),
                GrossAmount = Math.Round(gross, 2),
                PaidAmount = Math.Round(Math.Min(gross, paidNow), 2),
                Lines = validLines
            };

            CreatedPurchase = await _dataService.RecordPurchaseAsync(purchase);
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
