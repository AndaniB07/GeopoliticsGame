
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace GeopoliticsGameAPI.Models
{
    public class GameEvent
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string GameId { get; set; } = string.Empty;

        public string EventType { get; set; } = string.Empty;

        public int Round { get; set; }

        public string ActorCountryId { get; set; } = string.Empty;

        public string ActorCountryName { get; set; } = string.Empty;

        public string TargetCountryId { get; set; } = string.Empty;

        public string TargetCountryName { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string Commentary { get; set; } = string.Empty;

        public List<int> AttackerDice { get; set; } = new();

        public List<int> DefenderDice { get; set; } = new();

        public int AttackerLosses { get; set; }

        public int DefenderLosses { get; set; }

        public bool WasConquered { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
