
using GeopoliticsGameAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace GeopoliticsGameAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AITestController : ControllerBase
{
    private readonly OpenAIService _openAIService;

    public AITestController(OpenAIService openAIService)
    {
        _openAIService = openAIService;
    }

    [HttpGet("test")]
    public async Task<IActionResult> TestAI()
    {
        try
        {
            string response = await _openAIService.GenerateTextAsync(
                "You are a strategic advisor in a geopolitical strategy game. " +
                "Reply with one short sentence explaining why a country " +
                "should reinforce its weakest territory.");

            return Ok(new { message = response });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message = "AI request failed.",
                error = ex.Message
            });
        }
    }
}
