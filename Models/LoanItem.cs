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
    }
}
