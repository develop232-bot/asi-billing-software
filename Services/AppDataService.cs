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
        public List<LoanItem> Loans { get; set; } = new();
        public List<AssetItem> Assets { get; set; } = new();
        public Dictionary<string, int> Sequences { get; set; } = new();

        public bool IsSampleDataLoaded { get; set; } = false;

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
                    Loans,
                    Assets,
                    Sequences,
                    IsSampleDataLoaded
                };
                string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
                string tempFile = _localStateFile + ".tmp";
                File.WriteAllText(tempFile, json);
                File.Move(tempFile, _localStateFile, overwrite: true);
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
                    if (root.TryGetProperty("Loans", out var lElem))
                        Loans = JsonSerializer.Deserialize<List<LoanItem>>(lElem.GetRawText()) ?? new();
                    if (root.TryGetProperty("Assets", out var aElem))
                        Assets = JsonSerializer.Deserialize<List<AssetItem>>(aElem.GetRawText()) ?? new();
                    if (root.TryGetProperty("Sequences", out var sqElem))
                        Sequences = JsonSerializer.Deserialize<Dictionary<string, int>>(sqElem.GetRawText()) ?? new();
                    if (root.TryGetProperty("IsSampleDataLoaded", out var smElem))
                        IsSampleDataLoaded = false;

                    // Purge any legacy sample data
                    Loans.RemoveAll(l => l.Id == "l0" || l.Id == "sampleLoan" || l.Id.StartsWith("l_sample"));
                    Assets.RemoveAll(a => a.Id == "a0" || a.Id == "a1" || a.Id.StartsWith("a_sample"));
                    Products.RemoveAll(p => p.Id.StartsWith("p") && int.TryParse(p.Id.Substring(1), out _));
                    RawMaterials.RemoveAll(r => r.Id.StartsWith("r") && int.TryParse(r.Id.Substring(1), out _));
                    GeneralItems.RemoveAll(g => g.Id.StartsWith("g") && int.TryParse(g.Id.Substring(1), out _));
                    Sales.RemoveAll(s => s.Id.StartsWith("s") && int.TryParse(s.Id.Substring(1), out _));
                    Purchases.RemoveAll(pu => pu.Id.StartsWith("b") && int.TryParse(pu.Id.Substring(1), out _));
                    WorkOrders.RemoveAll(w => w.Id.StartsWith("wo") && int.TryParse(w.Id.Substring(2), out _));
                    Expenses.RemoveAll(e => e.Id.StartsWith("e_sal_") || e.Id.StartsWith("e_eb_") || e.Id.StartsWith("e_wtr_") || e.Id.StartsWith("e_rent_") || e.Id.StartsWith("e_esipf_") || e.Id.StartsWith("e_tea_") || e.Id.StartsWith("e_tr_") || e.Id.StartsWith("e_rep_"));

                    for (int i = 0; i < Products.Count; i++)
                    {
                        if (string.IsNullOrEmpty(Products[i].Barcode))
                        {
                            Products[i].Barcode = BarcodeService.GenerateBarcode(Products[i].Category, Products[i].SourceType, i + 1);
                        }
                        if (Products[i].RegisteredQty <= 0 && Products[i].Stock > 0)
                        {
                            Products[i].RegisteredQty = Products[i].Stock;
                        }
                    }
                    foreach (var r in RawMaterials) if (r.RegisteredQty <= 0 && r.Stock > 0) r.RegisteredQty = r.Stock;
                    foreach (var g in GeneralItems) if (g.RegisteredQty <= 0 && g.Stock > 0) g.RegisteredQty = g.Stock;

                    return;
                }
            }
            catch { }

            // Default startup: Keep fresh empty data (demo data removed)
            IsSampleDataLoaded = false;
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
            Loans.Clear();
            Assets.Clear();
            Sequences.Clear();
            IsSampleDataLoaded = false;
            SyncQueue.ClearQueue();
            NotifyDataChanged();
        }

        public void LoadDemoData()
        {
            // Demo data permanently removed - all data is loaded strictly from database
            StartFresh();
        }

        public void ReloadSampleData() => StartFresh();

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
                // 1. Process any pending local sync items first so cloud has the latest local writes
                if (SyncQueue.PendingCount > 0)
                {
                    await SyncQueue.ProcessQueueAsync();
                }

                var bundle = await Firebase.LoadAllFromCloudAsync();

                // 2. SMART BIDIRECTIONAL MERGE:
                // NEVER blindly wipe local collections when cloud has fewer items or is empty!
                // Merge by document Id to preserve all locally-saved entries.

                // Products
                var prodDict = Products.Where(p => !string.IsNullOrEmpty(p.Id)).ToDictionary(p => p.Id, p => p);
                foreach (var cp in bundle.Products)
                {
                    if (!string.IsNullOrEmpty(cp.Id))
                    {
                        prodDict[cp.Id] = cp;
                    }
                }
                Products = prodDict.Values.OrderBy(p => p.Name).ToList();

                // RawMaterials
                var rmDict = RawMaterials.Where(r => !string.IsNullOrEmpty(r.Id)).ToDictionary(r => r.Id, r => r);
                foreach (var crm in bundle.RawMaterials)
                {
                    if (!string.IsNullOrEmpty(crm.Id)) rmDict[crm.Id] = crm;
                }
                RawMaterials = rmDict.Values.OrderBy(r => r.Name).ToList();

                // GeneralItems
                var genDict = GeneralItems.Where(g => !string.IsNullOrEmpty(g.Id)).ToDictionary(g => g.Id, g => g);
                foreach (var cg in bundle.GeneralItems)
                {
                    if (!string.IsNullOrEmpty(cg.Id)) genDict[cg.Id] = cg;
                }
                GeneralItems = genDict.Values.OrderBy(g => g.Name).ToList();

                // Sales
                var salesDict = Sales.Where(s => !string.IsNullOrEmpty(s.Id)).ToDictionary(s => s.Id, s => s);
                foreach (var cs in bundle.Sales)
                {
                    if (!string.IsNullOrEmpty(cs.Id)) salesDict[cs.Id] = cs;
                }
                Sales = salesDict.Values.OrderByDescending(s => s.Date).ToList();

                // Purchases
                var purchDict = Purchases.Where(pu => !string.IsNullOrEmpty(pu.Id)).ToDictionary(pu => pu.Id, pu => pu);
                foreach (var cpu in bundle.Purchases)
                {
                    if (!string.IsNullOrEmpty(cpu.Id)) purchDict[cpu.Id] = cpu;
                }
                Purchases = purchDict.Values.OrderByDescending(pu => pu.Date).ToList();

                // WorkOrders
                var woDict = WorkOrders.Where(w => !string.IsNullOrEmpty(w.Id)).ToDictionary(w => w.Id, w => w);
                foreach (var cwo in bundle.WorkOrders)
                {
                    if (!string.IsNullOrEmpty(cwo.Id)) woDict[cwo.Id] = cwo;
                }
                WorkOrders = woDict.Values.OrderByDescending(w => w.OrderDate).ToList();

                // Expenses
                var expDict = Expenses.Where(e => !string.IsNullOrEmpty(e.Id)).ToDictionary(e => e.Id, e => e);
                foreach (var ce in bundle.Expenses)
                {
                    if (!string.IsNullOrEmpty(ce.Id)) expDict[ce.Id] = ce;
                }
                Expenses = expDict.Values.OrderByDescending(e => e.Date).ToList();

                // Investments
                var invDict = Investments.Where(i => !string.IsNullOrEmpty(i.Id)).ToDictionary(i => i.Id, i => i);
                foreach (var ci in bundle.Investments)
                {
                    if (!string.IsNullOrEmpty(ci.Id)) invDict[ci.Id] = ci;
                }
                Investments = invDict.Values.OrderByDescending(i => i.Date).ToList();

                // Loans
                var loanDict = Loans.Where(l => !string.IsNullOrEmpty(l.Id)).ToDictionary(l => l.Id, l => l);
                foreach (var cl in bundle.Loans)
                {
                    if (!string.IsNullOrEmpty(cl.Id)) loanDict[cl.Id] = cl;
                }
                Loans = loanDict.Values.OrderByDescending(l => l.Date).ToList();

                // Assets
                var assetDict = Assets.Where(a => !string.IsNullOrEmpty(a.Id)).ToDictionary(a => a.Id, a => a);
                foreach (var ca in bundle.Assets)
                {
                    if (!string.IsNullOrEmpty(ca.Id)) assetDict[ca.Id] = ca;
                }
                Assets = assetDict.Values.OrderByDescending(a => a.Date).ToList();

                // Sequences - keep highest sequence number
                foreach (var kvp in bundle.Sequences)
                {
                    if (!Sequences.TryGetValue(kvp.Key, out int localVal) || kvp.Value > localVal)
                    {
                        Sequences[kvp.Key] = kvp.Value;
                    }
                }

                Auth.MergeCloudUsers(bundle.Users);

                for (int i = 0; i < Products.Count; i++)
                {
                    if (string.IsNullOrEmpty(Products[i].Barcode))
                    {
                        Products[i].Barcode = BarcodeService.GenerateBarcode(Products[i].Category, Products[i].SourceType, i + 1);
                    }
                    if (Products[i].RegisteredQty <= 0 && Products[i].Stock > 0)
                    {
                        Products[i].RegisteredQty = Products[i].Stock;
                    }
                }
                foreach (var r in RawMaterials) if (r.RegisteredQty <= 0 && r.Stock > 0) r.RegisteredQty = r.Stock;
                foreach (var g in GeneralItems) if (g.RegisteredQty <= 0 && g.Stock > 0) g.RegisteredQty = g.Stock;

                IsSampleDataLoaded = false;

                // 3. Ensure any local items NOT yet present in cloud are queued to push!
                int pushedLocalCount = 0;
                foreach (var p in Products)
                {
                    if (!bundle.Products.Any(cp => cp.Id == p.Id))
                    {
                        SyncQueue.EnqueueSet("products", p.Id, p);
                        pushedLocalCount++;
                    }
                }
                foreach (var r in RawMaterials)
                {
                    if (!bundle.RawMaterials.Any(cr => cr.Id == r.Id))
                    {
                        SyncQueue.EnqueueSet("raw_materials", r.Id, r);
                        pushedLocalCount++;
                    }
                }
                foreach (var g in GeneralItems)
                {
                    if (!bundle.GeneralItems.Any(cg => cg.Id == g.Id))
                    {
                        SyncQueue.EnqueueSet("general_items", g.Id, g);
                        pushedLocalCount++;
                    }
                }
                foreach (var s in Sales)
                {
                    if (!bundle.Sales.Any(cs => cs.Id == s.Id))
                    {
                        SyncQueue.EnqueueSet("sales", s.Id, s);
                        pushedLocalCount++;
                    }
                }
                foreach (var pu in Purchases)
                {
                    if (!bundle.Purchases.Any(cpu => cpu.Id == pu.Id))
                    {
                        SyncQueue.EnqueueSet("purchases", pu.Id, pu);
                        pushedLocalCount++;
                    }
                }
                foreach (var w in WorkOrders)
                {
                    if (!bundle.WorkOrders.Any(cw => cw.Id == w.Id))
                    {
                        SyncQueue.EnqueueSet("work_orders", w.Id, w);
                        pushedLocalCount++;
                    }
                }
                foreach (var e in Expenses)
                {
                    if (!bundle.Expenses.Any(ce => ce.Id == e.Id))
                    {
                        SyncQueue.EnqueueSet("expenses", e.Id, e);
                        pushedLocalCount++;
                    }
                }
                foreach (var i in Investments)
                {
                    if (!bundle.Investments.Any(ci => ci.Id == i.Id))
                    {
                        SyncQueue.EnqueueSet("investments", i.Id, i);
                        pushedLocalCount++;
                    }
                }
                foreach (var l in Loans)
                {
                    if (!bundle.Loans.Any(cl => cl.Id == l.Id))
                    {
                        SyncQueue.EnqueueSet("loans", l.Id, l);
                        pushedLocalCount++;
                    }
                }
                foreach (var a in Assets)
                {
                    if (!bundle.Assets.Any(ca => ca.Id == a.Id))
                    {
                        SyncQueue.EnqueueSet("assets", a.Id, a);
                        pushedLocalCount++;
                    }
                }

                // Save merged state to local disk and notify UI
                NotifyDataChanged();

                if (pushedLocalCount > 0)
                {
                    _ = SyncQueue.ProcessQueueAsync();
                }

                int totalCount = Products.Count + RawMaterials.Count + GeneralItems.Count + Sales.Count + Purchases.Count + WorkOrders.Count + Expenses.Count + Investments.Count + Loans.Count + Assets.Count;
                string summary = $"Synced with Cloud Firestore: {totalCount} total active records ({Products.Count} products, {Sales.Count} sales, {Purchases.Count} purchases).";

                return (true, summary, totalCount);
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
            foreach (var l in Loans) SyncQueue.EnqueueSet("loans", l.Id, l);
            foreach (var a in Assets) SyncQueue.EnqueueSet("assets", a.Id, a);
            foreach (var u in Auth.Users) SyncQueue.EnqueueSet("users", u.Id, u);
            SyncQueue.EnqueueSet("meta", "sequences", Sequences);
            return Task.CompletedTask;
        }

        public List<ExpenseItem> GetAllExpenses()
        {
            var list = new List<ExpenseItem>();
            foreach (var e in Expenses)
            {
                if (string.IsNullOrEmpty(e.Group))
                {
                    if (e.Category.IndexOf("Salary", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        e.Description.IndexOf("Salary", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        e.Category.IndexOf("OT", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        e.Group = "Employee";
                    }
                    else if (e.Category.IndexOf("Rent", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             e.Category.IndexOf("EB", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             e.Category.IndexOf("Water", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             e.Category.IndexOf("Phone", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             e.Category.IndexOf("Internet", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        e.Group = "Utility";
                    }
                    else
                    {
                        e.Group = "Misc Exp";
                    }
                }
                list.Add(e);
            }

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
                    Group = "Purchase",
                    Category = kindLbl,
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
                    if (p.RegisteredQty <= 0 && p.Stock > 0)
                    {
                        p.RegisteredQty = p.Stock;
                    }
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

        public Task AddLoanAsync(LoanItem loan)
        {
            Loans.Insert(0, loan);
            NotifyDataChanged();
            if (!IsSampleDataLoaded)
            {
                SyncQueue.EnqueueSet("loans", loan.Id, loan);
            }
            return Task.CompletedTask;
        }

        public Task DeleteLoanAsync(string id)
        {
            Loans.RemoveAll(x => x.Id == id);
            NotifyDataChanged();
            if (!IsSampleDataLoaded)
            {
                SyncQueue.EnqueueDelete("loans", id);
            }
            return Task.CompletedTask;
        }

        public Task AddLoanRepaymentAsync(string loanId, LoanRepayment rep)
        {
            var loan = Loans.FirstOrDefault(l => l.Id == loanId);
            if (loan != null)
            {
                loan.Repayments.Add(rep);
                NotifyDataChanged();
                if (!IsSampleDataLoaded)
                {
                    SyncQueue.EnqueueSet("loans", loan.Id, loan);
                }
            }
            return Task.CompletedTask;
        }

        public Task AddAssetAsync(AssetItem asset)
        {
            Assets.Insert(0, asset);
            NotifyDataChanged();
            if (!IsSampleDataLoaded)
            {
                SyncQueue.EnqueueSet("assets", asset.Id, asset);
            }
            return Task.CompletedTask;
        }

        public Task DeleteAssetAsync(string id)
        {
            Assets.RemoveAll(x => x.Id == id);
            NotifyDataChanged();
            if (!IsSampleDataLoaded)
            {
                SyncQueue.EnqueueDelete("assets", id);
            }
            return Task.CompletedTask;
        }

        public Task SaveProductAsync(ProductItem p)
        {
            if (p.RegisteredQty <= 0 && p.Stock > 0) p.RegisteredQty = p.Stock;
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
            if (r.RegisteredQty <= 0 && r.Stock > 0) r.RegisteredQty = r.Stock;
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
            if (g.RegisteredQty <= 0 && g.Stock > 0) g.RegisteredQty = g.Stock;
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
