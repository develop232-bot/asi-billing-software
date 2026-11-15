using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Google.Cloud.Firestore;

namespace AsiErp.Models
{
    [FirestoreData]
    public class LoanRepayment : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? prop = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private string _id = Guid.NewGuid().ToString("N");
        private string _date = DateTime.Now.ToString("yyyy-MM-dd");
        private double _principal;
        private double _interest;

        [FirestoreProperty("id")]
        public string Id
        {
            get => _id;
            set { if (_id != value) { _id = value; OnPropertyChanged(); } }
        }

        [FirestoreProperty("date")]
        public string Date
        {
            get => _date;
            set { if (_date != value) { _date = value; OnPropertyChanged(); } }
        }

        [FirestoreProperty("p")]
        public double Principal
        {
            get => _principal;
            set
            {
                if (Math.Abs(_principal - value) > 0.0001)
                {
                    _principal = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TotalPayment));
                }
            }
        }

        [FirestoreProperty("i")]
        public double Interest
        {
            get => _interest;
            set
            {
                if (Math.Abs(_interest - value) > 0.0001)
                {
                    _interest = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TotalPayment));
                }
            }
        }

        public double TotalPayment => Principal + Interest;
    }

    [FirestoreData]
    public class LoanItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string? prop = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private string _id = Guid.NewGuid().ToString("N");
        private string _lender = string.Empty;
        private string _date = DateTime.Now.ToString("yyyy-MM-dd");
        private double _amount;
        private double _interestRate = 10.0;
        private string _note = string.Empty;
        private List<LoanRepayment> _repayments = new();
        private bool _isManuallyClosed;

        [FirestoreProperty("id")]
        public string Id
        {
            get => _id;
            set { if (_id != value) { _id = value; OnPropertyChanged(); } }
        }

        [FirestoreProperty("lender")]
        public string Lender
        {
            get => _lender;
            set { if (_lender != value) { _lender = value; OnPropertyChanged(); } }
        }

        [FirestoreProperty("date")]
        public string Date
        {
            get => _date;
            set { if (_date != value) { _date = value; OnPropertyChanged(); } }
        }

        [FirestoreProperty("amt")]
        public double Amount
        {
            get => _amount;
            set
            {
                if (Math.Abs(_amount - value) > 0.0001)
                {
                    _amount = value;
                    OnPropertyChanged();
                    NotifyCalculatedProperties();
                }
            }
        }

        [FirestoreProperty("rate")]
        public double InterestRate
        {
            get => _interestRate;
            set
            {
                if (Math.Abs(_interestRate - value) > 0.0001)
                {
                    _interestRate = value;
                    OnPropertyChanged();
                }
            }
        }

        [FirestoreProperty("note")]
        public string Note
        {
            get => _note;
            set { if (_note != value) { _note = value; OnPropertyChanged(); } }
        }

        [FirestoreProperty("pays")]
        public List<LoanRepayment> Repayments
        {
            get => _repayments;
            set
            {
                _repayments = value ?? new();
                OnPropertyChanged();
                NotifyCalculatedProperties();
            }
        }

        [FirestoreProperty("closed")]
        public bool IsManuallyClosed
        {
            get => _isManuallyClosed;
            set
            {
                if (_isManuallyClosed != value)
                {
                    _isManuallyClosed = value;
                    OnPropertyChanged();
                    NotifyCalculatedProperties();
                }
            }
        }

        public double TotalPrincipalPaid => Repayments?.Sum(r => r.Principal) ?? 0;
        public double TotalInterestPaid => Repayments?.Sum(r => r.Interest) ?? 0;
        public double BalanceRemaining => IsManuallyClosed ? 0 : Math.Max(0, Amount - TotalPrincipalPaid);
        public bool IsOpen => !IsManuallyClosed && BalanceRemaining > 0.5;
        public string Status => IsOpen ? "Open" : "Closed";
        public string StatusBrush => IsOpen ? "#D97706" : "#10B981";
        public string StatusBackground => IsOpen ? "#FEF3C7" : "#D1FAE5";

        public double RepaidPercent
        {
            get => Amount > 0 ? Math.Min(100.0, Math.Round((TotalPrincipalPaid / Amount) * 100.0, 1)) : (IsManuallyClosed ? 100.0 : 0.0);
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

        public void NotifyCalculatedProperties()
        {
            OnPropertyChanged(nameof(TotalPrincipalPaid));
            OnPropertyChanged(nameof(TotalInterestPaid));
            OnPropertyChanged(nameof(BalanceRemaining));
            OnPropertyChanged(nameof(IsOpen));
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(StatusBrush));
            OnPropertyChanged(nameof(StatusBackground));
            OnPropertyChanged(nameof(RepaidPercent));
            OnPropertyChanged(nameof(RepaidSummaryText));
            OnPropertyChanged(nameof(LastPaymentInfoText));
        }
    }
}
