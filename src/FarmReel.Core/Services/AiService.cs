using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FarmReel.Core.Utils;

namespace FarmReel.Core.Services
{
    /// <summary>OpenAI-compatible chat API for captions, status posts and chat replies.</summary>
    public class AiService
    {
        private readonly SettingsService _settings;
        private readonly HttpClient _http;

        public AiService(SettingsService settings)
        {
            _settings = settings;
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        }

        public bool IsConfigured => !string.IsNullOrEmpty(_settings.AiApiKey);

        public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, int maxTokens = 200)
        {
            if (!IsConfigured) throw new InvalidOperationException("AI API key is not configured");
            var payload = new
            {
                model = _settings.AiModel,
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                max_tokens = maxTokens,
                temperature = 0.9
            };
            var json = JsonSerializer.Serialize(payload);
            using var req = new HttpRequestMessage(HttpMethod.Post,
                _settings.AiBaseUrl.TrimEnd('/') + "/chat/completions");
            req.Headers.Add("Authorization", "Bearer " + _settings.AiApiKey);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await _http.SendAsync(req).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException("AI request failed: " + resp.StatusCode + " " + body);
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var content = choices[0].GetProperty("message").GetProperty("content").GetString();
                return (content ?? "").Trim();
            }
            return "";
        }

        public async Task<string> GenerateCaptionAsync(string topic, string pageName)
        {
            return await CompleteAsync(
                "You write short engaging Facebook captions with 2-4 relevant hashtags. Plain text only, max 80 words.",
                $"Topic: {topic}. Page: {pageName}. Write one caption.").ConfigureAwait(false);
        }

        public async Task<string> GenerateStatusAsync(string topic)
        {
            return await CompleteAsync(
                "You write short natural Facebook status updates as a real person would. Plain text only, max 60 words.",
                $"Topic: {topic}").ConfigureAwait(false);
        }

        public async Task<string> GenerateCommentAsync(string postContext)
        {
            return await CompleteAsync(
                "Write a short, natural, friendly Facebook comment (5-20 words). No hashtags. Plain text only.",
                $"Post: {postContext}").ConfigureAwait(false);
        }

        public async Task<bool> CheckHealthAsync()
        {
            try
            {
                var result = await CompleteAsync("Reply with OK.", "Health check", maxTokens: 5)
                    .ConfigureAwait(false);
                return !string.IsNullOrWhiteSpace(result);
            }
            catch { return false; }
        }
    }
}
