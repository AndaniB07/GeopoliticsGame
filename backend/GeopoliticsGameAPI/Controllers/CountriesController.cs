using GeopoliticsGameAPI.Data;
using GeopoliticsGameAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace GeopoliticsGameAPI.Controllers
{       
    [ApiController]
    [Route("api/[controller]")]
    public class CountriesController : Controller
    {

        private readonly MongoDbContext _context;

        public CountriesController(MongoDbContext context)
        {
            _context = context;
        }


        [HttpGet]
        public async Task<IActionResult> GetCountries()
        {
            var countries = await _context.Countries.Find(_ => true).ToListAsync();

            return Ok(countries);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> SeedCountries()
        {
            var existingCount = await _context.Countries.CountDocumentsAsync(FilterDefinition<Country>.Empty);

            if (existingCount > 0)
            {
                return BadRequest(new
                {
                    message = "Countries already exist. Seeding was skipped",
                    count = existingCount
                });
            }

            var countries = new List<Country>
            {
                new() { Name = "South Africa", Continent = "Africa", PopulationMillions = 65.0 },
                new() { Name = "United States", Continent = "North America", PopulationMillions = 349.0 },
                new() { Name = "Nigeria", Continent = "Africa", PopulationMillions = 242.0 },
                new() { Name = "Russia", Continent = "Europe", PopulationMillions = 143.4 },
                new() { Name = "Iran", Continent = "Asia", PopulationMillions = 93.0 },
                new() { Name = "Greece", Continent = "Europe", PopulationMillions = 9.8 },
                new() { Name = "Argentina", Continent = "South America", PopulationMillions = 46.0 },
                new() { Name = "United Kingdom", Continent = "Europe", PopulationMillions = 69.9 },
                new() { Name = "China", Continent = "Asia", PopulationMillions = 1412.0 },
                new() { Name = "Netherlands", Continent = "Europe", PopulationMillions = 18.0 },
                new() { Name = "Canada", Continent = "North America", PopulationMillions = 41.5 },
                new() { Name = "Mexico", Continent = "North America", PopulationMillions = 131.0 },
                new() { Name = "Cuba", Continent = "North America", PopulationMillions = 11.0 },
                new() { Name = "Colombia", Continent = "South America", PopulationMillions = 53.0 },
                new() { Name = "Chile", Continent = "South America", PopulationMillions = 19.8 },
                new() { Name = "Peru", Continent = "South America", PopulationMillions = 34.6 },
                new() { Name = "Egypt", Continent = "Africa", PopulationMillions = 118.4 },
                new() { Name = "Kenya", Continent = "Africa", PopulationMillions = 57.5 },
                new() { Name = "Ethiopia", Continent = "Africa", PopulationMillions = 135.5 },
                new() { Name = "Ghana", Continent = "Africa", PopulationMillions = 35.1 },
                new() { Name = "Japan", Continent = "Asia", PopulationMillions = 123.1 },
                new() { Name = "Indonesia", Continent = "Asia", PopulationMillions = 285.7 },
                new() { Name = "Pakistan", Continent = "Asia", PopulationMillions = 255.2 },
                new() { Name = "South Korea", Continent = "Asia", PopulationMillions = 51.7 },
                new() { Name = "Saudi Arabia", Continent = "Asia", PopulationMillions = 34.6 },
                new() { Name = "Germany", Continent = "Europe", PopulationMillions = 84.1 },
                new() { Name = "France", Continent = "Europe", PopulationMillions = 66.7 },
                new() { Name = "Italy", Continent = "Europe", PopulationMillions = 59.1 },
                new() { Name = "Spain", Continent = "Europe", PopulationMillions = 47.9 },
                new() { Name = "Poland", Continent = "Europe", PopulationMillions = 38.1 },
                new() { Name = "Australia", Continent = "Oceania", PopulationMillions = 27.0 },
                new() { Name = "New Zealand", Continent = "Oceania", PopulationMillions = 5.3 }
            };

            await _context.Countries.InsertManyAsync(countries);

            return Ok(new
            {
                message = "Countries seeded successfully",
                count = countries.Count
            });
        }
    }
}
