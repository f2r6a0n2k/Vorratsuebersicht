using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace VorratsUebersicht
{
    public class SyncClient
    {
        private readonly HttpClient _http;
        private string _serverUrl;

        public string ServerUrl => _serverUrl;
        public bool IsConnected { get; private set; }
        public string DatabaseId { get; private set; }

        public SyncClient()
        {
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        }

        public void Configure(string serverUrl)
        {
            _serverUrl = serverUrl.TrimEnd('/');
        }

        private string Url(string path) => $"{_serverUrl}{path}";

        /// <summary>Verbindung zum Master pr�fen.</summary>
        public async Task<bool> PingAsync()
        {
            try
            {
                var res = await _http.GetAsync(Url("/api/discovery/ping"));
                IsConnected = res.IsSuccessStatusCode;
                return IsConnected;
            }
            catch { IsConnected = false; return false; }
        }

        /// <summary>Master-Informationen abrufen (Version, DB-ID, IPs).</summary>
        public async Task<Dictionary<string, object>> GetDiscoveryInfoAsync()
        {
            try
            {
                var json = await _http.GetStringAsync(Url("/api/discovery"));
                var info = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                if (info.ContainsKey("databaseId"))
                    DatabaseId = info["databaseId"]?.ToString();
                return info;
            }
            catch { return null; }
        }

        /// <summary>Datenbank-Statistiken abrufen.</summary>
        public async Task<Dictionary<string, object>> GetDbInfoAsync()
        {
            try
            {
                var json = await _http.GetStringAsync(Url("/api/db/info"));
                return JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
            }
            catch { return null; }
        }

        /// <summary>Alle Artikel vom Master abrufen.</summary>
        public async Task<List<Dictionary<string, object>>> GetArticlesAsync()
        {
            try
            {
                var json = await _http.GetStringAsync(Url("/api/articles"));
                return JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json);
            }
            catch { return new List<Dictionary<string, object>>(); }
        }

        /// <summary>Lagerbestand vom Master abrufen.</summary>
        public async Task<List<Dictionary<string, object>>> GetStorageItemsAsync()
        {
            try
            {
                var json = await _http.GetStringAsync(Url("/api/storage-items"));
                return JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json);
            }
            catch { return new List<Dictionary<string, object>>(); }
        }

        /// <summary>Einkaufsliste vom Master abrufen.</summary>
        public async Task<List<Dictionary<string, object>>> GetShoppingItemsAsync()
        {
            try
            {
                var json = await _http.GetStringAsync(Url("/api/shopping-items"));
                return JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json);
            }
            catch { return new List<Dictionary<string, object>>(); }
        }

        /// <summary>�nderungen seit einem Zeitpunkt vom Master abrufen.</summary>
        public async Task<List<Dictionary<string, object>>> PullChangesAsync(DateTime since)
        {
            try
            {
                var url = Url($"/api/sync/changes?since={Uri.EscapeDataString(since.ToString("O"))}");
                var json = await _http.GetStringAsync(url);
                return JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json);
            }
            catch { return new List<Dictionary<string, object>>(); }
        }

        /// <summary>Eigene �nderungen an den Master senden.</summary>
        public async Task<List<Dictionary<string, object>>> PushChangesAsync(List<Dictionary<string, object>> changes)
        {
            try
            {
                var json = JsonConvert.SerializeObject(changes);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var res = await _http.PostAsync(Url("/api/sync/push"), content);
                var resJson = await res.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(resJson);
            }
            catch { return new List<Dictionary<string, object>>(); }
        }

        /// <summary>
        /// Sucht automatisch nach einem Master im lokalen Netzwerk.
        /// Nutzt UDP-Broadcast (Port 5190) und pr�ft dann die HTTP-API.
        /// </summary>
        public static async Task<string[]> AutoDiscoverMastersAsync(int timeoutMs = 3000)
        {
            try
            {
                var results = await DiscoveryService.DiscoverMasterAsync(timeoutMs);
                return results
                    .Select(r => r.Split('|'))
                    .Where(parts => parts.Length >= 3)
                    .Select(parts => $"http://{parts[1]}:{parts[2]}/")
                    .ToArray();
            }
            catch
            {
                return new string[0];
            }
        }
    }
}
