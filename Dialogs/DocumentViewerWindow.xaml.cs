using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AsiErp.Models;

namespace AsiErp.Dialogs
{
    public class DocViewerLineItem
    {
        public string Name { get; set; } = string.Empty;
        public string Hsn { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public double Qty { get; set; }
        public double Rate { get; set; }
        public double GstPercent { get; set; }
        public double GrossAmount { get; set; }
    }

    public partial class DocumentViewerWindow : Window
    {
        public DocumentViewerWindow(object doc)
        {
            InitializeComponent();

            if (doc is SaleDoc sale)
            {
                TxtDocTypeHeader.Text = sale.Type switch
                {
                    "export" => "EXPORT INVOICE",
                    "domestic" => "TAX INVOICE",
                    _ => "RETAIL CASH MEMO"
                };
                TxtDocNumber.Text = sale.InvoiceNo;
                TxtDocDate.Text = $"Date: {sale.Date}";

                TxtPartyName.Text = sale.Party;
                TxtContactPerson.Text = !string.IsNullOrEmpty(sale.ContactPerson) ? $"Contact: {sale.ContactPerson}" : "";
                TxtPhone.Text = !string.IsNullOrEmpty(sale.Phone) ? $"Phone: {sale.Phone}" : "";
                TxtGstin.Text = !string.IsNullOrEmpty(sale.CustomerGstin) ? $"GSTIN: {sale.CustomerGstin}" : "";

                TxtPaymentTerms.Text = $"Terms: {sale.SaleType} {(sale.SaleType == "Credit" ? $"({sale.CreditDays} days)" : "")}";
                TxtPaymentDueDate.Text = sale.SaleType == "Credit" ? $"Due Date: {sale.PaymentDueDate}" : "";

                if (sale.Type == "export")
                {
                    TxtShippingInfo.Text = $"Shipment: {sale.Incoterm} · {sale.PortOfLoading} → {sale.PortOfDischarge} ({sale.Country})";
                    CurEquivWrap.Visibility = Visibility.Visible;
                    TxtRupeeEquiv.Text = $"₹{sale.GrossAmount:N2} (@ {sale.FxRate:N2})";
                }
                else if (sale.Type == "domestic" && !string.IsNullOrEmpty(sale.VehicleNo))
                {
                    TxtShippingInfo.Text = $"Vehicle: {sale.VehicleNo}";
                }
                else if (sale.Type == "retail")
                {
                    TxtShippingInfo.Text = $"Sold At: {sale.Event} · Paid by {sale.PaymentMode}";
                }

                var lines = sale.Lines.Select(l => new DocViewerLineItem
                {
                    Name = l.Name,
                    Hsn = l.Hsn,
                    Size = l.Size,
                    Qty = l.Qty,
                    Rate = l.Rate,
                    GstPercent = l.GstPercent,
                    GrossAmount = l.GrossAmount
                }).ToList();
                DgInvoiceLines.ItemsSource = lines;

                string cur = sale.Currency ?? "INR";
                TxtSubtotal.Text = cur == "INR" ? $"₹{sale.NetAmount:N2}" : $"{cur} {sale.GrossAmountCurrency:N2}";
                TxtTaxAmount.Text = cur == "INR" ? $"₹{sale.TaxAmount:N2}" : "LUT Zero-Rated";
                TxtGrossTotal.Text = cur == "INR" ? $"₹{sale.GrossAmount:N2}" : $"{cur} {sale.GrossAmountCurrency:N2}";
                TxtPaidAmount.Text = $"₹{sale.PaidAmount:N2}";
                TxtBalanceDue.Text = $"₹{sale.BalanceAmount:N2}";
            }
            else if (doc is PurchaseDoc purchase)
            {
                TxtDocTypeHeader.Text = $"PURCHASE BILL ({purchase.KindLabel.ToUpper()})";
                TxtDocNumber.Text = purchase.BillNo;
                TxtDocDate.Text = $"Date: {purchase.Date}";

                TxtPartyName.Text = purchase.Party;
                TxtContactPerson.Text = !string.IsNullOrEmpty(purchase.VendorBillRef) ? $"Vendor Ref: {purchase.VendorBillRef}" : "";
                TxtPhone.Text = !string.IsNullOrEmpty(purchase.Phone) ? $"Phone: {purchase.Phone}" : "";
                TxtGstin.Text = "";

                TxtPaymentTerms.Text = $"Terms: {purchase.SaleType} {(purchase.SaleType == "Credit" ? $"({purchase.CreditDays} days)" : "")}";
                TxtPaymentDueDate.Text = purchase.SaleType == "Credit" ? $"Due Date: {purchase.PaymentDueDate}" : "";
                TxtShippingInfo.Text = "Recorded in Material Purchase Register";

                var lines = purchase.Lines.Select(l => new DocViewerLineItem
                {
                    Name = l.Name,
                    Hsn = "—",
                    Size = "—",
                    Qty = l.Qty,
                    Rate = l.Rate,
                    GstPercent = l.GstPercent,
                    GrossAmount = l.GrossAmount
                }).ToList();
                DgInvoiceLines.ItemsSource = lines;

                TxtSubtotal.Text = $"₹{purchase.NetAmount:N2}";
                TxtTaxAmount.Text = $"₹{purchase.TaxAmount:N2}";
                TxtGrossTotal.Text = $"₹{purchase.GrossAmount:N2}";
                TxtPaidAmount.Text = $"₹{purchase.PaidAmount:N2}";
                TxtBalanceDue.Text = $"₹{purchase.BalanceAmount:N2}";
            }
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                printDialog.PrintVisual(PrintArea, "ASI Document Print");
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
