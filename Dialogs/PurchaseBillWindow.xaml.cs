using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
        private bool _isUpdating;
        private bool _isInitialized;

        public ObservableCollection<PurchaseItemOption> AvailableItems { get; } = new();
        public ObservableCollection<PurchaseLine> Lines { get; } = new();

        public PurchaseDoc? CreatedPurchase { get; private set; }

        public PurchaseBillWindow(string initialKind = "raw")
        {
            _kind = string.IsNullOrWhiteSpace(initialKind) ? "raw" : initialKind.ToLowerInvariant();
            InitializeComponent();

            DgLines.ItemsSource = Lines;
            DpDate.SelectedDate = DateTime.Now;

            // Set initial dropdown without triggering infinite loops
            _isUpdating = true;
            try
            {
                foreach (ComboBoxItem item in CbKind.Items)
                {
                    if (item.Tag?.ToString() == _kind)
                    {
                        CbKind.SelectedItem = item;
                        break;
                    }
                }
            }
            finally
            {
                _isUpdating = false;
            }

            ReloadAvailableItems();
            UpdateDocNo();
            AddLine();
            _isInitialized = true;
        }

        private void UpdateDocNo()
        {
            if (_dataService == null || TxtBillNo == null || TxtTitle == null) return;
            TxtBillNo.Text = _dataService.NextDocNo(_kind, increment: false);
            string displayKind = _kind switch
            {
                "ready" => "Readymade Products",
                "gen" => "General Packaging",
                _ => "Raw Material"
            };
            TxtTitle.Text = $"New Purchase Bill ({displayKind})";
        }

        private void Kind_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            e.Handled = true;
            if (_isUpdating || !_isInitialized || _dataService == null) return;
            if (CbKind?.SelectedItem is ComboBoxItem item && item.Tag is string k)
            {
                if (_kind == k) return;
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

            if (_kind == "raw")
            {
                if (_dataService.RawMaterials != null && _dataService.RawMaterials.Any())
                {
                    foreach (var r in _dataService.RawMaterials)
                        AvailableItems.Add(new PurchaseItemOption { Id = r.Id, Name = r.Name, Unit = r.Unit, Rate = r.Rate, Gst = r.Gst });
                }
                else
                {
                    AvailableItems.Add(new PurchaseItemOption { Id = "raw_leather", Name = "Upper Leather (Black / Brown)", Unit = "sq.ft", Rate = 65, Gst = 5 });
                    AvailableItems.Add(new PurchaseItemOption { Id = "raw_sole", Name = "Rubber / PVC Sole (Pairs)", Unit = "pair", Rate = 85, Gst = 5 });
                    AvailableItems.Add(new PurchaseItemOption { Id = "raw_fabric", Name = "Uniform Shirting Fabric", Unit = "meter", Rate = 95, Gst = 5 });
                    AvailableItems.Add(new PurchaseItemOption { Id = "raw_lining", Name = "Inner Breathable Lining", Unit = "meter", Rate = 45, Gst = 5 });
                    AvailableItems.Add(new PurchaseItemOption { Id = "raw_thread", Name = "Heavy Duty Thread Spool", Unit = "roll", Rate = 120, Gst = 5 });
                    AvailableItems.Add(new PurchaseItemOption { Id = "raw_adhesive", Name = "Shoe Adhesive / Chemical Glue", Unit = "kg", Rate = 210, Gst = 18 });
                }
            }
            else if (_kind == "ready")
            {
                var prods = _dataService.Products?.ToList() ?? new List<ProductItem>();
                if (prods.Any())
                {
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
                else
                {
                    // Fallback readymade catalog suggestions so clicking Add Item always responds
                    AvailableItems.Add(new PurchaseItemOption { Id = "rdy_formal_shoe", Name = "Formal Derby Leather Shoe", Unit = "pair", Rate = 450, Gst = 5 });
                    AvailableItems.Add(new PurchaseItemOption { Id = "rdy_school_shoe", Name = "School Uniform Shoe Black", Unit = "pair", Rate = 280, Gst = 5 });
                    AvailableItems.Add(new PurchaseItemOption { Id = "rdy_sports_shoe", Name = "Sports Running Shoe", Unit = "pair", Rate = 380, Gst = 5 });
                    AvailableItems.Add(new PurchaseItemOption { Id = "rdy_safety_shoe", Name = "Industrial Steel Toe Safety Shoe", Unit = "pair", Rate = 620, Gst = 18 });
                    AvailableItems.Add(new PurchaseItemOption { Id = "rdy_uniform_shirt", Name = "School Uniform Half Shirt", Unit = "pcs", Rate = 180, Gst = 5 });
                    AvailableItems.Add(new PurchaseItemOption { Id = "rdy_uniform_pant", Name = "School Uniform Navy Pant", Unit = "pcs", Rate = 240, Gst = 5 });
                    AvailableItems.Add(new PurchaseItemOption { Id = "rdy_socks_pack", Name = "Cotton Socks (Pack of 3)", Unit = "pair", Rate = 45, Gst = 5 });
                }
            }
            else if (_kind == "gen")
            {
                if (_dataService.GeneralItems != null && _dataService.GeneralItems.Any())
                {
                    foreach (var g in _dataService.GeneralItems)
                        AvailableItems.Add(new PurchaseItemOption { Id = g.Id, Name = g.Name, Unit = g.Unit, Rate = g.Rate, Gst = g.Gst });
                }
                else
                {
                    AvailableItems.Add(new PurchaseItemOption { Id = "gen_shoe_box", Name = "Branded Footwear Shoe Box", Unit = "pcs", Rate = 14, Gst = 18 });
                    AvailableItems.Add(new PurchaseItemOption { Id = "gen_poly_bag", Name = "Uniform Garment Poly Cover", Unit = "pcs", Rate = 3, Gst = 18 });
                    AvailableItems.Add(new PurchaseItemOption { Id = "gen_tape", Name = "Packaging Brown BOPP Tape", Unit = "roll", Rate = 65, Gst = 18 });
                    AvailableItems.Add(new PurchaseItemOption { Id = "gen_carton", Name = "Corrugated Master Carton (24 prs)", Unit = "pcs", Rate = 85, Gst = 18 });
                    AvailableItems.Add(new PurchaseItemOption { Id = "gen_hang_tag", Name = "Product Price / Brand Hang Tag", Unit = "pcs", Rate = 2, Gst = 18 });
                }
            }
        }

        private void AddLine()
        {
            var first = AvailableItems.FirstOrDefault();
            string defaultUnit = _kind == "raw" ? "sq.ft" : (_kind == "gen" ? "pcs" : "pair");
            string defaultName = _kind == "ready" ? "Readymade Footwear / Uniform" : (_kind == "raw" ? "Raw Material Item" : "Packaging Supply");

            var line = new PurchaseLine
            {
                ItemId = first?.Id ?? "pur_item_" + Guid.NewGuid().ToString("N")[..6],
                Name = first?.Name ?? defaultName,
                Unit = !string.IsNullOrWhiteSpace(first?.Unit) ? first.Unit : defaultUnit,
                Qty = 10,
                Rate = first != null && first.Rate > 0 ? first.Rate : 100,
                GstPercent = first != null && first.Gst > 0 ? first.Gst : 5
            };
            line.RecalculateLine();
            line.PropertyChanged += Line_PropertyChanged;
            Lines.Add(line);
            Recalculate();
        }

        private void Line_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PurchaseLine.GrossAmount) ||
                e.PropertyName == nameof(PurchaseLine.NetAmount) ||
                e.PropertyName == nameof(PurchaseLine.TaxAmount) ||
                e.PropertyName == nameof(PurchaseLine.Rate) ||
                e.PropertyName == nameof(PurchaseLine.Qty))
            {
                Recalculate();
            }
        }

        private void AddLine_Click(object sender, RoutedEventArgs e) => AddLine();

        private void DeleteLine_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is PurchaseLine line)
            {
                line.PropertyChanged -= Line_PropertyChanged;
                Lines.Remove(line);
                if (!Lines.Any()) AddLine();
                Recalculate();
            }
        }

        private void ItemSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            e.Handled = true;
            if (_isUpdating) return;
            _isUpdating = true;

            try
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
                            if (it.Rate > 0) line.Rate = it.Rate;
                            line.GstPercent = it.Gst;
                            line.RecalculateLine();
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
            finally
            {
                _isUpdating = false;
            }
        }

        private void ItemComboBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is ComboBox cb && cb.DataContext is PurchaseLine line)
            {
                if (!string.IsNullOrWhiteSpace(cb.Text) && cb.SelectedItem == null)
                {
                    line.Name = cb.Text.Trim();
                    if (string.IsNullOrEmpty(line.ItemId) || line.ItemId.StartsWith("pur_item_"))
                    {
                        line.ItemId = "custom_" + Guid.NewGuid().ToString("N")[..6];
                    }
                    line.RecalculateLine();
                }
                Recalculate();
            }
        }

        private void DgLines_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() => Recalculate()));
        }

        private void Recalculate()
        {
            double subtotal = 0;
            double totalTax = 0;

            foreach (var l in Lines)
            {
                l.RecalculateLine();
                subtotal += l.NetAmount;
                totalTax += l.TaxAmount;
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
                l.RecalculateLine();
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
