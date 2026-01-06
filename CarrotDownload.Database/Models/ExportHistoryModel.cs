using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CarrotDownload.Database.Models;

public class ExportHistoryModel
{
	[BsonId]
	[BsonRepresentation(BsonType.ObjectId)]
	public string Id { get; set; } = string.Empty;

	public string UserId { get; set; } = string.Empty;
	
	public string ZipFileName { get; set; } = string.Empty;
	
	public string ZipFilePath { get; set; } = string.Empty;
	
	public List<string> ProjectTitles { get; set; } = new();
	
	public int TotalFiles { get; set; }
	
	public DateTime ExportedAt { get; set; }
}
