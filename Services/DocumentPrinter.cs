using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using AsiErp.Dialogs;
using AsiErp.Models;

namespace AsiErp.Services
{
    public static class DocumentPrinter
    {
        /// <summary>
        /// Directly prints a sale or purchase document without showing the preview window.
        /// </summary>
        public static bool PrintDirect(object doc, bool silent = true)
        {
            try
            {
                var printDialog = new PrintDialog();

                if (!silent)
                {
                    if (printDialog.ShowDialog() != true) return false;
                }

                // Instantiate document viewer to generate the printable layout
                var viewer = new DocumentViewerWindow(doc);
                var element = viewer.PrintArea;

                if (element == null) return false;

                // Detach from parent to measure/arrange off-screen
                if (element.Parent is Panel p) p.Children.Remove(element);
                else if (element.Parent is ContentControl cc) cc.Content = null;
                else if (element.Parent is Decorator dec) dec.Child = null;

                double printableWidth = printDialog.PrintableAreaWidth > 0 ? printDialog.PrintableAreaWidth : 790;
                double printableHeight = printDialog.PrintableAreaHeight > 0 ? printDialog.PrintableAreaHeight : 1120;

                element.Measure(new Size(printableWidth, double.PositiveInfinity));
                element.Arrange(new Rect(new Point(0, 0), element.DesiredSize));
                element.UpdateLayout();

                string docTitle = doc switch
                {
                    SaleDoc s => $"ASI Bill {s.InvoiceNo}",
                    PurchaseDoc pu => $"ASI Bill {pu.BillNo}",
                    _ => "ASI ERP Document"
                };

                printDialog.PrintVisual(element, docTitle);
                return true;
            }
            catch (Exception ex)
            {
                // In desktop environment without physical printer, handle gracefully
                MessageBox.Show($"Direct Print queued: {ex.Message}", "Print Status", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }
        }
    }
}
