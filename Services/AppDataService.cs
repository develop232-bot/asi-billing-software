using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AsiErp.Models;

namespace AsiErp.Services
{
    public class AppDataService
    {
        private static AppDataService? _instance;
        public static AppDataService Instance => _instance ??= new AppDataService();

        public FirebaseService Firebase { get; }
        public SyncQueueService SyncQueue { get; }
        public AuthService Auth { get; }

        public List<ProductItem> Products { get; set; } = new();
        public List<RawMaterialItem> RawMaterials { get; set; } = new();
        public List<GeneralItem> GeneralItems { get; set; } = new();
        public List<SaleDoc> Sales { get; set; } = new();
        public List<PurchaseDoc> Purchases { get; set; } = new();
        public List<WorkOrderDoc> WorkOrders { get; set; } = new();
        public List<ExpenseItem> Expenses { get; set; } = new();
        public List<InvestmentItem> Investments { get; set; } = new();
        public Dictionary<string, int> Sequences { get; set; } = new();

        public bool IsSampleDataLoaded { get; set; } = true;

        public event Action? DataChanged;

        private readonly string _localStateFile;

        public AppDataService()
        {
            Firebase = new FirebaseService();
            SyncQueue = new SyncQueueService(Firebase);
            Auth = new AuthService(Firebase, SyncQueue);

            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AsiErp");
            Directory.CreateDirectory(folder);
            _localStateFile = Path.Combine(folder, "asi_erp_state.json");

            LoadState();
        }

        public string GetFinancialYear()
        {
            int yr = DateTime.Now.Month >= 4 ? DateTime.Now.Year : DateTime.Now.Year - 1;
            return $"{yr}-{(yr + 1) % 100:D2}";
        }

        public string NextDocNo(string type, bool increment = true)
        {
            Sequences.TryGetValue(type, out int cur);
            int next = cur + 1;
            if (increment)
            {
                Sequences[type] = next;
                SaveLocalState();
                if (!IsSampleDataLoaded)
                {
                    SyncQueue.EnqueueSet("meta", "sequences", Sequences);
                }
            }

            string fy = GetFinancialYear();
            return type switch
            {
                "retail" => $"RB-{next:D4}",
                "domestic" => $"ASI/DOM/{fy}/{next:D4}",
                "export" => $"ASI/EXP/{fy}/{next:D4}",
                "raw" => $"PR-RM-{next:D4}",
                "ready" => $"PR-RP-{next:D4}",
                "gen" => $"PR-GN-{next:D4}",
                "wo" => $"WO-{next:D4}",
                _ => $"DOC-{next:D4}"
            };
        }

        public void NotifyDataChanged()
        {
            SaveLocalState();
            DataChanged?.Invoke();
        }

        public void SaveLocalState()
        {
            try
            {
                var payload = new
                {
                    Products,
                    RawMaterials,
                    GeneralItems,
                    Sales,
                    Purchases,
                    WorkOrders,
                    Expenses,
                    Investments,
                    Sequences,
                    IsSampleDataLoaded
                };
                string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_localStateFile, json);
            }
            catch { }
        }

        public void LoadState()
        {
            try
            {
                if (File.Exists(_localStateFile))
                {
                    string json = File.ReadAllText(_localStateFile);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("Products", out var pElem))
                        Products = JsonSerializer.Deserialize<List<ProductItem>>(pElem.GetRawText()) ?? new();
                    if (root.TryGetProperty("RawMaterials", out var rmElem))
                        RawMaterials = JsonSerializer.Deserialize<List<RawMaterialItem>>(rmElem.GetRawText()) ?? new();
                    if (root.TryGetProperty("GeneralItems", out var gElem))
                        GeneralItems = JsonSerializer.Deserialize<List<GeneralItem>>(gElem.GetRawText()) ?? new();
                    if (root.TryGetProperty("Sales", out var sElem))
                        Sales = JsonSerializer.Deserialize<List<SaleDoc>>(sElem.GetRawText()) ?? new();
                    if (root.TryGetProperty("Purchases", out var puElem))
                        Purchases = JsonSerializer.Deserialize<List<PurchaseDoc>>(puElem.GetRawText()) ?? new();
                    if (root.TryGetProperty("WorkOrders", out var wElem))
                        WorkOrders = JsonSerializer.Deserialize<List<WorkOrderDoc>>(wElem.GetRawText()) ?? new();
                    if (root.TryGetProperty("Expenses", out var eElem))
                        Expenses = JsonSerializer.Deserialize<List<ExpenseItem>>(eElem.GetRawText()) ?? new();
                    if (root.TryGetProperty("Investments", out var iElem))
                        Investments = JsonSerializer.Deserialize<List<InvestmentItem>>(iElem.GetRawText()) ?? new();
                    if (root.TryGetProperty("Sequences", out var sqElem))
                        Sequences = JsonSerializer.Deserialize<Dictionary<string, int>>(sqElem.GetRawText()) ?? new();
                    if (root.TryGetProperty("IsSampleDataLoaded", out var smElem))
                        IsSampleDataLoaded = smElem.GetBoolean();

                    for (int i = 0; i < Products.Count; i++)
                    {
                        if (string.IsNullOrEmpty(Products[i].Barcode))
                        {
                            Products[i].Barcode = BarcodeService.GenerateBarcode(Products[i].Category, Products[i].SourceType, i + 1);
                        }
                    }

                    if (!IsSampleDataLoaded)
                    {
                        return;
                    }

                    if (Products.Count > 0 || RawMaterials.Count > 0)
                    {
                        return;
                    }
                }
            }
            catch { }

            // Default startup: Demo Data
            LoadDemoData();
        }

        public void StartFresh()
        {
            Products.Clear();
            RawMaterials.Clear();
            GeneralItems.Clear();
            Sales.Clear();
            Purchases.Clear();
            WorkOrders.Clear();
            Expenses.Clear();
            Investments.Clear();
            Sequences.Clear();
            IsSampleDataLoaded = false;
            SyncQueue.ClearQueue();
            NotifyDataChanged();
        }

        public void LoadDemoData()
        {
            RawMaterials = SeedData.CreateRawMaterials();
            GeneralItems = SeedData.CreateGeneralItems();
            Products = SeedData.CreateProducts();
            Sales = new();
            Purchases = new();
            Expenses = new();
            Investments = new();
            WorkOrders = new();
            Sequences = new();

            SeedData.PopulateTransactions(this);

            IsSampleDataLoaded = true;
            SyncQueue.ClearQueue();
            NotifyDataChanged();
        }

        public void ReloadSampleData() => LoadDemoData();

        public async Task<(bool success, string message, int count)> LoadFromCloudDatabaseAsync()
        {
            if (!Firebase.IsConnected)
            {
                Firebase.TryInitialize();
            }

            if (!Firebase.IsConnected)
            {
                return (false, "Cannot connect to Firebase. Please verify firebase-key.json and your network.", 0);
            }

            try
            {
                var bundle = await Firebase.LoadAllFromCloudAsync();

                Products.Clear();
                RawMaterials.Clear();
                GeneralItems.Clear();
                Sales.Clear();
                Purchases.Clear();
                WorkOrders.Clear();
                Expenses.Clear();
                Investments.Clear();
                Sequences.Clear();

                Products = bundle.Products;
                RawMaterials = bundle.RawMaterials;
                GeneralItems = bundle.GeneralItems;
                Sales = bundle.Sales;
                Purchases = bundle.Purchases;
                WorkOrders = bundle.WorkOrders;
                Expenses = bundle.Expenses;
                Investments = bundle.Investments;
                Sequences = bundle.Sequences;

                Auth.MergeCloudUsers(bundle.Users);

                for (int i = 0; i < Products.Count; i++)
                {
                    if (string.IsNullOrEmpty(Products[i].Barcode))
                    {
                        Products[i].Barcode = BarcodeService.GenerateBarcode(Products[i].Category, Products[i].SourceType, i + 1);
                    }
                }

                IsSampleDataLoaded = false;
                SyncQueue.ClearQueue();
                NotifyDataChanged();

                string summary = bundle.TotalRecords > 0
                    ? $"Cloud DB loaded successfully: {Products.Count} products, {Sales.Count} sales, {Purchases.Count} purchases, {RawMaterials.Count} raw materials."
                    : $"Connected to Cloud Firestore ({Firebase.ProjectId}). Project currently has 0 records — ready for real data entry!";

                return (true, summary, bundle.TotalRecords);
            }
            catch (Exception ex)
            {
                return (false, $"Error reading Firestore: {ex.Message}", 0);
            }
        }

        public Task PushAllToCloudAsync()
        {
            foreach (var p in Products) SyncQueue.EnqueueSet("products", p.Id, p);
            foreach (var r in RawMaterials) SyncQueue.EnqueueSet("raw_materials", r.Id, r);
            foreach (var g in GeneralItems) SyncQueue.EnqueueSet("general_items", g.Id, g);
            foreach (var s in Sales) SyncQueue.EnqueueSet("sales", s.Id, s);
            foreach (var pu in Purchases) SyncQueue.EnqueueSet("purchases", pu.Id, pu);
            foreach (var w in WorkOrders) SyncQueue.EnqueueSet("work_orders", w.Id, w);
            foreach (var e in Expenses) SyncQueue.EnqueueSet("expenses", e.Id, e);
            foreach (var i in Investments) SyncQueue.EnqueueSet("investments", i.Id, i);
            foreach (var u in Auth.Users) SyncQueue.EnqueueSet("users", u.Id, u);
            SyncQueue.EnqueueSet("meta", "sequences", Sequences);
            return Task.CompletedTask;
        }

        public List<ExpenseItem> GetAllExpenses()
        {
            var list = new List<ExpenseItem>(Expenses);
            foreach (var b in Purchases)
            {
                string kindLbl = b.Kind switch
                {
                    "raw" => "Raw material",
                    "ready" => "Readymade product",
                    _ => "General"
                };
                list.Add(new ExpenseItem
                {
                    Id = "auto_" + b.Id,
                    Date = b.Date,
                    Category = "Purchase · " + kindLbl,
                    Frequency = "Purchase",
                    Description = $"{b.BillNo} · {b.Party}",
                    Amount = b.NetAmount,
                    IsAuto = true
                });
            }
            return list.OrderByDescending(x => x.Date).ToList();
        }

        public (double retail, double domestic, double export, double totalIncome, double totalExpense, double profit) GetFinancialTotals(string fromDate, string toDate)
        {
            double r = 0, d = 0, exp = 0;
            foreach (var s in Sales)
            {
                if (string.Compare(s.Date, fromDate) >= 0 && string.Compare(s.Date, toDate) <= 0)
                {
                    if (s.Type == "retail") r += s.NetAmount;
                    else if (s.Type == "domestic") d += s.NetAmount;
                    else if (s.Type == "export") exp += s.NetAmount;
                }
            }

            double totalInc = r + d + exp;
            double totalExp = GetAllExpenses()
                .Where(e => string.Compare(e.Date, fromDate) >= 0 && string.Compare(e.Date, toDate) <= 0)
                .Sum(e => e.Amount);

            return (r, d, exp, totalInc, totalExp, totalInc - totalExp);
        }

        public List<AppNotification> GetNotifications()
        {
            var list = new List<AppNotification>();
            string today = DateTime.Now.ToString("yyyy-MM-dd");

            foreach (var wo in WorkOrders.Where(o => !o.IsDone && o.Status != "Completed" && string.Compare(o.DueDate, today) < 0))
            {
                list.Add(new AppNotification
                {
                    Title = "Production overdue",
                    Subtitle = $"{wo.OrderNo} · due {wo.DueDate}",
                    Date = wo.DueDate,
                    RouteKey = "production",
                    BadgeColor = "#C23A32"
                });
            }

            foreach (var rm in RawMaterials.Where(r => r.Stock <= r.ReorderLevel))
            {
                list.Add(new AppNotification
                {
                    Title = "Low stock (Raw)",
                    Subtitle = $"{rm.Name} — {rm.Stock} {rm.Unit} left",
                    Date = today,
                    RouteKey = "stock:raw",
                    BadgeColor = "#E5A62E"
                });
            }

            foreach (var p in Products.Where(x => x.Stock <= x.ReorderLevel))
            {
                list.Add(new AppNotification
                {
                    Title = "Low stock",
                    Subtitle = $"{p.Name} — {p.Stock} {p.Unit} left",
                    Date = today,
                    RouteKey = p.IsManufactured ? "stock:fg" : "stock:ready",
                    BadgeColor = "#E5A62E"
                });
            }

            foreach (var s in Sales.Where(x => x.Status == "Unpaid").Take(10))
            {
                list.Add(new AppNotification
                {
                    Title = "Payment pending",
                    Subtitle = $"{s.InvoiceNo} · {s.Party}",
                    Date = s.Date,
                    RouteKey = "sales",
                    BadgeColor = "#3D62B8"
                });
            }

            return list.OrderByDescending(n => n.Date).ToList();
        }

        // --- BUSINESS ACTIONS (OFFLINE-FIRST LOCAL STORAGE + BACKGROUND SYNC QUEUE) ---

        public Task<SaleDoc> RecordSaleAsync(SaleDoc sale)
        {
            Sales.Insert(0, sale);

            foreach (var line in sale.Lines)
            {
                var p = Products.FirstOrDefault(x => x.Id == line.Pid);
                if (p != null)
                {
                    p.Stock = Math.Max(0, p.Stock - line.Qty);
                    if (!IsSampleDataLoaded) SyncQueue.EnqueueSet("products", p.Id, p);
                }
            }

            NotifyDataChanged();

            if (!IsSampleDataLoaded)
            {
                SyncQueue.EnqueueSet("sales", sale.Id, sale);
                SyncQueue.EnqueueSet("meta", "sequences", Sequences);
            }

            return Task.FromResult(sale);
        }

        public Task DeleteSaleAsync(string id)
        {
            Sales.RemoveAll(x => x.Id == id);
            NotifyDataChanged();
            if (!IsSampleDataLoaded)
            {
                SyncQueue.EnqueueDelete("sales", id);
            }
            return Task.CompletedTask;
        }

        public Task<PurchaseDoc> RecordPurchaseAsync(PurchaseDoc purchase)
        {
            Purchases.Insert(0, purchase);

            foreach (var line in purchase.Lines)
            {
                if (purchase.Kind == "raw")
                {
                    var r = RawMaterials.FirstOrDefault(x => x.Id == line.ItemId);
                    if (r != null)
                    {
                        r.Stock += line.Qty;
                        r.Rate = line.Rate;
                        if (!IsSampleDataLoaded) SyncQueue.EnqueueSet("raw_materials", r.Id, r);
                    }
                }
                else if (purchase.Kind == "ready")
                {
                    var p = Products.FirstOrDefault(x => x.Id == line.ItemId);
                    if (p != null)
                    {
                        p.Stock += line.Qty;
                        p.Cost = line.Rate;
                        if (!IsSampleDataLoaded) SyncQueue.EnqueueSet("products", p.Id, p);
                    }
                }
                else if (purchase.Kind == "gen")
                {
                    var g = GeneralItems.FirstOrDefault(x => x.Id == line.ItemId);
                    if (g != null)
                    {
                        g.Stock += line.Qty;
                        g.Rate = line.Rate;
                        if (!IsSampleDataLoaded) SyncQueue.EnqueueSet("general_items", g.Id, g);
                    }
                }
            }

            NotifyDataChanged();

            if (!IsSampleDataLoaded)
            {
                SyncQueue.EnqueueSet("purchases", purchase.Id, purchase);
                SyncQueue.EnqueueSet("meta", "sequences", Sequences);
            }

            return Task.FromResult(purchase);
        }

        public Task DeletePurchaseAsync(string id)
        {
            Purchases.RemoveAll(x => x.Id == id);
            NotifyDataChanged();
            if (!IsSampleDataLoaded)
            {
                SyncQueue.EnqueueDelete("purchases", id);
            }
            return Task.CompletedTask;
        }

        public Task RecordPaymentAsync(string collection, string id, double amount)
        {
            if (collection == "sales")
            {
                var s = Sales.FirstOrDefault(x => x.Id == id);
                if (s != null)
                {
                    s.PaidAmount = Math.Min(s.GrossAmount, s.PaidAmount + amount);
                    NotifyDataChanged();
                    if (!IsSampleDataLoaded)
                    {
                        SyncQueue.EnqueueSet("sales", s.Id, s);
                    }
                }
            }
            else if (collection == "purchases")
            {
                var p = Purchases.FirstOrDefault(x => x.Id == id);
                if (p != null)
                {
                    p.PaidAmount = Math.Min(p.GrossAmount, p.PaidAmount + amount);
                    NotifyDataChanged();
                    if (!IsSampleDataLoaded)
                    {
                        SyncQueue.EnqueueSet("purchases", p.Id, p);
                    }
                }
            }
            return Task.CompletedTask;
        }

        public Task SaveWorkOrderAsync(WorkOrderDoc wo, bool isNew)
        {
            if (isNew)
            {
                WorkOrders.Insert(0, wo);
            }
            else
            {
                int idx = WorkOrders.FindIndex(x => x.Id == wo.Id);
                if (idx >= 0) WorkOrders[idx] = wo;
                else WorkOrders.Insert(0, wo);
            }

            if (wo.Status == "Completed" && !wo.IsDone)
            {
                wo.IsDone = true;
                foreach (var it in wo.Items)
                {
                    var p = Products.FirstOrDefault(x => x.Id == it.ProductId);
                    if (p != null)
                    {
                        p.Stock += it.Qty;
                        if (!IsSampleDataLoaded) SyncQueue.EnqueueSet("products", p.Id, p);
                    }
                }
            }

            NotifyDataChanged();

            if (!IsSampleDataLoaded)
            {
                SyncQueue.EnqueueSet("work_orders", wo.Id, wo);
                SyncQueue.EnqueueSet("meta", "sequences", Sequences);
            }

            return Task.CompletedTask;
        }

        public Task DeleteWorkOrderAsync(string id)
        {
            WorkOrders.RemoveAll(x => x.Id == id);
            NotifyDataChanged();
            if (!IsSampleDataLoaded)
            {
                SyncQueue.EnqueueDelete("work_orders", id);
            }
            return Task.CompletedTask;
        }

        public Task AdjustStockAsync(string kind, string id, double change)
        {
            if (kind == "raw")
            {
                var r = RawMaterials.FirstOrDefault(x => x.Id == id);
                if (r != null)
                {
                    r.Stock = Math.Max(0, r.Stock + change);
                    if (!IsSampleDataLoaded) SyncQueue.EnqueueSet("raw_materials", r.Id, r);
                }
            }
            else if (kind == "gen")
            {
                var g = GeneralItems.FirstOrDefault(x => x.Id == id);
                if (g != null)
                {
                    g.Stock = Math.Max(0, g.Stock + change);
                    if (!IsSampleDataLoaded) SyncQueue.EnqueueSet("general_items", g.Id, g);
                }
            }
            else
            {
                var p = Products.FirstOrDefault(x => x.Id == id);
                if (p != null)
                {
                    p.Stock = Math.Max(0, p.Stock + change);
                    if (!IsSampleDataLoaded) SyncQueue.EnqueueSet("products", p.Id, p);
                }
            }

            NotifyDataChanged();
            return Task.CompletedTask;
        }

        public Task AddExpenseAsync(ExpenseItem exp)
        {
            Expenses.Insert(0, exp);
            NotifyDataChanged();
            if (!IsSampleDataLoaded)
            {
                SyncQueue.EnqueueSet("expenses", exp.Id, exp);
            }
            return Task.CompletedTask;
        }

        public Task DeleteExpenseAsync(string id)
        {
            Expenses.RemoveAll(x => x.Id == id);
            NotifyDataChanged();
            if (!IsSampleDataLoaded)
            {
                SyncQueue.EnqueueDelete("expenses", id);
            }
            return Task.CompletedTask;
        }

        public Task AddInvestmentAsync(InvestmentItem inv)
        {
            Investments.Insert(0, inv);
            NotifyDataChanged();
            if (!IsSampleDataLoaded)
            {
                SyncQueue.EnqueueSet("investments", inv.Id, inv);
            }
            return Task.CompletedTask;
        }

        public Task DeleteInvestmentAsync(string id)
        {
            Investments.RemoveAll(x => x.Id == id);
            NotifyDataChanged();
            if (!IsSampleDataLoaded)
            {
                SyncQueue.EnqueueDelete("investments", id);
            }
            return Task.CompletedTask;
        }

        public Task SaveProductAsync(ProductItem p)
        {
            int idx = Products.FindIndex(x => x.Id == p.Id);
            if (idx >= 0) Products[idx] = p;
            else Products.Insert(0, p);

            NotifyDataChanged();
            if (!IsSampleDataLoaded)
            {
                SyncQueue.EnqueueSet("products", p.Id, p);
            }
            return Task.CompletedTask;
        }

        public Task DeleteProductAsync(string id)
        {
            Products.RemoveAll(x => x.Id == id);
            NotifyDataChanged();
            if (!IsSampleDataLoaded)
            {
                SyncQueue.EnqueueDelete("products", id);
            }
            return Task.CompletedTask;
        }

        public Task SaveRawMaterialAsync(RawMaterialItem r)
        {
            int idx = RawMaterials.FindIndex(x => x.Id == r.Id);
            if (idx >= 0) RawMaterials[idx] = r;
            else RawMaterials.Insert(0, r);

            NotifyDataChanged();
            if (!IsSampleDataLoaded)
            {
                SyncQueue.EnqueueSet("raw_materials", r.Id, r);
            }
            return Task.CompletedTask;
        }

        public Task DeleteRawMaterialAsync(string id)
        {
            RawMaterials.RemoveAll(x => x.Id == id);
            NotifyDataChanged();
            if (!IsSampleDataLoaded)
            {
                SyncQueue.EnqueueDelete("raw_materials", id);
            }
            return Task.CompletedTask;
        }

        public Task SaveGeneralItemAsync(GeneralItem g)
        {
            int idx = GeneralItems.FindIndex(x => x.Id == g.Id);
            if (idx >= 0) GeneralItems[idx] = g;
            else GeneralItems.Insert(0, g);

            NotifyDataChanged();
            if (!IsSampleDataLoaded)
            {
                SyncQueue.EnqueueSet("general_items", g.Id, g);
            }
            return Task.CompletedTask;
        }

        public Task DeleteGeneralItemAsync(string id)
        {
            GeneralItems.RemoveAll(x => x.Id == id);
            NotifyDataChanged();
            if (!IsSampleDataLoaded)
            {
                SyncQueue.EnqueueDelete("general_items", id);
            }
            return Task.CompletedTask;
        }
    }
}
