using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CarrotDownload.Database.Models;

public class ProgrammingFileModel
{
	[BsonId]
	[BsonRepresentation(BsonType.ObjectId)]
	public string Id { get; set; }

	public string UserId { get; set; } // Links to the user
	public string ProgramTitle { get; set; }
	public string FileName { get; set; }
	public string FilePath { get; set; }
	public string SlotPosition { get; set; } // a-z
	public bool IsPrivate { get; set; }
	public DateTime CreatedAt { get; set; }
}
