using GeopoliticsGameAPI.Models;
using GeopoliticsGameAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace GeopoliticsGameAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PlayerController : ControllerBase
    {
        private readonly AuthService _authService;

        public PlayerController(AuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] Register request)
        {
            try
            {
                var player = await _authService.RegisterAsync(request);

                return Ok(new
                {
                    player.Id,
                    player.Username,
                    player.FullName,
                    player.Email,
                    player.CreatedAt
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }


        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] Login request)
        {
            var authResponse = await _authService.LoginAsync(request);

            if (authResponse == null)
            {
                return Unauthorized(new
                {
                    message = "Invalid username/email or password."
                });
            }

            return Ok(authResponse);
        }

    }
}
