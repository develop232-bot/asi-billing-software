using System;
using System.Collections.Generic;
using System.Linq;
using Google.Cloud.Firestore;

namespace AsiErp.Models
{
    [FirestoreData]
    public class ProductItem
    {
        [FirestoreProperty("id")]
        public string Id { get; set; } = string.Empty;

        [FirestoreProperty("createdAt")]
        public string CreatedAt { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");

        [FirestoreProperty("sku")]
        public string Sku { get; set; } = string.Empty;

        [FirestoreProperty("barcode")]
        public string Barcode { get; set; } = string.Empty;

        [FirestoreProperty("name")]
        public string Name { get; set; } = string.Empty;

        [FirestoreProperty("category")]
        public string Category { get; set; } = "Shoe"; // Shoe, Uniform, Accessory

        [FirestoreProperty("src")]
        public string SourceType { get; set; } = "M"; // M = Manufactured, R = Readymade

        [FirestoreProperty("unit")]
        public string Unit { get; set; } = "pair";

        [FirestoreProperty("sizes")]
        public List<string> Sizes { get; set; } = new();

        [FirestoreProperty("hsn")]
        public string Hsn { get; set; } = "6403";

        [FirestoreProperty("gst")]
        public double Gst { get; set; } = 5;

        [FirestoreProperty("cost")]
        public double Cost { get; set; }

        [FirestoreProperty("retail")]
        public double RetailPrice { get; set; }

        [FirestoreProperty("bulk")]
        public double BulkPrice { get; set; }

        [FirestoreProperty("dom")]
        public double DomesticPrice { get; set; }

        [FirestoreProperty("exp")]
        public double ExportPrice { get; set; } // in USD

        [FirestoreProperty("stock")]
        public double Stock { get; set; }

        [FirestoreProperty("registeredQty")]
        public double RegisteredQty { get; set; }

        [FirestoreProperty("reorder")]
        public double ReorderLevel { get; set; } = 50;

        [FirestoreProperty("pack")]
        public string PackItemId { get; set; } = string.Empty;

        [FirestoreProperty("bom")]
        public List<BomItem> Bom { get; set; } = new();

        public bool IsManufactured
        {
            get => string.Equals(SourceType, "M", StringComparison.OrdinalIgnoreCase) || string.Equals(SourceType, "mfg", StringComparison.OrdinalIgnoreCase);
            set { }
        }

        public bool IsReadymade
        {
            get => string.Equals(SourceType, "R", StringComparison.OrdinalIgnoreCase)
                || string.Equals(SourceType, "ready", StringComparison.OrdinalIgnoreCase)
                || string.Equals(SourceType, "readymade", StringComparison.OrdinalIgnoreCase);
            set { }
        }

        public string TypeLabel { get => IsManufactured ? "Finished product" : "Readymade product"; set { } }

        public string SizesDisplay { get => (Sizes != null && Sizes.Count > 0) ? string.Join(", ", Sizes) : "Free size"; set { } }

        public double EffectiveCost
        {
            get
            {
                if (IsManufactured && Bom != null && Bom.Count > 0)
                {
                    return Cost > 0 ? Cost : Math.Round(DomesticPrice * 0.6, 2);
                }
                return Cost;
            }
            set { }
        }

        public double StockValue { get => Stock * (EffectiveCost > 0 ? EffectiveCost : DomesticPrice); set { } }

        public string StatusLevel
        {
            get
            {
                double target = System.Math.Max(ReorderLevel, 1) * 2.0;
                double pct = (Stock / target) * 100;
                if (Stock < 10 || pct < 10) return "out";
                if (Stock <= ReorderLevel || pct < 50) return "low";
                return "ok";
            }
            set { }
        }

        public string StatusLevelText
        {
            get
            {
                double target = System.Math.Max(ReorderLevel, 1) * 2.0;
                double pct = (Stock / target) * 100;
                if (Stock <= 0) return "Out";
                if (Stock < 10 || pct < 10) return "Low";
                if (Stock <= ReorderLevel || pct < 50) return "Medium";
                return "In stock";
            }
            set { }
        }

        public double AutoGstRate { get => RetailPrice <= 2500 ? 5 : 18; set { } }

        public string DisplayBarcode
        {
            get => !string.IsNullOrEmpty(Barcode) ? Barcode : (!string.IsNullOrEmpty(Sku) ? Sku : Id);
            set { }
        }
    }
}
