using GeopoliticsGameAPI.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace GeopoliticsGameAPI.Controllers
{

    [ApiController ]
    [Route("api/[controller]")]
    public class DatabaseController : ControllerBase
    {

        private readonly MongoDbContext _context;

        public DatabaseController(MongoDbContext context)
        {
            _context = context;
        }


        [Authorize]
        [HttpGet("test")]
        public async Task<IActionResult> TestConnection()
        {
            try
            {
                await _context.Database.RunCommandAsync<MongoDB.Bson.BsonDocument>(
                    new MongoDB.Bson.BsonDocument("ping", 1));

                return Ok(new
                {
                    message = "MongoDB connection successful!"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "MongoDB connection failed.",
                    error = ex.Message
                });
            }
        }

    }
}
