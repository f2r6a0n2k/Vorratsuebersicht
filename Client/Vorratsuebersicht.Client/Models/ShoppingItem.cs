using SQLite;

namespace Vorratsuebersicht.Client.Models
{
    public class ShoppingItem
    {
        [PrimaryKey]
        public int ShoppingListId { get; set; }
        public int ArticleId { get; set; }
        public int Quantity { get; set; }
        public bool Bought { get; set; }
        public string ArticleName { get; set; }
    }
}
