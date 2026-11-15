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
    public partial class WorkOrderWindow : Window
    {
        private readonly AppDataService _dataService = AppDataService.Instance;
        private readonly WorkOrderDoc? _existingOrder;
        private readonly bool _isEdit;

        public List<ProductItem> ManufacturedProducts { get; }
        public List<RawMaterialItem> AvailableRawMaterials { get; }

        public ObservableCollection<WorkOrderItem> Items { get; } = new();
        public ObservableCollection<WorkOrderMaterial> Materials { get; } = new();

        public WorkOrderWindow(WorkOrderDoc? existing = null)
        {
            InitializeComponent();
            _dataService = AppDataService.Instance;
            _existingOrder = existing;
            _isEdit = existing != null;

            ManufacturedProducts = _dataService.Products.ToList();
            AvailableRawMaterials = _dataService.RawMaterials.ToList();

            DgItems.ItemsSource = Items;
            DgMaterials.ItemsSource = Materials;

            if (_isEdit && _existingOrder != null)
            {
                BtnDeleteOrder.Visibility = Visibility.Visible;
                TxtTitle.Text = $"Edit Work Order {_existingOrder.OrderNo}";
                TxtOrderNo.Text = _existingOrder.OrderNo;
                TxtCustomer.Text = _existingOrder.Customer;
                TxtNotes.Text = _existingOrder.Notes ?? "";

                foreach (ComboBoxItem itm in CbOrderType.Items)
                {
                    if (itm.Tag?.ToString() == _existingOrder.OrderType)
                    {
                        CbOrderType.SelectedItem = itm;
                        break;
                    }
                }

                if (DateTime.TryParse(_existingOrder.OrderDate, out var od)) DpOrderDate.SelectedDate = od;
                if (DateTime.TryParse(_existingOrder.DueDate, out var dd)) DpDueDate.SelectedDate = dd;
                if (DateTime.TryParse(_existingOrder.StartDate, out var sd)) DpStartDate.SelectedDate = sd;
                if (DateTime.TryParse(_existingOrder.EndDate, out var ed)) DpEndDate.SelectedDate = ed;

                foreach (ComboBoxItem itm in CbStatus.Items)
                {
                    if (string.Equals(itm.Content?.ToString(), _existingOrder.Status, StringComparison.OrdinalIgnoreCase))
                    {
                        CbStatus.SelectedItem = itm;
                        break;
                    }
                }

                foreach (var it in _existingOrder.Items)
                    Items.Add(new WorkOrderItem { ProductId = it.ProductId, ProductName = it.ProductName, Size = it.Size, Unit = it.Unit, Qty = it.Qty });

                foreach (var m in _existingOrder.Materials)
                    Materials.Add(new WorkOrderMaterial { ItemId = m.ItemId, Name = m.Name, Unit = m.Unit, Qty = m.Qty });
            }
            else
            {
                TxtTitle.Text = "New Production Work Order";
                TxtOrderNo.Text = _dataService.NextDocNo("wo", increment: false);
                DpOrderDate.SelectedDate = DateTime.Now;
                DpDueDate.SelectedDate = DateTime.Now.AddDays(7);

                AddItem();
            }
        }

        private void CbStatus_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CbStatus?.SelectedItem is ComboBoxItem item)
            {
                string status = item.Content?.ToString() ?? "";
                if (status != "Pending" && DpStartDate != null && !DpStartDate.SelectedDate.HasValue)
                {
                    DpStartDate.SelectedDate = DateTime.Now;
                }
                if (status == "Completed" && DpEndDate != null && !DpEndDate.SelectedDate.HasValue)
                {
                    DpEndDate.SelectedDate = DateTime.Now;
                }
            }
        }

        private void AddItem()
        {
            var p = ManufacturedProducts.FirstOrDefault() ?? _dataService.Products.FirstOrDefault();
            Items.Add(new WorkOrderItem
            {
                ProductId = p?.Id ?? string.Empty,
                ProductName = p?.Name ?? string.Empty,
                Size = p?.Sizes?.FirstOrDefault() ?? string.Empty,
                Unit = !string.IsNullOrWhiteSpace(p?.Unit) ? p.Unit : "pairs",
                Qty = 100
            });
        }

        private void AddItem_Click(object sender, RoutedEventArgs e) => AddItem();

        private void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is WorkOrderItem item)
            {
                Items.Remove(item);
            }
        }

        private void ProductSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cb && cb.DataContext is WorkOrderItem item)
            {
                string pid = cb.SelectedValue as string ?? "";
                var p = (!string.IsNullOrEmpty(pid) ? ManufacturedProducts.FirstOrDefault(x => x.Id == pid) : null)
                    ?? (cb.SelectedItem as ProductItem);

                if (p != null)
                {
                    item.ProductId = p.Id;
                    item.ProductName = p.Name;
                    item.Unit = p.Unit;
                    item.Size = p.Sizes?.FirstOrDefault() ?? "";

                    if (Materials.Count == 0 && item.Qty > 0)
                    {
                        ApplyRecipeSuggestion(showMessage: false);
                    }
                }
            }
        }

        private void ProductComboBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is ComboBox cb && cb.DataContext is WorkOrderItem item)
            {
                string typed = cb.Text?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(typed))
                {
                    var match = ManufacturedProducts.FirstOrDefault(x =>
                        string.Equals(x.Name, typed, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(x.Sku, typed, StringComparison.OrdinalIgnoreCase))
                        ?? _dataService.Products.FirstOrDefault(x =>
                            string.Equals(x.Name, typed, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(x.Sku, typed, StringComparison.OrdinalIgnoreCase))
                        ?? ManufacturedProducts.FirstOrDefault(x =>
                            x.Name.Contains(typed, StringComparison.OrdinalIgnoreCase) ||
                            x.Sku.Contains(typed, StringComparison.OrdinalIgnoreCase));

                    if (match != null)
                    {
                        item.ProductId = match.Id;
                        item.ProductName = match.Name;
                        item.Unit = match.Unit;
                        if (string.IsNullOrEmpty(item.Size))
                            item.Size = match.Sizes?.FirstOrDefault() ?? "";
                        cb.SelectedValue = match.Id;

                        if (Materials.Count == 0 && item.Qty > 0)
                        {
                            ApplyRecipeSuggestion(showMessage: false);
                        }
                    }
                    else
                    {
                        item.ProductName = typed;
                    }
                }
            }
        }

        private void AddMat_Click(object sender, RoutedEventArgs e)
        {
            var rm = AvailableRawMaterials.FirstOrDefault();
            Materials.Add(new WorkOrderMaterial
            {
                ItemId = rm?.Id ?? string.Empty,
                Name = rm?.Name ?? string.Empty,
                Unit = !string.IsNullOrWhiteSpace(rm?.Unit) ? rm.Unit : "sq ft",
                Qty = 10
            });
        }

        private void DeleteMat_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is WorkOrderMaterial mat)
            {
                Materials.Remove(mat);
            }
        }

        private void MaterialSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cb && cb.DataContext is WorkOrderMaterial mat && cb.SelectedValue is string rId)
            {
                var rm = AvailableRawMaterials.FirstOrDefault(x => x.Id == rId);
                if (rm != null)
                {
                    mat.Name = rm.Name;
                    mat.Unit = rm.Unit;
                }
            }
        }

        private void MaterialComboBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is ComboBox cb && cb.DataContext is WorkOrderMaterial mat)
            {
                string typed = cb.Text?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(typed))
                {
                    var match = AvailableRawMaterials.FirstOrDefault(x =>
                        string.Equals(x.Name, typed, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(x.Sku, typed, StringComparison.OrdinalIgnoreCase))
                        ?? AvailableRawMaterials.FirstOrDefault(x =>
                            x.Name.Contains(typed, StringComparison.OrdinalIgnoreCase) ||
                            x.Sku.Contains(typed, StringComparison.OrdinalIgnoreCase));

                    if (match != null)
                    {
                        mat.ItemId = match.Id;
                        mat.Name = match.Name;
                        mat.Unit = match.Unit;
                        cb.SelectedValue = match.Id;
                    }
                    else
                    {
                        mat.Name = typed;
                    }
                }
            }
        }

        private void SuggestRecipe_Click(object sender, RoutedEventArgs e)
        {
            ApplyRecipeSuggestion(showMessage: true);
        }

        private void ApplyRecipeSuggestion(bool showMessage)
        {
            var validItems = Items.Where(x => (!string.IsNullOrEmpty(x.ProductId) || !string.IsNullOrEmpty(x.ProductName)) && x.Qty > 0).ToList();
            if (!validItems.Any())
            {
                if (showMessage)
                {
                    MessageBox.Show("Please add at least one product with quantity in 'Products to Make' first.", "Recipe Suggestion", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                return;
            }

            var need = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in validItems)
            {
                var prod = (!string.IsNullOrEmpty(item.ProductId) ? _dataService.Products.FirstOrDefault(x => x.Id == item.ProductId) : null)
                    ?? _dataService.Products.FirstOrDefault(x => string.Equals(x.Name, item.ProductName, StringComparison.OrdinalIgnoreCase))
                    ?? ManufacturedProducts.FirstOrDefault(x => x.Id == item.ProductId || string.Equals(x.Name, item.ProductName, StringComparison.OrdinalIgnoreCase));

                // 1. Check if product has explicit BOM
                if (prod?.Bom != null && prod.Bom.Count > 0)
                {
                    foreach (var b in prod.Bom)
                    {
                        string key = !string.IsNullOrEmpty(b.Id) ? b.Id : b.Name;
                        need.TryGetValue(key, out double cur);
                        need[key] = cur + (b.Qty * item.Qty);
                    }
                }
                else
                {
                    // 2. Auto recipe connection based on AvailableRawMaterials
                    bool isShoe = prod?.Category == "Shoe" ||
                                  (!string.IsNullOrEmpty(item.ProductName) &&
                                   (item.ProductName.Contains("shoe", StringComparison.OrdinalIgnoreCase) ||
                                    item.ProductName.Contains("lofar", StringComparison.OrdinalIgnoreCase) ||
                                    item.ProductName.Contains("cut", StringComparison.OrdinalIgnoreCase) ||
                                    item.ProductName.Contains("formal", StringComparison.OrdinalIgnoreCase) ||
                                    item.ProductName.Contains("boot", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(item.Unit, "pair", StringComparison.OrdinalIgnoreCase)));

                    if (isShoe && AvailableRawMaterials.Any())
                    {
                        foreach (var rm in AvailableRawMaterials)
                        {
                            string rmLower = rm.Name.ToLowerInvariant();
                            double perPairRate = 0;

                            if (rmLower.Contains("leather") || rmLower.Contains("upper") || rmLower.Contains("fabric") || rmLower.Contains("material"))
                            {
                                perPairRate = 2.0; // 2 sq.ft per pair
                            }
                            else if (rmLower.Contains("insole") || rmLower.Contains("sole"))
                            {
                                perPairRate = rm.Unit.Contains("pc", StringComparison.OrdinalIgnoreCase) ? 2.0 : 1.0;
                            }
                            else if (rmLower.Contains("last") || rmLower.Contains("mould"))
                            {
                                perPairRate = rm.Unit.Contains("pc", StringComparison.OrdinalIgnoreCase) ? 2.0 : 1.0;
                            }
                            else if (rmLower.Contains("thread") || rmLower.Contains("adhesive") || rmLower.Contains("glue"))
                            {
                                perPairRate = 0.05;
                            }
                            else
                            {
                                perPairRate = 1.0;
                            }

                            if (perPairRate > 0)
                            {
                                need.TryGetValue(rm.Id, out double cur);
                                need[rm.Id] = cur + (perPairRate * item.Qty);
                            }
                        }
                    }
                    else if (AvailableRawMaterials.Any())
                    {
                        foreach (var rm in AvailableRawMaterials)
                        {
                            need.TryGetValue(rm.Id, out double cur);
                            need[rm.Id] = cur + (1.0 * item.Qty);
                        }
                    }
                }
            }

            if (!need.Any())
            {
                if (showMessage)
                {
                    MessageBox.Show("No matching raw materials found. Add raw materials under Stock > Raw Materials.", "Recipe Suggestion", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                return;
            }

            Materials.Clear();
            foreach (var kvp in need)
            {
                var rm = AvailableRawMaterials.FirstOrDefault(x => x.Id == kvp.Key || string.Equals(x.Name, kvp.Key, StringComparison.OrdinalIgnoreCase))
                    ?? _dataService.RawMaterials.FirstOrDefault(x => x.Id == kvp.Key || string.Equals(x.Name, kvp.Key, StringComparison.OrdinalIgnoreCase));

                if (rm != null)
                {
                    Materials.Add(new WorkOrderMaterial
                    {
                        ItemId = rm.Id,
                        Name = rm.Name,
                        Unit = rm.Unit,
                        Qty = Math.Round(kvp.Value, 2)
                    });
                }
            }

            if (showMessage)
            {
                MessageBox.Show($"Connected {Materials.Count} raw material(s) based on recipe formula! You can review and adjust quantities.", "Recipe Applied", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            var validItems = Items.Where(x => (!string.IsNullOrEmpty(x.ProductId) || !string.IsNullOrEmpty(x.ProductName)) && x.Qty > 0).ToList();
            if (!validItems.Any())
            {
                MessageBox.Show("Please add at least one product to make.", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            foreach (var itm in validItems)
            {
                if (string.IsNullOrEmpty(itm.ProductId))
                {
                    var match = _dataService.Products.FirstOrDefault(p => string.Equals(p.Name, itm.ProductName, StringComparison.OrdinalIgnoreCase));
                    itm.ProductId = match?.Id ?? ("p_" + Guid.NewGuid().ToString("N")[..8]);
                }
            }

            string orderType = (CbOrderType.SelectedItem as ComboBoxItem)?.Tag?.ToString()
                ?? (!string.IsNullOrWhiteSpace(CbOrderType.Text) ? CbOrderType.Text.Trim() : "general");
            string customer = TxtCustomer.Text.Trim();
            if (orderType != "general" && string.IsNullOrWhiteSpace(customer))
            {
                MessageBox.Show("Please enter the customer name for domestic/export orders.", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var validMaterials = Materials.Where(m => (!string.IsNullOrEmpty(m.ItemId) || !string.IsNullOrEmpty(m.Name)) && m.Qty > 0).ToList();

            // Verify stock
            if (!_isEdit || _existingOrder?.Status == "Pending")
            {
                foreach (var m in validMaterials)
                {
                    var rm = AvailableRawMaterials.FirstOrDefault(x => x.Id == m.ItemId || x.Name.Equals(m.Name, StringComparison.OrdinalIgnoreCase));
                    if (rm != null && rm.Stock < m.Qty)
                    {
                        MessageBox.Show($"Not enough {rm.Name} in stock. Available: {rm.Stock} {rm.Unit}, Needed: {m.Qty} {rm.Unit}.", "Raw Material Shortage", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                // Deduct material
                foreach (var m in validMaterials)
                {
                    var rm = AvailableRawMaterials.FirstOrDefault(x => x.Id == m.ItemId || x.Name.Equals(m.Name, StringComparison.OrdinalIgnoreCase));
                    if (rm != null) rm.Stock = Math.Max(0, rm.Stock - m.Qty);
                }
            }

            string status = (CbStatus.SelectedItem as ComboBoxItem)?.Content?.ToString()
                ?? (!string.IsNullOrWhiteSpace(CbStatus.Text) ? CbStatus.Text.Trim() : "Pending");
            string orderNo = _isEdit ? _existingOrder!.OrderNo : _dataService.NextDocNo("wo", increment: true);
            string id = _isEdit ? _existingOrder!.Id : "w_" + Guid.NewGuid().ToString("N")[..8];

            var wo = new WorkOrderDoc
            {
                Id = id,
                OrderNo = orderNo,
                Customer = customer,
                OrderType = orderType,
                OrderDate = DpOrderDate.SelectedDate?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd"),
                DueDate = DpDueDate.SelectedDate?.ToString("yyyy-MM-dd") ?? DateTime.Now.AddDays(7).ToString("yyyy-MM-dd"),
                StartDate = DpStartDate.SelectedDate?.ToString("yyyy-MM-dd") ?? (status != "Pending" ? DateTime.Now.ToString("yyyy-MM-dd") : ""),
                EndDate = DpEndDate.SelectedDate?.ToString("yyyy-MM-dd") ?? ((status == "Completed" || status == "Completed This Month") ? DateTime.Now.ToString("yyyy-MM-dd") : ""),
                Status = status,
                Notes = TxtNotes.Text.Trim(),
                Items = validItems,
                Materials = validMaterials,
                IsDone = status == "Completed" || status == "Completed This Month"
            };

            await _dataService.SaveWorkOrderAsync(wo, !_isEdit);
            DialogResult = true;
            Close();
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (_existingOrder == null) return;
            var res = MessageBox.Show(
                $"Are you sure you want to permanently delete Work Order '{_existingOrder.OrderNo}'?",
                "Delete Work Order",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (res == MessageBoxResult.Yes)
            {
                await _dataService.DeleteWorkOrderAsync(_existingOrder.Id);
                DialogResult = true;
                Close();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
