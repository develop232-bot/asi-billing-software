using System;
using System.Linq;
using AsiErp.Services;

namespace AsiErp.ViewModels
{
    public class RawMaterialPurchaseViewModel : PurchaseEntryViewModelBase
    {
        public override string Kind => "raw";
        public override string Title => "Raw Material Sourcing";
        public override string Subtitle => "Leather, soles, fabrics, thread, glue and everything that goes into production";
        public override string CategoryBadge => "RAW MATERIAL · TANNERIES & MILLS";
        public override string DefaultUnit => "sq ft";

        public RawMaterialPurchaseViewModel(AppDataService dataService, Action<string> navigateAction, Action<string, object?> openDialogAction)
            : base(dataService, navigateAction, openDialogAction)
        {
            ResetForm();
        }

        public override void ReloadAvailableItems()
        {
            AvailableItems.Clear();

            // 1. Primary: Catalog raw materials added in Products
            if (_dataService.RawMaterials != null && _dataService.RawMaterials.Any())
            {
                foreach (var r in _dataService.RawMaterials.Where(x => !x.Id.StartsWith("pur_it_") && !x.Id.StartsWith("pur_")))
                {
                    if (string.IsNullOrWhiteSpace(r.Name)) continue;
                    AvailableItems.Add(new SourcingItemOption
                    {
                        Id = r.Id,
                        Name = r.Name,
                        Sku = !string.IsNullOrWhiteSpace(r.Sku) ? r.Sku : r.Id,
                        Unit = !string.IsNullOrWhiteSpace(r.Unit) ? r.Unit : DefaultUnit,
                        Rate = r.Rate,
                        Gst = r.Gst > 0 ? r.Gst : 5,
                        Stock = r.Stock
                    });
                }
            }

            // 2. Secondary: Items from past raw purchases (if not already present)
            var pastItems = _dataService.Purchases
                .Where(p => p.Kind == "raw")
                .SelectMany(p => p.Lines ?? new())
                .Where(l => !string.IsNullOrWhiteSpace(l.Name))
                .GroupBy(l => l.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            foreach (var l in pastItems)
            {
                if (!AvailableItems.Any(x => x.Name.Equals(l.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    AvailableItems.Add(new SourcingItemOption
                    {
                        Id = l.ItemId,
                        Name = l.Name,
                        Sku = l.Sku,
                        Unit = l.Unit,
                        Rate = l.Rate,
                        Gst = l.GstPercent
                    });
                }
            }

            // 3. Standard baseline raw material options (fallback defaults)
            var standardOptions = new[]
            {
                new SourcingItemOption { Id = "raw_leather", Name = "Upper Leather (Black / Brown)", Sku = "ASI-RAW-001", Unit = "sq ft", Rate = 65, Gst = 5 },
                new SourcingItemOption { Id = "raw_sole", Name = "Rubber / PVC Sole (Pairs)", Sku = "ASI-RAW-002", Unit = "pair", Rate = 85, Gst = 5 },
                new SourcingItemOption { Id = "raw_fabric", Name = "Uniform Shirting Fabric", Sku = "ASI-RAW-003", Unit = "meter", Rate = 95, Gst = 5 },
                new SourcingItemOption { Id = "raw_lining", Name = "Inner Breathable Lining", Sku = "ASI-RAW-004", Unit = "meter", Rate = 45, Gst = 5 },
                new SourcingItemOption { Id = "raw_thread", Name = "Heavy Duty Thread Spool", Sku = "ASI-RAW-005", Unit = "roll", Rate = 120, Gst = 5 },
                new SourcingItemOption { Id = "raw_adhesive", Name = "Shoe Adhesive / Chemical Glue", Sku = "ASI-RAW-006", Unit = "kg", Rate = 210, Gst = 18 }
            };

            foreach (var opt in standardOptions)
            {
                if (!AvailableItems.Any(x => x.Name.Equals(opt.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    AvailableItems.Add(opt);
                }
            }
        }
    }
}
