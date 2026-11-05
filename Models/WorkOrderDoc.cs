using System;
using System.Collections.Generic;
using System.Linq;
using Google.Cloud.Firestore;

namespace AsiErp.Models
{
    [FirestoreData]
    public class WorkOrderMaterial
    {
        [FirestoreProperty("id")]
        public string ItemId { get; set; } = string.Empty;

        [FirestoreProperty("name")]
        public string Name { get; set; } = string.Empty;

        [FirestoreProperty("unit")]
        public string Unit { get; set; } = "sq ft";

        [FirestoreProperty("qty")]
        public double Qty { get; set; } = 1;
    }

    [FirestoreData]
    public class WorkOrderItem
    {
        [FirestoreProperty("pid")]
        public string ProductId { get; set; } = string.Empty;

        [FirestoreProperty("name")]
        public string ProductName { get; set; } = string.Empty;

        [FirestoreProperty("size")]
        public string Size { get; set; } = string.Empty;

        [FirestoreProperty("unit")]
        public string Unit { get; set; } = "pair";

        [FirestoreProperty("qty")]
        public double Qty { get; set; } = 1;
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
