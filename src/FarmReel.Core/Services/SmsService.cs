using System;
using System.Net.Http;
using System.Threading.Tasks;
using FarmReel.Core.Utils;

namespace FarmReel.Core.Services
{
    /// <summary>Virtual phone number / OTP via SMS-Activate style gateways.</summary>
    public class SmsService
    {
        private readonly SettingsService _settings;
        private readonly HttpClient _http;

        public SmsService(SettingsService settings)
        {
            _settings = settings;
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }

        private string ApiUrl(string action, params (string k, string v)[] extra)
        {
            var sb = new System.Text.StringBuilder(
                "https://sms-activate.guru/stubs/handler_api.php?api_key=" +
                Uri.EscapeDataString(_settings.SmsApiKey) + "&action=" + action);
            foreach (var (k, v) in extra)
                sb.Append("&").Append(k).Append("=").Append(Uri.EscapeDataString(v));
            return sb.ToString();
        }

        /// <summary>Rent a number for service 'fb' (or 'ig'). Returns (activationId, number).</summary>
        public async Task<(string id, string number)> GetNumberAsync(string service = "fb", string country = "")
        {
            if (string.IsNullOrEmpty(_settings.SmsApiKey))
                throw new InvalidOperationException("SMS API key is not configured");
            var url = ApiUrl("getNumber",
                ("service", service),
                ("country", country),
                ("forward", "0"));
            var body = await _http.GetStringAsync(url).ConfigureAwait(false);
            if (body.StartsWith("ACCESS_NUMBER", StringComparison.Ordinal))
            {
                var parts = body.Split(':');
                if (parts.Length >= 3) return (parts[1], parts[2]);
            }
            throw new InvalidOperationException("SMS getNumber failed: " + body);
        }

        /// <summary>Poll for the SMS code on an activation. Returns code or empty.</summary>
        public async Task<string> GetCodeAsync(string activationId, int waitSeconds = 120)
        {
            var deadline = DateTime.UtcNow.AddSeconds(waitSeconds);
            while (DateTime.UtcNow < deadline)
            {
                var url = ApiUrl("getStatus", ("id", activationId));
                var body = await _http.GetStringAsync(url).ConfigureAwait(false);
                if (body.StartsWith("STATUS_OK", StringComparison.Ordinal))
                    return body.Split(':')[1];
                if (body.StartsWith("STATUS_WAIT", StringComparison.Ordinal)) { }
                else if (body.StartsWith("ERROR", StringComparison.Ordinal))
                    throw new InvalidOperationException("SMS getStatus: " + body);
                await Task.Delay(5000).ConfigureAwait(false);
            }
            return "";
        }

        public async Task ReleaseNumberAsync(string activationId)
        {
            try
            {
                var url = ApiUrl("setStatus", ("id", activationId), ("status", "8"));
                await _http.GetStringAsync(url).ConfigureAwait(false);
            }
            catch (Exception ex) { Log.Warn("Sms", "Release failed: " + ex.Message); }
        }
    }
}
