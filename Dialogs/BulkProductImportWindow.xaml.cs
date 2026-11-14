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
            sb.AppendLine("ITEM CLASSIFICATION,CATEGORY,UNIT OF MEASUREMENT,ITEM NAME,SIZES,QTY,SKU,HSN,BARCODE,COST PRICE,SELLING PRICE,LOW ALERT,MEDIUM ALERT");

            if (type == "ready" || type == "all")
            {
                sb.AppendLine("\"Readymade Product\",\"Bag\",\"pcs\",\"Ladies checked bag\",\"Medium\",\"20\",\"ASI-RDY-004\",\"42022110\",\"\",\"500\",\"1501\",\"3\",\"7\"");
                sb.AppendLine("\"Readymade Product\",\"Shoe\",\"pair\",\"Brown lofar\",\"7,8,9,10,11\",\"28\",\"ASI-RDY-007\",\"6403\",\"\",\"320\",\"961\",\"3\",\"7\"");
            }
            if (type == "mfg" || type == "all")
            {
                sb.AppendLine("\"Finished Product\",\"Shoe\",\"pair\",\"Leather Formal Derby\",\"6,7,8,9,10,11\",\"50\",\"ASI-MFG-001\",\"6403\",\"\",\"850\",\"2499\",\"10\",\"25\"");
                sb.AppendLine("\"Finished Product\",\"Shoe\",\"pair\",\"Safety Shoe Steel Toe\",\"7,8,9,10\",\"35\",\"ASI-MFG-002\",\"64031990\",\"\",\"950\",\"2899\",\"5\",\"15\"");
            }
            if (type == "raw" || type == "all")
            {
                sb.AppendLine("\"Raw Material\",\"\",\"sq ft\",\"Upper Full Grain Leather\",\"\",\"300\",\"ASI-RM-001\",\"4107\",\"\",\"140\",\"0\",\"50\",\"100\"");
                sb.AppendLine("\"Raw Material\",\"\",\"pair\",\"PU Sole Unit\",\"6,7,8,9,10\",\"250\",\"ASI-RM-002\",\"64062000\",\"\",\"160\",\"0\",\"40\",\"80\"");
            }
            if (type == "gen" || type == "all")
            {
                sb.AppendLine("\"General Packaging\",\"\",\"pcs\",\"ASI Shoe Packaging Box\",\"\",\"1000\",\"ASI-GEN-001\",\"48191010\",\"\",\"24\",\"0\",\"150\",\"300\"");
                sb.AppendLine("\"General Packaging\",\"\",\"pcs\",\"Clear Poly Dust Bag\",\"\",\"2000\",\"ASI-GEN-002\",\"39232100\",\"\",\"4\",\"0\",\"300\",\"600\"");
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
            var batchUsedBarcodes = _dataService.GetAllUsedBarcodes();

            try
            {
                foreach (DataRow r in _currentTable.Rows)
                {
                    string name = GetVal(r, "Name");
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    string classification = GetVal(r, "Type", _targetType).Trim().ToLowerInvariant();

                    if (classification.Contains("raw"))
                    {
                        // 1. RAW MATERIAL
                        string unit = GetVal(r, "Unit", "units");
                        string sku = GetVal(r, "SKU");
                        if (string.IsNullOrWhiteSpace(sku))
                        {
                            sku = "ASI-RM-" + (_dataService.RawMaterials.Count + importedCount + 1).ToString("D3");
                        }
                        string hsn = GetVal(r, "HSN");
                        double.TryParse(GetVal(r, "Stock", "0"), out double stock);
                        double.TryParse(GetVal(r, "ReorderLevel", "10"), out double reorder);
                        double.TryParse(GetVal(r, "Cost", "0"), out double rate);
                        double.TryParse(GetVal(r, "Gst", "5"), out double gst);

                        string barcode = GetVal(r, "Barcode");
                        if (string.IsNullOrWhiteSpace(barcode) || barcode == "—" || batchUsedBarcodes.Contains(barcode) || autoBarcode)
                        {
                            if (string.IsNullOrWhiteSpace(barcode) || barcode == "—" || batchUsedBarcodes.Contains(barcode))
                            {
                                barcode = BarcodeService.GenerateUniqueBarcode(batchUsedBarcodes, "raw", "Raw Material");
                            }
                        }
                        batchUsedBarcodes.Add(barcode);

                        var existing = updateExisting ? _dataService.RawMaterials.FirstOrDefault(m =>
                            (!string.IsNullOrEmpty(sku) && m.Sku.Equals(sku, StringComparison.OrdinalIgnoreCase)) ||
                            m.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) : null;

                        if (existing != null)
                        {
                            existing.Rate = rate;
                            existing.Gst = gst;
                            existing.Stock += stock;
                            existing.ReorderLevel = reorder;
                            if (!string.IsNullOrEmpty(hsn)) existing.Hsn = hsn;
                            if (string.IsNullOrEmpty(existing.Barcode) && !string.IsNullOrEmpty(barcode))
                            {
                                existing.Barcode = barcode;
                            }
                            await _dataService.SaveRawMaterialAsync(existing);
                            updatedCount++;
                        }
                        else
                        {
                            var rm = new RawMaterialItem
                            {
                                Id = "rm_" + Guid.NewGuid().ToString("N")[..8],
                                Name = name,
                                Sku = sku,
                                Hsn = hsn,
                                Barcode = barcode,
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
                    else if (classification.Contains("pack") || classification.Contains("gen"))
                    {
                        // 2. GENERAL PACKAGING
                        string unit = GetVal(r, "Unit", "pcs");
                        string sku = GetVal(r, "SKU");
                        if (string.IsNullOrWhiteSpace(sku))
                        {
                            sku = "ASI-GEN-" + (_dataService.GeneralItems.Count + importedCount + 1).ToString("D3");
                        }
                        string hsn = GetVal(r, "HSN");
                        double.TryParse(GetVal(r, "Stock", "0"), out double stock);
                        double.TryParse(GetVal(r, "ReorderLevel", "10"), out double reorder);
                        double.TryParse(GetVal(r, "Cost", "0"), out double rate);
                        double.TryParse(GetVal(r, "Gst", "5"), out double gst);

                        string barcode = GetVal(r, "Barcode");
                        if (string.IsNullOrWhiteSpace(barcode) || barcode == "—" || batchUsedBarcodes.Contains(barcode) || autoBarcode)
                        {
                            if (string.IsNullOrWhiteSpace(barcode) || barcode == "—" || batchUsedBarcodes.Contains(barcode))
                            {
                                barcode = BarcodeService.GenerateUniqueBarcode(batchUsedBarcodes, "gen", "Packaging");
                            }
                        }
                        batchUsedBarcodes.Add(barcode);

                        var existing = updateExisting ? _dataService.GeneralItems.FirstOrDefault(g =>
                            (!string.IsNullOrEmpty(sku) && g.Sku.Equals(sku, StringComparison.OrdinalIgnoreCase)) ||
                            g.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) : null;

                        if (existing != null)
                        {
                            existing.Rate = rate;
                            existing.Gst = gst;
                            existing.Stock += stock;
                            existing.ReorderLevel = reorder;
                            if (!string.IsNullOrEmpty(hsn)) existing.Hsn = hsn;
                            if (string.IsNullOrEmpty(existing.Barcode) && !string.IsNullOrEmpty(barcode))
                            {
                                existing.Barcode = barcode;
                            }
                            await _dataService.SaveGeneralItemAsync(existing);
                            updatedCount++;
                        }
                        else
                        {
                            var gi = new GeneralItem
                            {
                                Id = "gen_" + Guid.NewGuid().ToString("N")[..8],
                                Name = name,
                                Sku = sku,
                                Hsn = hsn,
                                Barcode = barcode,
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
                    else
                    {
                        // 3. FINISHED PRODUCT OR 4. READYMADE PRODUCT
                        bool isReady = classification.Contains("ready") || (!classification.Contains("finish") && !classification.Contains("mfg") && _targetType == "ready");

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
                        string unit = GetVal(r, "Unit", isReady ? "pcs" : "pair");
                        string sizes = GetVal(r, "Sizes", "Free size");
                        string hsn = GetVal(r, "HSN", isReady ? "4202" : "6403");

                        double.TryParse(GetVal(r, "Cost", "0"), out double cost);
                        double.TryParse(GetVal(r, "RetailPrice", "0"), out double retail);
                        double.TryParse(GetVal(r, "WholesalePrice", "0"), out double wholesale);
                        if (wholesale <= 0) wholesale = retail > 0 ? Math.Round(retail * 0.7, 0) : cost * 1.3;

                        double.TryParse(GetVal(r, "Stock", "0"), out double stock);
                        double.TryParse(GetVal(r, "ReorderLevel", "10"), out double reorder);
                        double.TryParse(GetVal(r, "Gst", "5"), out double gst);
                        if (gst <= 0) gst = 5;

                        string barcode = GetVal(r, "Barcode");
                        if (string.IsNullOrWhiteSpace(barcode) || barcode == "—" || batchUsedBarcodes.Contains(barcode) || (string.IsNullOrWhiteSpace(barcode) && autoBarcode))
                        {
                            barcode = BarcodeService.GenerateUniqueBarcode(batchUsedBarcodes, isReady ? "ready" : "mfg", category);
                        }
                        batchUsedBarcodes.Add(barcode);

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
                            if (!string.IsNullOrEmpty(hsn)) existing.Hsn = hsn;
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
                                Hsn = hsn,
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
                "name" => new[] { "name", "item name", "itemname", "item description", "itemdescription", "description", "product name", "productname", "product", "item", "title", "particulars" },
                "sku" => new[] { "sku", "item code", "itemcode", "product code", "productcode", "code", "style no", "styleno", "model no", "modelno", "style" },
                "category" => new[] { "category", "product category", "item category" },
                "subcategory" => new[] { "subcategory", "sub category", "sub-category", "group" },
                "unit" => new[] { "unit", "unit of measurment", "unit of measurement", "unitofmeasurment", "unitofmeasurement", "uom", "measurment", "measurement" },
                "sizes" => new[] { "sizes", "size", "available sizes", "size list" },
                "cost" => new[] { "cost", "cost price", "costprice", "purchase price", "purchaseprice", "buy price", "buying price", "rate", "cost rate", "purchase rate" },
                "retailprice" => new[] { "retailprice", "selling price", "sellingprice", "retail price", "price", "mrp", "sale price", "saleprice", "unit price" },
                "wholesaleprice" => new[] { "wholesaleprice", "wholesale price", "bulk price", "bulkprice", "b2b price", "dealer price", "trade price" },
                "stock" => new[] { "stock", "qty", "quantity", "opening stock", "openingstock", "units", "count" },
                "reorderlevel" => new[] { "reorderlevel", "reorder level", "low alert", "lowalert", "medium alert", "mediumalert", "min stock", "minstock", "alert level", "reorder" },
                "barcode" => new[] { "barcode", "bar code", "ean", "ean13", "upc", "code128" },
                "gst" => new[] { "gst", "tax", "gst%", "tax%" },
                "hsn" => new[] { "hsn", "hsn code", "hsncode" },
                "type" => new[] { "type", "item classification", "itemclassification", "classification", "item type", "product type", "source type" },
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
