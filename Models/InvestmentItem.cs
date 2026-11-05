using System;
using Google.Cloud.Firestore;

namespace AsiErp.Models
{
    [FirestoreData]
    public class InvestmentItem
    {
        [FirestoreProperty("id")]
        public string Id { get; set; } = string.Empty;

        [FirestoreProperty("date")]
        public string Date { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");

        [FirestoreProperty("by")]
        public string InvestedBy { get; set; } = string.Empty;

        [FirestoreProperty("purpose")]
        public string Purpose { get; set; } = string.Empty;

        [FirestoreProperty("amt")]
        public double Amount { get; set; }

        [FirestoreProperty("pre")]
        public bool IsPreStart { get; set; } // true if made before sales started
    }

    public class AppNotification
    {
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string RouteKey { get; set; } = string.Empty;
        public string BadgeColor { get; set; } = "#3D62B8";
    }
}
