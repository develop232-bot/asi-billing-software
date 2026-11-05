using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Google.Cloud.Firestore;

namespace AsiErp.Models
{
    [FirestoreData]
    public class PurchaseLine : INotifyPropertyChanged
    {
        private string _itemId = string.Empty;
        private string _name = string.Empty;
        private double _qty = 1;
        private string _unit = "pcs";
        private double _rate;
        private double _gstPercent = 5;
        private double _netAmount;
        private double _taxAmount;
        private double _grossAmount;

        [FirestoreProperty("id")]
        public string ItemId
        {
            get => _itemId;
            set { if (_itemId != value) { _itemId = value; OnPropertyChanged(); } }
        }

        [FirestoreProperty("name")]
        public string Name
        {
            get => _name;
            set { if (_name != value) { _name = value; OnPropertyChanged(); } }
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
                    RecalculateLine();
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
                    RecalculateLine();
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
                    RecalculateLine();
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

        public double LineTotal { get => NetAmount; set { } }

        public void RecalculateLine()
        {
            double net = Math.Round(_qty * _rate, 2);
            double tax = Math.Round(net * (_gstPercent / 100.0), 2);
            double gross = Math.Round(net + tax, 2);

            _netAmount = net;
            _taxAmount = tax;
            _grossAmount = gross;

            OnPropertyChanged(nameof(NetAmount));
            OnPropertyChanged(nameof(TaxAmount));
            OnPropertyChanged(nameof(GrossAmount));
            OnPropertyChanged(nameof(LineTotal));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
