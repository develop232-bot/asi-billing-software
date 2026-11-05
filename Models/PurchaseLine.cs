using Google.Cloud.Firestore;

namespace AsiErp.Models
{
    [FirestoreData]
    public class PurchaseLine
    {
        [FirestoreProperty("id")]
        public string ItemId { get; set; } = string.Empty;

        [FirestoreProperty("name")]
        public string Name { get; set; } = string.Empty;

        [FirestoreProperty("q")]
        public double Qty { get; set; } = 1;

        [FirestoreProperty("unit")]
        public string Unit { get; set; } = "pcs";

        [FirestoreProperty("rate")]
        public double Rate { get; set; }

        [FirestoreProperty("gst")]
        public double GstPercent { get; set; } = 5;

        [FirestoreProperty("net")]
        public double NetAmount { get; set; }

        [FirestoreProperty("tax")]
        public double TaxAmount { get; set; }

        [FirestoreProperty("gross")]
        public double GrossAmount { get; set; }

        public double LineTotal { get => Qty * Rate; set { } }
    }
}
