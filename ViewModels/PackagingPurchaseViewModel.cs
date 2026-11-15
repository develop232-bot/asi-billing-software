using System;
using System.Linq;
using AsiErp.Services;

namespace AsiErp.ViewModels
{
    public class PackagingPurchaseViewModel : PurchaseEntryViewModelBase
    {
        public override string Kind => "gen";
        public override string Title => "General Packaging Sourcing";
        public override string Subtitle => "Shoe boxes, uniform covers, tissue paper, ASI logo bags, single-shoe covers and other packing items.";
        public override string CategoryBadge => "GENERAL PACKAGING · SUPPLIES & BOXES";
        public override string DefaultUnit => "pcs";

        public PackagingPurchaseViewModel(AppDataService dataService, Action<string> navigateAction, Action<string, object?> openDialogAction)
            : base(dataService, navigateAction, openDialogAction)
        {
            ResetForm();
        }

        public override void ReloadAvailableItems()
        {
            AvailableItems.Clear();
            if (_dataService.GeneralItems != null && _dataService.GeneralItems.Any())
            {
                foreach (var g in _dataService.GeneralItems)
                {
                    AvailableItems.Add(new SourcingItemOption
                    {
                        Id = g.Id,
                        Name = g.Name,
                        Sku = g.Sku,
                        Unit = g.Unit,
                        Rate = g.Rate,
                        Gst = g.Gst,
                        Stock = g.Stock
                    });
                }
            }
            else
            {
                AvailableItems.Add(new SourcingItemOption { Id = "gen_shoe_box", Name = "Branded Footwear Shoe Box", Sku = "ASI-PKG-001", Unit = "pcs", Rate = 14, Gst = 18 });
                AvailableItems.Add(new SourcingItemOption { Id = "gen_poly_bag", Name = "Uniform Garment Poly Cover", Sku = "ASI-PKG-002", Unit = "pcs", Rate = 3, Gst = 18 });
                AvailableItems.Add(new SourcingItemOption { Id = "gen_tape", Name = "Packaging Brown BOPP Tape", Sku = "ASI-PKG-003", Unit = "roll", Rate = 65, Gst = 18 });
                AvailableItems.Add(new SourcingItemOption { Id = "gen_carton", Name = "Corrugated Master Carton (24 prs)", Sku = "ASI-PKG-004", Unit = "pcs", Rate = 85, Gst = 18 });
                AvailableItems.Add(new SourcingItemOption { Id = "gen_hang_tag", Name = "Product Price / Brand Hang Tag", Sku = "ASI-PKG-005", Unit = "pcs", Rate = 2, Gst = 18 });
            }
        }
    }
}
