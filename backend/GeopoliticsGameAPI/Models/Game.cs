using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;


namespace GeopoliticsGameAPI.Models
{
    public class Game
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }
        public string PlayerCountryId { get; set; } = string.Empty;
        public string OwnerPlayerId { get; set; } = string.Empty;
        public List<GameCountry> Countries { get; set; } = new();
        public string Status { get; set; } = "InProgress";
        public string Outcome { get; set; } = "None";
        public int Round { get; set; } = 1;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public List<string> TurnOrder { get; set; } = new();
        public int CurrentTurnIndex { get; set; } = 0;
        public string CurrentTurnControllerId { get; set; } = string.Empty;
        public bool HasAttackedThisTurn { get; set; } = false;
        public int ReinforcementsAvailable { get; set; } = 0;

        //Tracks an attack across multiple dice rolls
        public bool BattleInProgress { get; set; } = false;
        public string ActiveAttackerCountryId { get; set; } = string.Empty;
        public string ActiveDefenderCountryId { get; set; } = string.Empty;

        //Tracks a conquered country awaiting troop transfer
        public bool ConquestTransferPending { get; set; } = false;
        public string PendingConquestCountryId { get; set; } = string.Empty;
        public string PendingConquestAttackerCountryId { get; set; } = string.Empty;
        public int ConquestBonusAvailable { get; set; } = 0;

        public List<GameEvent> Events { get; set; } = new();
    }

    public class GameCountry
    {
        public string CountryId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Continent { get; set; } = string.Empty;
        public int StartingSoldiers { get; set; }
        public int Soldiers { get; set; }
        public double PopulationMillions { get; set; }
        public bool IsHumanPlayer { get; set; }
        public bool IsEliminated { get; set; }
        public string ControllerCountryId { get; set; } = string.Empty;
    }


 
}
