using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using AsiErp.Models;

namespace AsiErp.Services
{
    public class CloudDataBundle
    {
        public List<ProductItem> Products { get; set; } = new();
        public List<RawMaterialItem> RawMaterials { get; set; } = new();
        public List<GeneralItem> GeneralItems { get; set; } = new();
        public List<SaleDoc> Sales { get; set; } = new();
        public List<PurchaseDoc> Purchases { get; set; } = new();
        public List<WorkOrderDoc> WorkOrders { get; set; } = new();
        public List<ExpenseItem> Expenses { get; set; } = new();
        public List<InvestmentItem> Investments { get; set; } = new();
        public List<AppUser> Users { get; set; } = new();
        public Dictionary<string, int> Sequences { get; set; } = new();

        public int TotalRecords => Products.Count + RawMaterials.Count + GeneralItems.Count +
                                  Sales.Count + Purchases.Count + WorkOrders.Count +
                                  Expenses.Count + Investments.Count + Users.Count;
    }

    public class FirebaseService
    {
        private FirestoreDb? _db;
        private string _projectId = string.Empty;
        private bool _isConnected = false;
        private string _statusMessage = "Not connected";
        private string _keyFilePath = string.Empty;

        public bool IsConnected => _isConnected;
        public string ProjectId => _projectId;
        public string StatusMessage => _statusMessage;
        public string KeyFilePath => _keyFilePath;

        public event Action? ConnectionChanged;

        public FirebaseService()
        {
            TryInitialize();
        }

        public void TryInitialize()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AsiErp");
                
                string[] searchLocations = new[]
                {
                    Path.Combine(baseDir, "firebase-key.json"),
                    Path.Combine(appDataDir, "firebase-key.json"),
                    Path.Combine(Directory.GetCurrentDirectory(), "firebase-key.json"),
                    Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "firebase-key.json")),
                    Path.GetFullPath(Path.Combine(baseDir, "..", "..", "firebase-key.json")),
                    Path.GetFullPath(Path.Combine(baseDir, "..", "firebase-key.json"))
                };

                string keyPath = string.Empty;
                foreach (var loc in searchLocations)
                {
                    if (File.Exists(loc))
                    {
                        keyPath = loc;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(keyPath))
                {
                    _isConnected = false;
                    _statusMessage = "firebase-key.json not found. Running in local mode.";
                    ConnectionChanged?.Invoke();
                    return;
                }

                _keyFilePath = keyPath;
                string jsonText = File.ReadAllText(keyPath);

                using var doc = JsonDocument.Parse(jsonText);
                if (doc.RootElement.TryGetProperty("project_id", out var projProp))
                {
                    _projectId = projProp.GetString() ?? string.Empty;
                }

                if (string.IsNullOrWhiteSpace(_projectId) || jsonText.Contains("PLACEHOLDER_KEY_REPLACE_WITH_YOUR_FIREBASE"))
                {
                    _isConnected = false;
                    _statusMessage = "Replace firebase-key.json with your Firebase Service Account to enable cloud sync.";
                    ConnectionChanged?.Invoke();
                    return;
                }

                Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", keyPath);

                var credential = GoogleCredential.FromFile(keyPath);
                var builder = new FirestoreDbBuilder
                {
                    ProjectId = _projectId,
                    Credential = credential
                };
                _db = builder.Build();

                _isConnected = true;
                _statusMessage = $"Connected to Firebase Firestore ({_projectId})";
            }
            catch (Exception ex)
            {
                _isConnected = false;
                _statusMessage = $"Firebase init note: {ex.Message}";
            }

            ConnectionChanged?.Invoke();
        }

        public async Task<bool> TestConnectionAsync()
        {
            if (_db == null || !_isConnected)
            {
                TryInitialize();
                if (_db == null || !_isConnected) return false;
            }

            try
            {
                var docRef = _db.Collection("meta").Document("health");
                await docRef.SetAsync(new Dictionary<string, object>
                {
                    { "lastSync", DateTime.UtcNow.ToString("o") },
                    { "app", "ASI ERP" },
                    { "version", "2.0" }
                });
                _statusMessage = $"Connected to Firebase ({_projectId})";
                ConnectionChanged?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                _isConnected = false;
                _statusMessage = $"Firebase connection error: {ex.Message}";
                ConnectionChanged?.Invoke();
                return false;
            }
        }

        // --- READ / LOAD METHODS FROM FIRESTORE ---

        public async Task<CloudDataBundle> LoadAllFromCloudAsync()
        {
            var bundle = new CloudDataBundle();
            if (_db == null || !_isConnected) return bundle;

            try
            {
                var tProducts = GetProductsAsync();
                var tRaw = GetRawMaterialsAsync();
                var tGen = GetGeneralItemsAsync();
                var tSales = GetSalesAsync();
                var tPurchases = GetPurchasesAsync();
                var tOrders = GetWorkOrdersAsync();
                var tExpenses = GetExpensesAsync();
                var tInvest = GetInvestmentsAsync();
                var tUsers = GetUsersAsync();
                var tSeq = GetSequencesAsync();

                await Task.WhenAll(tProducts, tRaw, tGen, tSales, tPurchases, tOrders, tExpenses, tInvest, tUsers, tSeq);

                bundle.Products = await tProducts;
                bundle.RawMaterials = await tRaw;
                bundle.GeneralItems = await tGen;
                bundle.Sales = await tSales;
                bundle.Purchases = await tPurchases;
                bundle.WorkOrders = await tOrders;
                bundle.Expenses = await tExpenses;
                bundle.Investments = await tInvest;
                bundle.Users = await tUsers;
                bundle.Sequences = await tSeq;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading cloud bundle: {ex.Message}");
            }

            return bundle;
        }

        public async Task<List<ProductItem>> GetProductsAsync()
        {
            var list = new List<ProductItem>();
            if (_db == null || !_isConnected) return list;
            try
            {
                var snap = await _db.Collection("products").GetSnapshotAsync();
                foreach (var doc in snap.Documents)
                {
                    if (doc.Exists)
                    {
                        var item = doc.ConvertTo<ProductItem>();
                        if (string.IsNullOrEmpty(item.Id)) item.Id = doc.Id;
                        list.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetProductsAsync error: {ex.Message}");
            }
            return list;
        }

        public async Task<List<RawMaterialItem>> GetRawMaterialsAsync()
        {
            var list = new List<RawMaterialItem>();
            if (_db == null || !_isConnected) return list;
            try
            {
                var snap = await _db.Collection("raw_materials").GetSnapshotAsync();
                foreach (var doc in snap.Documents)
                {
                    if (doc.Exists)
                    {
                        var item = doc.ConvertTo<RawMaterialItem>();
                        if (string.IsNullOrEmpty(item.Id)) item.Id = doc.Id;
                        list.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetRawMaterialsAsync error: {ex.Message}");
            }
            return list;
        }

        public async Task<List<GeneralItem>> GetGeneralItemsAsync()
        {
            var list = new List<GeneralItem>();
            if (_db == null || !_isConnected) return list;
            try
            {
                var snap = await _db.Collection("general_items").GetSnapshotAsync();
                foreach (var doc in snap.Documents)
                {
                    if (doc.Exists)
                    {
                        var item = doc.ConvertTo<GeneralItem>();
                        if (string.IsNullOrEmpty(item.Id)) item.Id = doc.Id;
                        list.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetGeneralItemsAsync error: {ex.Message}");
            }
            return list;
        }

        public async Task<List<SaleDoc>> GetSalesAsync()
        {
            var list = new List<SaleDoc>();
            if (_db == null || !_isConnected) return list;
            try
            {
                var snap = await _db.Collection("sales").GetSnapshotAsync();
                foreach (var doc in snap.Documents)
                {
                    if (doc.Exists)
                    {
                        var item = doc.ConvertTo<SaleDoc>();
                        if (string.IsNullOrEmpty(item.Id)) item.Id = doc.Id;
                        list.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetSalesAsync error: {ex.Message}");
            }
            return list.OrderByDescending(s => s.Date).ThenByDescending(s => s.InvoiceNo).ToList();
        }

        public async Task<List<PurchaseDoc>> GetPurchasesAsync()
        {
            var list = new List<PurchaseDoc>();
            if (_db == null || !_isConnected) return list;
            try
            {
                var snap = await _db.Collection("purchases").GetSnapshotAsync();
                foreach (var doc in snap.Documents)
                {
                    if (doc.Exists)
                    {
                        var item = doc.ConvertTo<PurchaseDoc>();
                        if (string.IsNullOrEmpty(item.Id)) item.Id = doc.Id;
                        list.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetPurchasesAsync error: {ex.Message}");
            }
            return list.OrderByDescending(p => p.Date).ThenByDescending(p => p.BillNo).ToList();
        }

        public async Task<List<WorkOrderDoc>> GetWorkOrdersAsync()
        {
            var list = new List<WorkOrderDoc>();
            if (_db == null || !_isConnected) return list;
            try
            {
                var snap = await _db.Collection("work_orders").GetSnapshotAsync();
                foreach (var doc in snap.Documents)
                {
                    if (doc.Exists)
                    {
                        var item = doc.ConvertTo<WorkOrderDoc>();
                        if (string.IsNullOrEmpty(item.Id)) item.Id = doc.Id;
                        list.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetWorkOrdersAsync error: {ex.Message}");
            }
            return list.OrderByDescending(w => w.OrderDate).ToList();
        }

        public async Task<List<ExpenseItem>> GetExpensesAsync()
        {
            var list = new List<ExpenseItem>();
            if (_db == null || !_isConnected) return list;
            try
            {
                var snap = await _db.Collection("expenses").GetSnapshotAsync();
                foreach (var doc in snap.Documents)
                {
                    if (doc.Exists)
                    {
                        var item = doc.ConvertTo<ExpenseItem>();
                        if (string.IsNullOrEmpty(item.Id)) item.Id = doc.Id;
                        list.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetExpensesAsync error: {ex.Message}");
            }
            return list.OrderByDescending(e => e.Date).ToList();
        }

        public async Task<List<InvestmentItem>> GetInvestmentsAsync()
        {
            var list = new List<InvestmentItem>();
            if (_db == null || !_isConnected) return list;
            try
            {
                var snap = await _db.Collection("investments").GetSnapshotAsync();
                foreach (var doc in snap.Documents)
                {
                    if (doc.Exists)
                    {
                        var item = doc.ConvertTo<InvestmentItem>();
                        if (string.IsNullOrEmpty(item.Id)) item.Id = doc.Id;
                        list.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetInvestmentsAsync error: {ex.Message}");
            }
            return list.OrderByDescending(i => i.Date).ToList();
        }

        public async Task<Dictionary<string, int>> GetSequencesAsync()
        {
            var result = new Dictionary<string, int>();
            if (_db == null || !_isConnected) return result;
            try
            {
                var doc = await _db.Collection("meta").Document("sequences").GetSnapshotAsync();
                if (doc.Exists)
                {
                    var dict = doc.ToDictionary();
                    foreach (var kvp in dict)
                    {
                        if (int.TryParse(kvp.Value?.ToString(), out int val))
                        {
                            result[kvp.Key] = val;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetSequencesAsync error: {ex.Message}");
            }
            return result;
        }

        public async Task<List<AppUser>> GetUsersAsync()
        {
            var list = new List<AppUser>();
            if (_db == null || !_isConnected) return list;
            try
            {
                var snap = await _db.Collection("users").GetSnapshotAsync();
                foreach (var doc in snap.Documents)
                {
                    if (doc.Exists)
                    {
                        var item = doc.ConvertTo<AppUser>();
                        if (string.IsNullOrEmpty(item.Id)) item.Id = doc.Id;
                        list.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetUsersAsync error: {ex.Message}");
            }
            return list;
        }

        public async Task SaveUserAsync(AppUser user)
        {
            if (_db == null || !_isConnected || string.IsNullOrEmpty(user.Id)) return;
            try
            {
                var doc = _db.Collection("users").Document(user.Id);
                await doc.SetAsync(user);
            }
            catch { }
        }

        public async Task DeleteUserAsync(string id)
        {
            if (_db == null || !_isConnected || string.IsNullOrEmpty(id)) return;
            try
            {
                await _db.Collection("users").Document(id).DeleteAsync();
            }
            catch { }
        }

        // --- WRITE / SAVE / DELETE METHODS ---

        public async Task<bool> ProcessSingleSyncItemAsync(string operation, string collection, string documentId, string jsonPayload)
        {
            if (_db == null || !_isConnected)
            {
                TryInitialize();
                if (_db == null || !_isConnected) return false;
            }

            try
            {
                if (operation == "delete")
                {
                    await _db.Collection(collection).Document(documentId).DeleteAsync();
                    return true;
                }

                switch (collection)
                {
                    case "products":
                        var prod = JsonSerializer.Deserialize<ProductItem>(jsonPayload);
                        if (prod != null) await _db.Collection("products").Document(documentId).SetAsync(prod);
                        break;

                    case "raw_materials":
                        var rm = JsonSerializer.Deserialize<RawMaterialItem>(jsonPayload);
                        if (rm != null) await _db.Collection("raw_materials").Document(documentId).SetAsync(rm);
                        break;

                    case "general_items":
                        var gen = JsonSerializer.Deserialize<GeneralItem>(jsonPayload);
                        if (gen != null) await _db.Collection("general_items").Document(documentId).SetAsync(gen);
                        break;

                    case "sales":
                        var sale = JsonSerializer.Deserialize<SaleDoc>(jsonPayload);
                        if (sale != null) await _db.Collection("sales").Document(documentId).SetAsync(sale);
                        break;

                    case "purchases":
                        var purchase = JsonSerializer.Deserialize<PurchaseDoc>(jsonPayload);
                        if (purchase != null) await _db.Collection("purchases").Document(documentId).SetAsync(purchase);
                        break;

                    case "work_orders":
                        var wo = JsonSerializer.Deserialize<WorkOrderDoc>(jsonPayload);
                        if (wo != null) await _db.Collection("work_orders").Document(documentId).SetAsync(wo);
                        break;

                    case "expenses":
                        var exp = JsonSerializer.Deserialize<ExpenseItem>(jsonPayload);
                        if (exp != null) await _db.Collection("expenses").Document(documentId).SetAsync(exp);
                        break;

                    case "investments":
                        var inv = JsonSerializer.Deserialize<InvestmentItem>(jsonPayload);
                        if (inv != null) await _db.Collection("investments").Document(documentId).SetAsync(inv);
                        break;

                    case "users":
                        var usr = JsonSerializer.Deserialize<AppUser>(jsonPayload);
                        if (usr != null) await _db.Collection("users").Document(documentId).SetAsync(usr);
                        break;

                    case "meta":
                        if (documentId == "sequences")
                        {
                            var seq = JsonSerializer.Deserialize<Dictionary<string, int>>(jsonPayload);
                            if (seq != null)
                            {
                                var dict = new Dictionary<string, object>();
                                foreach (var kvp in seq) dict[kvp.Key] = kvp.Value;
                                await _db.Collection("meta").Document("sequences").SetAsync(dict);
                            }
                        }
                        else
                        {
                            var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonPayload);
                            if (dict != null) await _db.Collection("meta").Document(documentId).SetAsync(dict);
                        }
                        break;

                    default:
                        var genericDict = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonPayload);
                        if (genericDict != null) await _db.Collection(collection).Document(documentId).SetAsync(genericDict);
                        break;
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ProcessSingleSyncItemAsync error on {collection}/{documentId}: {ex.Message}");
                return false;
            }
        }

        public async Task SaveSequencesAsync(Dictionary<string, int> sequences)
        {
            if (_db == null || !_isConnected) return;
            try
            {
                var doc = _db.Collection("meta").Document("sequences");
                var dict = new Dictionary<string, object>();
                foreach (var kvp in sequences) dict[kvp.Key] = kvp.Value;
                await doc.SetAsync(dict);
            }
            catch { }
        }

        public async Task SaveProductAsync(ProductItem item)
        {
            if (_db == null || !_isConnected || string.IsNullOrEmpty(item.Id)) return;
            try
            {
                var doc = _db.Collection("products").Document(item.Id);
                await doc.SetAsync(item);
            }
            catch { }
        }

        public async Task DeleteProductAsync(string id)
        {
            if (_db == null || !_isConnected || string.IsNullOrEmpty(id)) return;
            try
            {
                await _db.Collection("products").Document(id).DeleteAsync();
            }
            catch { }
        }

        public async Task SaveRawMaterialAsync(RawMaterialItem item)
        {
            if (_db == null || !_isConnected || string.IsNullOrEmpty(item.Id)) return;
            try
            {
                var doc = _db.Collection("raw_materials").Document(item.Id);
                await doc.SetAsync(item);
            }
            catch { }
        }

        public async Task DeleteRawMaterialAsync(string id)
        {
            if (_db == null || !_isConnected || string.IsNullOrEmpty(id)) return;
            try
            {
                await _db.Collection("raw_materials").Document(id).DeleteAsync();
            }
            catch { }
        }

        public async Task SaveGeneralItemAsync(GeneralItem item)
        {
            if (_db == null || !_isConnected || string.IsNullOrEmpty(item.Id)) return;
            try
            {
                var doc = _db.Collection("general_items").Document(item.Id);
                await doc.SetAsync(item);
            }
            catch { }
        }

        public async Task DeleteGeneralItemAsync(string id)
        {
            if (_db == null || !_isConnected || string.IsNullOrEmpty(id)) return;
            try
            {
                await _db.Collection("general_items").Document(id).DeleteAsync();
            }
            catch { }
        }

        public async Task SaveSaleAsync(SaleDoc sale)
        {
            if (_db == null || !_isConnected || string.IsNullOrEmpty(sale.Id)) return;
            try
            {
                var doc = _db.Collection("sales").Document(sale.Id);
                await doc.SetAsync(sale);
            }
            catch { }
        }

        public async Task DeleteSaleAsync(string id)
        {
            if (_db == null || !_isConnected || string.IsNullOrEmpty(id)) return;
            try
            {
                await _db.Collection("sales").Document(id).DeleteAsync();
            }
            catch { }
        }

        public async Task SavePurchaseAsync(PurchaseDoc purchase)
        {
            if (_db == null || !_isConnected || string.IsNullOrEmpty(purchase.Id)) return;
            try
            {
                var doc = _db.Collection("purchases").Document(purchase.Id);
                await doc.SetAsync(purchase);
            }
            catch { }
        }

        public async Task DeletePurchaseAsync(string id)
        {
            if (_db == null || !_isConnected || string.IsNullOrEmpty(id)) return;
            try
            {
                await _db.Collection("purchases").Document(id).DeleteAsync();
            }
            catch { }
        }

        public async Task SaveWorkOrderAsync(WorkOrderDoc wo)
        {
            if (_db == null || !_isConnected || string.IsNullOrEmpty(wo.Id)) return;
            try
            {
                var doc = _db.Collection("work_orders").Document(wo.Id);
                await doc.SetAsync(wo);
            }
            catch { }
        }

        public async Task DeleteWorkOrderAsync(string id)
        {
            if (_db == null || !_isConnected || string.IsNullOrEmpty(id)) return;
            try
            {
                await _db.Collection("work_orders").Document(id).DeleteAsync();
            }
            catch { }
        }

        public async Task SaveExpenseAsync(ExpenseItem exp)
        {
            if (_db == null || !_isConnected || string.IsNullOrEmpty(exp.Id)) return;
            try
            {
                var doc = _db.Collection("expenses").Document(exp.Id);
                await doc.SetAsync(exp);
            }
            catch { }
        }

        public async Task DeleteExpenseAsync(string id)
        {
            if (_db == null || !_isConnected || string.IsNullOrEmpty(id)) return;
            try
            {
                await _db.Collection("expenses").Document(id).DeleteAsync();
            }
            catch { }
        }

        public async Task SaveInvestmentAsync(InvestmentItem inv)
        {
            if (_db == null || !_isConnected || string.IsNullOrEmpty(inv.Id)) return;
            try
            {
                var doc = _db.Collection("investments").Document(inv.Id);
                await doc.SetAsync(inv);
            }
            catch { }
        }

        public async Task DeleteInvestmentAsync(string id)
        {
            if (_db == null || !_isConnected || string.IsNullOrEmpty(id)) return;
            try
            {
                await _db.Collection("investments").Document(id).DeleteAsync();
            }
            catch { }
        }

        // --- BATCH HELPERS FOR BULK SYNC ---

        private async Task BatchSaveChunkedAsync<T>(string collectionName, IEnumerable<T> items, Func<T, string> idSelector)
        {
            if (_db == null || !_isConnected) return;
            var list = items.ToList();
            if (list.Count == 0) return;

            for (int i = 0; i < list.Count; i += 400)
            {
                var chunk = list.Skip(i).Take(400);
                var batch = _db.StartBatch();
                foreach (var item in chunk)
                {
                    string id = idSelector(item);
                    if (!string.IsNullOrEmpty(id))
                    {
                        var doc = _db.Collection(collectionName).Document(id);
                        batch.Set(doc, item);
                    }
                }
                await batch.CommitAsync();
            }
        }

        public async Task SaveProductsAsync(IEnumerable<ProductItem> items)
        {
            try
            {
                await BatchSaveChunkedAsync("products", items, p => p.Id);
            }
            catch { }
        }

        public async Task SaveRawMaterialsAsync(IEnumerable<RawMaterialItem> items)
        {
            try
            {
                await BatchSaveChunkedAsync("raw_materials", items, r => r.Id);
            }
            catch { }
        }

        public async Task SaveGeneralItemsAsync(IEnumerable<GeneralItem> items)
        {
            try
            {
                await BatchSaveChunkedAsync("general_items", items, g => g.Id);
            }
            catch { }
        }

        public async Task SaveSalesAsync(IEnumerable<SaleDoc> items)
        {
            try
            {
                await BatchSaveChunkedAsync("sales", items, s => s.Id);
            }
            catch { }
        }

        public async Task SavePurchasesAsync(IEnumerable<PurchaseDoc> items)
        {
            try
            {
                await BatchSaveChunkedAsync("purchases", items, p => p.Id);
            }
            catch { }
        }

        public async Task SaveWorkOrdersAsync(IEnumerable<WorkOrderDoc> items)
        {
            try
            {
                await BatchSaveChunkedAsync("work_orders", items, w => w.Id);
            }
            catch { }
        }

        public async Task SaveExpensesAsync(IEnumerable<ExpenseItem> items)
        {
            try
            {
                await BatchSaveChunkedAsync("expenses", items, e => e.Id);
            }
            catch { }
        }

        public async Task SaveInvestmentsAsync(IEnumerable<InvestmentItem> items)
        {
            try
            {
                await BatchSaveChunkedAsync("investments", items, i => i.Id);
            }
            catch { }
        }
    }
}
