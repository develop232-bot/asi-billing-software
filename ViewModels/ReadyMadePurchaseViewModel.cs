using System;
using System.Linq;
using AsiErp.Services;

namespace AsiErp.ViewModels
{
    public class ReadyMadePurchaseViewModel : PurchaseEntryViewModelBase
    {
        public override string Kind => "ready";
        public override string Title => "Readymade Stock Purchase";
        public override string Subtitle => "Trading goods bought from outside vendors to resell to domestic and export customers.";
        public override string CategoryBadge => "READYMADE STOCK · TRADING GOODS";
        public override string DefaultUnit => "pair";

        public ReadyMadePurchaseViewModel(AppDataService dataService, Action<string> navigateAction, Action<string, object?> openDialogAction)
            : base(dataService, navigateAction, openDialogAction)
        {
            ResetForm();
        }

        public override void ReloadAvailableItems()
        {
            AvailableItems.Clear();
            var prods = _dataService.Products?.ToList();
            if (prods != null && prods.Any())
            {
                foreach (var p in prods)
                {
                    double buyRate = p.Cost > 0 ? p.Cost : (p.BulkPrice > 0 ? p.BulkPrice : (p.RetailPrice > 0 ? p.RetailPrice * 0.6 : 100));
                    AvailableItems.Add(new SourcingItemOption
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Sku = p.Sku,
                        Unit = string.IsNullOrWhiteSpace(p.Unit) ? "pair" : p.Unit,
                        Rate = Math.Round(buyRate, 2),
                        SellingPrice = p.RetailPrice,
                        Gst = p.Gst > 0 ? p.Gst : 5,
                        Stock = p.Stock
                    });
                }
            }
            else
            {
                AvailableItems.Add(new SourcingItemOption { Id = "rdy_formal_shoe", Name = "Formal Derby Leather Shoe", Sku = "ASI-RDY-001", Unit = "pair", Rate = 450, SellingPrice = 999, Gst = 5 });
                AvailableItems.Add(new SourcingItemOption { Id = "rdy_school_shoe", Name = "School Uniform Shoe Black", Sku = "ASI-RDY-002", Unit = "pair", Rate = 280, SellingPrice = 599, Gst = 5 });
                AvailableItems.Add(new SourcingItemOption { Id = "rdy_sports_shoe", Name = "Sports Running Shoe", Sku = "ASI-RDY-003", Unit = "pair", Rate = 380, SellingPrice = 799, Gst = 5 });
                AvailableItems.Add(new SourcingItemOption { Id = "rdy_safety_shoe", Name = "Industrial Steel Toe Safety Shoe", Sku = "ASI-RDY-004", Unit = "pair", Rate = 620, SellingPrice = 1299, Gst = 18 });
                AvailableItems.Add(new SourcingItemOption { Id = "rdy_uniform_shirt", Name = "School Uniform Half Shirt", Sku = "ASI-RDY-005", Unit = "pcs", Rate = 180, SellingPrice = 350, Gst = 5 });
                AvailableItems.Add(new SourcingItemOption { Id = "rdy_uniform_pant", Name = "School Uniform Navy Pant", Sku = "ASI-RDY-006", Unit = "pcs", Rate = 240, SellingPrice = 490, Gst = 5 });
                AvailableItems.Add(new SourcingItemOption { Id = "rdy_socks_pack", Name = "Cotton Socks (Pack of 3)", Sku = "ASI-RDY-007", Unit = "pair", Rate = 45, SellingPrice = 99, Gst = 5 });
            }

            var sorted = AvailableItems.OrderBy(x => AsiErp.Helpers.NaturalSortHelper.GetSkuSortKey(x.Sku)).ThenBy(x => x.Name).ToList();
            AvailableItems.Clear();
            foreach (var it in sorted) AvailableItems.Add(it);
        }
    }
}
