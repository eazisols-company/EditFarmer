using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CarrotDownload.Database.Models;

public class PlaylistModel
{
	[BsonId]
	[BsonRepresentation(BsonType.ObjectId)]
	public string Id { get; set; }

	public string ProjectId { get; set; } // Links to the project
	public string FileName { get; set; }
	public string FilePath { get; set; }
	public string SlotPosition { get; set; } // A-Z
	public int OrderIndex { get; set; } // Order of files in playlist (0, 1, 2, ...)
	public string UserId { get; set; } // Owner of the playlist
	public List<string> Notes { get; set; } = new(); // Notes for this file
	public bool IsPrivate { get; set; }
	public DateTime CreatedAt { get; set; }
}
