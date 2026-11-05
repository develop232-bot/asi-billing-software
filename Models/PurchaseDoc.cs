using System;
using System.Collections.Generic;
using Google.Cloud.Firestore;

namespace AsiErp.Models
{
    [FirestoreData]
    public class PurchaseDoc
    {
        [FirestoreProperty("id")]
        public string Id { get; set; } = string.Empty;

        [FirestoreProperty("kind")]
        public string Kind { get; set; } = "raw"; // raw, ready, gen

        [FirestoreProperty("no")]
        public string BillNo { get; set; } = string.Empty;

        [FirestoreProperty("ref")]
        public string VendorBillRef { get; set; } = string.Empty;

        [FirestoreProperty("date")]
        public string Date { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");

        [FirestoreProperty("party")]
        public string Party { get; set; } = string.Empty;

        [FirestoreProperty("phone")]
        public string Phone { get; set; } = string.Empty;

        [FirestoreProperty("saleType")]
        public string SaleType { get; set; } = "Cash"; // Cash, Credit

        [FirestoreProperty("creditDays")]
        public int CreditDays { get; set; }

        [FirestoreProperty("paymentDue")]
        public string PaymentDueDate { get; set; } = string.Empty;

        [FirestoreProperty("net")]
        public double NetAmount { get; set; }

        [FirestoreProperty("tax")]
        public double TaxAmount { get; set; }

        [FirestoreProperty("gross")]
        public double GrossAmount { get; set; }

        [FirestoreProperty("paid")]
        public double PaidAmount { get; set; }

        [FirestoreProperty("lines")]
        public List<PurchaseLine> Lines { get; set; } = new();

        public double BalanceAmount => Math.Max(0, Math.Round(GrossAmount - PaidAmount, 2));

        public string Status
        {
            get
            {
                if (PaidAmount >= GrossAmount - 0.5) return "Paid";
                if (PaidAmount <= 0.05) return "Unpaid";
                return "Partial";
            }
        }

        public string KindLabel => Kind switch
        {
            "raw" => "Raw Material",
            "ready" => "Readymade Product",
            "gen" => "General",
            _ => Kind
        };
    }
}
