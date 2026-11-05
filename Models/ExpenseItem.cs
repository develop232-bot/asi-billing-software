using System;
using Google.Cloud.Firestore;

namespace AsiErp.Models
{
    [FirestoreData]
    public class ExpenseItem
    {
        [FirestoreProperty("id")]
        public string Id { get; set; } = string.Empty;

        [FirestoreProperty("date")]
        public string Date { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");

        [FirestoreProperty("cat")]
        public string Category { get; set; } = "Everyday Exp";

        [FirestoreProperty("freq")]
        public string Frequency { get; set; } = "Daily"; // Daily, Monthly, Yearly, Purchase

        [FirestoreProperty("desc")]
        public string Description { get; set; } = string.Empty;

        [FirestoreProperty("amt")]
        public double Amount { get; set; }

        [FirestoreProperty("mode")]
        public string PaymentMode { get; set; } = "Cash";

        [FirestoreProperty("auto")]
        public bool IsAuto { get; set; } // true for purchase bills
    }
}
