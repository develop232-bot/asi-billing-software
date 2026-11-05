using System;
using System.Collections.Generic;
using Google.Cloud.Firestore;

namespace AsiErp.Models
{
    [FirestoreData]
    public class SaleDoc
    {
        [FirestoreProperty("id")]
        public string Id { get; set; } = string.Empty;

        [FirestoreProperty("type")]
        public string Type { get; set; } = "domestic"; // retail, domestic, export

        [FirestoreProperty("no")]
        public string InvoiceNo { get; set; } = string.Empty;

        [FirestoreProperty("date")]
        public string Date { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");

        [FirestoreProperty("party")]
        public string Party { get; set; } = string.Empty;

        [FirestoreProperty("person")]
        public string ContactPerson { get; set; } = string.Empty;

        [FirestoreProperty("phone")]
        public string Phone { get; set; } = string.Empty;

        [FirestoreProperty("saleType")]
        public string SaleType { get; set; } = "Cash"; // Cash, Credit

        [FirestoreProperty("creditDays")]
        public int CreditDays { get; set; }

        [FirestoreProperty("paymentDue")]
        public string PaymentDueDate { get; set; } = string.Empty;

        [FirestoreProperty("event")]
        public string Event { get; set; } = string.Empty;

        [FirestoreProperty("mode")]
        public string PaymentMode { get; set; } = "Cash"; // Cash, UPI, Card

        [FirestoreProperty("cur")]
        public string Currency { get; set; } = "INR";

        [FirestoreProperty("fx")]
        public double FxRate { get; set; } = 1.0;

        [FirestoreProperty("disc")]
        public double DiscountPercent { get; set; }

        [FirestoreProperty("net")]
        public double NetAmount { get; set; }

        [FirestoreProperty("tax")]
        public double TaxAmount { get; set; }

        [FirestoreProperty("gross")]
        public double GrossAmount { get; set; }

        [FirestoreProperty("grossCur")]
        public double GrossAmountCurrency { get; set; }

        [FirestoreProperty("paid")]
        public double PaidAmount { get; set; }

        [FirestoreProperty("lines")]
        public List<SaleLine> Lines { get; set; } = new();

        // Metadata
        [FirestoreProperty("gstin")]
        public string CustomerGstin { get; set; } = string.Empty;

        [FirestoreProperty("taxType")]
        public string TaxType { get; set; } = "intra"; // intra, inter

        [FirestoreProperty("vehicle")]
        public string VehicleNo { get; set; } = string.Empty;

        [FirestoreProperty("country")]
        public string Country { get; set; } = string.Empty;

        [FirestoreProperty("incoterm")]
        public string Incoterm { get; set; } = "FOB";

        [FirestoreProperty("pol")]
        public string PortOfLoading { get; set; } = "Chennai";

        [FirestoreProperty("pod")]
        public string PortOfDischarge { get; set; } = string.Empty;

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

        public string ChannelLabel => Type switch
        {
            "retail" => "Retail",
            "domestic" => "Domestic",
            "export" => "Export",
            _ => Type
        };

        public string ChannelPillColor => Type switch
        {
            "retail" => "#E5A62E",
            "domestic" => "#3D62B8",
            "export" => "#23968A",
            _ => "#78839A"
        };
    }
}
