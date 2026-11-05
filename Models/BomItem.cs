using Google.Cloud.Firestore;

namespace AsiErp.Models
{
    [FirestoreData]
    public class BomItem
    {
        [FirestoreProperty("id")]
        public string Id { get; set; } = string.Empty;

        [FirestoreProperty("name")]
        public string Name { get; set; } = string.Empty;

        [FirestoreProperty("unit")]
        public string Unit { get; set; } = string.Empty;

        [FirestoreProperty("q")]
        public double Qty { get; set; }
    }
}
