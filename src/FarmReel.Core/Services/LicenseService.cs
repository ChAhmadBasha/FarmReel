using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FarmReel.Core.Models;
using FarmReel.Core.Utils;

namespace FarmReel.Core.Services
{
    /// <summary>License + metered quota (Time Change Keys) management.</summary>
    public class LicenseService
    {
        private readonly SettingsService _settings;
        private LicenseInfo _info;

        public LicenseService(SettingsService settings)
        {
            _settings = settings;
            _info = LoadLocal();
        }

        public LicenseInfo Info => _info;

        private LicenseInfo LoadLocal()
        {
            var json = _settings.Get("license_info", "");
            if (!string.IsNullOrEmpty(json))
            {
                try { return JsonSerializer.Deserialize<LicenseInfo>(json) ?? NewInfo(); }
                catch { }
            }
            return NewInfo();
        }

        private LicenseInfo NewInfo() => new LicenseInfo
        {
            Key = _settings.LicenseKey,
            Plan = "trial",
            TimeChangeKeysTotal = 2,
            Note = "Trial: 2 Time Change Keys. Purchase a plan to unlock more."
        };

        private void Save() => _settings.Set("license_info", JsonSerializer.Serialize(_info));

        public async Task<LicenseInfo> ActivateAsync(string key)
        {
            _settings.LicenseKey = key;
            _info.Key = key;
            var server = _settings.LicenseServer;
            if (!string.IsNullOrEmpty(server))
            {
                try
                {
                    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                    var resp = await http.PostAsJsonAsync(server.TrimEnd('/') + "/api/license/activate",
                        new { key }).ConfigureAwait(false);
                    if (resp.IsSuccessStatusCode)
                    {
                        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                        var doc = JsonSerializer.Deserialize<JsonElement>(body);
                        if (doc.TryGetProperty("ok", out var ok) && ok.GetBoolean())
                        {
                            _info.Plan = doc.TryGetProperty("plan", out var p) ? p.GetString() : "1m";
                            _info.ExpiresAt = doc.TryGetProperty("expiresAt", out var e)
                                ? DateTime.TryParse(e.GetString(), out var d) ? d : (DateTime?)null
                                : null;
                            _info.TimeChangeKeysTotal = doc.TryGetProperty("keys", out var k) ? k.GetInt32() : 2;
                            _info.RegFullEnabled = doc.TryGetProperty("regFull", out var rf) && rf.GetBoolean();
                            _info.VerifyNoveryEnabled = doc.TryGetProperty("novery", out var nv) && nv.GetBoolean();
                            _info.Note = doc.TryGetProperty("note", out var n) ? n.GetString() ?? "" : "";
                            Save();
                            return _info;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn("License", "Server unreachable, using offline mode: " + ex.Message);
                }
            }
            // Offline fallback: accept key format and grant trial quotas
            if (!string.IsNullOrWhiteSpace(key))
            {
                _info.Plan = key.StartsWith("FR-", StringComparison.OrdinalIgnoreCase) ? "1m" : "trial";
                _info.TimeChangeKeysTotal = _info.Plan == "trial" ? 2 : 10;
                Save();
            }
            return _info;
        }

        public bool ConsumeTimeChangeKey()
        {
            if (_info.TimeChangeKeysUsed >= _info.TimeChangeKeysTotal) return false;
            _info.TimeChangeKeysUsed++;
            Save();
            return true;
        }

        /// <summary>Consume a Time Change Key via the server (hard quota) when a license
        /// server is configured; otherwise falls back to the local quota ledger.</summary>
        public async Task<(bool ok, int remaining)> ConsumeTimeChangeKeyAsync()
        {
            var server = _settings.LicenseServer;
            if (!string.IsNullOrEmpty(server))
            {
                try
                {
                    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                    var resp = await http.PostAsJsonAsync(server.TrimEnd('/') + "/api/license/consume",
                        new { key = _info.Key, feature = "timechange" }).ConfigureAwait(false);
                    if (resp.IsSuccessStatusCode)
                    {
                        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                        using var doc = JsonDocument.Parse(body);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("ok", out var ok) && ok.GetBoolean())
                        {
                            if (root.TryGetProperty("used", out var u) && u.TryGetInt32(out var used))
                                _info.TimeChangeKeysUsed = used;
                            Save();
                            return (true, _info.TimeChangeKeysTotal - _info.TimeChangeKeysUsed);
                        }
                        if (root.TryGetProperty("error", out var err))
                            Log.Warn("License", "Server refused key: " + err.GetString());
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn("License", "Server quota unreachable, using local quota: " + ex.Message);
                }
            }
            var okLocal = ConsumeTimeChangeKey();
            return (okLocal, _info.TimeChangeKeysTotal - _info.TimeChangeKeysUsed);
        }

        public bool CanUseRegFull => _info.RegFullEnabled;
        public bool CanUseNovery => _info.VerifyNoveryEnabled;
    }
}
