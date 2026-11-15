using System;
using System.Collections.Generic;
using System.Linq;
using Google.Cloud.Firestore;

namespace AsiErp.Models
{
    [FirestoreData]
    public class WorkOrderMaterial : System.ComponentModel.INotifyPropertyChanged
    {
        private string _itemId = string.Empty;
        private string _name = string.Empty;
        private string _unit = "sq ft";
        private double _qty = 1;

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string prop) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(prop));

        [FirestoreProperty("id")]
        public string ItemId
        {
            get => _itemId;
            set { if (_itemId != value) { _itemId = value; OnPropertyChanged(nameof(ItemId)); } }
        }

        [FirestoreProperty("name")]
        public string Name
        {
            get => _name;
            set { if (_name != value) { _name = value; OnPropertyChanged(nameof(Name)); } }
        }

        [FirestoreProperty("unit")]
        public string Unit
        {
            get => _unit;
            set { if (_unit != value) { _unit = value; OnPropertyChanged(nameof(Unit)); } }
        }

        [FirestoreProperty("qty")]
        public double Qty
        {
            get => _qty;
            set { if (_qty != value) { _qty = value; OnPropertyChanged(nameof(Qty)); } }
        }
    }

    [FirestoreData]
    public class WorkOrderItem : System.ComponentModel.INotifyPropertyChanged
    {
        private string _productId = string.Empty;
        private string _productName = string.Empty;
        private string _size = string.Empty;
        private string _unit = "pair";
        private double _qty = 1;

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string prop) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(prop));

        [FirestoreProperty("pid")]
        public string ProductId
        {
            get => _productId;
            set { if (_productId != value) { _productId = value; OnPropertyChanged(nameof(ProductId)); } }
        }

        [FirestoreProperty("name")]
        public string ProductName
        {
            get => _productName;
            set { if (_productName != value) { _productName = value; OnPropertyChanged(nameof(ProductName)); } }
        }

        [FirestoreProperty("size")]
        public string Size
        {
            get => _size;
            set { if (_size != value) { _size = value; OnPropertyChanged(nameof(Size)); } }
        }

        [FirestoreProperty("unit")]
        public string Unit
        {
            get => _unit;
            set { if (_unit != value) { _unit = value; OnPropertyChanged(nameof(Unit)); } }
        }

        [FirestoreProperty("qty")]
        public double Qty
        {
            get => _qty;
            set { if (_qty != value) { _qty = value; OnPropertyChanged(nameof(Qty)); } }
        }
    }

    [FirestoreData]
    public class WorkOrderDoc
    {
        [FirestoreProperty("id")]
        public string Id { get; set; } = string.Empty;

        [FirestoreProperty("no")]
        public string OrderNo { get; set; } = string.Empty;

        [FirestoreProperty("customer")]
        public string Customer { get; set; } = string.Empty;

        [FirestoreProperty("orderType")]
        public string OrderType { get; set; } = "domestic"; // general, domestic, export

        [FirestoreProperty("orderDate")]
        public string OrderDate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");

        [FirestoreProperty("due")]
        public string DueDate { get; set; } = DateTime.Now.AddDays(7).ToString("yyyy-MM-dd");

        [FirestoreProperty("start")]
        public string StartDate { get; set; } = string.Empty;

        [FirestoreProperty("end")]
        public string EndDate { get; set; } = string.Empty;

        [FirestoreProperty("status")]
        public string Status { get; set; } = "Pending";
        // Stages: Pending, Cutting, Stitching, Lasting & pressing, Finishing, Packing, Completed

        [FirestoreProperty("notes")]
        public string Notes { get; set; } = string.Empty;

        [FirestoreProperty("materials")]
        public List<WorkOrderMaterial> Materials { get; set; } = new();

        [FirestoreProperty("items")]
        public List<WorkOrderItem> Items { get; set; } = new();

        [FirestoreProperty("done")]
        public bool IsDone { get; set; }

        public bool IsOverdue => !IsDone && Status != "Completed" && string.Compare(DueDate, DateTime.Now.ToString("yyyy-MM-dd")) < 0;

        public string ProductsSummary
        {
            get
            {
                if (Items == null || Items.Count == 0) return "—";
                if (Items.Count == 1) return $"{Items[0].ProductName} ({Items[0].Qty} {Items[0].Unit})";
                return $"{Items.Count} products ({Items.Sum(x => x.Qty)} units)";
            }
        }

        public string OrderTypeLabel => OrderType switch
        {
            "general" => "General / Stock",
            "domestic" => "Domestic",
            "export" => "Export",
            _ => OrderType
        };

        public string StatusColor => Status switch
        {
            "Completed" => "#1B8556",
            "Pending" => "#78839A",
            _ => "#3D62B8"
        };
    }
}
