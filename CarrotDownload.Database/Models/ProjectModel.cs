using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CarrotDownload.Database.Models
{
    public class ProjectModel
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } // MongoDB ObjectId

        public string ProjectId { get; set; } // Our custom 8-character ID
        public string Title { get; set; }
        public bool IsPrivate { get; set; } // true = Private, false = Public
        public string StoragePath { get; set; } // Path to project folder
        public List<string> Files { get; set; } = new List<string>(); // List of file paths
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string UserId { get; set; } // User who created the project
    }
}
