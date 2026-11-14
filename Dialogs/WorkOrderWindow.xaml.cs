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

            ManufacturedProducts = _dataService.Products.Where(p => p.IsManufactured).ToList();
            AvailableRawMaterials = _dataService.RawMaterials.ToList();

            DgItems.ItemsSource = Items;
            DgMaterials.ItemsSource = Materials;

            if (_isEdit && _existingOrder != null)
            {
                TxtTitle.Text = $"Edit Work Order {_existingOrder.OrderNo}";
                TxtOrderNo.Text = _existingOrder.OrderNo;
                TxtCustomer.Text = _existingOrder.Customer;

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

                string normalizedStatus = _existingOrder.Status switch
                {
                    "Cutting" => "Started",
                    "Stitching" or "Lasting & pressing" or "Finishing" or "Packing" => "In Progress",
                    _ => _existingOrder.Status
                };

                foreach (ComboBoxItem itm in CbStatus.Items)
                {
                    if (string.Equals(itm.Content?.ToString(), normalizedStatus, StringComparison.OrdinalIgnoreCase))
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

        private void AddItem()
        {
            var p = ManufacturedProducts.FirstOrDefault();
            if (p != null)
            {
                Items.Add(new WorkOrderItem
                {
                    ProductId = p.Id,
                    ProductName = p.Name,
                    Size = p.Sizes?.FirstOrDefault() ?? "",
                    Unit = p.Unit,
                    Qty = 100
                });
            }
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
            if (sender is ComboBox cb && cb.DataContext is WorkOrderItem item && cb.SelectedValue is string pid)
            {
                var p = ManufacturedProducts.FirstOrDefault(x => x.Id == pid);
                if (p != null)
                {
                    item.ProductName = p.Name;
                    item.Unit = p.Unit;
                    item.Size = p.Sizes?.FirstOrDefault() ?? "";
                }
            }
        }

        private void AddMat_Click(object sender, RoutedEventArgs e)
        {
            var rm = AvailableRawMaterials.FirstOrDefault();
            if (rm != null)
            {
                Materials.Add(new WorkOrderMaterial
                {
                    ItemId = rm.Id,
                    Name = rm.Name,
                    Unit = rm.Unit,
                    Qty = 10
                });
            }
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

        private void SuggestRecipe_Click(object sender, RoutedEventArgs e)
        {
            var need = new Dictionary<string, double>();

            foreach (var item in Items.Where(x => !string.IsNullOrEmpty(x.ProductId) && x.Qty > 0))
            {
                var prod = ManufacturedProducts.FirstOrDefault(x => x.Id == item.ProductId);
                if (prod?.Bom != null)
                {
                    foreach (var b in prod.Bom)
                    {
                        need.TryGetValue(b.Id, out double cur);
                        need[b.Id] = cur + (b.Qty * item.Qty);
                    }
                }
            }

            if (!need.Any())
            {
                MessageBox.Show("No recipe found for the selected products. Add BOM recipe under Products first.", "Recipe Suggestion", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Materials.Clear();
            foreach (var kvp in need)
            {
                var rm = AvailableRawMaterials.FirstOrDefault(x => x.Id == kvp.Key);
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

            MessageBox.Show("Raw materials suggested based on BOM recipe! Please review and adjust as needed.", "Recipe Applied", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            var validItems = Items.Where(x => !string.IsNullOrEmpty(x.ProductId) && x.Qty > 0).ToList();
            if (!validItems.Any())
            {
                MessageBox.Show("Please add at least one product to make.", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string orderType = (CbOrderType.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "general";
            string customer = TxtCustomer.Text.Trim();
            if (orderType != "general" && string.IsNullOrWhiteSpace(customer))
            {
                MessageBox.Show("Please enter the customer name for domestic/export orders.", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var validMaterials = Materials.Where(m => !string.IsNullOrEmpty(m.ItemId) && m.Qty > 0).ToList();

            // Verify stock
            if (!_isEdit || _existingOrder?.Status == "Pending")
            {
                foreach (var m in validMaterials)
                {
                    var rm = AvailableRawMaterials.FirstOrDefault(x => x.Id == m.ItemId);
                    if (rm != null && rm.Stock < m.Qty)
                    {
                        MessageBox.Show($"Not enough {rm.Name} in stock. Available: {rm.Stock} {rm.Unit}, Needed: {m.Qty} {rm.Unit}.", "Raw Material Shortage", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                // Deduct material
                foreach (var m in validMaterials)
                {
                    var rm = AvailableRawMaterials.FirstOrDefault(x => x.Id == m.ItemId);
                    if (rm != null) rm.Stock = Math.Max(0, rm.Stock - m.Qty);
                }
            }

            string status = (CbStatus.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Pending";
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
                StartDate = status != "Pending" ? DateTime.Now.ToString("yyyy-MM-dd") : "",
                EndDate = status == "Completed" ? DateTime.Now.ToString("yyyy-MM-dd") : "",
                Status = status,
                Items = validItems,
                Materials = validMaterials,
                IsDone = status == "Completed"
            };

            await _dataService.SaveWorkOrderAsync(wo, !_isEdit);
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
