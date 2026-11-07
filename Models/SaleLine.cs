using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Google.Cloud.Firestore;

namespace AsiErp.Models
{
    [FirestoreData]
    public class SaleLine : INotifyPropertyChanged
    {
        private string _pid = string.Empty;
        private string _name = string.Empty;
        private string _hsn = string.Empty;
        private string _size = string.Empty;
        private double _qty = 1;
        private string _unit = "pair";
        private double _rate;
        private double _discPercent;
        private double _gstPercent = 5;
        private double _netAmount;
        private double _taxAmount;
        private double _grossAmount;

        [FirestoreProperty("pid")]
        public string Pid
        {
            get => _pid;
            set { if (_pid != value) { _pid = value; OnPropertyChanged(); } }
        }

        [FirestoreProperty("name")]
        public string Name
        {
            get => _name;
            set { if (_name != value) { _name = value; OnPropertyChanged(); } }
        }

        [FirestoreProperty("hsn")]
        public string Hsn
        {
            get => _hsn;
            set { if (_hsn != value) { _hsn = value; OnPropertyChanged(); } }
        }

        [FirestoreProperty("size")]
        public string Size
        {
            get => _size;
            set { if (_size != value) { _size = value; OnPropertyChanged(); } }
        }

        [FirestoreProperty("q")]
        public double Qty
        {
            get => _qty;
            set
            {
                if (Math.Abs(_qty - value) > 0.0001)
                {
                    _qty = value;
                    OnPropertyChanged();
                }
            }
        }

        [FirestoreProperty("unit")]
        public string Unit
        {
            get => _unit;
            set { if (_unit != value) { _unit = value; OnPropertyChanged(); } }
        }

        [FirestoreProperty("rate")]
        public double Rate
        {
            get => _rate;
            set
            {
                if (Math.Abs(_rate - value) > 0.0001)
                {
                    _rate = value;
                    OnPropertyChanged();
                }
            }
        }

        [FirestoreProperty("disc")]
        public double DiscPercent
        {
            get => _discPercent;
            set
            {
                if (Math.Abs(_discPercent - value) > 0.0001)
                {
                    _discPercent = value;
                    OnPropertyChanged();
                }
            }
        }

        [FirestoreProperty("gst")]
        public double GstPercent
        {
            get => _gstPercent;
            set
            {
                if (Math.Abs(_gstPercent - value) > 0.0001)
                {
                    _gstPercent = value;
                    OnPropertyChanged();
                }
            }
        }

        [FirestoreProperty("net")]
        public double NetAmount
        {
            get => _netAmount;
            set { if (Math.Abs(_netAmount - value) > 0.0001) { _netAmount = value; OnPropertyChanged(); } }
        }

        [FirestoreProperty("tax")]
        public double TaxAmount
        {
            get => _taxAmount;
            set { if (Math.Abs(_taxAmount - value) > 0.0001) { _taxAmount = value; OnPropertyChanged(); } }
        }

        [FirestoreProperty("gross")]
        public double GrossAmount
        {
            get => _grossAmount;
            set { if (Math.Abs(_grossAmount - value) > 0.0001) { _grossAmount = value; OnPropertyChanged(); } }
        }

        public double LineTotal => NetAmount;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
