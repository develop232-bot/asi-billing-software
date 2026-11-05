using System;
using System.Collections.Generic;
using AsiErp.Models;

namespace AsiErp.Services
{
    public static class SeedData
    {
        public static List<RawMaterialItem> CreateRawMaterials() => new()
        {
            new() { Id = "r1", Name = "Full-grain leather", Unit = "sq ft", Stock = 5200, ReorderLevel = 2500, Rate = 48, Gst = 5 },
            new() { Id = "r2", Name = "PU synthetic leather", Unit = "m", Stock = 620, ReorderLevel = 300, Rate = 145, Gst = 5 },
            new() { Id = "r3", Name = "Rubber sole", Unit = "pair", Stock = 1800, ReorderLevel = 800, Rate = 85, Gst = 18 },
            new() { Id = "r4", Name = "Insole", Unit = "pair", Stock = 2400, ReorderLevel = 1000, Rate = 18, Gst = 5 },
            new() { Id = "r5", Name = "Lining fabric", Unit = "m", Stock = 900, ReorderLevel = 400, Rate = 60, Gst = 5 },
            new() { Id = "r6", Name = "Shoe laces", Unit = "pair", Stock = 3000, ReorderLevel = 1200, Rate = 6, Gst = 5 },
            new() { Id = "r7", Name = "Sewing thread", Unit = "cone", Stock = 85, ReorderLevel = 40, Rate = 120, Gst = 5 },
            new() { Id = "r8", Name = "Adhesive / glue", Unit = "kg", Stock = 12, ReorderLevel = 25, Rate = 260, Gst = 18 },
            new() { Id = "r9", Name = "Steel toe cap", Unit = "pair", Stock = 40, ReorderLevel = 100, Rate = 70, Gst = 18 },
            new() { Id = "r10", Name = "Shirting fabric", Unit = "m", Stock = 2600, ReorderLevel = 1200, Rate = 95, Gst = 5 },
            new() { Id = "r11", Name = "Trouser / coverall fabric", Unit = "m", Stock = 3400, ReorderLevel = 1500, Rate = 140, Gst = 5 },
            new() { Id = "r12", Name = "Buttons", Unit = "pcs", Stock = 9000, ReorderLevel = 4000, Rate = 1, Gst = 5 },
            new() { Id = "r13", Name = "Zippers", Unit = "pcs", Stock = 900, ReorderLevel = 400, Rate = 8, Gst = 5 },
            new() { Id = "r14", Name = "Velcro tape", Unit = "m", Stock = 300, ReorderLevel = 150, Rate = 22, Gst = 5 }
        };

        public static List<GeneralItem> CreateGeneralItems() => new()
        {
            new() { Id = "g1", Name = "Shoe box – ASI branded", Unit = "pcs", Stock = 1400, ReorderLevel = 800, Rate = 14, Gst = 5 },
            new() { Id = "g2", Name = "Uniform poly cover", Unit = "pcs", Stock = 900, ReorderLevel = 600, Rate = 3, Gst = 18 },
            new() { Id = "g3", Name = "Tissue paper for inside shoe", Unit = "pack", Stock = 30, ReorderLevel = 40, Rate = 85, Gst = 5 },
            new() { Id = "g4", Name = "ASI logo customer bag", Unit = "pcs", Stock = 600, ReorderLevel = 300, Rate = 9, Gst = 18 },
            new() { Id = "g5", Name = "Single shoe dust cover", Unit = "pcs", Stock = 2200, ReorderLevel = 1000, Rate = 2.5, Gst = 18 },
            new() { Id = "g6", Name = "Master carton (12 pair)", Unit = "pcs", Stock = 260, ReorderLevel = 150, Rate = 38, Gst = 5 },
            new() { Id = "g7", Name = "Hang tags", Unit = "pcs", Stock = 4000, ReorderLevel = 2000, Rate = 0.8, Gst = 5 },
            new() { Id = "g8", Name = "Packing tape", Unit = "roll", Stock = 48, ReorderLevel = 24, Rate = 55, Gst = 18 }
        };

        public static List<ProductItem> CreateProducts()
        {
            var shoeSizes = new List<string> { "6", "7", "8", "9", "10", "11" };
            var unifSizes = new List<string> { "S", "M", "L", "XL", "XXL" };

            return new()
            {
                new()
                {
                    Id = "p1", Sku = "ASI-SH-001", Barcode = "8906401000018", Name = "School Shoe – Black Lace-up", Category = "Shoe", SourceType = "M", Unit = "pair",
                    Sizes = new(shoeSizes), Hsn = "6403", Gst = 5, Cost = 0, RetailPrice = 649, BulkPrice = 549, DomesticPrice = 310, ExportPrice = 4.6,
                    Stock = 420, ReorderLevel = 150, PackItemId = "g1",
                    Bom = new()
                    {
                        new() { Id = "r1", Name = "Full-grain leather", Unit = "sq ft", Qty = 1.2 },
                        new() { Id = "r3", Name = "Rubber sole", Unit = "pair", Qty = 1 },
                        new() { Id = "r4", Name = "Insole", Unit = "pair", Qty = 1 },
                        new() { Id = "r5", Name = "Lining fabric", Unit = "m", Qty = 0.15 },
                        new() { Id = "r6", Name = "Shoe laces", Unit = "pair", Qty = 1 },
                        new() { Id = "r7", Name = "Sewing thread", Unit = "cone", Qty = 0.02 },
                        new() { Id = "r8", Name = "Adhesive / glue", Unit = "kg", Qty = 0.03 }
                    }
                },
                new()
                {
                    Id = "p2", Sku = "ASI-SH-002", Barcode = "8906401000025", Name = "Safety Shoe – Steel Toe", Category = "Shoe", SourceType = "M", Unit = "pair",
                    Sizes = new(shoeSizes), Hsn = "6403", Gst = 5, Cost = 0, RetailPrice = 1299, BulkPrice = 1099, DomesticPrice = 640, ExportPrice = 9.5,
                    Stock = 260, ReorderLevel = 100, PackItemId = "g1",
                    Bom = new()
                    {
                        new() { Id = "r1", Name = "Full-grain leather", Unit = "sq ft", Qty = 1.6 },
                        new() { Id = "r3", Name = "Rubber sole", Unit = "pair", Qty = 1 },
                        new() { Id = "r9", Name = "Steel toe cap", Unit = "pair", Qty = 1 },
                        new() { Id = "r4", Name = "Insole", Unit = "pair", Qty = 1 },
                        new() { Id = "r5", Name = "Lining fabric", Unit = "m", Qty = 0.2 },
                        new() { Id = "r6", Name = "Shoe laces", Unit = "pair", Qty = 1 },
                        new() { Id = "r7", Name = "Sewing thread", Unit = "cone", Qty = 0.03 },
                        new() { Id = "r8", Name = "Adhesive / glue", Unit = "kg", Qty = 0.05 }
                    }
                },
                new()
                {
                    Id = "p3", Sku = "ASI-SH-003", Barcode = "8906401000032", Name = "Formal Derby – Men", Category = "Shoe", SourceType = "M", Unit = "pair",
                    Sizes = new(shoeSizes), Hsn = "6403", Gst = 5, Cost = 0, RetailPrice = 999, BulkPrice = 849, DomesticPrice = 470, ExportPrice = 6.8,
                    Stock = 180, ReorderLevel = 80, PackItemId = "g1",
                    Bom = new()
                    {
                        new() { Id = "r1", Name = "Full-grain leather", Unit = "sq ft", Qty = 1.5 },
                        new() { Id = "r3", Name = "Rubber sole", Unit = "pair", Qty = 1 },
                        new() { Id = "r4", Name = "Insole", Unit = "pair", Qty = 1 },
                        new() { Id = "r5", Name = "Lining fabric", Unit = "m", Qty = 0.2 },
                        new() { Id = "r6", Name = "Shoe laces", Unit = "pair", Qty = 1 },
                        new() { Id = "r7", Name = "Sewing thread", Unit = "cone", Qty = 0.02 },
                        new() { Id = "r8", Name = "Adhesive / glue", Unit = "kg", Qty = 0.04 }
                    }
                },
                new()
                {
                    Id = "p4", Sku = "ASI-SH-004", Barcode = "8906401000049", Name = "Sports Sneaker – White", Category = "Shoe", SourceType = "M", Unit = "pair",
                    Sizes = new(shoeSizes), Hsn = "6404", Gst = 5, Cost = 0, RetailPrice = 899, BulkPrice = 749, DomesticPrice = 360, ExportPrice = 5.3,
                    Stock = 95, ReorderLevel = 100, PackItemId = "g1",
                    Bom = new()
                    {
                        new() { Id = "r2", Name = "PU synthetic leather", Unit = "m", Qty = 0.3 },
                        new() { Id = "r3", Name = "Rubber sole", Unit = "pair", Qty = 1 },
                        new() { Id = "r4", Name = "Insole", Unit = "pair", Qty = 1 },
                        new() { Id = "r5", Name = "Lining fabric", Unit = "m", Qty = 0.2 },
                        new() { Id = "r6", Name = "Shoe laces", Unit = "pair", Qty = 1 },
                        new() { Id = "r7", Name = "Sewing thread", Unit = "cone", Qty = 0.02 },
                        new() { Id = "r8", Name = "Adhesive / glue", Unit = "kg", Qty = 0.04 }
                    }
                },
                new()
                {
                    Id = "p5", Sku = "ASI-SH-005", Barcode = "8906401000056", Name = "Kids Velcro School Shoe", Category = "Shoe", SourceType = "M", Unit = "pair",
                    Sizes = new() { "9", "10", "11", "12", "13", "1", "2" }, Hsn = "6403", Gst = 5, Cost = 0, RetailPrice = 499, BulkPrice = 429, DomesticPrice = 250, ExportPrice = 3.6,
                    Stock = 210, ReorderLevel = 100, PackItemId = "g1",
                    Bom = new()
                    {
                        new() { Id = "r2", Name = "PU synthetic leather", Unit = "m", Qty = 0.2 },
                        new() { Id = "r3", Name = "Rubber sole", Unit = "pair", Qty = 1 },
                        new() { Id = "r4", Name = "Insole", Unit = "pair", Qty = 1 },
                        new() { Id = "r5", Name = "Lining fabric", Unit = "m", Qty = 0.1 },
                        new() { Id = "r14", Name = "Velcro tape", Unit = "m", Qty = 0.1 },
                        new() { Id = "r7", Name = "Sewing thread", Unit = "cone", Qty = 0.02 },
                        new() { Id = "r8", Name = "Adhesive / glue", Unit = "kg", Qty = 0.03 }
                    }
                },
                new()
                {
                    Id = "p6", Sku = "ASI-UN-006", Barcode = "8906201000062", Name = "School Uniform Set (Shirt + Pant)", Category = "Uniform", SourceType = "M", Unit = "set",
                    Sizes = new(unifSizes), Hsn = "6211", Gst = 5, Cost = 0, RetailPrice = 799, BulkPrice = 699, DomesticPrice = 380, ExportPrice = 5.4,
                    Stock = 340, ReorderLevel = 120, PackItemId = "g2",
                    Bom = new()
                    {
                        new() { Id = "r10", Name = "Shirting fabric", Unit = "m", Qty = 1.3 },
                        new() { Id = "r11", Name = "Trouser / coverall fabric", Unit = "m", Qty = 1.1 },
                        new() { Id = "r12", Name = "Buttons", Unit = "pcs", Qty = 8 },
                        new() { Id = "r13", Name = "Zippers", Unit = "pcs", Qty = 1 },
                        new() { Id = "r7", Name = "Sewing thread", Unit = "cone", Qty = 0.05 }
                    }
                },
                new()
                {
                    Id = "p7", Sku = "ASI-UN-007", Barcode = "8906201000079", Name = "Industrial Coverall", Category = "Uniform", SourceType = "M", Unit = "pcs",
                    Sizes = new(unifSizes), Hsn = "6211", Gst = 5, Cost = 0, RetailPrice = 1199, BulkPrice = 1049, DomesticPrice = 560, ExportPrice = 7.6,
                    Stock = 150, ReorderLevel = 80, PackItemId = "g2",
                    Bom = new()
                    {
                        new() { Id = "r11", Name = "Trouser / coverall fabric", Unit = "m", Qty = 2.8 },
                        new() { Id = "r13", Name = "Zippers", Unit = "pcs", Qty = 1 },
                        new() { Id = "r12", Name = "Buttons", Unit = "pcs", Qty = 6 },
                        new() { Id = "r7", Name = "Sewing thread", Unit = "cone", Qty = 0.08 }
                    }
                },
                new()
                {
                    Id = "p8", Sku = "ASI-UN-008", Barcode = "8906201000086", Name = "Hospital Scrub Set", Category = "Uniform", SourceType = "M", Unit = "set",
                    Sizes = new(unifSizes), Hsn = "6211", Gst = 5, Cost = 0, RetailPrice = 899, BulkPrice = 779, DomesticPrice = 420, ExportPrice = 5.8,
                    Stock = 60, ReorderLevel = 80, PackItemId = "g2",
                    Bom = new()
                    {
                        new() { Id = "r10", Name = "Shirting fabric", Unit = "m", Qty = 2.2 },
                        new() { Id = "r12", Name = "Buttons", Unit = "pcs", Qty = 6 },
                        new() { Id = "r7", Name = "Sewing thread", Unit = "cone", Qty = 0.05 }
                    }
                },
                new()
                {
                    Id = "p9", Sku = "ASI-SH-009", Barcode = "8906412000092", Name = "Rubber Slippers (ready-made)", Category = "Shoe", SourceType = "R", Unit = "pair",
                    Sizes = new(shoeSizes), Hsn = "6402", Gst = 5, Cost = 95, RetailPrice = 199, BulkPrice = 169, DomesticPrice = 130, ExportPrice = 2.2,
                    Stock = 500, ReorderLevel = 150
                },
                new()
                {
                    Id = "p10", Sku = "ASI-AC-010", Barcode = "8904202000108", Name = "Cotton Socks – 3 pack", Category = "Accessory", SourceType = "R", Unit = "pack",
                    Sizes = new() { "Free size" }, Hsn = "6115", Gst = 5, Cost = 60, RetailPrice = 149, BulkPrice = 125, DomesticPrice = 85, ExportPrice = 1.6,
                    Stock = 800, ReorderLevel = 200
                },
                new()
                {
                    Id = "p11", Sku = "ASI-AC-011", Barcode = "8904202000115", Name = "Safety Gloves", Category = "Accessory", SourceType = "R", Unit = "pair",
                    Sizes = new() { "Free size" }, Hsn = "6216", Gst = 12, Cost = 40, RetailPrice = 99, BulkPrice = 79, DomesticPrice = 55, ExportPrice = 1.1,
                    Stock = 300, ReorderLevel = 100
                },
                new()
                {
                    Id = "p12", Sku = "ASI-AC-012", Barcode = "8904202000122", Name = "Leather Belt", Category = "Accessory", SourceType = "R", Unit = "pcs",
                    Sizes = new() { "32", "34", "36", "38", "40" }, Hsn = "4203", Gst = 18, Cost = 110, RetailPrice = 299, BulkPrice = 249, DomesticPrice = 175, ExportPrice = 3.0,
                    Stock = 40, ReorderLevel = 60
                }
            };
        }

        public static void PopulateTransactions(AppDataService ds)
        {
            var now = DateTime.Now;
            var fy = ds.GetFinancialYear();

            // Investments
            ds.Investments.Add(new() { Id = "inv1", Date = now.AddMonths(-5).ToString("yyyy-MM-05"), InvestedBy = "Promoter – Director 1", Purpose = "Stitching & lasting machinery", Amount = 1850000, IsPreStart = true });
            ds.Investments.Add(new() { Id = "inv2", Date = now.AddMonths(-5).ToString("yyyy-MM-12"), InvestedBy = "Promoter – Director 2", Purpose = "Factory advance & interiors", Amount = 600000, IsPreStart = true });
            ds.Investments.Add(new() { Id = "inv3", Date = now.AddMonths(-4).ToString("yyyy-MM-02"), InvestedBy = "Bank term loan", Purpose = "Opening raw material stock", Amount = 900000, IsPreStart = true });
            ds.Investments.Add(new() { Id = "inv4", Date = now.AddMonths(-4).ToString("yyyy-MM-18"), InvestedBy = "Promoter – Director 1", Purpose = "Working capital cash", Amount = 500000, IsPreStart = true });
            ds.Investments.Add(new() { Id = "inv5", Date = now.AddMonths(-3).ToString("yyyy-MM-10"), InvestedBy = "Promoter – Director 2", Purpose = "Legal, GST registration & licences", Amount = 85000, IsPreStart = true });

            // Expenses across past months
            for (int m = 5; m >= 0; m--)
            {
                var dt = now.AddMonths(-m);
                string moDate = dt.ToString("yyyy-MM-28");
                string mName = dt.ToString("MMM");

                ds.Expenses.Add(new() { Id = $"e_sal_{m}", Date = moDate, Category = "Salary", Frequency = "Monthly", Description = $"Staff salary – {mName}", Amount = 218000 + m * 2500, PaymentMode = "Bank" });
                ds.Expenses.Add(new() { Id = $"e_eb_{m}", Date = dt.ToString("yyyy-MM-10"), Category = "EB (Electricity)", Frequency = "Monthly", Description = "TNEB bill", Amount = 38500, PaymentMode = "Bank" });
                ds.Expenses.Add(new() { Id = $"e_wtr_{m}", Date = dt.ToString("yyyy-MM-12"), Category = "Water Bill", Frequency = "Monthly", Description = "Water charges", Amount = 3100, PaymentMode = "Bank" });
                ds.Expenses.Add(new() { Id = $"e_rent_{m}", Date = dt.ToString("yyyy-MM-01"), Category = "Rent", Frequency = "Monthly", Description = "Factory rent", Amount = 40000, PaymentMode = "Bank" });
                ds.Expenses.Add(new() { Id = $"e_esipf_{m}", Date = dt.ToString("yyyy-MM-15"), Category = "ESI / PF", Frequency = "Monthly", Description = "ESI & PF contribution", Amount = 21800, PaymentMode = "Bank" });

                ds.Expenses.Add(new() { Id = $"e_tea_{m}", Date = dt.ToString("yyyy-MM-08"), Category = "Everyday Exp", Frequency = "Daily", Description = "Tea & snacks for factory staff", Amount = 850, PaymentMode = "Cash" });
                ds.Expenses.Add(new() { Id = $"e_tr_{m}", Date = dt.ToString("yyyy-MM-16"), Category = "Transport & Freight", Frequency = "Daily", Description = "Local auto transport for materials", Amount = 1400, PaymentMode = "UPI" });
                ds.Expenses.Add(new() { Id = $"e_rep_{m}", Date = dt.ToString("yyyy-MM-22"), Category = "Repairs & Maintenance", Frequency = "Daily", Description = "Machine needle replacement & oiling", Amount = 1200, PaymentMode = "Cash" });
            }

            // Work Orders
            ds.WorkOrders.Add(new()
            {
                Id = "wo1", OrderNo = "WO-0001", Customer = "Sri Balaji Schools Group", OrderType = "domestic",
                OrderDate = now.AddDays(-30).ToString("yyyy-MM-dd"), DueDate = now.AddDays(-23).ToString("yyyy-MM-dd"),
                StartDate = now.AddDays(-29).ToString("yyyy-MM-dd"), EndDate = now.AddDays(-24).ToString("yyyy-MM-dd"),
                Status = "Completed", IsDone = true, Notes = "Completed on time.",
                Items = new() { new() { ProductId = "p1", ProductName = "School Shoe – Black Lace-up", Size = "8", Unit = "pair", Qty = 300 } },
                Materials = new() { new() { ItemId = "r1", Name = "Full-grain leather", Unit = "sq ft", Qty = 360 }, new() { ItemId = "r3", Name = "Rubber sole", Unit = "pair", Qty = 300 } }
            });

            ds.WorkOrders.Add(new()
            {
                Id = "wo2", OrderNo = "WO-0002", Customer = "Al Noor Trading LLC", OrderType = "export",
                OrderDate = now.AddDays(-15).ToString("yyyy-MM-dd"), DueDate = now.AddDays(-5).ToString("yyyy-MM-dd"),
                StartDate = now.AddDays(-14).ToString("yyyy-MM-dd"), EndDate = now.AddDays(-6).ToString("yyyy-MM-dd"),
                Status = "Completed", IsDone = true, Notes = "Export lot finished.",
                Items = new() { new() { ProductId = "p2", ProductName = "Safety Shoe – Steel Toe", Size = "9", Unit = "pair", Qty = 200 } }
            });

            ds.WorkOrders.Add(new()
            {
                Id = "wo3", OrderNo = "WO-0003", Customer = "Gulf Safety Wear FZE", OrderType = "export",
                OrderDate = now.AddDays(-6).ToString("yyyy-MM-dd"), DueDate = now.AddDays(4).ToString("yyyy-MM-dd"),
                StartDate = now.AddDays(-5).ToString("yyyy-MM-dd"), Status = "Lasting & pressing", IsDone = false, Notes = "In assembly.",
                Items = new() { new() { ProductId = "p2", ProductName = "Safety Shoe – Steel Toe", Size = "10", Unit = "pair", Qty = 150 } }
            });

            ds.WorkOrders.Add(new()
            {
                Id = "wo4", OrderNo = "WO-0004", Customer = "Sunrise Public School", OrderType = "domestic",
                OrderDate = now.AddDays(-4).ToString("yyyy-MM-dd"), DueDate = now.AddDays(5).ToString("yyyy-MM-dd"),
                StartDate = now.AddDays(-3).ToString("yyyy-MM-dd"), Status = "Cutting", IsDone = false, Notes = "Fabric cutting ongoing.",
                Items = new() { new() { ProductId = "p6", ProductName = "School Uniform Set", Size = "M", Unit = "set", Qty = 200 } }
            });

            ds.WorkOrders.Add(new()
            {
                Id = "wo5", OrderNo = "WO-0005", Customer = "", OrderType = "general",
                OrderDate = now.AddDays(-1).ToString("yyyy-MM-dd"), DueDate = now.AddDays(7).ToString("yyyy-MM-dd"),
                Status = "Pending", IsDone = false, Notes = "Awaiting sole delivery.",
                Items = new() { new() { ProductId = "p4", ProductName = "Sports Sneaker – White", Size = "8", Unit = "pair", Qty = 200 } }
            });

            // Purchases
            ds.Purchases.Add(new()
            {
                Id = "b1", Kind = "raw", BillNo = "PR-RM-0001", VendorBillRef = "INV-7841",
                Date = now.AddDays(-14).ToString("yyyy-MM-dd"), Party = "Ambur Leather House", Phone = "9843211001",
                SaleType = "Credit", CreditDays = 30, PaymentDueDate = now.AddDays(16).ToString("yyyy-MM-dd"),
                NetAmount = 120000, TaxAmount = 6000, GrossAmount = 126000, PaidAmount = 60000,
                Lines = new() { new() { ItemId = "r1", Name = "Full-grain leather", Qty = 2500, Unit = "sq ft", Rate = 48, GstPercent = 5, NetAmount = 120000, TaxAmount = 6000, GrossAmount = 126000 } }
            });

            ds.Purchases.Add(new()
            {
                Id = "b2", Kind = "raw", BillNo = "PR-RM-0002", VendorBillRef = "INV-4412",
                Date = now.AddDays(-7).ToString("yyyy-MM-dd"), Party = "Sri Vinayaga Rubber Soles", Phone = "9843211002",
                SaleType = "Cash", CreditDays = 0, PaymentDueDate = now.AddDays(-7).ToString("yyyy-MM-dd"),
                NetAmount = 85000, TaxAmount = 15300, GrossAmount = 100300, PaidAmount = 100300,
                Lines = new() { new() { ItemId = "r3", Name = "Rubber sole", Qty = 1000, Unit = "pair", Rate = 85, GstPercent = 18, NetAmount = 85000, TaxAmount = 15300, GrossAmount = 100300 } }
            });

            ds.Purchases.Add(new()
            {
                Id = "b3", Kind = "ready", BillNo = "PR-RP-0001", VendorBillRef = "INV-9932",
                Date = now.AddDays(-5).ToString("yyyy-MM-dd"), Party = "Star Footwear Wholesalers", Phone = "9843212001",
                SaleType = "Cash", CreditDays = 0, PaymentDueDate = now.AddDays(-5).ToString("yyyy-MM-dd"),
                NetAmount = 38000, TaxAmount = 1900, GrossAmount = 39900, PaidAmount = 39900,
                Lines = new() { new() { ItemId = "p9", Name = "Rubber Slippers (ready-made)", Qty = 400, Unit = "pair", Rate = 95, GstPercent = 5, NetAmount = 38000, TaxAmount = 1900, GrossAmount = 39900 } }
            });

            // Sales (Domestic, Export, Retail)
            ds.Sales.Add(new()
            {
                Id = "s1", Type = "domestic", InvoiceNo = $"ASI/DOM/{fy}/0001",
                Date = now.AddDays(-18).ToString("yyyy-MM-dd"), Party = "Sri Balaji Schools Group", ContactPerson = "K. Ganesan", Phone = "9884012345",
                SaleType = "Credit", CreditDays = 30, PaymentDueDate = now.AddDays(12).ToString("yyyy-MM-dd"),
                NetAmount = 124000, TaxAmount = 6200, GrossAmount = 130200, PaidAmount = 130200,
                TaxType = "intra", CustomerGstin = "33ABCDE1234F1Z5", VehicleNo = "TN-09-AB-1234",
                Lines = new() { new() { Pid = "p1", Name = "School Shoe – Black Lace-up", Size = "8", Qty = 400, Unit = "pair", Rate = 310, GstPercent = 5, NetAmount = 124000, TaxAmount = 6200, GrossAmount = 130200 } }
            });

            ds.Sales.Add(new()
            {
                Id = "s2", Type = "export", InvoiceNo = $"ASI/EXP/{fy}/0001",
                Date = now.AddDays(-10).ToString("yyyy-MM-dd"), Party = "Al Noor Trading LLC", ContactPerson = "Ahmed Al Noor", Phone = "+971 50 123 4567",
                Country = "UAE", Currency = "AED", FxRate = 24.1, Incoterm = "FOB", PortOfLoading = "Chennai", PortOfDischarge = "Jebel Ali",
                SaleType = "Credit", CreditDays = 45, PaymentDueDate = now.AddDays(35).ToString("yyyy-MM-dd"),
                NetAmount = 190000, TaxAmount = 0, GrossAmount = 190000, GrossAmountCurrency = 7883.82, PaidAmount = 95000,
                Lines = new() { new() { Pid = "p2", Name = "Safety Shoe – Steel Toe", Size = "9", Qty = 250, Unit = "pair", Rate = 34.88, GstPercent = 0, NetAmount = 190000, TaxAmount = 0, GrossAmount = 190000 } }
            });

            ds.Sales.Add(new()
            {
                Id = "s3", Type = "retail", InvoiceNo = "RB-0001",
                Date = now.ToString("yyyy-MM-dd"), Party = "Ramesh Kumar", Phone = "9884567001", Event = "Showroom",
                PaymentMode = "UPI", NetAmount = 1237.14, TaxAmount = 61.86, GrossAmount = 1298, PaidAmount = 1298,
                Lines = new() { new() { Pid = "p1", Name = "School Shoe – Black Lace-up", Size = "7", Qty = 2, Unit = "pair", Rate = 649, GstPercent = 5, NetAmount = 1237.14, TaxAmount = 61.86, GrossAmount = 1298 } }
            });

            ds.Sales.Add(new()
            {
                Id = "s4", Type = "retail", InvoiceNo = "RB-0002",
                Date = now.ToString("yyyy-MM-dd"), Party = "Priya Sharma", Phone = "9884567002", Event = "Showroom",
                PaymentMode = "Cash", NetAmount = 1237.14, TaxAmount = 61.86, GrossAmount = 1299, PaidAmount = 1299,
                Lines = new() { new() { Pid = "p2", Name = "Safety Shoe – Steel Toe", Size = "8", Qty = 1, Unit = "pair", Rate = 1299, GstPercent = 5, NetAmount = 1237.14, TaxAmount = 61.86, GrossAmount = 1299 } }
            });

            ds.Sequences["domestic"] = 1;
            ds.Sequences["export"] = 1;
            ds.Sequences["retail"] = 2;
            ds.Sequences["raw"] = 2;
            ds.Sequences["ready"] = 1;
            ds.Sequences["wo"] = 5;
        }
    }
}
