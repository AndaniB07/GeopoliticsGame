using GeopoliticsGameAPI.Models;
using GeopoliticsGameAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;


namespace GeopoliticsGameAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BattleController : ControllerBase
    {

        private readonly GameService _gameService;

        public BattleController(GameService gameService)
        {
            _gameService = gameService;
        }



        [Authorize]
        [HttpPost("{id}/attack")]
        public async Task<IActionResult> Attack(
                                string id,
                                [FromBody] AttackRequest request)
        {
            if (!MongoDB.Bson.ObjectId.TryParse(id, out _))
            {
                return BadRequest(new
                {
                    message = "Invalid game ID."
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

            var existingGame = await _gameService.GetGameAsync(id);

            if (existingGame == null ||
                existingGame.OwnerPlayerId != ownerPlayerId)
            {
                return NotFound(new
                {
                    message = "Game not found or you do not own this game."
                });
            }

            try
            {
                var result = await _gameService.AttackAsync(id, request);

                if (result == null)
                {
                    return NotFound(new
                    {
                        message = "Game not found."
                    });
                }

                return Ok(result);
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
