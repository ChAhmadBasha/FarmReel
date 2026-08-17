using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FarmReel.Core.Utils;

namespace FarmReel.Core.Services
{
    /// <summary>Captcha solving via 2Captcha (image base64 + FunCaptcha token).</summary>
    public class CaptchaService
    {
        private readonly SettingsService _settings;
        private readonly HttpClient _http;

        public CaptchaService(SettingsService settings)
        {
            _settings = settings;
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }

        public bool IsConfigured => !string.IsNullOrEmpty(_settings.CaptchaApiKey);

        /// <summary>Solve an image captcha (PNG bytes).</summary>
        public async Task<string> SolveImageAsync(byte[] imageBytes)
        {
            var key = RequireKey();
            var form = new MultipartFormDataContent
            {
                { new StringContent(key), "key" },
                { new StringContent("base64"), "method" },
                { new StringContent(Convert.ToBase64String(imageBytes)), "body" },
                { new StringContent("1"), "json" }
            };
            var resp = await _http.PostAsync("https://2captcha.com/in.php", form).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            var id = ParseId(body);
            if (id == null) throw new InvalidOperationException("2Captcha submit failed: " + body);
            return await PollAsync(key, id).ConfigureAwait(false);
        }

        /// <summary>Solve a FunCaptcha (used by Facebook) given the public key and page URL.</summary>
        public async Task<string> SolveFunCaptchaAsync(string publicKey, string pageUrl)
        {
            var key = RequireKey();
            var form = new MultipartFormDataContent
            {
                { new StringContent(key), "key" },
                { new StringContent("funcaptcha"), "method" },
                { new StringContent(publicKey), "publickey" },
                { new StringContent(pageUrl), "pageurl" },
                { new StringContent("1"), "json" }
            };
            var resp = await _http.PostAsync("https://2captcha.com/in.php", form).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            var id = ParseId(body);
            if (id == null) throw new InvalidOperationException("2Captcha funcaptcha submit failed: " + body);
            return await PollAsync(key, id).ConfigureAwait(false);
        }

        private async Task<string> PollAsync(string key, string id)
        {
            var deadline = DateTime.UtcNow.AddSeconds(120);
            while (DateTime.UtcNow < deadline)
            {
                await Task.Delay(5000).ConfigureAwait(false);
                var url = $"https://2captcha.com/res.php?key={Uri.EscapeDataString(key)}&action=get&id={id}&json=1";
                var resp = await _http.GetStringAsync(url).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(resp);
                var root = doc.RootElement;
                if (root.TryGetProperty("status", out var status) && status.GetInt32() == 1)
                    return root.GetProperty("request").GetString();
                if (root.TryGetProperty("request", out var req) &&
                    req.GetString()?.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase) == true)
                    throw new InvalidOperationException("2Captcha error: " + req.GetString());
            }
            throw new TimeoutException("Captcha solving timed out");
        }

        private string RequireKey()
        {
            var key = _settings.CaptchaApiKey;
            if (string.IsNullOrEmpty(key)) throw new InvalidOperationException("Captcha API key is not configured");
            return key;
        }

        private static string ParseId(string body)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.TryGetProperty("status", out var st) && st.GetInt32() == 1)
                    return root.GetProperty("request").GetString();
                return null;
            }
            catch { return null; }
        }
    }
}
