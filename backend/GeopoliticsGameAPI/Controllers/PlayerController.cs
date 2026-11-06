
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
        public async Task<IActionResult> Register(
            [FromBody] Register request)
        {
            try
            {
                var player = await _authService.RegisterAsync(request);

                return Ok(new
                {
                    message = "Account created. Please check your email to verify your account.",
                    player.Id,
                    player.Username,
                    player.FullName,
                    player.Email,
                    player.CreatedAt,
                    player.IsEmailVerified
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
        }


        [HttpPost("verify-email")]
        public async Task<IActionResult> VerifyEmail(
            [FromBody] VerifyEmailRequest request)
        {
            bool verified = await _authService.VerifyEmailAsync(
                request.Email,
                request.Token);

            if (!verified)
            {
                return BadRequest(new
                {
                    message = "The verification link is invalid, expired, or has already been used."
                });
            }

            return Ok(new
            {
                message = "Your email address has been successfully verified. You can now log in."
            });
        }


        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(
            [FromBody] ForgotPasswordRequest request)
        {
            await _authService.ForgotPasswordAsync(request.Email);

            return Ok(new
            {
                message = "If an account with that email exists and is verified, a password reset link will be sent."
            });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(
            [FromBody] ResetPasswordRequest request)
        {
            bool reset = await _authService.ResetPasswordAsync(
                request.Email,
                request.Token,
                request.NewPassword);

            if (!reset)
            {
                return BadRequest(new
                {
                    message = "The password reset link is invalid, expired, or the new password does not meet the requirements."
                });
            }

            return Ok(new
            {
                message = "Your password has been successfully reset. You can now log in."
            });
        }



        [HttpPost("resend-verification")]
        public async Task<IActionResult> ResendVerification(
            [FromBody] ResendVerificationRequest request)
        {
            await _authService.ResendVerificationEmailAsync(request.Email);

            return Ok(new
            {
                message = "If an unverified account exists with that email, a new verification link will be sent."
            });
        }




        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] Login request)
        {
            try
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
            catch (InvalidOperationException ex)
            {
                return StatusCode(403, new
                {
                    message = ex.Message
                });
            }
        }

    }
}
