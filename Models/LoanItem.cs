using System;
using System.Collections.Generic;
using System.Linq;
using Google.Cloud.Firestore;

namespace AsiErp.Models
{
    [FirestoreData]
    public class LoanRepayment
    {
        [FirestoreProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [FirestoreProperty("date")]
        public string Date { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");

        [FirestoreProperty("p")]
        public double Principal { get; set; }

        [FirestoreProperty("i")]
        public double Interest { get; set; }
    }

    [FirestoreData]
    public class LoanItem
    {
        [FirestoreProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [FirestoreProperty("lender")]
        public string Lender { get; set; } = string.Empty;

        [FirestoreProperty("date")]
        public string Date { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");

        [FirestoreProperty("amt")]
        public double Amount { get; set; }

        [FirestoreProperty("rate")]
        public double InterestRate { get; set; } = 10.0;

        [FirestoreProperty("note")]
        public string Note { get; set; } = string.Empty;

        [FirestoreProperty("pays")]
        public List<LoanRepayment> Repayments { get; set; } = new();

        public double TotalPrincipalPaid => Repayments.Sum(r => r.Principal);
        public double TotalInterestPaid => Repayments.Sum(r => r.Interest);
        public double BalanceRemaining => Math.Max(0, Amount - TotalPrincipalPaid);
        public bool IsOpen => BalanceRemaining > 0.5;
        public string Status => IsOpen ? "Open" : "Closed";
        public string StatusBrush => IsOpen ? "#D97706" : "#10B981";
        public string StatusBackground => IsOpen ? "#FEF3C7" : "#D1FAE5";

        public double RepaidPercent
        {
            get => Amount > 0 ? Math.Min(100.0, Math.Round((TotalPrincipalPaid / Amount) * 100.0, 1)) : 0.0;
            set { }
        }

        public string RepaidSummaryText
        {
            get
            {
                var culture = System.Globalization.CultureInfo.GetCultureInfo("en-IN");
                return string.Format(culture, "{0:0.#}% repaid of ₹{1:N0}", RepaidPercent, Amount);
            }
        }

        public string LastPaymentInfoText
        {
            get
            {
                var culture = System.Globalization.CultureInfo.GetCultureInfo("en-IN");
                if (Repayments == null || Repayments.Count == 0)
                {
                    return "Last Payment: None | ₹0 (Principal: ₹0, Interest: ₹0)";
                }
                var last = Repayments.Last();
                string dateStr = last.Date;
                if (DateTime.TryParse(last.Date, out var dt))
                {
                    dateStr = dt.ToString("dd-MM-yyyy");
                }
                double total = last.Principal + last.Interest;
                return string.Format(culture, "Last Payment: {0} | ₹{1:N0} (Principal: ₹{2:N0}, Interest: ₹{3:N0})",
                    dateStr, total, last.Principal, last.Interest);
            }
        }
    }
}
