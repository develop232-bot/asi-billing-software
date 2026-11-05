using System.Windows;
using AsiErp.Models;
using AsiErp.Services;

namespace AsiErp.Dialogs
{
    public partial class PaymentWindow : Window
    {
        private readonly string _collection;
        private readonly string _id;
        private readonly double _balanceDue;
        private readonly AppDataService _dataService = AppDataService.Instance;

        public PaymentWindow(string collection, object doc)
        {
            InitializeComponent();
            _collection = collection;
            _dataService = AppDataService.Instance;

            if (doc is SaleDoc sale)
            {
                _id = sale.Id;
                _balanceDue = sale.BalanceAmount;
                TxtDocHeader.Text = $"Receive Payment · {sale.InvoiceNo} ({sale.Party})";
            }
            else if (doc is PurchaseDoc purchase)
            {
                _id = purchase.Id;
                _balanceDue = purchase.BalanceAmount;
                TxtDocHeader.Text = $"Pay Vendor · {purchase.BillNo} ({purchase.Party})";
            }
            else
            {
                _id = string.Empty;
                _balanceDue = 0;
            }

            TxtBalanceDue.Text = $"Balance Due: ₹{_balanceDue:N2}";
            TxtAmount.Text = _balanceDue.ToString("F2");
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(TxtAmount.Text, out double amt) || amt <= 0)
            {
                MessageBox.Show("Please enter a valid payment amount.", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await _dataService.RecordPaymentAsync(_collection, _id, amt);
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
