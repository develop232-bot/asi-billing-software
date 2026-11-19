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

            // Purge any stale local state file so it can never resurrect deleted records
            try
            {
                if (File.Exists(_localStateFile))
                {
                    File.Delete(_localStateFile);
                }
            }
            catch { }

            HookRealtimeEvents();

            if (Firebase.IsConnected)
            {
                Firebase.StartRealtimeListeners();
            }
            else
            {
                Firebase.ConnectionChanged += () =>
                {
                    if (Firebase.IsConnected)
                    {
                        Firebase.StartRealtimeListeners();
                    }
                };
            }
        }

        private void HookRealtimeEvents()
        {
            Firebase.ProductsRealtimeReceived += list =>
            {
                Products = list.OrderBy(p => p.Name).ToList();
                NotifyDataChanged();
            };

            Firebase.RawMaterialsRealtimeReceived += list =>
            {
                RawMaterials = list.Where(r => !r.Id.StartsWith("pur_it_") && !r.Id.StartsWith("pur_")).OrderBy(r => r.Name).ToList();
                NotifyDataChanged();
            };

            Firebase.GeneralItemsRealtimeReceived += list =>
            {
                GeneralItems = list.OrderBy(g => g.Name).ToList();
                NotifyDataChanged();
            };

            Firebase.SalesRealtimeReceived += list =>
            {
                Sales = list.OrderByDescending(s => s.Date).ToList();
                NotifyDataChanged();
            };

            Firebase.PurchasesRealtimeReceived += list =>
            {
                Purchases = list.OrderByDescending(p => p.Date).ToList();
                NotifyDataChanged();
            };

            Firebase.WorkOrdersRealtimeReceived += list =>
            {
                WorkOrders = list.OrderByDescending(w => w.OrderDate).ToList();
                NotifyDataChanged();
            };

            Firebase.ExpensesRealtimeReceived += list =>
            {
                Expenses = list.OrderByDescending(e => e.Date).ToList();
                NotifyDataChanged();
            };

            Firebase.InvestmentsRealtimeReceived += list =>
            {
                Investments = list.OrderByDescending(i => i.Date).ToList();
                NotifyDataChanged();
            };

            Firebase.LoansRealtimeReceived += list =>
            {
                Loans = list.OrderByDescending(l => l.Date).ToList();
                NotifyDataChanged();
            };

            Firebase.AssetsRealtimeReceived += list =>
            {
                Assets = list.OrderByDescending(a => a.Date).ToList();
                NotifyDataChanged();
            };

            Firebase.UsersRealtimeReceived += list =>
            {
                Auth.MergeCloudUsers(list);
                NotifyDataChanged();
            };

            Firebase.SequencesRealtimeReceived += dict =>
            {
                Sequences = dict;
                NotifyDataChanged();
            };
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
                if (!IsSampleDataLoaded)
                {
                    SyncQueue.EnqueueSet("meta", "sequences", Sequences);
                    if (Firebase != null && Firebase.IsConnected)
                    {
                        try { _ = Firebase.SaveSequencesAsync(Sequences); } catch { }
                    }
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
            if (System.Windows.Application.Current != null && System.Windows.Application.Current.Dispatcher != null && !System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    DataChanged?.Invoke();
                }));
            }
            else
            {
                DataChanged?.Invoke();
            }
        }

        public void SaveLocalState()
        {
            // Local JSON caching removed: database is strictly live realtime Firestore
        }

        public void LoadState()
        {
            // Local JSON caching removed: database is strictly live realtime Firestore
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

                // PURE LIVE CLOUD REPLACEMENT: Cloud Firestore is the single source of truth.
                // Any record deleted in the database is removed everywhere.
                Products = bundle.Products.OrderBy(p => p.Name).ToList();
                RawMaterials = bundle.RawMaterials.Where(r => !r.Id.StartsWith("pur_it_") && !r.Id.StartsWith("pur_")).OrderBy(r => r.Name).ToList();
                GeneralItems = bundle.GeneralItems.OrderBy(g => g.Name).ToList();
                Sales = bundle.Sales.OrderByDescending(s => s.Date).ToList();
                Purchases = bundle.Purchases.OrderByDescending(p => p.Date).ToList();
                WorkOrders = bundle.WorkOrders.OrderByDescending(w => w.OrderDate).ToList();
                Expenses = bundle.Expenses.OrderByDescending(e => e.Date).ToList();
                Investments = bundle.Investments.OrderByDescending(i => i.Date).ToList();
                Loans = bundle.Loans.OrderByDescending(l => l.Date).ToList();
                Assets = bundle.Assets.OrderByDescending(a => a.Date).ToList();
                if (bundle.Sequences != null && bundle.Sequences.Count > 0)
                {
                    Sequences = bundle.Sequences;
                }

                Auth.MergeCloudUsers(bundle.Users);

                CleanLegacyBarcodesAndHsn();
                EnsureUniqueBarcodesAcrossCatalog();
                HarmonizeRawMaterialsAndWorkOrders();

                IsSampleDataLoaded = false;

                Firebase.StartRealtimeListeners();
                NotifyDataChanged();

                int totalCount = Products.Count + RawMaterials.Count + GeneralItems.Count + Sales.Count + Purchases.Count + WorkOrders.Count + Expenses.Count + Investments.Count + Loans.Count + Assets.Count;
                string summary = $"Live Realtime DB: {totalCount} active records synced from Firestore ({Firebase.ProjectId}).";
                return (true, summary, totalCount);
            }
            catch (Exception ex)
            {
                return (false, $"Error reading Firestore: {ex.Message}", 0);
            }
        }

        public Task PushAllToCloudAsync()
        {
            // Manual full-database push removed: database is strictly live realtime Firestore
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

        public async Task<SaleDoc> RecordSaleAsync(SaleDoc sale)
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
                    SyncQueue.EnqueueSet("products", p.Id, p);
                    if (Firebase != null && Firebase.IsConnected)
                    {
                        try { await Firebase.SaveProductAsync(p); } catch { }
                    }
                }
            }

            NotifyDataChanged();

            if (!IsSampleDataLoaded)
            {
                SyncQueue.EnqueueSet("sales", sale.Id, sale);
                SyncQueue.EnqueueSet("meta", "sequences", Sequences);
                if (Firebase != null && Firebase.IsConnected)
                {
                    try { await Firebase.SaveSaleAsync(sale); } catch { }
                    try { await Firebase.SaveSequencesAsync(Sequences); } catch { }
                }
            }

            return sale;
        }

        public async Task DeleteSaleAsync(string id)
        {
            Sales.RemoveAll(x => x.Id == id);
            NotifyDataChanged();
            if (!IsSampleDataLoaded)
            {
                SyncQueue.EnqueueDelete("sales", id);
                if (Firebase != null && Firebase.IsConnected)
                {
                    try { await Firebase.DeleteSaleAsync(id); } catch { }
                }
            }
        }

        public async Task<PurchaseDoc> RecordPurchaseAsync(PurchaseDoc purchase)
        {
            Purchases.Insert(0, purchase);

            foreach (var line in purchase.Lines)
            {
                if (string.IsNullOrWhiteSpace(line.Name)) continue;

                if (purchase.Kind == "raw")
                {
                    // If this raw material exists in the catalog (by ItemId, Name or Sku), update its live stock
                    var rm = RawMaterials.FirstOrDefault(x => (!string.IsNullOrEmpty(line.ItemId) && x.Id == line.ItemId) ||
                                                             x.Name.Equals(line.Name, StringComparison.OrdinalIgnoreCase) ||
                                                             (!string.IsNullOrWhiteSpace(line.Sku) && !string.IsNullOrWhiteSpace(x.Sku) && x.Sku.Equals(line.Sku, StringComparison.OrdinalIgnoreCase)));
                    if (rm != null)
                    {
                        rm.Stock = Math.Round(rm.Stock + line.Qty, 2);
                        rm.Rate = line.Rate;
                        if (!string.IsNullOrWhiteSpace(line.Unit)) rm.Unit = line.Unit;
                        if (!string.IsNullOrWhiteSpace(rm.Sku)) line.Sku = rm.Sku;
                        else if (!string.IsNullOrWhiteSpace(line.Sku)) rm.Sku = line.Sku;
                        SyncQueue.EnqueueSet("raw_materials", rm.Id, rm);
                        if (Firebase != null && Firebase.IsConnected) { try { await Firebase.SaveRawMaterialAsync(rm); } catch { } }
                    }
                }
                else if (purchase.Kind == "ready")
                {
                    var p = Products.FirstOrDefault(x => (!string.IsNullOrEmpty(line.ItemId) && x.Id == line.ItemId) ||
                                                         x.Name.Equals(line.Name, StringComparison.OrdinalIgnoreCase));
                    if (p != null)
                    {
                        p.Stock += line.Qty;
                        p.Cost = line.Rate;
                        if (line.SellingPrice > 0) p.RetailPrice = line.SellingPrice;
                        if (!string.IsNullOrWhiteSpace(line.Unit)) p.Unit = line.Unit;
                        if (!string.IsNullOrWhiteSpace(p.Sku)) line.Sku = p.Sku;
                        else if (!string.IsNullOrWhiteSpace(line.Sku)) p.Sku = line.Sku;
                        SyncQueue.EnqueueSet("products", p.Id, p);
                        if (Firebase != null && Firebase.IsConnected) { try { await Firebase.SaveProductAsync(p); } catch { } }
                    }
                    else
                    {
                        var newProd = new ProductItem
                        {
                            Id = !string.IsNullOrWhiteSpace(line.ItemId) ? line.ItemId : "prod_" + Guid.NewGuid().ToString("N")[..8],
                            Name = line.Name,
                            Unit = string.IsNullOrWhiteSpace(line.Unit) ? "pair" : line.Unit,
                            Cost = line.Rate,
                            RetailPrice = line.SellingPrice > 0 ? line.SellingPrice : Math.Round(line.Rate * 1.5, 0),
                            Gst = line.GstPercent > 0 ? line.GstPercent : 5,
                            Stock = line.Qty,
                            Sku = !string.IsNullOrWhiteSpace(line.Sku) ? line.Sku : $"ASI-RDY-{Products.Count + 1:D3}",
                            Category = "Ready Made Products"
                        };
                        if (!string.IsNullOrWhiteSpace(line.Size))
                        {
                            newProd.Sizes = line.Size.Split(new[] { ',', ' ', '/' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                        }
                        line.ItemId = newProd.Id;
                        Products.Add(newProd);
                        SyncQueue.EnqueueSet("products", newProd.Id, newProd);
                        if (Firebase != null && Firebase.IsConnected) { try { await Firebase.SaveProductAsync(newProd); } catch { } }
                    }
                }
                else if (purchase.Kind == "gen")
                {
                    var g = GeneralItems.FirstOrDefault(x => (!string.IsNullOrEmpty(line.ItemId) && x.Id == line.ItemId) ||
                                                             x.Name.Equals(line.Name, StringComparison.OrdinalIgnoreCase));
                    if (g != null)
                    {
                        g.Stock += line.Qty;
                        g.Rate = line.Rate;
                        if (!string.IsNullOrWhiteSpace(line.Unit)) g.Unit = line.Unit;
                        if (!string.IsNullOrWhiteSpace(g.Sku)) line.Sku = g.Sku;
                        else if (!string.IsNullOrWhiteSpace(line.Sku)) g.Sku = line.Sku;
                        SyncQueue.EnqueueSet("general_items", g.Id, g);
                        if (Firebase != null && Firebase.IsConnected) { try { await Firebase.SaveGeneralItemAsync(g); } catch { } }
                    }
                    else
                    {
                        var newGen = new GeneralItem
                        {
                            Id = !string.IsNullOrWhiteSpace(line.ItemId) ? line.ItemId : "gen_" + Guid.NewGuid().ToString("N")[..8],
                            Name = line.Name,
                            Unit = string.IsNullOrWhiteSpace(line.Unit) ? "pcs" : line.Unit,
                            Rate = line.Rate,
                            Gst = line.GstPercent > 0 ? line.GstPercent : 5,
                            Stock = line.Qty,
                            Sku = !string.IsNullOrWhiteSpace(line.Sku) ? line.Sku : ""
                        };
                        line.ItemId = newGen.Id;
                        GeneralItems.Add(newGen);
                        SyncQueue.EnqueueSet("general_items", newGen.Id, newGen);
                        if (Firebase != null && Firebase.IsConnected) { try { await Firebase.SaveGeneralItemAsync(newGen); } catch { } }
                    }
                }
            }

            NotifyDataChanged();

            if (!IsSampleDataLoaded)
            {
                SyncQueue.EnqueueSet("purchases", purchase.Id, purchase);
                SyncQueue.EnqueueSet("meta", "sequences", Sequences);
                if (Firebase != null && Firebase.IsConnected)
                {
                    try { await Firebase.SavePurchaseAsync(purchase); } catch { }
                    try { await Firebase.SaveSequencesAsync(Sequences); } catch { }
                }
            }

            return purchase;
        }

        public async Task DeletePurchaseAsync(string id)
        {
            var p = Purchases.FirstOrDefault(x => x.Id == id);
            if (p != null && p.Lines != null)
            {
                foreach (var line in p.Lines)
                {
                    if (p.Kind == "raw")
                    {
                        var rm = RawMaterials.FirstOrDefault(x => (!string.IsNullOrEmpty(line.ItemId) && x.Id == line.ItemId) ||
                                                                 x.Name.Equals(line.Name, StringComparison.OrdinalIgnoreCase));
                        if (rm != null)
                        {
                            rm.Stock = Math.Max(0, Math.Round(rm.Stock - line.Qty, 2));
                            SyncQueue.EnqueueSet("raw_materials", rm.Id, rm);
                            if (Firebase != null && Firebase.IsConnected) { try { await Firebase.SaveRawMaterialAsync(rm); } catch { } }
                        }
                    }
                    else if (p.Kind == "ready")
                    {
                        var prod = Products.FirstOrDefault(x => (!string.IsNullOrEmpty(line.ItemId) && x.Id == line.ItemId) ||
                                                               x.Name.Equals(line.Name, StringComparison.OrdinalIgnoreCase));
                        if (prod != null)
                        {
                            prod.Stock = Math.Max(0, Math.Round(prod.Stock - line.Qty, 2));
                            SyncQueue.EnqueueSet("products", prod.Id, prod);
                            if (Firebase != null && Firebase.IsConnected) { try { await Firebase.SaveProductAsync(prod); } catch { } }
                        }
                    }
                    else if (p.Kind == "gen")
                    {
                        var g = GeneralItems.FirstOrDefault(x => (!string.IsNullOrEmpty(line.ItemId) && x.Id == line.ItemId) ||
                                                                x.Name.Equals(line.Name, StringComparison.OrdinalIgnoreCase));
                        if (g != null)
                        {
                            g.Stock = Math.Max(0, Math.Round(g.Stock - line.Qty, 2));
                            SyncQueue.EnqueueSet("general_items", g.Id, g);
                            if (Firebase != null && Firebase.IsConnected) { try { await Firebase.SaveGeneralItemAsync(g); } catch { } }
                        }
                    }
                }
            }

            Purchases.RemoveAll(x => x.Id == id);
            NotifyDataChanged();
            if (!IsSampleDataLoaded)
            {
                SyncQueue.EnqueueDelete("purchases", id);
                if (Firebase != null && Firebase.IsConnected)
                {
                    try { await Firebase.DeletePurchaseAsync(id); } catch { }
                }
            }
        }

        public async Task RecordPaymentAsync(string collection, string id, double amount)
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
                        if (Firebase != null && Firebase.IsConnected)
                        {
                            try { await Firebase.SaveSaleAsync(s); } catch { }
                        }
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
                        if (Firebase != null && Firebase.IsConnected)
                        {
                            try { await Firebase.SavePurchaseAsync(p); } catch { }
                        }
                    }
                }
            }
        }

        public async Task SaveWorkOrderAsync(WorkOrderDoc wo, bool isNew)
        {
            if (isNew)
            {
                WorkOrders.Insert(0, wo);
                // Deduct consumed raw materials for new work order
                if (wo.Materials != null)
                {
                    foreach (var m in wo.Materials)
                    {
                        if (m.Qty <= 0) continue;
                        var rm = RawMaterials.FirstOrDefault(x => (!string.IsNullOrEmpty(m.ItemId) && x.Id == m.ItemId) ||
                                                                 x.Name.Equals(m.Name, StringComparison.OrdinalIgnoreCase) ||
                                                                 (!string.IsNullOrWhiteSpace(m.Name) && x.Name.Contains(m.Name, StringComparison.OrdinalIgnoreCase)));
                        if (rm != null)
                        {
                            rm.Stock = Math.Max(0, Math.Round(rm.Stock - m.Qty, 2));
                            if (!IsSampleDataLoaded) SyncQueue.EnqueueSet("raw_materials", rm.Id, rm);
                            if (Firebase != null) { try { await Firebase.SaveRawMaterialAsync(rm); } catch { } }
                        }
                    }
                }
            }
            else
            {
                int idx = WorkOrders.FindIndex(x => x.Id == wo.Id);
                var existing = idx >= 0 ? WorkOrders[idx] : null;

                if (existing != null && existing.Materials != null && wo.Materials != null)
                {
                    // Refund old materials
                    foreach (var oldM in existing.Materials)
                    {
                        var rm = RawMaterials.FirstOrDefault(x => (!string.IsNullOrEmpty(oldM.ItemId) && x.Id == oldM.ItemId) ||
                                                                 x.Name.Equals(oldM.Name, StringComparison.OrdinalIgnoreCase));
                        if (rm != null) rm.Stock = Math.Round(rm.Stock + oldM.Qty, 2);
                    }
                    // Apply new materials
                    foreach (var newM in wo.Materials)
                    {
                        var rm = RawMaterials.FirstOrDefault(x => (!string.IsNullOrEmpty(newM.ItemId) && x.Id == newM.ItemId) ||
                                                                 x.Name.Equals(newM.Name, StringComparison.OrdinalIgnoreCase));
                        if (rm != null) rm.Stock = Math.Max(0, Math.Round(rm.Stock - newM.Qty, 2));
                    }
                    // Sync touched raw materials
                    var touched = existing.Materials.Concat(wo.Materials).ToList();
                    foreach (var m in touched)
                    {
                        var rm = RawMaterials.FirstOrDefault(x => (!string.IsNullOrEmpty(m.ItemId) && x.Id == m.ItemId) ||
                                                                 x.Name.Equals(m.Name, StringComparison.OrdinalIgnoreCase));
                        if (rm != null)
                        {
                            if (!IsSampleDataLoaded) SyncQueue.EnqueueSet("raw_materials", rm.Id, rm);
                            if (Firebase != null) { try { await Firebase.SaveRawMaterialAsync(rm); } catch { } }
                        }
                    }
                }

                if (idx >= 0) WorkOrders[idx] = wo;
                else WorkOrders.Insert(0, wo);
            }

            if (wo.Status == "Completed" && !wo.IsDone)
            {
                wo.IsDone = true;
                foreach (var it in wo.Items)
                {
                    var p = Products.FirstOrDefault(x => x.Id == it.ProductId || x.Name.Equals(it.ProductName, StringComparison.OrdinalIgnoreCase));
                    if (p != null)
                    {
                        p.Stock += it.Qty;
                        if (!IsSampleDataLoaded) SyncQueue.EnqueueSet("products", p.Id, p);
                        if (Firebase != null) { try { await Firebase.SaveProductAsync(p); } catch { } }
                    }
                }
            }

            NotifyDataChanged();
            SaveLocalState();

            if (!IsSampleDataLoaded)
            {
                SyncQueue.EnqueueSet("work_orders", wo.Id, wo);
                SyncQueue.EnqueueSet("meta", "sequences", Sequences);
                if (Firebase != null) { try { await Firebase.SaveWorkOrderAsync(wo); } catch { } }
            }
        }

        public async Task DeleteWorkOrderAsync(string id)
        {
            var wo = WorkOrders.FirstOrDefault(x => x.Id == id);
            if (wo != null && wo.Materials != null)
            {
                foreach (var m in wo.Materials)
                {
                    var rm = RawMaterials.FirstOrDefault(x => (!string.IsNullOrEmpty(m.ItemId) && x.Id == m.ItemId) ||
                                                             x.Name.Equals(m.Name, StringComparison.OrdinalIgnoreCase));
                    if (rm != null)
                    {
                        rm.Stock = Math.Round(rm.Stock + m.Qty, 2);
                        if (!IsSampleDataLoaded) SyncQueue.EnqueueSet("raw_materials", rm.Id, rm);
                        if (Firebase != null) { try { await Firebase.SaveRawMaterialAsync(rm); } catch { } }
                    }
                }
            }

            WorkOrders.RemoveAll(x => x.Id == id);
            NotifyDataChanged();
            SaveLocalState();
            if (!IsSampleDataLoaded)
            {
                SyncQueue.EnqueueDelete("work_orders", id);
                if (Firebase != null) { try { await Firebase.DeleteWorkOrderAsync(id); } catch { } }
            }
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

        public async Task AddExpenseAsync(ExpenseItem exp)
        {
            Expenses.Insert(0, exp);
            NotifyDataChanged();
            if (!IsSampleDataLoaded)
            {
                SyncQueue.EnqueueSet("expenses", exp.Id, exp);
                if (Firebase != null && Firebase.IsConnected)
                {
                    try { await Firebase.SaveExpenseAsync(exp); } catch { }
                }
            }
        }

        public async Task DeleteExpenseAsync(string id)
        {
            Expenses.RemoveAll(x => x.Id == id);
            NotifyDataChanged();
            if (!IsSampleDataLoaded)
            {
                SyncQueue.EnqueueDelete("expenses", id);
                if (Firebase != null && Firebase.IsConnected)
                {
                    try { await Firebase.DeleteExpenseAsync(id); } catch { }
                }
            }
        }

        public async Task AddInvestmentAsync(InvestmentItem inv)
        {
            Investments.Insert(0, inv);
            NotifyDataChanged();
            if (!IsSampleDataLoaded)
            {
                SyncQueue.EnqueueSet("investments", inv.Id, inv);
                if (Firebase != null && Firebase.IsConnected)
                {
                    try { await Firebase.SaveInvestmentAsync(inv); } catch { }
                }
            }
        }

        public async Task DeleteInvestmentAsync(string id)
        {
            Investments.RemoveAll(x => x.Id == id);
            NotifyDataChanged();
            if (!IsSampleDataLoaded)
            {
                SyncQueue.EnqueueDelete("investments", id);
                if (Firebase != null && Firebase.IsConnected)
                {
                    try { await Firebase.DeleteInvestmentAsync(id); } catch { }
                }
            }
        }

        public async Task AddLoanAsync(LoanItem loan)
        {
            Loans.Insert(0, loan);
            NotifyDataChanged();
            if (!IsSampleDataLoaded)
            {
                SyncQueue.EnqueueSet("loans", loan.Id, loan);
                if (Firebase != null && Firebase.IsConnected)
                {
                    try { await Firebase.SaveLoanAsync(loan); } catch { }
                }
            }
        }

        public async Task UpdateLoanAsync(LoanItem loan)
        {
            int idx = Loans.FindIndex(x => x.Id == loan.Id);
            if (idx >= 0) Loans[idx] = loan;
            else Loans.Insert(0, loan);

            NotifyDataChanged();
            if (!IsSampleDataLoaded)
            {
                SyncQueue.EnqueueSet("loans", loan.Id, loan);
                if (Firebase != null && Firebase.IsConnected)
                {
                    try { await Firebase.SaveLoanAsync(loan); } catch { }
                }
            }
        }

        public async Task DeleteLoanAsync(string id)
        {
            Loans.RemoveAll(x => x.Id == id);
            NotifyDataChanged();
            if (!IsSampleDataLoaded)
            {
                SyncQueue.EnqueueDelete("loans", id);
                if (Firebase != null && Firebase.IsConnected)
                {
                    try { await Firebase.DeleteLoanAsync(id); } catch { }
                }
            }
        }

        public async Task AddLoanRepaymentAsync(string loanId, LoanRepayment rep)
        {
            var loan = Loans.FirstOrDefault(l => l.Id == loanId);
            if (loan != null)
            {
                loan.Repayments.Add(rep);
                NotifyDataChanged();
                if (!IsSampleDataLoaded)
                {
                    SyncQueue.EnqueueSet("loans", loan.Id, loan);
                    if (Firebase != null && Firebase.IsConnected)
                    {
                        try { await Firebase.SaveLoanAsync(loan); } catch { }
                    }
                }
            }
        }

        public async Task AddAssetAsync(AssetItem asset)
        {
            Assets.Insert(0, asset);
            NotifyDataChanged();
            if (!IsSampleDataLoaded)
            {
                SyncQueue.EnqueueSet("assets", asset.Id, asset);
                if (Firebase != null && Firebase.IsConnected)
                {
                    try { await Firebase.SaveAssetAsync(asset); } catch { }
                }
            }
        }

        public async Task DeleteAssetAsync(string id)
        {
            Assets.RemoveAll(x => x.Id == id);
            NotifyDataChanged();
            if (!IsSampleDataLoaded)
            {
                SyncQueue.EnqueueDelete("assets", id);
                if (Firebase != null && Firebase.IsConnected)
                {
                    try { await Firebase.DeleteAssetAsync(id); } catch { }
                }
            }
        }

        public HashSet<string> GetAllUsedBarcodes(string? excludeId = null)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in Products)
            {
                if (p.Id != excludeId && !string.IsNullOrWhiteSpace(p.Barcode) && p.Barcode != "—")
                {
                    set.Add(p.Barcode.Trim());
                }
            }
            foreach (var r in RawMaterials)
            {
                if (r.Id != excludeId && !string.IsNullOrWhiteSpace(r.Barcode) && r.Barcode != "—")
                {
                    set.Add(r.Barcode.Trim());
                }
            }
            foreach (var g in GeneralItems)
            {
                if (g.Id != excludeId && !string.IsNullOrWhiteSpace(g.Barcode) && g.Barcode != "—")
                {
                    set.Add(g.Barcode.Trim());
                }
            }
            return set;
        }

        public string GetNextUniqueBarcode(string itemType = "mfg", string category = "Shoe", string? excludeId = null)
        {
            var used = GetAllUsedBarcodes(excludeId);
            return BarcodeService.GenerateUniqueBarcode(used, itemType, category);
        }

        private void CleanLegacyBarcodesAndHsn()
        {
            foreach (var rm in RawMaterials)
            {
                if (rm.Hsn == "6403" || rm.Hsn == "6406") rm.Hsn = string.Empty;
                if (rm.Barcode != null && (rm.Barcode.StartsWith("890650") || rm.Barcode == "—" || rm.Barcode == "-")) rm.Barcode = string.Empty;
            }
            foreach (var gi in GeneralItems)
            {
                if (gi.Hsn == "4819" || gi.Hsn == "6403") gi.Hsn = string.Empty;
                if (gi.Barcode != null && (gi.Barcode.StartsWith("890660") || gi.Barcode == "—" || gi.Barcode == "-")) gi.Barcode = string.Empty;
            }
            foreach (var prod in Products)
            {
                if (prod.SourceType != "R")
                {
                    if (prod.Hsn == "6403" || prod.Hsn == "6406") prod.Hsn = string.Empty;
                    if (prod.Barcode != null && (prod.Barcode.StartsWith("890640") || prod.Barcode == "—" || prod.Barcode == "-")) prod.Barcode = string.Empty;
                }
            }
        }

        public int EnsureUniqueBarcodesAcrossCatalog()
        {
            var usedBarcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int changesCount = 0;

            // 1. Process Finished & Readymade Products
            foreach (var p in Products)
            {
                if (p.SourceType == "R")
                {
                    string current = p.Barcode?.Trim() ?? "";
                    bool needsNew = string.IsNullOrEmpty(current)
                                    || current == "—"
                                    || current == "-"
                                    || usedBarcodes.Contains(current);

                    if (needsNew)
                    {
                        string newBc = BarcodeService.GenerateUniqueBarcode(usedBarcodes, "ready", p.Category);
                        p.Barcode = newBc;
                        usedBarcodes.Add(newBc);
                        changesCount++;
                        if (!IsSampleDataLoaded)
                        {
                            SyncQueue.EnqueueSet("products", p.Id, p);
                        }
                    }
                    else
                    {
                        usedBarcodes.Add(current);
                    }
                }
                else
                {
                    // Finished Product (Mfg): manual barcode only; do not auto-generate
                    if (!string.IsNullOrWhiteSpace(p.Barcode) && p.Barcode != "—" && p.Barcode != "-")
                    {
                        usedBarcodes.Add(p.Barcode.Trim());
                    }
                    else
                    {
                        p.Barcode = string.Empty;
                    }
                }
            }

            // 2. Process Raw Materials: manual barcode only; do not auto-generate
            foreach (var r in RawMaterials)
            {
                if (!string.IsNullOrWhiteSpace(r.Barcode) && r.Barcode != "—" && r.Barcode != "-")
                {
                    usedBarcodes.Add(r.Barcode.Trim());
                }
                else
                {
                    r.Barcode = string.Empty;
                }
            }

            // 3. Process General Packaging: manual barcode only; do not auto-generate
            foreach (var g in GeneralItems)
            {
                if (!string.IsNullOrWhiteSpace(g.Barcode) && g.Barcode != "—" && g.Barcode != "-")
                {
                    usedBarcodes.Add(g.Barcode.Trim());
                }
                else
                {
                    g.Barcode = string.Empty;
                }
            }

            if (changesCount > 0)
            {
                SaveLocalState();
            }

            return changesCount;
        }

        public void EnsureShoeBomConnections()
        {
            if (RawMaterials == null || RawMaterials.Count == 0 || Products == null || Products.Count == 0) return;

            bool modified = false;
            foreach (var prod in Products)
            {
                bool isShoe = prod.Category == "Shoe" ||
                              prod.Name.Contains("shoe", StringComparison.OrdinalIgnoreCase) ||
                              prod.Name.Contains("lofar", StringComparison.OrdinalIgnoreCase) ||
                              prod.Name.Contains("boot", StringComparison.OrdinalIgnoreCase) ||
                              prod.Name.Contains("derby", StringComparison.OrdinalIgnoreCase);

                if (isShoe && (prod.Bom == null || prod.Bom.Count == 0))
                {
                    prod.Bom ??= new();
                    foreach (var rm in RawMaterials)
                    {
                        string rmLower = rm.Name.ToLowerInvariant();
                        double qty = 0;
                        if (rmLower.Contains("leather") || rmLower.Contains("upper"))
                        {
                            qty = 2.0;
                        }
                        else if (rmLower.Contains("insole") || rmLower.Contains("sole"))
                        {
                            qty = rm.Unit.Contains("pc", StringComparison.OrdinalIgnoreCase) ? 2.0 : 1.0;
                        }
                        else if (rmLower.Contains("last") || rmLower.Contains("mould"))
                        {
                            qty = rm.Unit.Contains("pc", StringComparison.OrdinalIgnoreCase) ? 2.0 : 1.0;
                        }

                        if (qty > 0)
                        {
                            prod.Bom.Add(new BomItem
                            {
                                Id = rm.Id,
                                Name = rm.Name,
                                Unit = rm.Unit,
                                Qty = qty
                            });
                        }
                    }

                    if (prod.Bom.Count > 0)
                    {
                        prod.IsManufactured = true;
                        modified = true;
                    }
                }
            }

            if (modified)
            {
                SaveLocalState();
            }
        }

        public void HarmonizeRawMaterialsAndWorkOrders()
        {
            if (RawMaterials == null || RawMaterials.Count == 0) return;

            bool modified = false;

            // 1. Consolidate duplicate raw materials by name (e.g. Leather ASI-RAW-001 vs Leather ASI-RAW-004)
            var groups = RawMaterials
                .Where(r => !string.IsNullOrWhiteSpace(r.Name) && !r.Id.StartsWith("pur_it_"))
                .GroupBy(r => r.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var g in groups)
            {
                // Prefer the item with the primary/earliest SKU (e.g. "001" or "ASI-RAW-001")
                var primary = g.OrderBy(x => x.Sku ?? "").First();
                var duplicates = g.Where(x => x.Id != primary.Id).ToList();

                foreach (var dup in duplicates)
                {
                    // Reassign any Work Orders referencing duplicate ID
                    foreach (var wo in WorkOrders)
                    {
                        if (wo.Materials == null) continue;
                        foreach (var m in wo.Materials)
                        {
                            if (m.ItemId == dup.Id || (string.IsNullOrWhiteSpace(m.ItemId) && m.Name.Equals(dup.Name, StringComparison.OrdinalIgnoreCase)))
                            {
                                m.ItemId = primary.Id;
                                m.Name = primary.Name;
                            }
                        }
                    }

                    // Reassign any Purchases referencing duplicate ID
                    foreach (var pu in Purchases)
                    {
                        if (pu.Lines == null) continue;
                        foreach (var l in pu.Lines)
                        {
                            if (l.ItemId == dup.Id)
                            {
                                l.ItemId = primary.Id;
                                l.Name = primary.Name;
                                if (!string.IsNullOrWhiteSpace(primary.Sku)) l.Sku = primary.Sku;
                            }
                        }
                    }

                    RawMaterials.Remove(dup);
                    modified = true;
                }
            }

            if (modified)
            {
                SaveLocalState();
            }
        }

        public async Task SaveProductAsync(ProductItem p)
        {
            if (p.RegisteredQty <= 0 && p.Stock > 0) p.RegisteredQty = p.Stock;
            if (p.Barcode == "—" || p.Barcode == "-") p.Barcode = string.Empty;
            if (p.Hsn == "—" || p.Hsn == "-") p.Hsn = string.Empty;

            int idx = Products.FindIndex(x => x.Id == p.Id);
            if (idx >= 0) Products[idx] = p;
            else Products.Insert(0, p);

            NotifyDataChanged();
            SaveLocalState();

            if (!IsSampleDataLoaded)
            {
                SyncQueue.EnqueueSet("products", p.Id, p);
                if (Firebase != null)
                {
                    try { await Firebase.SaveProductAsync(p); } catch { }
                }
            }
        }

        public async Task DeleteProductAsync(string id)
        {
            Products.RemoveAll(x => x.Id == id);
            NotifyDataChanged();
            SaveLocalState();

            if (!IsSampleDataLoaded)
            {
                SyncQueue.EnqueueDelete("products", id);
                if (Firebase != null)
                {
                    try { await Firebase.DeleteProductAsync(id); } catch { }
                }
            }
        }

        public async Task SaveRawMaterialAsync(RawMaterialItem r)
        {
            if (r.RegisteredQty <= 0 && r.Stock > 0) r.RegisteredQty = r.Stock;
            if (r.Barcode == "—" || r.Barcode == "-") r.Barcode = string.Empty;
            if (r.Hsn == "—" || r.Hsn == "-") r.Hsn = string.Empty;

            int idx = RawMaterials.FindIndex(x => x.Id == r.Id);
            if (idx >= 0) RawMaterials[idx] = r;
            else RawMaterials.Insert(0, r);

            NotifyDataChanged();
            SaveLocalState();

            if (!IsSampleDataLoaded)
            {
                SyncQueue.EnqueueSet("raw_materials", r.Id, r);
                if (Firebase != null)
                {
                    try { await Firebase.SaveRawMaterialAsync(r); } catch { }
                }
            }
        }

        public async Task DeleteRawMaterialAsync(string id)
        {
            RawMaterials.RemoveAll(x => x.Id == id);
            NotifyDataChanged();
            SaveLocalState();

            if (!IsSampleDataLoaded)
            {
                SyncQueue.EnqueueDelete("raw_materials", id);
                if (Firebase != null)
                {
                    try { await Firebase.DeleteRawMaterialAsync(id); } catch { }
                }
            }
        }

        public async Task SaveGeneralItemAsync(GeneralItem g)
        {
            if (g.RegisteredQty <= 0 && g.Stock > 0) g.RegisteredQty = g.Stock;
            if (g.Barcode == "—" || g.Barcode == "-") g.Barcode = string.Empty;
            if (g.Hsn == "—" || g.Hsn == "-") g.Hsn = string.Empty;

            int idx = GeneralItems.FindIndex(x => x.Id == g.Id);
            if (idx >= 0) GeneralItems[idx] = g;
            else GeneralItems.Insert(0, g);

            NotifyDataChanged();
            SaveLocalState();

            if (!IsSampleDataLoaded)
            {
                SyncQueue.EnqueueSet("general_items", g.Id, g);
                if (Firebase != null)
                {
                    try { await Firebase.SaveGeneralItemAsync(g); } catch { }
                }
            }
        }

        public async Task DeleteGeneralItemAsync(string id)
        {
            GeneralItems.RemoveAll(x => x.Id == id);
            NotifyDataChanged();
            SaveLocalState();

            if (!IsSampleDataLoaded)
            {
                SyncQueue.EnqueueDelete("general_items", id);
                if (Firebase != null)
                {
                    try { await Firebase.DeleteGeneralItemAsync(id); } catch { }
                }
            }
        }
    }
}
