using SQLite;
using Vorratsuebersicht.Client.Models;

namespace Vorratsuebersicht.Client.Services
{
    public class LocalDatabase
    {
        private SQLiteAsyncConnection _db;

        private string DbPath => Path.Combine(
            FileSystem.AppDataDirectory,
            "vorratsync_client.db3");

        public async Task InitAsync()
        {
            if (_db != null) return;

            _db = new SQLiteAsyncConnection(DbPath);
            await _db.CreateTableAsync<Article>();
            await _db.CreateTableAsync<StorageItem>();
            await _db.CreateTableAsync<ShoppingItem>();
            await _db.CreateTableAsync<SyncChangeLog>();
        }

        private async Task<SQLiteAsyncConnection> GetDbAsync()
        {
            await InitAsync();
            return _db;
        }

        public async Task<List<Article>> GetArticlesAsync()
        {
            var db = await GetDbAsync();
            return await db.Table<Article>().OrderBy(a => a.Name).ToListAsync();
        }

        public async Task<Article> GetArticleAsync(int id)
        {
            var db = await GetDbAsync();
            return await db.Table<Article>().Where(a => a.ArticleId == id).FirstOrDefaultAsync();
        }

        public async Task<int> SaveArticleAsync(Article article)
        {
            var db = await GetDbAsync();
            var existing = await db.Table<Article>().Where(a => a.ArticleId == article.ArticleId).FirstOrDefaultAsync();
            if (existing != null)
                return await db.UpdateAsync(article);
            else
                return await db.InsertAsync(article);
        }

        public async Task DeleteArticleAsync(int id)
        {
            var db = await GetDbAsync();
            await db.ExecuteAsync("DELETE FROM Article WHERE ArticleId = ?", id);
            await db.ExecuteAsync("DELETE FROM StorageItem WHERE ArticleId = ?", id);
            await db.ExecuteAsync("DELETE FROM ShoppingItem WHERE ArticleId = ?", id);
        }

        public async Task<List<StorageItem>> GetStorageItemsAsync()
        {
            var db = await GetDbAsync();
            return await db.QueryAsync<StorageItem>(
                "SELECT s.*, a.Name AS ArticleName FROM StorageItem s " +
                "LEFT JOIN Article a ON s.ArticleId = a.ArticleId " +
                "ORDER BY a.Name COLLATE NOCASE");
        }

        public async Task<int> SaveStorageItemAsync(StorageItem item)
        {
            var db = await GetDbAsync();
            var existing = await db.Table<StorageItem>().Where(s => s.StorageItemId == item.StorageItemId).FirstOrDefaultAsync();
            if (existing != null)
                return await db.UpdateAsync(item);
            else
                return await db.InsertAsync(item);
        }

        public async Task DeleteStorageItemAsync(int id)
        {
            var db = await GetDbAsync();
            await db.ExecuteAsync("DELETE FROM StorageItem WHERE StorageItemId = ?", id);
        }

        public async Task<List<ShoppingItem>> GetShoppingItemsAsync()
        {
            var db = await GetDbAsync();
            return await db.QueryAsync<ShoppingItem>(
                "SELECT s.*, a.Name AS ArticleName FROM ShoppingItem s " +
                "LEFT JOIN Article a ON s.ArticleId = a.ArticleId " +
                "ORDER BY s.Bought, a.Name COLLATE NOCASE");
        }

        public async Task<int> SaveShoppingItemAsync(ShoppingItem item)
        {
            var db = await GetDbAsync();
            var existing = await db.Table<ShoppingItem>().Where(s => s.ShoppingListId == item.ShoppingListId).FirstOrDefaultAsync();
            if (existing != null)
                return await db.UpdateAsync(item);
            else
                return await db.InsertAsync(item);
        }

        public async Task DeleteShoppingItemAsync(int id)
        {
            var db = await GetDbAsync();
            await db.ExecuteAsync("DELETE FROM ShoppingItem WHERE ShoppingListId = ?", id);
        }

        public async Task ClearAllAsync()
        {
            var db = await GetDbAsync();
            await db.DeleteAllAsync<Article>();
            await db.DeleteAllAsync<StorageItem>();
            await db.DeleteAllAsync<ShoppingItem>();
            await db.DeleteAllAsync<SyncChangeLog>();
        }

        // Sync tracking
        public async Task<DateTime> GetLastSyncAsync()
        {
            var db = await GetDbAsync();
            var last = await db.Table<SyncChangeLog>()
                .OrderByDescending(c => c.Timestamp)
                .FirstOrDefaultAsync();
            if (last != null && DateTime.TryParse(last.Timestamp, out var dt))
                return dt;
            return DateTime.MinValue;
        }

        public async Task<int> GetChangeCountAsync()
        {
            var db = await GetDbAsync();
            return await db.Table<SyncChangeLog>().CountAsync();
        }
    }
}
