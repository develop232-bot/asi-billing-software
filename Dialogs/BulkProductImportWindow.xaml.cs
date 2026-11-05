using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AsiErp.Models;
using AsiErp.Services;
using Microsoft.Win32;

namespace AsiErp.Dialogs
{
    public partial class BulkProductImportWindow : Window
    {
        private readonly AppDataService _dataService = AppDataService.Instance;
        private DataTable? _currentTable;
        private string _targetType = "mfg";
        private string _loadedFilePath = string.Empty;

        public BulkProductImportWindow()
        {
            InitializeComponent();
            _targetType = (CbTargetType.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "mfg";
        }

        private void TargetType_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (CbTargetType?.SelectedItem is ComboBoxItem item && item.Tag is string t)
            {
                _targetType = t;
            }
        }

        private void DownloadSampleCsv_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog
            {
                Title = "Save Sample CSV Template",
                Filter = "CSV Files (*.csv)|*.csv",
                FileName = $"ASI_{_targetType.ToUpper()}_Sample_Template.csv"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    string content = GetSampleCsvContent(_targetType);
                    File.WriteAllText(sfd.FileName, content, Encoding.UTF8);

                    var res = MessageBox.Show($"Sample CSV template saved successfully to:\n{sfd.FileName}\n\nWould you like to open it now?",
                        "Template Created", MessageBoxButton.YesNo, MessageBoxImage.Information);

                    if (res == MessageBoxResult.Yes)
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = sfd.FileName,
                            UseShellExecute = true
                        });
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not save sample file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private string GetSampleCsvContent(string type)
        {
            var sb = new StringBuilder();
            if (type == "mfg" || type == "ready")
            {
                sb.AppendLine("Name,SKU,Category,Subcategory,Unit,Sizes,Cost,RetailPrice,WholesalePrice,Stock,ReorderLevel,Barcode");
                if (type == "mfg")
                {
                    sb.AppendLine("\"ASI Derby Formal Shoe Black\",\"ASI-MFG-001\",\"Shoe\",\"Formal\",\"pair\",\"6,7,8,9,10,11\",850,2499,1450,120,20,");
                    sb.AppendLine("\"ASI Sports Runner Blue\",\"ASI-MFG-002\",\"Shoe\",\"Sports\",\"pair\",\"7,8,9,10\",650,1899,1100,85,15,");
                    sb.AppendLine("\"ASI School Black Leather Oxford\",\"ASI-MFG-003\",\"Shoe\",\"School\",\"pair\",\"1,2,3,4,5,6\",420,999,650,200,30,");
                    sb.AppendLine("\"ASI Genuine Leather Belt\",\"ASI-MFG-004\",\"Accessory\",\"Formal\",\"pcs\",\"Free Size\",180,599,350,150,25,");
                }
                else
                {
                    sb.AppendLine("\"Readymade School Shirt White\",\"ASI-RDY-001\",\"Uniform\",\"School\",\"pcs\",\"S,M,L,XL\",220,550,350,100,20,");
                    sb.AppendLine("\"Navy Blue Uniform Trousers\",\"ASI-RDY-002\",\"Uniform\",\"School\",\"pcs\",\"28,30,32,34\",310,750,480,80,15,");
                    sb.AppendLine("\"White Cotton School Socks\",\"ASI-RDY-003\",\"Accessory\",\"Socks\",\"pair\",\"Free Size\",45,120,75,300,50,");
                }
            }
            else if (type == "raw")
            {
                sb.AppendLine("Name,Unit,Stock,ReorderLevel,Rate,Gst");
                sb.AppendLine("\"Upper Full Grain Black Leather\",\"sq.ft\",500,50,135,5");
                sb.AppendLine("\"PU Moulded Sole Unit - Size 8\",\"pair\",300,40,160,18");
                sb.AppendLine("\"Heavy Duty Nylon Stitching Thread (Spool)\",\"pcs\",150,20,85,12");
                sb.AppendLine("\"Cushioned Insole EVA Foam Sheets\",\"sheet\",80,15,220,18");
            }
            else
            {
                sb.AppendLine("Name,Unit,Stock,ReorderLevel,Rate,Gst");
                sb.AppendLine("\"ASI Shoe Packaging Box - Printed\",\"pcs\",1200,150,24,12");
                sb.AppendLine("\"Clear Poly Protective Carry Bag\",\"pcs\",2000,300,4,18");
                sb.AppendLine("\"Barcode Hangtags with String\",\"pcs\",3000,500,2,12");
                sb.AppendLine("\"Brown Carton Master Outer 24-Pair\",\"pcs\",150,25,65,12");
            }

            return sb.ToString();
        }

        private void BrowseFile_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Title = "Select Products CSV File",
                Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*"
            };

            if (ofd.ShowDialog() == true)
            {
                try
                {
                    _loadedFilePath = ofd.FileName;
                    TxtFilePath.Text = ofd.FileName;

                    _currentTable = ParseCsvToDataTable(_loadedFilePath);
                    DgPreview.ItemsSource = _currentTable.DefaultView;
                    TxtPreviewCount.Text = $"{_currentTable.Rows.Count} valid items ready to import";
                    TxtStatus.Text = $"File loaded successfully ({_currentTable.Rows.Count} rows). Click 'Import All Items' below to save.";
                    BtnStartImport.IsEnabled = _currentTable.Rows.Count > 0;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error reading CSV file: {ex.Message}", "CSV Read Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private DataTable ParseCsvToDataTable(string path)
        {
            var dt = new DataTable();
            var lines = File.ReadAllLines(path, Encoding.UTF8);
            if (lines.Length == 0) return dt;

            // Header line
            var headers = ParseCsvLine(lines[0]);
            foreach (var h in headers)
            {
                dt.Columns.Add(string.IsNullOrWhiteSpace(h) ? $"Column_{dt.Columns.Count + 1}" : h.Trim());
            }

            // Data rows
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                var fields = ParseCsvLine(line);
                var row = dt.NewRow();
                for (int col = 0; col < Math.Min(fields.Count, dt.Columns.Count); col++)
                {
                    row[col] = fields[col];
                }
                dt.Rows.Add(row);
            }

            return dt;
        }

        private List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var sb = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(sb.ToString().Trim());
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }
            result.Add(sb.ToString().Trim());
            return result;
        }

        private async void StartImport_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTable == null || _currentTable.Rows.Count == 0) return;

            BtnStartImport.IsEnabled = false;
            TxtStatus.Text = "Importing items into database, please wait...";

            int importedCount = 0;
            int updatedCount = 0;
            bool autoBarcode = ChkAutoBarcode.IsChecked == true;
            bool updateExisting = ChkUpdateExisting.IsChecked == true;

            try
            {
                if (_targetType == "mfg" || _targetType == "ready")
                {
                    bool isReady = _targetType == "ready";

                    foreach (DataRow r in _currentTable.Rows)
                    {
                        string name = GetVal(r, "Name");
                        if (string.IsNullOrWhiteSpace(name)) continue;

                        string sku = GetVal(r, "SKU");
                        if (string.IsNullOrWhiteSpace(sku))
                        {
                            sku = "ASI-" + (_targetType == "mfg" ? "MFG" : "RDY") + "-" + (_dataService.Products.Count + importedCount + 1).ToString("D3");
                        }

                        string category = GetVal(r, "Category", "Shoe");
                        string subcat = GetVal(r, "Subcategory", "All");
                        string unit = GetVal(r, "Unit", "pair");
                        string sizes = GetVal(r, "Sizes", "6,7,8,9,10,11");

                        double.TryParse(GetVal(r, "Cost", "0"), out double cost);
                        double.TryParse(GetVal(r, "RetailPrice", "0"), out double retail);
                        double.TryParse(GetVal(r, "WholesalePrice", "0"), out double wholesale);
                        if (wholesale <= 0) wholesale = retail > 0 ? Math.Round(retail * 0.7, 0) : cost * 1.3;

                        double.TryParse(GetVal(r, "Stock", "0"), out double stock);
                        double.TryParse(GetVal(r, "ReorderLevel", "10"), out double reorder);

                        string barcode = GetVal(r, "Barcode");
                        if (string.IsNullOrWhiteSpace(barcode) && autoBarcode)
                        {
                            barcode = BarcodeService.GenerateBarcode();
                        }

                        // Check existing
                        var existing = updateExisting ? _dataService.Products.FirstOrDefault(p =>
                            (!string.IsNullOrEmpty(sku) && p.Sku.Equals(sku, StringComparison.OrdinalIgnoreCase)) ||
                            p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) : null;

                        if (existing != null)
                        {
                            existing.RetailPrice = retail;
                            existing.Cost = cost;
                            existing.BulkPrice = wholesale;
                            existing.DomesticPrice = wholesale;
                            existing.Stock += stock;
                            existing.ReorderLevel = reorder;
                            if (string.IsNullOrEmpty(existing.Barcode) && !string.IsNullOrEmpty(barcode))
                            {
                                existing.Barcode = barcode;
                            }
                            await _dataService.SaveProductAsync(existing);
                            updatedCount++;
                        }
                        else
                        {
                            var sizeList = sizes.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
                            var newProd = new ProductItem
                            {
                                Id = "p_" + Guid.NewGuid().ToString("N")[..8],
                                Name = name,
                                Sku = sku,
                                Category = category,
                                SourceType = isReady ? "R" : "M",
                                Unit = unit,
                                Sizes = sizeList,
                                Cost = cost,
                                RetailPrice = retail,
                                BulkPrice = wholesale,
                                DomesticPrice = wholesale,
                                Stock = stock,
                                ReorderLevel = reorder,
                                Barcode = barcode
                            };

                            await _dataService.SaveProductAsync(newProd);
                            importedCount++;
                        }
                    }
                }
                else if (_targetType == "raw")
                {
                    foreach (DataRow r in _currentTable.Rows)
                    {
                        string name = GetVal(r, "Name");
                        if (string.IsNullOrWhiteSpace(name)) continue;

                        string unit = GetVal(r, "Unit", "units");
                        double.TryParse(GetVal(r, "Stock", "0"), out double stock);
                        double.TryParse(GetVal(r, "ReorderLevel", "10"), out double reorder);
                        double.TryParse(GetVal(r, "Rate", "0"), out double rate);
                        double.TryParse(GetVal(r, "Gst", "18"), out double gst);

                        var existing = updateExisting ? _dataService.RawMaterials.FirstOrDefault(m =>
                            m.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) : null;

                        if (existing != null)
                        {
                            existing.Rate = rate;
                            existing.Gst = gst;
                            existing.Stock += stock;
                            existing.ReorderLevel = reorder;
                            await _dataService.SaveRawMaterialAsync(existing);
                            updatedCount++;
                        }
                        else
                        {
                            var rm = new RawMaterialItem
                            {
                                Id = "rm_" + Guid.NewGuid().ToString("N")[..8],
                                Name = name,
                                Unit = unit,
                                Stock = stock,
                                ReorderLevel = reorder,
                                Rate = rate,
                                Gst = gst
                            };
                            await _dataService.SaveRawMaterialAsync(rm);
                            importedCount++;
                        }
                    }
                }
                else if (_targetType == "gen")
                {
                    foreach (DataRow r in _currentTable.Rows)
                    {
                        string name = GetVal(r, "Name");
                        if (string.IsNullOrWhiteSpace(name)) continue;

                        string unit = GetVal(r, "Unit", "pcs");
                        double.TryParse(GetVal(r, "Stock", "0"), out double stock);
                        double.TryParse(GetVal(r, "ReorderLevel", "10"), out double reorder);
                        double.TryParse(GetVal(r, "Rate", "0"), out double rate);
                        double.TryParse(GetVal(r, "Gst", "12"), out double gst);

                        var existing = updateExisting ? _dataService.GeneralItems.FirstOrDefault(g =>
                            g.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) : null;

                        if (existing != null)
                        {
                            existing.Rate = rate;
                            existing.Gst = gst;
                            existing.Stock += stock;
                            existing.ReorderLevel = reorder;
                            await _dataService.SaveGeneralItemAsync(existing);
                            updatedCount++;
                        }
                        else
                        {
                            var gi = new GeneralItem
                            {
                                Id = "gen_" + Guid.NewGuid().ToString("N")[..8],
                                Name = name,
                                Unit = unit,
                                Stock = stock,
                                ReorderLevel = reorder,
                                Rate = rate,
                                Gst = gst
                            };
                            await _dataService.SaveGeneralItemAsync(gi);
                            importedCount++;
                        }
                    }
                }

                _dataService.NotifyDataChanged();
                TxtStatus.Text = $"Import completed! {importedCount} new items added, {updatedCount} existing items updated.";

                MessageBox.Show(
                    $"Bulk CSV Import Successful!\n\n" +
                    $"• New Items Created: {importedCount}\n" +
                    $"• Existing Items Updated: {updatedCount}\n" +
                    $"• Auto-generated Barcodes Applied: {(autoBarcode ? "Yes" : "No")}\n\n" +
                    $"All items are now active in the catalog and synchronized with cloud database.",
                    "Import Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Error during import: {ex.Message}";
                MessageBox.Show($"An error occurred during import: {ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                BtnStartImport.IsEnabled = true;
            }
        }

        private string GetVal(DataRow r, string colName, string fallback = "")
        {
            if (_currentTable != null && _currentTable.Columns.Contains(colName))
            {
                var v = r[colName]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(v)) return v;
            }
            return fallback;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
