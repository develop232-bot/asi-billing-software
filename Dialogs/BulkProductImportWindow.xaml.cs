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
                sb.AppendLine("Item Description,Unit of Measurement,Type,SKU,Barcode,Cost Price,Selling Price,GST,Quantity,Sizes,Category");
                if (type == "mfg")
                {
                    sb.AppendLine("\"ASI Derby Formal Shoe Black\",\"pair\",\"Manufactured\",\"ASI-MFG-001\",\"\",\"850\",\"2499\",\"5\",\"120\",\"6,7,8,9,10,11\",\"Shoe\"");
                    sb.AppendLine("\"ASI Sports Runner Blue\",\"pair\",\"Manufactured\",\"ASI-MFG-002\",\"\",\"650\",\"1899\",\"5\",\"85\",\"7,8,9,10\",\"Shoe\"");
                    sb.AppendLine("\"ASI School Black Leather Oxford\",\"pair\",\"Manufactured\",\"ASI-MFG-003\",\"\",\"420\",\"999\",\"5\",\"200\",\"1,2,3,4,5,6\",\"Shoe\"");
                }
                else
                {
                    sb.AppendLine("\"Ladies hand bag\",\"pcs\",\"Readymade Product\",\"ASI-RDY-001\",\"8906400000010\",\"650\",\"1951\",\"5\",\"20\",\"Big\",\"Bag\"");
                    sb.AppendLine("\"Black lofar\",\"pair\",\"Readymade Product\",\"ASI-RDY-002\",\"8906400000010\",\"320\",\"961\",\"5\",\"24\",\"8,9,10,11\",\"Shoe\"");
                    sb.AppendLine("\"Readymade School Shirt White\",\"pcs\",\"Readymade Product\",\"ASI-RDY-003\",\"\",\"220\",\"550\",\"5\",\"100\",\"S,M,L,XL\",\"Uniform\"");
                }
            }
            else if (type == "raw")
            {
                sb.AppendLine("Item Description,Unit of Measurement,Type,Rate,GST,Quantity,Reorder Level");
                sb.AppendLine("\"Upper Full Grain Black Leather\",\"sq.ft\",\"Raw Material\",\"135\",\"5\",\"500\",\"50\"");
                sb.AppendLine("\"PU Moulded Sole Unit - Size 8\",\"pair\",\"Raw Material\",\"160\",\"18\",\"300\",\"40\"");
                sb.AppendLine("\"Heavy Duty Nylon Stitching Thread\",\"pcs\",\"Raw Material\",\"85\",\"12\",\"150\",\"20\"");
            }
            else
            {
                sb.AppendLine("Item Description,Unit of Measurement,Type,Rate,GST,Quantity,Reorder Level");
                sb.AppendLine("\"ASI Shoe Packaging Box - Printed\",\"pcs\",\"Packaging\",\"24\",\"12\",\"1200\",\"150\"");
                sb.AppendLine("\"Clear Poly Protective Carry Bag\",\"pcs\",\"Packaging\",\"4\",\"18\",\"2000\",\"300\"");
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
                catch (IOException)
                {
                    MessageBox.Show(
                        $"Cannot read the CSV file because it is being used by another application (such as Microsoft Excel).\n\n" +
                        $"Please close Microsoft Excel and click 'Browse CSV File' again.",
                        "File Locked by Excel",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
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
            var lines = new List<string>();

            // Open with FileShare.ReadWrite so it reads seamlessly even if currently open in Excel
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream, Encoding.UTF8, true))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    lines.Add(line);
                }
            }

            if (lines.Count == 0) return dt;

            // Header line
            var headers = ParseCsvLine(lines[0]);
            foreach (var h in headers)
            {
                string colName = string.IsNullOrWhiteSpace(h) ? $"Column_{dt.Columns.Count + 1}" : h.Trim();
                string finalColName = colName;
                int suffix = 1;
                while (dt.Columns.Contains(finalColName))
                {
                    finalColName = $"{colName}_{++suffix}";
                }
                dt.Columns.Add(finalColName);
            }

            // Data rows
            for (int i = 1; i < lines.Count; i++)
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
                    foreach (DataRow r in _currentTable.Rows)
                    {
                        string name = GetVal(r, "Name");
                        if (string.IsNullOrWhiteSpace(name)) continue;

                        string rowType = GetVal(r, "Type", "").ToLowerInvariant();
                        bool isReady = _targetType == "ready";
                        if (rowType.Contains("ready") || rowType == "r") isReady = true;
                        else if (rowType.Contains("mfg") || rowType.Contains("manufactur") || rowType == "m") isReady = false;

                        string sku = GetVal(r, "SKU");
                        if (string.IsNullOrWhiteSpace(sku))
                        {
                            sku = "ASI-" + (isReady ? "RDY" : "MFG") + "-" + (_dataService.Products.Count + importedCount + 1).ToString("D3");
                        }

                        string category = GetVal(r, "Category");
                        if (string.IsNullOrWhiteSpace(category))
                        {
                            category = isReady ? "Readymade" : "Shoe";
                        }
                        string subcat = GetVal(r, "Subcategory", "All");
                        string unit = GetVal(r, "Unit", isReady ? "pcs" : "pair");
                        string sizes = GetVal(r, "Sizes", "Free size");

                        double.TryParse(GetVal(r, "Cost", "0"), out double cost);
                        double.TryParse(GetVal(r, "RetailPrice", "0"), out double retail);
                        double.TryParse(GetVal(r, "WholesalePrice", "0"), out double wholesale);
                        if (wholesale <= 0) wholesale = retail > 0 ? Math.Round(retail * 0.7, 0) : cost * 1.3;

                        double.TryParse(GetVal(r, "Stock", "0"), out double stock);
                        double.TryParse(GetVal(r, "ReorderLevel", "10"), out double reorder);
                        double.TryParse(GetVal(r, "Gst", "5"), out double gst);
                        if (gst <= 0) gst = 5;

                        string barcode = GetVal(r, "Barcode");
                        if (string.IsNullOrWhiteSpace(barcode) && autoBarcode)
                        {
                            barcode = BarcodeService.GenerateBarcode(category, isReady ? "R" : "M", _dataService.Products.Count + importedCount + 1);
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
                            existing.Gst = gst;
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
                            if (sizeList.Count == 0) sizeList.Add("Free size");

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
                                Barcode = barcode,
                                Gst = gst
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

                if (importedCount == 0 && updatedCount == 0)
                {
                    MessageBox.Show(
                        "No matching items could be found in the CSV file.\n\n" +
                        "Please verify that your CSV contains an 'Item Description' or 'Name' column header.\n" +
                        "You can click 'Download Sample CSV' above to download a working template.",
                        "No Items Imported",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    BtnStartImport.IsEnabled = true;
                    return;
                }

                _dataService.NotifyDataChanged();
                _ = _dataService.SyncQueue.ProcessQueueAsync();
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

        private string GetVal(DataRow r, string key, string fallback = "")
        {
            if (_currentTable == null) return fallback;

            string[] candidates = key.ToLowerInvariant() switch
            {
                "name" => new[] { "name", "item description", "itemdescription", "description", "product name", "productname", "item name", "itemname", "product", "item", "title", "particulars" },
                "sku" => new[] { "sku", "item code", "itemcode", "product code", "productcode", "code", "style no", "styleno", "model no", "modelno", "style" },
                "category" => new[] { "category", "product category", "item category" },
                "subcategory" => new[] { "subcategory", "sub category", "sub-category", "group" },
                "unit" => new[] { "unit", "unit of measurement", "unitofmeasurement", "uom", "measurement" },
                "sizes" => new[] { "sizes", "size", "available sizes", "size list" },
                "cost" => new[] { "cost", "cost price", "costprice", "purchase price", "purchaseprice", "buy price", "buying price", "rate", "cost rate" },
                "retailprice" => new[] { "retailprice", "selling price", "sellingprice", "retail price", "price", "mrp", "sale price", "saleprice", "unit price" },
                "wholesaleprice" => new[] { "wholesaleprice", "wholesale price", "bulk price", "bulkprice", "b2b price", "dealer price", "trade price" },
                "stock" => new[] { "stock", "quantity", "qty", "opening stock", "openingstock", "units", "count" },
                "reorderlevel" => new[] { "reorderlevel", "reorder level", "min stock", "minstock", "alert level", "reorder" },
                "barcode" => new[] { "barcode", "bar code", "ean", "ean13", "upc", "code128" },
                "gst" => new[] { "gst", "tax", "gst%", "tax%" },
                "hsn" => new[] { "hsn", "hsn code", "hsncode" },
                "type" => new[] { "type", "item type", "product type", "source type" },
                _ => new[] { key.ToLowerInvariant() }
            };

            foreach (var cand in candidates)
            {
                foreach (DataColumn col in _currentTable.Columns)
                {
                    string colClean = col.ColumnName.Trim().ToLowerInvariant();
                    if (colClean == cand || colClean.Replace(" ", "").Replace("_", "").Replace("-", "") == cand.Replace(" ", "").Replace("_", "").Replace("-", ""))
                    {
                        var val = r[col]?.ToString()?.Trim();
                        if (!string.IsNullOrEmpty(val)) return val;
                    }
                }
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
