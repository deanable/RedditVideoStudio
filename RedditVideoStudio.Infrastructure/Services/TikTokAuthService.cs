using Microsoft.Extensions.Logging;
using RedditVideoStudio.Core.Exceptions;
using RedditVideoStudio.Core.Interfaces;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Web;

namespace RedditVideoStudio.Infrastructure.Services
{
    public class TikTokAuthService
    {
        private readonly ILogger<TikTokAuthService> _logger;
        private readonly IAppConfiguration _config;
        private string? _codeVerifier;

        public TikTokAuthService(ILogger<TikTokAuthService> logger, IAppConfiguration config)
        {
            _logger = logger;
            _config = config;
        }

        public Task<string> AuthorizeAndGetTokenAsync(CancellationToken cancellationToken)
        {
            var settings = _config.Settings.TikTok;
            string redirectUri = "http://localhost:8912/callback/";

            _logger.LogInformation("Starting TikTok authorization flow (Manual Copy/Paste)...");

            _codeVerifier = GenerateCodeVerifier();
            string codeChallenge = GenerateCodeChallenge(_codeVerifier);
            string state = Guid.NewGuid().ToString();

            var authUrlBuilder = new StringBuilder();
            authUrlBuilder.Append("https://www.tiktok.com/v2/auth/authorize/");
            authUrlBuilder.Append($"?client_key={settings.ClientKey}");
            authUrlBuilder.Append($"&scope={settings.Scopes}");
            authUrlBuilder.Append("&response_type=code");
            authUrlBuilder.Append($"&redirect_uri={WebUtility.UrlEncode(redirectUri)}");
            authUrlBuilder.Append($"&state={state}");
            authUrlBuilder.Append($"&code_challenge={codeChallenge}");
            authUrlBuilder.Append("&code_challenge_method=S256");

            string authUrl = authUrlBuilder.ToString();
            _logger.LogInformation("Launching browser to authorize...");
            Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });

            string? callbackUrl = ShowUrlInputDialog();

            if (string.IsNullOrWhiteSpace(callbackUrl))
            {
                throw new OperationCanceledException("TikTok authentication was canceled.");
            }

            var query = new Uri(callbackUrl).Query;
            var queryParams = HttpUtility.ParseQueryString(query);
            string? authCode = queryParams.Get("code");
            string? incomingState = queryParams.Get("state");

            if (string.IsNullOrEmpty(authCode) || incomingState != state)
            {
                _logger.LogError("TikTok authorization failed. Code or state mismatch.");
                throw new ApiException("TikTok authorization failed. The code or state was invalid.");
            }

            _logger.LogInformation("Received authorization code successfully.");

            string accessToken = "placeholder_access_token_from_tiktok";
            _logger.LogInformation("Successfully obtained TikTok access token (placeholder).");
            return Task.FromResult(accessToken);
        }

        private string? ShowUrlInputDialog()
        {
            using (var form = new Form())
            {
                form.Text = "Paste Callback URL";
                form.Size = new Size(500, 180);
                form.StartPosition = FormStartPosition.CenterScreen;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.TopMost = true;

                var label = new Label() { Left = 20, Top = 20, Width = 440, Text = "After authorizing in your browser, copy the full URL from the address bar (it will show an error page) and paste it below." };
                var textBox = new TextBox() { Left = 20, Top = 60, Width = 440 };
                var buttonOk = new Button() { Text = "OK", Left = 300, Width = 100, Top = 90, DialogResult = DialogResult.OK };
                var buttonCancel = new Button() { Text = "Cancel", Left = 40, Width = 100, Top = 90, DialogResult = DialogResult.Cancel };

                form.Controls.Add(label);
                form.Controls.Add(textBox);
                form.Controls.Add(buttonOk);
                form.Controls.Add(buttonCancel);

                form.AcceptButton = buttonOk;
                form.CancelButton = buttonCancel;

                if (form.ShowDialog() == DialogResult.OK)
                {
                    return textBox.Text;
                }
                return null;
            }
        }

        private string GenerateCodeVerifier()
        {
            var randomBytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }
            return Convert.ToBase64String(randomBytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private string GenerateCodeChallenge(string codeVerifier)
        {
            using (var sha256 = SHA256.Create())
            {
                var challengeBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
                return Convert.ToBase64String(challengeBytes)
                    .TrimEnd('=')
                    .Replace('+', '-')
                    .Replace('/', '_');
            }
        }
    }
}