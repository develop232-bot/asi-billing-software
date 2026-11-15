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
            if (_dataService.RawMaterials != null && _dataService.RawMaterials.Any())
            {
                foreach (var r in _dataService.RawMaterials)
                {
                    AvailableItems.Add(new SourcingItemOption
                    {
                        Id = r.Id,
                        Name = r.Name,
                        Sku = r.Sku,
                        Unit = r.Unit,
                        Rate = r.Rate,
                        Gst = r.Gst,
                        Stock = r.Stock
                    });
                }
            }
            else
            {
                // Fallbacks so user always has standard options ready
                AvailableItems.Add(new SourcingItemOption { Id = "raw_leather", Name = "Upper Leather (Black / Brown)", Sku = "ASI-RAW-001", Unit = "sq ft", Rate = 65, Gst = 5 });
                AvailableItems.Add(new SourcingItemOption { Id = "raw_sole", Name = "Rubber / PVC Sole (Pairs)", Sku = "ASI-RAW-002", Unit = "pair", Rate = 85, Gst = 5 });
                AvailableItems.Add(new SourcingItemOption { Id = "raw_fabric", Name = "Uniform Shirting Fabric", Sku = "ASI-RAW-003", Unit = "meter", Rate = 95, Gst = 5 });
                AvailableItems.Add(new SourcingItemOption { Id = "raw_lining", Name = "Inner Breathable Lining", Sku = "ASI-RAW-004", Unit = "meter", Rate = 45, Gst = 5 });
                AvailableItems.Add(new SourcingItemOption { Id = "raw_thread", Name = "Heavy Duty Thread Spool", Sku = "ASI-RAW-005", Unit = "roll", Rate = 120, Gst = 5 });
                AvailableItems.Add(new SourcingItemOption { Id = "raw_adhesive", Name = "Shoe Adhesive / Chemical Glue", Sku = "ASI-RAW-006", Unit = "kg", Rate = 210, Gst = 18 });
            }
        }
    }
}
