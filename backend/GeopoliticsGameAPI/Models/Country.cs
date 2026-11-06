using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;


namespace GeopoliticsGameAPI.Models
{
    public class Country
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]

        public string? Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Continent { get; set; } = string.Empty;

        public double PopulationMillions { get; set; }

        public List<string> NeighbourCountryIds { get; set; } = new();
    }
}
