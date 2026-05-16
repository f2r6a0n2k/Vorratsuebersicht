using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Widget;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace VorratsUebersicht
{
    public class SyncServerListener : IDisposable
    {
        private HttpListener _listener;
        private bool _running;
        private const int MaxRequestBodySize = 5 * 1024 * 1024; // 5 MB max
        private const int MaxPathDepth = 4;
        private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore
        };

        public int Port { get; private set; }
        public bool IsRunning => _running;
        public bool RequireAccessKey { get; set; } = false;

        public event Action<string> OnClientConnected;
        public event Action<string> OnError;

        /// <summary>Access-Key aus der Datenbank abrufen oder erzeugen.</summary>
        public static string GetOrCreateAccessKey()
        {
            try
            {
                var db = Android_Database.Instance.GetConnection();
                var key = db.ExecuteScalar<string>("SELECT Value FROM Settings WHERE Key = 'SYNC_ACCESS_KEY'");
                if (string.IsNullOrEmpty(key))
                {
                    key = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
                    db.Execute("INSERT INTO Settings (Key, Value) VALUES ('SYNC_ACCESS_KEY', ?)", key);
                }
                return key;
            }
            catch { return null; }
        }

        /// <summary>Access-Key validieren.</summary>
        private bool IsAccessKeyValid(string providedKey)
        {
            if (!RequireAccessKey) return true;
            var storedKey = GetOrCreateAccessKey();
            return !string.IsNullOrEmpty(storedKey) && string.Equals(providedKey?.Trim(), storedKey, StringComparison.OrdinalIgnoreCase);
        }


        public void Start(int port)
        {
            if (_running) return;
            Port = port;

            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://+:{port}/");
                _listener.Start();
                _running = true;
                Task.Run(() => ListenLoop());
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Fehler beim Starten: {ex.Message}");
            }
        }

        public void Stop()
        {
            _running = false;
            try { _listener?.Stop(); } catch { }
            try { _listener?.Close(); } catch { }
            _listener = null;
        }

        private async Task ListenLoop()
        {
            while (_running && _listener != null && _listener.IsListening)
            {
                try
                {
                    var ctx = await _listener.GetContextAsync();
                    Task.Run(() => HandleRequest(ctx));
                }
                catch (ObjectDisposedException) { break; }
                catch (HttpListenerException) { continue; }
                catch (Exception ex)
                {
                    OnError?.Invoke($"Listener Fehler: {ex.Message}");
                }
            }
        }

        private void SetSecurityHeaders(HttpListenerContext ctx)
        {
            ctx.Response.AppendHeader("Access-Control-Allow-Origin", "*");
            ctx.Response.AppendHeader("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
            ctx.Response.AppendHeader("Access-Control-Allow-Headers", "Content-Type, X-Access-Key");
            ctx.Response.AppendHeader("Access-Control-Max-Age", "86400");
            ctx.Response.AppendHeader("X-Content-Type-Options", "nosniff");
            ctx.Response.AppendHeader("X-Frame-Options", "DENY");
            ctx.Response.AppendHeader("X-XSS-Protection", "0");
            ctx.Response.AppendHeader("Cache-Control", "no-store");
            ctx.Response.AppendHeader("Content-Security-Policy",
                "default-src 'self'; " +
                "style-src 'self' 'unsafe-inline'; " +
                "script-src 'self'; " +
                "connect-src 'self'; " +
                "img-src 'self' data:; " +
                "font-src 'none'; " +
                "frame-ancestors 'none'; " +
                "base-uri 'self'");
        }

        private string GetDatabaseId()
        {
            var db = Android_Database.Instance.GetConnection();
            var id = db.ExecuteScalar<string>("SELECT Value FROM Settings WHERE Key = 'SYNC_DATABASE_ID'");
            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString("N");
                db.Execute("INSERT INTO Settings (Key, Value) VALUES ('SYNC_DATABASE_ID', ?)", id);
            }
            return id;
        }

        private void HandleRequest(HttpListenerContext ctx)
        {
            try
            {
                SetSecurityHeaders(ctx);

                var method = ctx.Request.HttpMethod;
                var path = ctx.Request.Url.AbsolutePath.TrimEnd('/');
                var query = ctx.Request.QueryString;

                if (method == "OPTIONS")
                {
                    ctx.Response.StatusCode = 204;
                    ctx.Response.OutputStream.Close();
                    return;
                }

                OnClientConnected?.Invoke($"{method} {path} von {ctx.Request.RemoteEndPoint}");

                // Pfad-Tiefe begrenzen (Schutz vor rekursiven Pfaden)
                var pathParts = path.Split('/');
                if (pathParts.Length > MaxPathDepth)
                {
                    RespondJson(ctx, 400, new { error = "Invalid path" });
                    return;
                }

                // Statische Dateien ohne Zugangsschlüssel ausliefern
                if (path == "/" || path == "" || path == "/index.html"
                    || path == "/css/app.css" || path == "/js/app.js"
                    || path == "/manifest.json" || path == "/sw.js")
                {
                    var contentType = path.EndsWith(".css") ? "text/css; charset=utf-8"
                        : path.EndsWith(".js") ? "application/javascript; charset=utf-8"
                        : path.EndsWith(".json") ? "application/manifest+json; charset=utf-8"
                        : "text/html; charset=utf-8";
                    var filePath = (path == "/" || path == "") ? "/index.html" : path;
                    ServeFile(ctx, "wwwroot" + filePath, contentType);
                    return;
                }

                // API-Routen benötigen Zugangsschlüssel (wenn aktiviert)
                if (path.StartsWith("/api/"))
                {
                    var accessKey = ctx.Request.Headers["X-Access-Key"];
                    if (!IsAccessKeyValid(accessKey))
                    {
                        RespondJson(ctx, 401, new { error = "Access key required. Set X-Access-Key header." });
                        return;
                    }
                }

                // API-Routing
                if (path == "/api/discovery" && method == "GET")
                    HandleDiscovery(ctx);
                else if (path == "/api/discovery/ping" && method == "GET")
                    RespondJson(ctx, 200, new { status = "ok", timestamp = DateTime.UtcNow.ToString("O") });
                else if (path == "/api/articles" && method == "GET")
                    HandleGetArticles(ctx);
                else if (path == "/api/articles" && method == "POST")
                    HandleCreateArticle(ctx);
                else if (path.StartsWith("/api/articles/") && method == "GET")
                    TryParseId(ctx, path, 3, id => HandleGetArticle(ctx, id));
                else if (path.StartsWith("/api/articles/") && method == "PUT")
                    TryParseId(ctx, path, 3, id => HandleUpdateArticle(ctx, id));
                else if (path.StartsWith("/api/articles/") && method == "DELETE")
                    TryParseId(ctx, path, 3, id => HandleDeleteArticle(ctx, id));
                else if (path == "/api/storage-items" && method == "GET")
                    HandleGetStorageItems(ctx);
                else if (path == "/api/storage-items" && method == "POST")
                    HandleCreateStorageItem(ctx);
                else if (path.StartsWith("/api/storage-items/") && method == "DELETE")
                    TryParseId(ctx, path, 3, id => HandleDeleteStorageItem(ctx, id));
                else if (path == "/api/shopping-items" && method == "GET")
                    HandleGetShoppingItems(ctx);
                else if (path == "/api/shopping-items" && method == "POST")
                    HandleCreateShoppingItem(ctx);
                else if (path.StartsWith("/api/shopping-items/") && method == "PUT")
                    TryParseId(ctx, path, 3, id => HandleUpdateShoppingItem(ctx, id));
                else if (path.StartsWith("/api/shopping-items/") && method == "DELETE")
                    TryParseId(ctx, path, 3, id => HandleDeleteShoppingItem(ctx, id));
                else if (path == "/api/sync/changes" && method == "GET")
                    HandleSyncPull(ctx, query["since"]);
                else if (path == "/api/sync/push" && method == "POST")
                    HandleSyncPush(ctx);
                else if (path == "/api/db/info" && method == "GET")
                    HandleDbInfo(ctx);
                else
                    RespondJson(ctx, 404, new { error = "Not found" });
            }
            catch (Exception ex)
            {
                // Keine internen Details preisgeben (kein ex.Message in Produktion)
                try { RespondJson(ctx, 500, new { error = "Internal server error" }); } catch { }
            }
        }

        /// <summary>Sicheres Parsen einer ID aus dem Pfad.</summary>
        private void TryParseId(HttpListenerContext ctx, string path, int index, Action<int> handler)
        {
            var parts = path.Split('/');
            if (parts.Length <= index || !int.TryParse(parts[index], out var id) || id <= 0)
            {
                RespondJson(ctx, 400, new { error = "Invalid ID" });
                return;
            }
            handler(id);
        }

        private void ServeFile(HttpListenerContext ctx, string relativePath, string contentType)
        {
            try
            {
                var files = Application.Context.Assets;
                using (var stream = files.Open(relativePath))
                using (var reader = new StreamReader(stream))
                {
                    var content = reader.ReadToEnd();
                    ctx.Response.ContentType = contentType;
                    ctx.Response.StatusCode = 200;
                    var buffer = Encoding.UTF8.GetBytes(content);
                    ctx.Response.ContentLength64 = buffer.Length;
                    ctx.Response.OutputStream.Write(buffer, 0, buffer.Length);
                    ctx.Response.OutputStream.Close();
                }
            }
            catch (Exception)
            {
                RespondJson(ctx, 404, new { error = "File not found" });
            }
        }

        private void HandleDiscovery(HttpListenerContext ctx)
        {
            var hostName = Java.Net.InetAddress.GetByName(null)?.HostName ?? "android";
            var localIps = new List<string>();
            try
            {
                var ifaces = Java.Net.NetworkInterface.NetworkInterfaces;
                while (ifaces.HasMoreElements)
                {
                    var addr = ifaces.NextElement() as Java.Net.NetworkInterface;
                    if (addr == null) continue;
                    var inets = addr.InetAddresses;
                    while (inets.HasMoreElements)
                    {
                        var inet = inets.NextElement() as Java.Net.InetAddress;
                        if (inet == null) continue;
                        var addrStr = inet.HostAddress;
                        if (!inet.IsLoopbackAddress && addrStr.Contains('.'))
                            localIps.Add(addrStr);
                    }
                }
            }
            catch { }

            RespondJson(ctx, 200, new
            {
                name = "Vorratsuebersicht (Android-Master)",
                version = "9.00-sync",
                databaseId = GetDatabaseId(),
                hostName = hostName,
                localIPs = localIps.ToArray(),
                framework = "Xamarin.Android",
                endpoints = new
                {
                    articles = "/api/articles",
                    storageItems = "/api/storage-items",
                    shoppingItems = "/api/shopping-items",
                    syncPull = "/api/sync/changes?since={timestamp}",
                    syncPush = "/api/sync/push",
                    dbInfo = "/api/db/info"
                }
            });
        }

        private void HandleDbInfo(HttpListenerContext ctx)
        {
            var db = Android_Database.Instance.GetConnection();
            var articleCount = db.ExecuteScalar<int>("SELECT COUNT(*) FROM Article");
            var storageCount = db.ExecuteScalar<int>("SELECT COUNT(*) FROM StorageItem");
            var shoppingCount = db.ExecuteScalar<int>("SELECT COUNT(*) FROM ShoppingList");
            var changeCount = db.ExecuteScalar<int>("SELECT MAX(SyncChangeLogId) FROM SyncChangeLog");
            var lastChange = db.ExecuteScalar<string>("SELECT MAX(Timestamp) FROM SyncChangeLog");

            RespondJson(ctx, 200, new
            {
                databaseId = GetDatabaseId(),
                statistics = new
                {
                    articles = articleCount,
                    storageItems = storageCount,
                    shoppingItems = shoppingCount,
                    totalChanges = changeCount
                },
                lastChange = lastChange ?? "2000-01-01T00:00:00Z",
                serverTime = DateTime.UtcNow.ToString("O")
            });
        }

        private void HandleGetArticles(HttpListenerContext ctx)
        {
            var db = Android_Database.Instance.GetConnection();
            var articles = db.Query<Article>("SELECT * FROM Article ORDER BY Name COLLATE NOCASE");
            var result = new List<object>();
            foreach (var a in articles)
            {
                result.Add(new
                {
                    articleId = a.ArticleId,
                    name = a.Name ?? "",
                    manufacturer = a.Manufacturer,
                    category = a.Category,
                    subCategory = a.SubCategory,
                    durableInfinity = a.DurableInfinity,
                    warnInDays = a.WarnInDays,
                    size = a.Size,
                    unit = a.Unit,
                    calorie = a.Calorie,
                    notes = a.Notes,
                    eanCode = a.EANCode,
                    storageName = a.StorageName,
                    minQuantity = a.MinQuantity,
                    prefQuantity = a.PrefQuantity,
                    supermarket = a.Supermarket,
                    price = a.Price,
                    createdAt = a.CreatedAt ?? "2000-01-01T00:00:00Z",
                    updatedAt = a.UpdatedAt ?? "2000-01-01T00:00:00Z"
                });
            }
            RespondJson(ctx, 200, result);
        }

        private void HandleGetArticle(HttpListenerContext ctx, int id)
        {
            var db = Android_Database.Instance.GetConnection();
            var article = db.Find<Article>(id);
            if (article == null) { RespondJson(ctx, 404, new { error = "Not found" }); return; }
            RespondJson(ctx, 200, article);
        }

        private void HandleCreateArticle(HttpListenerContext ctx)
        {
            var body = ReadBody(ctx);
            var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(body);
            var now = DateTime.UtcNow.ToString("O");

            var article = new Article
            {
                Name = GetStr(data, "name") ?? GetStr(data, "Name") ?? "",
                Manufacturer = GetStr(data, "manufacturer") ?? GetStr(data, "Manufacturer"),
                Category = GetStr(data, "category") ?? GetStr(data, "Category"),
                SubCategory = GetStr(data, "subCategory") ?? GetStr(data, "SubCategory"),
                DurableInfinity = GetBool(data, "durableInfinity") || GetBool(data, "DurableInfinity"),
                WarnInDays = GetInt(data, "warnInDays") ?? GetInt(data, "WarnInDays"),
                Size = GetDec(data, "size") ?? GetDec(data, "Size"),
                Unit = GetStr(data, "unit") ?? GetStr(data, "Unit"),
                Calorie = GetInt(data, "calorie") ?? GetInt(data, "Calorie"),
                Notes = GetStr(data, "notes") ?? GetStr(data, "Notes"),
                EANCode = GetStr(data, "eanCode") ?? GetStr(data, "EANCode"),
                StorageName = GetStr(data, "storageName") ?? GetStr(data, "StorageName"),
                MinQuantity = GetInt(data, "minQuantity") ?? GetInt(data, "MinQuantity"),
                PrefQuantity = GetInt(data, "prefQuantity") ?? GetInt(data, "PrefQuantity"),
                Supermarket = GetStr(data, "supermarket") ?? GetStr(data, "Supermarket"),
                Price = GetDec(data, "price") ?? GetDec(data, "Price"),
                CreatedAt = now,
                UpdatedAt = now
            };

            var db = Android_Database.Instance.GetConnection();
            db.Insert(article);
            Database.LogChange(db, "Article", article.ArticleId, "create");

            RespondJson(ctx, 201, article);
        }

        private void HandleUpdateArticle(HttpListenerContext ctx, int id)
        {
            var body = ReadBody(ctx);
            var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(body);
            var now = DateTime.UtcNow.ToString("O");

            var db = Android_Database.Instance.GetConnection();
            var article = db.Find<Article>(id);
            if (article == null) { RespondJson(ctx, 404, new { error = "Not found" }); return; }

            if (HasKey(data, "name")) article.Name = GetStr(data, "name") ?? "";
            if (HasKey(data, "manufacturer")) article.Manufacturer = GetStr(data, "manufacturer");
            if (HasKey(data, "category")) article.Category = GetStr(data, "category");
            if (HasKey(data, "subCategory")) article.SubCategory = GetStr(data, "subCategory");
            if (HasKey(data, "durableInfinity")) article.DurableInfinity = GetBool(data, "durableInfinity");
            if (HasKey(data, "warnInDays")) article.WarnInDays = GetInt(data, "warnInDays");
            if (HasKey(data, "size")) article.Size = GetDec(data, "size");
            if (HasKey(data, "unit")) article.Unit = GetStr(data, "unit");
            if (HasKey(data, "calorie")) article.Calorie = GetInt(data, "calorie");
            if (HasKey(data, "notes")) article.Notes = GetStr(data, "notes");
            if (HasKey(data, "eanCode")) article.EANCode = GetStr(data, "eanCode");
            if (HasKey(data, "storageName")) article.StorageName = GetStr(data, "storageName");
            if (HasKey(data, "minQuantity")) article.MinQuantity = GetInt(data, "minQuantity");
            if (HasKey(data, "prefQuantity")) article.PrefQuantity = GetInt(data, "prefQuantity");
            if (HasKey(data, "supermarket")) article.Supermarket = GetStr(data, "supermarket");
            if (HasKey(data, "price")) article.Price = GetDec(data, "price");
            article.UpdatedAt = now;

            db.Update(article);
            Database.LogChange(db, "Article", id, "update");

            RespondJson(ctx, 200, article);
        }

        private void HandleDeleteArticle(HttpListenerContext ctx, int id)
        {
            var db = Android_Database.Instance.GetConnection();
            db.Execute("DELETE FROM StorageItem WHERE ArticleId = ?", id);
            db.Execute("DELETE FROM ShoppingList WHERE ArticleId = ?", id);
            db.Delete<Article>(id);
            Database.LogChange(db, "Article", id, "delete");
            RespondJson(ctx, 200, new { status = "deleted" });
        }

        private void HandleGetStorageItems(HttpListenerContext ctx)
        {
            var db = Android_Database.Instance.GetConnection();
            var cmd = "SELECT s.StorageItemId, s.ArticleId, s.Quantity, s.BestBefore, s.StorageName, a.Name AS ArticleName FROM StorageItem s LEFT JOIN Article a ON s.ArticleId = a.ArticleId ORDER BY a.Name COLLATE NOCASE";
            var items = db.Query<SyncStorageItemResult>(cmd);
            var result = items.Select(s => new
            {
                storageItemId = s.StorageItemId,
                articleId = s.ArticleId,
                quantity = s.Quantity,
                bestBeforeDate = s.BestBefore,
                storageName = s.StorageName,
                articleName = s.ArticleName
            });
            RespondJson(ctx, 200, result);
        }

        private void HandleCreateStorageItem(HttpListenerContext ctx)
        {
            var body = ReadBody(ctx);
            var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(body);

            var db = Android_Database.Instance.GetConnection();
            db.Execute("INSERT INTO StorageItem (ArticleId, Quantity, BestBefore, StorageName) VALUES (?, ?, ?, ?)",
                GetInt(data, "articleId"), GetInt(data, "quantity", 1), GetStr(data, "bestBeforeDate"), GetStr(data, "storageName"));
            var newId = db.ExecuteScalar<int>("SELECT last_insert_rowid()");
            Database.LogChange(db, "StorageItem", newId, "create");

            RespondJson(ctx, 201, new { storageItemId = newId });
        }

        private void HandleDeleteStorageItem(HttpListenerContext ctx, int id)
        {
            var db = Android_Database.Instance.GetConnection();
            db.Execute("DELETE FROM StorageItem WHERE StorageItemId = ?", id);
            Database.LogChange(db, "StorageItem", id, "delete");
            RespondJson(ctx, 200, new { status = "deleted" });
        }

        private void HandleGetShoppingItems(HttpListenerContext ctx)
        {
            var db = Android_Database.Instance.GetConnection();
            var cmd = "SELECT s.ShoppingListId, s.ArticleId, s.Quantity, s.Bought, a.Name AS ArticleName FROM ShoppingList s LEFT JOIN Article a ON s.ArticleId = a.ArticleId ORDER BY s.Bought, a.Name COLLATE NOCASE";
            var items = db.Query<SyncShoppingItemResult>(cmd);
            var result = items.Select(s => new
            {
                shoppingItemId = s.ShoppingListId,
                articleId = s.ArticleId,
                quantity = s.Quantity,
                isChecked = s.Bought,
                articleName = s.ArticleName
            });
            RespondJson(ctx, 200, result);
        }

        private void HandleCreateShoppingItem(HttpListenerContext ctx)
        {
            var body = ReadBody(ctx);
            var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(body);

            var db = Android_Database.Instance.GetConnection();
            db.Execute("INSERT INTO ShoppingList (ArticleId, Quantity) VALUES (?, ?)",
                GetInt(data, "articleId"), GetInt(data, "quantity", 1));
            var newId = db.ExecuteScalar<int>("SELECT last_insert_rowid()");
            Database.LogChange(db, "ShoppingItem", newId, "create");

            RespondJson(ctx, 201, new { shoppingItemId = newId });
        }

        private void HandleUpdateShoppingItem(HttpListenerContext ctx, int id)
        {
            var body = ReadBody(ctx);
            var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(body);

            var db = Android_Database.Instance.GetConnection();
            if (HasKey(data, "isChecked") || HasKey(data, "bought"))
            {
                var isChecked = GetBool(data, "isChecked") || GetBool(data, "bought");
                db.Execute("UPDATE ShoppingList SET Bought = ? WHERE ShoppingListId = ?",
                    isChecked ? 1 : 0, id);
            }
            if (HasKey(data, "quantity"))
            {
                db.Execute("UPDATE ShoppingList SET Quantity = ? WHERE ShoppingListId = ?",
                    GetInt(data, "quantity"), id);
            }
            Database.LogChange(db, "ShoppingItem", id, "update");

            RespondJson(ctx, 200, new { status = "updated" });
        }

        private void HandleDeleteShoppingItem(HttpListenerContext ctx, int id)
        {
            var db = Android_Database.Instance.GetConnection();
            db.Execute("DELETE FROM ShoppingList WHERE ShoppingListId = ?", id);
            Database.LogChange(db, "ShoppingItem", id, "delete");
            RespondJson(ctx, 200, new { status = "deleted" });
        }

        private void HandleSyncPull(HttpListenerContext ctx, string since)
        {
            var db = Android_Database.Instance.GetConnection();
            DateTime sinceDate;
            if (!DateTime.TryParse(since, out sinceDate))
                sinceDate = DateTime.UtcNow.AddDays(-30);

            var sinceStr = sinceDate.ToString("O");
            var changes = db.Query<SyncChangeLog>(
                "SELECT * FROM SyncChangeLog WHERE Timestamp > ? ORDER BY Timestamp ASC LIMIT 500",
                sinceStr);

            var result = changes.Select(c => new
            {
                syncChangeLogId = c.SyncChangeLogId,
                entityType = c.EntityType,
                entityId = c.EntityId,
                operation = c.Operation,
                timestamp = c.Timestamp,
                data = c.Operation != "delete" ? GetEntityData(db, c.EntityType, c.EntityId) : null
            });

            RespondJson(ctx, 200, result);
        }

        private void HandleSyncPush(HttpListenerContext ctx)
        {
            var body = ReadBody(ctx);
            var changes = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(body);
            var results = new List<object>();
            var db = Android_Database.Instance.GetConnection();

            db.RunInTransaction(() =>
            {
                foreach (var change in changes)
                {
                    var clientId = GetStr(change, "clientChangeId") ?? Guid.NewGuid().ToString();
                    var entityType = GetStr(change, "entityType");
                    var operation = GetStr(change, "operation");
                    var data = change.ContainsKey("data") ? change["data"] as Dictionary<string, object> : null;
                    var entityId = change.ContainsKey("entityId") ? Convert.ToInt32(change["entityId"]) : (int?)null;

                    try
                    {
                        var result = ApplyChange(db, entityType, operation, entityId, data);
                        results.Add(new { clientChangeId = clientId, accepted = true, entityId = result });
                    }
                    catch (Exception ex)
                    {
                        results.Add(new { clientChangeId = clientId, accepted = false, error = ex.Message });
                    }
                }
            });

            RespondJson(ctx, 200, results);
        }

        private object ApplyChange(SQLite.SQLiteConnection db, string entityType, string operation, int? entityId, Dictionary<string, object> data)
        {
            switch (entityType)
            {
                case "Article":
                    if (operation == "create" && data != null)
                    {
                        var a = new Article
                        {
                            Name = GetStr(data, "name") ?? GetStr(data, "Name") ?? "",
                            Manufacturer = GetStr(data, "manufacturer") ?? GetStr(data, "Manufacturer"),
                            Category = GetStr(data, "category") ?? GetStr(data, "Category"),
                            SubCategory = GetStr(data, "subCategory") ?? GetStr(data, "SubCategory"),
                            DurableInfinity = GetBool(data, "durableInfinity") || GetBool(data, "DurableInfinity"),
                            WarnInDays = GetInt(data, "warnInDays") ?? GetInt(data, "WarnInDays"),
                            Size = GetDec(data, "size") ?? GetDec(data, "Size"),
                            Unit = GetStr(data, "unit") ?? GetStr(data, "Unit"),
                            Calorie = GetInt(data, "calorie") ?? GetInt(data, "Calorie"),
                            Notes = GetStr(data, "notes") ?? GetStr(data, "Notes"),
                            EANCode = GetStr(data, "eanCode") ?? GetStr(data, "EANCode"),
                            StorageName = GetStr(data, "storageName") ?? GetStr(data, "StorageName"),
                            MinQuantity = GetInt(data, "minQuantity") ?? GetInt(data, "MinQuantity"),
                            PrefQuantity = GetInt(data, "prefQuantity") ?? GetInt(data, "PrefQuantity"),
                            Supermarket = GetStr(data, "supermarket") ?? GetStr(data, "Supermarket"),
                            Price = GetDec(data, "price") ?? GetDec(data, "Price"),
                            CreatedAt = DateTime.UtcNow.ToString("O"),
                            UpdatedAt = DateTime.UtcNow.ToString("O")
                        };
                        db.Insert(a);
                        Database.LogChange(db, "Article", a.ArticleId, "create");
                        return new { entityId = a.ArticleId };
                    }
                    if (operation == "update" && entityId.HasValue && data != null)
                    {
                        var existing = db.Find<Article>(entityId.Value);
                        if (existing != null)
                        {
                            if (HasKey(data, "name")) existing.Name = GetStr(data, "name") ?? "";
                            if (HasKey(data, "manufacturer")) existing.Manufacturer = GetStr(data, "manufacturer");
                            if (HasKey(data, "category")) existing.Category = GetStr(data, "category");
                            if (HasKey(data, "subCategory")) existing.SubCategory = GetStr(data, "subCategory");
                            if (HasKey(data, "durableInfinity")) existing.DurableInfinity = GetBool(data, "durableInfinity");
                            if (HasKey(data, "warnInDays")) existing.WarnInDays = GetInt(data, "warnInDays");
                            if (HasKey(data, "size")) existing.Size = GetDec(data, "size");
                            if (HasKey(data, "unit")) existing.Unit = GetStr(data, "unit");
                            if (HasKey(data, "calorie")) existing.Calorie = GetInt(data, "calorie");
                            if (HasKey(data, "notes")) existing.Notes = GetStr(data, "notes");
                            if (HasKey(data, "eanCode")) existing.EANCode = GetStr(data, "eanCode");
                            if (HasKey(data, "storageName")) existing.StorageName = GetStr(data, "storageName");
                            if (HasKey(data, "minQuantity")) existing.MinQuantity = GetInt(data, "minQuantity");
                            if (HasKey(data, "prefQuantity")) existing.PrefQuantity = GetInt(data, "prefQuantity");
                            if (HasKey(data, "supermarket")) existing.Supermarket = GetStr(data, "supermarket");
                            if (HasKey(data, "price")) existing.Price = GetDec(data, "price");
                            existing.UpdatedAt = DateTime.UtcNow.ToString("O");
                            db.Update(existing);
                            Database.LogChange(db, "Article", entityId.Value, "update");
                        }
                        return new { entityId = entityId.Value };
                    }
                    if (operation == "delete" && entityId.HasValue)
                    {
                        db.Execute("DELETE FROM StorageItem WHERE ArticleId = ?", entityId.Value);
                        db.Execute("DELETE FROM ShoppingList WHERE ArticleId = ?", entityId.Value);
                        db.Delete<Article>(entityId.Value);
                        Database.LogChange(db, "Article", entityId.Value, "delete");
                        return new { entityId = entityId.Value };
                    }
                    break;

                case "StorageItem":
                    if (operation == "create" && data != null)
                    {
                        db.Execute("INSERT INTO StorageItem (ArticleId, Quantity, BestBefore, StorageName) VALUES (?, ?, ?, ?)",
                            GetInt(data, "articleId") ?? GetInt(data, "ArticleId"),
                            GetInt(data, "quantity") ?? GetInt(data, "Quantity") ?? 1,
                            GetStr(data, "bestBeforeDate") ?? GetStr(data, "BestBefore"),
                            GetStr(data, "storageName") ?? GetStr(data, "StorageName"));
                        var newId = db.ExecuteScalar<int>("SELECT last_insert_rowid()");
                        Database.LogChange(db, "StorageItem", newId, "create");
                        return new { entityId = newId };
                    }
                    if (operation == "update" && entityId.HasValue && data != null)
                    {
                        if (HasKey(data, "quantity"))
                            db.Execute("UPDATE StorageItem SET Quantity = ? WHERE StorageItemId = ?",
                                GetInt(data, "quantity"), entityId.Value);
                        if (HasKey(data, "bestBeforeDate") || HasKey(data, "BestBefore"))
                            db.Execute("UPDATE StorageItem SET BestBefore = ? WHERE StorageItemId = ?",
                                GetStr(data, "bestBeforeDate") ?? GetStr(data, "BestBefore"), entityId.Value);
                        if (HasKey(data, "storageName") || HasKey(data, "StorageName"))
                            db.Execute("UPDATE StorageItem SET StorageName = ? WHERE StorageItemId = ?",
                                GetStr(data, "storageName") ?? GetStr(data, "StorageName"), entityId.Value);
                        Database.LogChange(db, "StorageItem", entityId.Value, "update");
                        return new { entityId = entityId.Value };
                    }
                    if (operation == "delete" && entityId.HasValue)
                    {
                        db.Execute("DELETE FROM StorageItem WHERE StorageItemId = ?", entityId.Value);
                        Database.LogChange(db, "StorageItem", entityId.Value, "delete");
                        return new { entityId = entityId.Value };
                    }
                    break;

                case "ShoppingItem":
                    if (operation == "create" && data != null)
                    {
                        db.Execute("INSERT INTO ShoppingList (ArticleId, Quantity) VALUES (?, ?)",
                            GetInt(data, "articleId") ?? GetInt(data, "ArticleId"),
                            GetInt(data, "quantity") ?? GetInt(data, "Quantity") ?? 1);
                        var newId = db.ExecuteScalar<int>("SELECT last_insert_rowid()");
                        Database.LogChange(db, "ShoppingItem", newId, "create");
                        return new { entityId = newId };
                    }
                    if (operation == "update" && entityId.HasValue && data != null)
                    {
                        if (HasKey(data, "isChecked") || HasKey(data, "bought") || HasKey(data, "Bought"))
                        {
                            var isChecked = GetBool(data, "isChecked") || GetBool(data, "bought") || GetBool(data, "Bought");
                            db.Execute("UPDATE ShoppingList SET Bought = ? WHERE ShoppingListId = ?", isChecked ? 1 : 0, entityId.Value);
                        }
                        if (HasKey(data, "quantity") || HasKey(data, "Quantity"))
                            db.Execute("UPDATE ShoppingList SET Quantity = ? WHERE ShoppingListId = ?",
                                GetInt(data, "quantity") ?? GetInt(data, "Quantity"), entityId.Value);
                        Database.LogChange(db, "ShoppingItem", entityId.Value, "update");
                        return new { entityId = entityId.Value };
                    }
                    if (operation == "delete" && entityId.HasValue)
                    {
                        db.Execute("DELETE FROM ShoppingList WHERE ShoppingListId = ?", entityId.Value);
                        Database.LogChange(db, "ShoppingItem", entityId.Value, "delete");
                        return new { entityId = entityId.Value };
                    }
                    break;
            }
            return null;
        }

        private object GetEntityData(SQLite.SQLiteConnection db, string entityType, int entityId)
        {
            switch (entityType)
            {
                case "Article":
                    return db.Find<Article>(entityId);
                case "StorageItem":
                    return db.Query<SyncStorageItemResult>(
                        "SELECT s.StorageItemId, s.ArticleId, s.Quantity, s.BestBefore, s.StorageName, a.Name AS ArticleName FROM StorageItem s LEFT JOIN Article a ON s.ArticleId = a.ArticleId WHERE s.StorageItemId = ?", entityId)
                        .FirstOrDefault();
                case "ShoppingItem":
                    return db.Query<SyncShoppingItemResult>(
                        "SELECT s.ShoppingListId, s.ArticleId, s.Quantity, s.Bought, a.Name AS ArticleName FROM ShoppingList s LEFT JOIN Article a ON s.ArticleId = a.ArticleId WHERE s.ShoppingListId = ?", entityId)
                        .FirstOrDefault();
            }
            return null;
        }

        private string ReadBody(HttpListenerContext ctx)
        {
            if (ctx.Request.ContentLength64 > MaxRequestBodySize)
                throw new InvalidOperationException("Request body too large");

            using (var reader = new StreamReader(
                new LimitedStream(ctx.Request.InputStream, MaxRequestBodySize),
                ctx.Request.ContentEncoding))
            {
                return reader.ReadToEnd();
            }
        }

        private void RespondJson(HttpListenerContext ctx, int status, object data)
        {
            var json = JsonConvert.SerializeObject(data, _jsonSettings);
            var buffer = Encoding.UTF8.GetBytes(json);
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = "application/json; charset=utf-8";
            ctx.Response.ContentLength64 = buffer.Length;
            ctx.Response.OutputStream.Write(buffer, 0, buffer.Length);
            ctx.Response.OutputStream.Close();
        }

        private static bool HasKey(Dictionary<string, object> d, string k) => d != null && d.ContainsKey(k);
        private static string GetStr(Dictionary<string, object> d, string k) => d != null && d.ContainsKey(k) ? d[k]?.ToString() : null;
        private static bool GetBool(Dictionary<string, object> d, string k) => d != null && d.ContainsKey(k) && d[k] is bool b && b;
        private static int? GetInt(Dictionary<string, object> d, string k) => d != null && d.ContainsKey(k) && d[k] != null ? Convert.ToInt32(d[k]) : (int?)null;
        private static int GetInt(Dictionary<string, object> d, string k, int defaultValue) => d != null && d.ContainsKey(k) && d[k] != null ? Convert.ToInt32(d[k]) : defaultValue;
        private static decimal? GetDec(Dictionary<string, object> d, string k) => d != null && d.ContainsKey(k) && d[k] != null ? Convert.ToDecimal(d[k]) : (decimal?)null;

        public void Dispose()
        {
            Stop();
        }
    }

    public class SyncStorageItemResult
    {
        public int StorageItemId { get; set; }
        public int ArticleId { get; set; }
        public int Quantity { get; set; }
        public string BestBefore { get; set; }
        public string StorageName { get; set; }
        public string ArticleName { get; set; }
    }

    public class SyncShoppingItemResult
    {
        public int ShoppingListId { get; set; }
        public int ArticleId { get; set; }
        public int Quantity { get; set; }
        public bool Bought { get; set; }
        public string ArticleName { get; set; }
    }

    /// <summary>Stream-Begrenzung auf maximale Gr��e (Schutz vor DoS).</summary>
    public class LimitedStream : Stream
    {
        private readonly Stream _inner;
        private readonly long _maxLength;
        private long _totalRead;

        public LimitedStream(Stream inner, long maxLength)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _maxLength = maxLength;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var toRead = (int)Math.Min(count, _maxLength - _totalRead);
            if (toRead <= 0) throw new InvalidOperationException("Request body too large");
            var read = _inner.Read(buffer, offset, toRead);
            _totalRead += read;
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
