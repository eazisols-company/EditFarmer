using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CarrotDownload.Database.Models
{
    [BsonIgnoreExtraElements]
    public class UserModel
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("name")]
        public string? FullName { get; set; }

        [BsonElement("email")]
        public string? Email { get; set; }

        [BsonElement("password")]
        public string? PasswordHash { get; set; } // Hashed password, never store plain text!

        [BsonElement("kind")]
        public string? Role { get; set; } = "User";

        [BsonElement("verified")]
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }
        public string? DeviceId { get; set; } // Stores the unique MAC ID associated with this user
        public bool IsBanned { get; set; } = false;
        public bool IsWhitelisted { get; set; } = false;
        public DateTime? BannedAt { get; set; }
        public List<ShippingAddress> ShippingAddresses { get; set; } = new();
    }

    public class ShippingAddress
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FullName { get; set; }
        public string StreetAddress { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Zip { get; set; }
        public string Country { get; set; } = "United States";
        public string Phone { get; set; }
        public string? Neighborhood { get; set; }
        public string? Landmark { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
