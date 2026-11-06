
using System.Net;
using System.Net.Mail;

namespace GeopoliticsGameAPI.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private string GetGmailEmail()
        {
            return _configuration["Gmail:Email"]
                ?? throw new InvalidOperationException(
                    "Gmail email address is missing.");
        }

        private string GetGmailAppPassword()
        {
            return _configuration["Gmail:AppPassword"]
                ?? throw new InvalidOperationException(
                    "Gmail app password is missing.");
        }

        private string GetFrontendUrl()
        {
            return (_configuration["Frontend:BaseUrl"]
                ?? throw new InvalidOperationException(
                    "Frontend URL is missing."))
                .TrimEnd('/');
        }

        private async Task SendEmailAsync(
            string recipient,
            string subject,
            string htmlBody)
        {
            using var message = new MailMessage();

            message.From = new MailAddress(
                GetGmailEmail(),
                "Geopolitics Game"
            );

            message.To.Add(recipient);
            message.Subject = subject;
            message.Body = htmlBody;
            message.IsBodyHtml = true;

            using var smtpClient = new SmtpClient(
                "smtp.gmail.com",
                587
            );

            smtpClient.EnableSsl = true;
            smtpClient.UseDefaultCredentials = false;

            smtpClient.Credentials = new NetworkCredential(
                GetGmailEmail(),
                GetGmailAppPassword()
            );

            await smtpClient.SendMailAsync(message);
        }

        public async Task SendVerificationEmailAsync(
            string email,
            string username,
            string token)
        {
            string verificationLink =
                $"{GetFrontendUrl()}/verify-email?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";

            string safeUsername = WebUtility.HtmlEncode(username);

            string htmlBody = $@"
                <div style='font-family: Arial, sans-serif; padding: 25px;'>
                    <h2>Welcome to Geopolitics Game, {safeUsername}!</h2>

                    <p>Thank you for creating an account.</p>

                    <p>Please verify your email address by clicking below:</p>

                    <a href='{verificationLink}'
                       style='background-color: #2563eb;
                              color: white;
                              padding: 12px 24px;
                              text-decoration: none;
                              border-radius: 6px;
                              display: inline-block;'>
                        Verify Email Address
                    </a>

                    <p>This link expires in 24 hours.</p>

                    <p>If you did not create this account, you can ignore this email.</p>

                    <p>Geopolitics Game Team</p>
                </div>";

            await SendEmailAsync(
                email,
                "Verify Your Geopolitics Game Account",
                htmlBody
            );
        }

        public async Task SendPasswordResetEmailAsync(
            string email,
            string username,
            string token)
        {
            string resetLink =
                $"{GetFrontendUrl()}/reset-password?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";

            string safeUsername = WebUtility.HtmlEncode(username);

            string htmlBody = $@"
                <div style='font-family: Arial, sans-serif; padding: 25px;'>
                    <h2>Password Reset Request</h2>

                    <p>Hello {safeUsername},</p>

                    <p>We received a request to reset your password.</p>

                    <a href='{resetLink}'
                       style='background-color: #2563eb;
                              color: white;
                              padding: 12px 24px;
                              text-decoration: none;
                              border-radius: 6px;
                              display: inline-block;'>
                        Reset Password
                    </a>

                    <p>This link expires in 30 minutes.</p>

                    <p>If you did not request a password reset, ignore this email.</p>

                    <p>Geopolitics Game Team</p>
                </div>";

            await SendEmailAsync(
                email,
                "Reset Your Geopolitics Game Password",
                htmlBody
            );
        }
    }
}
