using System;
using Google.Cloud.Firestore;

namespace AsiErp.Models
{
    [FirestoreData]
    public class ExpenseItem
    {
        [FirestoreProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [FirestoreProperty("date")]
        public string Date { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");

        [FirestoreProperty("group")]
        public string Group { get; set; } = "Employee"; // Employee, Utility, Misc Exp, Purchase

        [FirestoreProperty("cat")]
        public string Category { get; set; } = "Salary"; // Type: Salary, OT, Rent, EB, Tea, etc.

        [FirestoreProperty("emp")]
        public string EmployeeName { get; set; } = string.Empty;

        [FirestoreProperty("pos")]
        public string Position { get; set; } = string.Empty;

        [FirestoreProperty("freq")]
        public string Frequency { get; set; } = "Monthly";

        [FirestoreProperty("desc")]
        public string Description { get; set; } = string.Empty;

        [FirestoreProperty("amt")]
        public double Amount { get; set; }

        [FirestoreProperty("mode")]
        public string PaymentMode { get; set; } = "Cash"; // Cash, Bank, UPI

        [FirestoreProperty("auto")]
        public bool IsAuto { get; set; } // true for purchase bills

        // UI formatting properties
        public string GroupDisplay => string.IsNullOrEmpty(Group) ? "Misc Exp" : Group;

        public string GroupPillBrush => GroupDisplay switch
        {
            "Employee" => "#2563EB",
            "Utility" => "#7C3AED",
            "Misc Exp" => "#D97706",
            "Purchase" => "#334155",
            _ => "#64748B"
        };

        public string GroupPillBackground => GroupDisplay switch
        {
            "Employee" => "#EFF6FF",
            "Utility" => "#F5F3FF",
            "Misc Exp" => "#FEF3C7",
            "Purchase" => "#F1F5F9",
            _ => "#F1F5F9"
        };

        public bool HasEmployeeInfo => !string.IsNullOrWhiteSpace(EmployeeName);
        public string EmployeeSubtitle => HasEmployeeInfo ? $"{EmployeeName}{(string.IsNullOrWhiteSpace(Position) ? "" : " · " + Position)}" : string.Empty;
        public string DisplayDetails => !string.IsNullOrWhiteSpace(Description) ? Description : "—";
    }
}
