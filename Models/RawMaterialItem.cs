using Google.Cloud.Firestore;

namespace AsiErp.Models
{
    [FirestoreData]
    public class RawMaterialItem
    {
        [FirestoreProperty("id")]
        public string Id { get; set; } = string.Empty;

        [FirestoreProperty("name")]
        public string Name { get; set; } = string.Empty;

        [FirestoreProperty("unit")]
        public string Unit { get; set; } = "sq ft";

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
                if (Stock <= 0) return "out";
                if (Stock <= ReorderLevel) return "low";
                return "ok";
            }
            set { }
        }

        public string StatusLevelText
        {
            get
            {
                if (Stock <= 0) return "Out";
                if (Stock <= ReorderLevel) return "Low";
                return "In stock";
            }
            set { }
        }
    }
}
