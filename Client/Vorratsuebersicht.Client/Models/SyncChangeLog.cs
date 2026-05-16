using SQLite;

namespace Vorratsuebersicht.Client.Models
{
    public class SyncChangeLog
    {
        [PrimaryKey, AutoIncrement]
        public int SyncChangeLogId { get; set; }
        public string EntityType { get; set; }
        public int EntityId { get; set; }
        public string Operation { get; set; }
        public string Timestamp { get; set; }
        public string Data { get; set; }
    }
}
