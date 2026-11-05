using Google.Cloud.Firestore;

namespace AsiErp.Models
{
    [FirestoreData]
    public class SaleLine
    {
        [FirestoreProperty("pid")]
        public string Pid { get; set; } = string.Empty;

        [FirestoreProperty("name")]
        public string Name { get; set; } = string.Empty;

        [FirestoreProperty("hsn")]
        public string Hsn { get; set; } = string.Empty;

        [FirestoreProperty("size")]
        public string Size { get; set; } = string.Empty;

        [FirestoreProperty("q")]
        public double Qty { get; set; } = 1;

        [FirestoreProperty("unit")]
        public string Unit { get; set; } = "pair";

        [FirestoreProperty("rate")]
        public double Rate { get; set; }

        [FirestoreProperty("disc")]
        public double DiscPercent { get; set; }

        [FirestoreProperty("gst")]
        public double GstPercent { get; set; } = 5;

        [FirestoreProperty("net")]
        public double NetAmount { get; set; }

        [FirestoreProperty("tax")]
        public double TaxAmount { get; set; }

        [FirestoreProperty("gross")]
        public double GrossAmount { get; set; }

        public double LineTotal => Qty * Rate * (1.0 - DiscPercent / 100.0);
    }
}
