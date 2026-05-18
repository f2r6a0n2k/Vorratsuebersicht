using Newtonsoft.Json;
using SQLite;

namespace Vorratsuebersicht.Client.Models
{
    public class StorageItem
    {
        [PrimaryKey]
        public int StorageItemId { get; set; }
        public int ArticleId { get; set; }
        public int Quantity { get; set; }

        [JsonProperty("bestBeforeDate")]
        public string BestBefore { get; set; }

        public string StorageName { get; set; }
        public string ArticleName { get; set; }

        [Ignore]
        public string BestBeforeFormatted
        {
            get
            {
                if (string.IsNullOrEmpty(BestBefore)) return "";
                if (DateTime.TryParse(BestBefore, out var dt))
                    return dt.ToString("dd.MM.yyyy");
                return BestBefore;
            }
        }
    }
}
