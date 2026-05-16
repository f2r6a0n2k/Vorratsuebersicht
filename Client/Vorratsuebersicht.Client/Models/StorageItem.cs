using SQLite;

namespace Vorratsuebersicht.Client.Models
{
    public class StorageItem
    {
        [PrimaryKey]
        public int StorageItemId { get; set; }
        public int ArticleId { get; set; }
        public int Quantity { get; set; }
        public string BestBefore { get; set; }
        public string StorageName { get; set; }
        public string ArticleName { get; set; }
    }
}
