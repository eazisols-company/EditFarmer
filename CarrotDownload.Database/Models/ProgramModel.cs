using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CarrotDownload.Database.Models
{
    public class ProgramModel
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        public string Title { get; set; }
        public string Type { get; set; } // "Files" or "Playlist"
        public string Visibility { get; set; } // "Private" or "Public"
        public List<ProgramSlot> Slots { get; set; } = new List<ProgramSlot>();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string UserId { get; set; }
    }

    public class ProgramSlot
    {
        public string SlotPosition { get; set; } // "A", "B", etc.
        public string FileName { get; set; }
        public string FilePath { get; set; } // Local path or cloud URL
    }
}
