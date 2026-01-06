using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CarrotDownload.Database.Models
{
    public class MacIdLogModel
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        public string MacId { get; set; }
        public string UserId { get; set; }
        public string UserEmail { get; set; }
        public string UserName { get; set; }
        public DateTime FirstUsedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;
        public bool IsBanned { get; set; } = false;
        public DateTime? BannedAt { get; set; }
        public int LoginCount { get; set; } = 1;
    }
}
