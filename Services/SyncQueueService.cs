using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AsiErp.Services
{
    public class SyncQueueItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Operation { get; set; } = "set"; // "set", "delete"
        public string Collection { get; set; } = string.Empty;
        public string DocumentId { get; set; } = string.Empty;
        public string PayloadJson { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int RetryCount { get; set; } = 0;
        public string? LastError { get; set; }
    }

    public class SyncQueueService
    {
        private readonly FirebaseService _firebase;
        private readonly string _queueFilePath;
        private readonly List<SyncQueueItem> _queue = new();
        private readonly object _lock = new();
        private Timer? _syncTimer;
        private bool _isProcessing = false;

        public event Action<int, string>? QueueChanged;

        public int PendingCount
        {
            get
            {
                lock (_lock)
                {
                    return _queue.Count;
                }
            }
        }

        public bool IsProcessing => _isProcessing;

        public SyncQueueService(FirebaseService firebase)
        {
            _firebase = firebase;
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AsiErp");
            Directory.CreateDirectory(folder);
            _queueFilePath = Path.Combine(folder, "sync_queue.json");

            LoadQueue();

            // Run sync check every 4 seconds
            _syncTimer = new Timer(async _ => await ProcessQueueAsync(), null, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(4));
        }

        public void EnqueueSet<T>(string collection, string documentId, T item)
        {
            if (string.IsNullOrEmpty(documentId)) return;
            string json = JsonSerializer.Serialize(item);

            lock (_lock)
            {
                // Coalesce / deduplicate: If this same document is already queued to be set, update its payload
                var existing = _queue.FirstOrDefault(x => x.Collection == collection && x.DocumentId == documentId && x.Operation == "set");
                if (existing != null)
                {
                    existing.PayloadJson = json;
                    existing.CreatedAt = DateTime.UtcNow;
                }
                else
                {
                    _queue.Add(new SyncQueueItem
                    {
                        Operation = "set",
                        Collection = collection,
                        DocumentId = documentId,
                        PayloadJson = json
                    });
                }

                SaveQueue();
            }

            QueueChanged?.Invoke(PendingCount, $"Queued {collection}/{documentId}");

            // Trigger sync immediately if online
            _ = Task.Run(ProcessQueueAsync);
        }

        public void EnqueueDelete(string collection, string documentId)
        {
            if (string.IsNullOrEmpty(documentId)) return;

            lock (_lock)
            {
                // Remove any pending set operations for this document
                _queue.RemoveAll(x => x.Collection == collection && x.DocumentId == documentId);

                _queue.Add(new SyncQueueItem
                {
                    Operation = "delete",
                    Collection = collection,
                    DocumentId = documentId
                });

                SaveQueue();
            }

            QueueChanged?.Invoke(PendingCount, $"Queued delete {collection}/{documentId}");
            _ = Task.Run(ProcessQueueAsync);
        }

        public async Task ProcessQueueAsync()
        {
            if (_isProcessing) return;

            lock (_lock)
            {
                if (_queue.Count == 0) return;
            }

            _isProcessing = true;
            try
            {
                // Quick test of connectivity before starting
                if (!_firebase.IsConnected)
                {
                    _firebase.TryInitialize();
                }

                if (!_firebase.IsConnected)
                {
                    _isProcessing = false;
                    return;
                }

                bool canReachCloud = await _firebase.TestConnectionAsync();
                if (!canReachCloud)
                {
                    _isProcessing = false;
                    return;
                }

                // Process items one by one in FIFO order
                while (true)
                {
                    SyncQueueItem? current = null;
                    int remaining = 0;

                    lock (_lock)
                    {
                        if (_queue.Count > 0)
                        {
                            current = _queue[0];
                            remaining = _queue.Count;
                        }
                    }

                    if (current == null) break;

                    QueueChanged?.Invoke(remaining, $"Sending {current.Collection}/{current.DocumentId} to cloud...");

                    bool success = await _firebase.ProcessSingleSyncItemAsync(
                        current.Operation,
                        current.Collection,
                        current.DocumentId,
                        current.PayloadJson);

                    if (success)
                    {
                        lock (_lock)
                        {
                            _queue.Remove(current);
                            SaveQueue();
                            remaining = _queue.Count;
                        }

                        QueueChanged?.Invoke(remaining, remaining > 0 ? $"{remaining} items pending..." : "All items synced!");

                        // Short delay between sends for smooth transmission
                        await Task.Delay(100);
                    }
                    else
                    {
                        // Failed to send (connection may have dropped)
                        current.RetryCount++;
                        current.LastError = "Failed to sync. Will retry.";
                        lock (_lock)
                        {
                            SaveQueue();
                        }
                        // Stop this sync round and wait for next interval
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ProcessQueueAsync exception: {ex.Message}");
            }
            finally
            {
                _isProcessing = false;
                QueueChanged?.Invoke(PendingCount, PendingCount == 0 ? "Synced" : $"{PendingCount} pending");
            }
        }

        public void ClearQueue()
        {
            lock (_lock)
            {
                _queue.Clear();
                SaveQueue();
            }
            QueueChanged?.Invoke(0, "Queue cleared");
        }

        private void SaveQueue()
        {
            try
            {
                string json = JsonSerializer.Serialize(_queue, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_queueFilePath, json);
            }
            catch { }
        }

        private void LoadQueue()
        {
            try
            {
                if (File.Exists(_queueFilePath))
                {
                    string json = File.ReadAllText(_queueFilePath);
                    var items = JsonSerializer.Deserialize<List<SyncQueueItem>>(json);
                    if (items != null)
                    {
                        lock (_lock)
                        {
                            _queue.Clear();
                            _queue.AddRange(items);
                        }
                    }
                }
            }
            catch { }
        }
    }
}
