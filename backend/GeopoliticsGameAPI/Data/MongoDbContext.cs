using MongoDB.Driver;
using GeopoliticsGameAPI.Models;

namespace GeopoliticsGameAPI.Data
{
    public class MongoDbContext
    {

        private readonly IMongoDatabase _database;

        public MongoDbContext(IConfiguration configuration)
        {
            var connectionString = configuration["MongoDB:ConnectionString"];
            var databaseName = configuration["MongoDB:DatabaseName"];

            if ( string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("MongoDB connection string is missing.");
            }

            if (string.IsNullOrWhiteSpace(databaseName))
            {
                throw new InvalidOperationException(
                    "MongoDB database name is missing.");
            }

            var client = new MongoClient(connectionString);
            _database = client.GetDatabase(databaseName);

        }

        public IMongoDatabase Database => _database;

        public IMongoCollection<Country> Countries => _database.GetCollection<Country>("Countries");

        public IMongoCollection<Game> Games => _database.GetCollection<Game>("Games");

        public IMongoCollection<GameEvent> GameEvents => _database.GetCollection<GameEvent>("GameEvents");

        public IMongoCollection<Player> Players => _database.GetCollection<Player>("Players");
    }
}
