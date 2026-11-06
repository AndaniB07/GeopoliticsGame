using Microsoft.AspNetCore.Mvc;
using GeopoliticsGameAPI.Services;
using GeopoliticsGameAPI.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace GeopoliticsGameAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GamesController : ControllerBase
    {
        private readonly GameService _gameService;

        public GamesController(GameService gameService)
        {
            _gameService = gameService;
        }

        private async Task<bool> OwnsGameAsync(string id)
        {
            string? ownerPlayerId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(ownerPlayerId))
            {
                return false;
            }

            var game = await _gameService.GetGameAsync(id);

            if (game == null)
            {
                return false;
            }

            return game.OwnerPlayerId == ownerPlayerId;
        }

        public class CreateGameRequest
        {
            public string PlayerCountryId { get; set; } = string.Empty;
        }

        public class AllocateReinforcementsRequest
        {
            public List<ReinforcementAllocation> Allocations { get; set; } = new();
        }

        public class TransferConquestRequest
        {
            public int SoldiersToTransfer { get; set; }
        }


        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateGame(
            [FromBody] CreateGameRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PlayerCountryId))
            {
                return BadRequest(new
                {
                    message = "A player country ID is required."
                });
            }

            string? ownerPlayerId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(ownerPlayerId))
            {
                return Unauthorized(new
                {
                    message = "The authenticated player's ID could not be found."
                });
            }

            try
            {
                var game = await _gameService.CreateGameAsync(
                    request.PlayerCountryId,
                    ownerPlayerId);

                return CreatedAtAction(
                    nameof(GetGame),
                    new { id = game.Id },
                    game);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }


        [HttpGet("{id}")]
        public async Task<IActionResult> GetGame(string id)
        {
            if (!MongoDB.Bson.ObjectId.TryParse(id, out _))
            {
                return BadRequest(new { message = "Invalid game ID" });
            }

            var game = await _gameService.GetGameAsync(id);

            if (game == null)
            {
                return NotFound(new { message = "Game not found." });
            }

            return Ok(game);
        }


        [Authorize]
        [HttpGet("my-games")]
        public async Task<IActionResult> GetMyGame()
        {
            string? ownerPlayerId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(ownerPlayerId))
            {
                return Unauthorized(new
                {
                    message = "The aunthenticated player's ID could not be found."
                });
            }

            var games = await _gameService.GetPlayerGamesAsync(ownerPlayerId);

            return Ok(games);
        }


        [HttpGet("{id}/events")]
        public async Task<IActionResult> GetGameEvents(string id)
        {
            if (!MongoDB.Bson.ObjectId.TryParse(id, out _))
            {
                return BadRequest(new { message = "Invalid game ID." });
            }

            var game = await _gameService.GetGameAsync(id);

            if (game == null)
            {
                return NotFound(new { message = "Game not found." });
            }

            var events = game.Events
                .OrderBy(gameEvent => gameEvent.CreatedAt)
                .ToList();

            return Ok(events);
        }


        [HttpGet("{id}/statistics")]
        public async Task<IActionResult> GetGameStatistics(string id)
        {
            if (!MongoDB.Bson.ObjectId.TryParse(id, out _))
            {
                return BadRequest(new { message = "Invalid game ID." });
            }

            var statistics = await _gameService.GetGameStatisticsAsync(id);

            if (statistics == null)
            {
                return NotFound(new { message = "Game not found." });
            }

            return Ok(statistics);
        }


        [Authorize]
        [HttpPost("{id}/advanced-turn")]
        public async Task<IActionResult> AdvanceTurn(string id)
        {
            if (!MongoDB.Bson.ObjectId.TryParse(id, out _))
            {
                return BadRequest(new
                {
                    message = "Invalid game ID."
                });
            }

            if (!await OwnsGameAsync(id))
            {
                return NotFound(new
                {
                    message = "Game not found or you do not own this game"
                });
            }

            try
            {
                var game = await _gameService.AdvanceTurnAsync(id);

                if (game == null)
                {
                    return NotFound(new
                    {
                        message = "Game not found."
                    });
                }

                return Ok(game);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
        }

        [Authorize]
        [HttpPost("{id}/allocate-reinforcements")]
        public async Task<IActionResult> AllocateReinforcemnts(string id,
            [FromBody] AllocateReinforcementsRequest request)
        {
            if (!MongoDB.Bson.ObjectId.TryParse(id, out _))
            {
                return BadRequest(new { message = "Invalid game ID." });
            }

            if (!await OwnsGameAsync(id))
            {
                return NotFound(new
                {
                    message = "Game not found or you do not own this game."
                });
            }

            try
            {
                var game = await _gameService.AllocateReinforcementsAsync(id, request.Allocations);

                if (game == null)
                {
                    return NotFound(new { message = "Game not found." });
                }

                return Ok(game);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new {message = ex.Message});
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new {message = ex.Message});
            }
        }

        [Authorize]
        [HttpPost("{id}/transfer-conquest-soldiers")]
        public async Task<IActionResult> TransferConquestSoldiers(string id, [FromBody] TransferConquestRequest request)
        {

            if (!MongoDB.Bson.ObjectId.TryParse(id, out _))
            {
                return BadRequest(new { message = "Invalid game ID" });
            }

            if(!await OwnsGameAsync(id))
            {
                return NotFound(new
                {
                    message = "Game not found or you do not own this game"
                });
            }
            try
            {
                var game = await _gameService.TransferConquestSoldiersAsync(id, request.SoldiersToTransfer);

                if (game == null)
                {
                    return NotFound("Game not found");
                }

                return Ok(game);
            }
            catch(ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }



        [Authorize]
        [HttpPost("{id}/transfer-soldiers")]
        public async Task<IActionResult> TransferSoldiers(
            string id,
            [FromBody] TransferRequest request)
        {
            if (!MongoDB.Bson.ObjectId.TryParse(id, out _))
            {
                return BadRequest(new { message = "Invalid game ID." });
            }

            if (!await OwnsGameAsync(id))
            {
                return NotFound(new
                {
                    message = "Game not found or you do not own this game."
                });
            }

            try
            {
                var game = await _gameService.TransferSoldiersAsync(
                    id,
                    request);

                if (game == null)
                {
                    return NotFound(new { message = "Game not found." });
                }

                return Ok(game);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }


    }
}
