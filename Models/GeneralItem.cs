using Google.Cloud.Firestore;

namespace AsiErp.Models
{
    [FirestoreData]
    public class GeneralItem
    {
        [FirestoreProperty("id")]
        public string Id { get; set; } = string.Empty;

        [FirestoreProperty("createdAt")]
        public string CreatedAt { get; set; } = System.DateTime.Now.ToString("yyyy-MM-dd");

        private string _sku = string.Empty;

        [FirestoreProperty("sku")]
        public string Sku
        {
            get => !string.IsNullOrWhiteSpace(_sku) ? _sku : (!string.IsNullOrWhiteSpace(Id) ? $"ASI-PKG-{Id.Replace("g", "").PadLeft(3, '0')}" : string.Empty);
            set => _sku = value;
        }

        [FirestoreProperty("name")]
        public string Name { get; set; } = string.Empty;

        [FirestoreProperty("unit")]
        public string Unit { get; set; } = "pcs";

        [FirestoreProperty("stock")]
        public double Stock { get; set; }

        [FirestoreProperty("reorder")]
        public double ReorderLevel { get; set; }

        [FirestoreProperty("rate")]
        public double Rate { get; set; }

        [FirestoreProperty("gst")]
        public double Gst { get; set; } = 5;

        public double StockValue { get => Stock * Rate; set { } }

        public string StatusLevel
        {
            get
            {
                double target = System.Math.Max(ReorderLevel, 1) * 2.0;
                double pct = (Stock / target) * 100;
                if (Stock < 10 || pct < 10) return "out";
                if (Stock <= ReorderLevel || pct < 50) return "low";
                return "ok";
            }
            set { }
        }

        public string StatusLevelText
        {
            get
            {
                double target = System.Math.Max(ReorderLevel, 1) * 2.0;
                double pct = (Stock / target) * 100;
                if (Stock <= 0) return "Out";
                if (Stock < 10 || pct < 10) return "Low";
                if (Stock <= ReorderLevel || pct < 50) return "Medium";
                return "In stock";
            }
            set { }
        }
    }
}
