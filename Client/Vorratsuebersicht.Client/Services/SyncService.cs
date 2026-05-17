using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Vorratsuebersicht.Client.Models;

namespace Vorratsuebersicht.Client.Services
{
    public class SyncService
    {
        private readonly HttpClient _http;
        private readonly LocalDatabase _db;
        private string _masterUrl;
        private string _accessKey;
        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore
        };

        public string MasterUrl => _masterUrl;
        public bool IsConnected { get; private set; }
        public string DatabaseId { get; private set; }

        public SyncService(LocalDatabase db)
        {
            _db = db;
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        }

        public void Configure(string masterUrl, string accessKey = null)
        {
            _masterUrl = masterUrl?.TrimEnd('/');
            _accessKey = accessKey?.Trim();
        }

        private HttpRequestMessage CreateRequest(HttpMethod method, string path)
        {
            var req = new HttpRequestMessage(method, Url(path));
            if (!string.IsNullOrEmpty(_accessKey))
                req.Headers.Add("X-Access-Key", _accessKey);
            return req;
        }

        private async Task<string> SendAsync(HttpRequestMessage req)
        {
            var res = await _http.SendAsync(req);
            res.EnsureSuccessStatusCode();
            return await res.Content.ReadAsStringAsync();
        }

        private string Url(string path) => $"{_masterUrl}{path}";

        public async Task<bool> PingAsync()
        {
            var (ok, _) = await PingWithErrorAsync();
            return ok;
        }

        public async Task<(bool Success, string Error)> PingWithErrorAsync()
        {
            try
            {
                var res = await _http.SendAsync(CreateRequest(HttpMethod.Get, "/api/discovery/ping"));
                IsConnected = res.IsSuccessStatusCode;
                if (IsConnected)
                    return (true, null);
                var body = await res.Content.ReadAsStringAsync();
                return (false, $"HTTP {(int)res.StatusCode}: {body}");
            }
            catch (Exception ex)
            {
                IsConnected = false;
                return (false, ex.Message);
            }
        }

        public async Task<Dictionary<string, object>> GetDiscoveryInfoAsync()
        {
            try
            {
                var json = await SendAsync(CreateRequest(HttpMethod.Get, "/api/discovery"));
                var info = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                if (info?.ContainsKey("databaseId") == true)
                    DatabaseId = info["databaseId"]?.ToString();
                return info;
            }
            catch { return null; }
        }

        /// <summary>Komplette Synchronisation: Holt alle Daten vom Master.</summary>
        public async Task<bool> FullSyncAsync(IProgress<string> progress = null)
        {
            try
            {
                progress?.Report("Verbinde mit Master...");
                if (!await PingAsync())
                {
                    progress?.Report("Fehler: Master nicht erreichbar");
                    return false;
                }

                progress?.Report("Lade Artikel...");
                var articles = await GetAsync<List<Article>>("/api/articles");
                if (articles != null)
                {
                    await _db.ClearAllAsync();
                    foreach (var a in articles)
                        await _db.SaveArticleAsync(a);
                    progress?.Report($"{articles.Count} Artikel geladen");
                }

                progress?.Report("Lade Lagerbestand...");
                var storageItems = await GetAsync<List<StorageItem>>("/api/storage-items");
                if (storageItems != null)
                {
                    foreach (var s in storageItems)
                        await _db.SaveStorageItemAsync(s);
                    progress?.Report($"{storageItems.Count} Lagerpositionen geladen");
                }

                progress?.Report("Lade Einkaufsliste...");
                var shoppingItems = await GetAsync<List<ShoppingItem>>("/api/shopping-items");
                if (shoppingItems != null)
                {
                    foreach (var s in shoppingItems)
                        await _db.SaveShoppingItemAsync(s);
                    progress?.Report($"{shoppingItems.Count} Einkaufsartikel geladen");
                }

                progress?.Report("Synchronisation abgeschlossen!");
                return true;
            }
            catch (Exception ex)
            {
                progress?.Report($"Fehler: {ex.Message}");
                return false;
            }
        }

        /// <summary>Inkrementelle Synchronisation: Nur �nderungen seit letztem Sync.</summary>
        public async Task<bool> IncrementalSyncAsync(IProgress<string> progress = null)
        {
            try
            {
                if (!await PingAsync())
                {
                    progress?.Report("Master nicht erreichbar");
                    return false;
                }

                var lastSync = await _db.GetLastSyncAsync();
                if (lastSync == DateTime.MinValue)
                {
                    progress?.Report("Erster Sync - vollst�ndige Synchronisation...");
                    return await FullSyncAsync(progress);
                }

                progress?.Report($"Pr�fe �nderungen seit {lastSync:g}...");
                var changes = await GetAsync<List<ChangeEntry>>(
                    $"/api/sync/changes?since={Uri.EscapeDataString(lastSync.ToString("O"))}");

                if (changes == null || changes.Count == 0)
                {
                    progress?.Report("Keine �nderungen seit letztem Sync");
                    return true;
                }

                progress?.Report($"{changes.Count} �nderungen gefunden, wende an...");
                foreach (var change in changes)
                {
                    await ApplyChangeAsync(change);
                }

                progress?.Report($"{changes.Count} �nderungen �bernommen");
                return true;
            }
            catch (Exception ex)
            {
                progress?.Report($"Fehler: {ex.Message}");
                return false;
            }
        }

        private async Task ApplyChangeAsync(ChangeEntry change)
        {
            switch (change.EntityType)
            {
                case "Article":
                    if (change.Operation == "delete")
                        await _db.DeleteArticleAsync(change.EntityId);
                    else if (change.Data != null)
                        await _db.SaveArticleAsync(JsonConvert.DeserializeObject<Article>(change.Data.ToString()));
                    break;

                case "StorageItem":
                    if (change.Operation == "delete")
                        await _db.DeleteStorageItemAsync(change.EntityId);
                    else if (change.Data != null)
                        await _db.SaveStorageItemAsync(JsonConvert.DeserializeObject<StorageItem>(change.Data.ToString()));
                    break;

                case "ShoppingItem":
                    if (change.Operation == "delete")
                        await _db.DeleteShoppingItemAsync(change.EntityId);
                    else if (change.Data != null)
                        await _db.SaveShoppingItemAsync(JsonConvert.DeserializeObject<ShoppingItem>(change.Data.ToString()));
                    break;
            }
        }

        private async Task<T> GetAsync<T>(string path)
        {
            try
            {
                var json = await SendAsync(CreateRequest(HttpMethod.Get, path));
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch { return default; }
        }

        private class ChangeEntry
        {
            public int SyncChangeLogId { get; set; }
            public string EntityType { get; set; }
            public int EntityId { get; set; }
            public string Operation { get; set; }
            public string Timestamp { get; set; }
            public object Data { get; set; }
        }
    }
}
