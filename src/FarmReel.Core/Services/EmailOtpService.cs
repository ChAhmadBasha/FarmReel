using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FarmReel.Core.Models;
using FarmReel.Core.Utils;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MimeKit;

namespace FarmReel.Core.Services
{
    /// <summary>Reads Facebook verification codes from IMAP inboxes (Zoho / Gmail / Outlook / Yandex).</summary>
    public class EmailOtpService
    {
        private static readonly string[] FacebookSenders =
        {
            "facebookmail.com", "facebook.com", "support.facebook.com", "fb.com", "meta.com"
        };

        private static readonly Dictionary<string, (string host, int port)> Providers =
            new Dictionary<string, (string, int)>(StringComparer.OrdinalIgnoreCase)
            {
                ["zoho"] = ("imap.zoho.com", 993),
                ["gmail"] = ("imap.gmail.com", 993),
                ["outlook"] = ("outlook.office365.com", 993),
                ["microsoft"] = ("outlook.office365.com", 993),
                ["yandex"] = ("imap.yandex.com", 993),
                ["hotmail"] = ("outlook.office365.com", 993)
            };

        /// <summary>Fetch the latest verification code delivered to a receiver address using the stored mail account.</summary>
        public async Task<string> GetCodeAsync(string receiverEmail, string mailAccountEmail, string mailAccountPassword,
            string provider, int waitSeconds = 30)
        {
            if (string.IsNullOrEmpty(receiverEmail)) return "";
            var deadline = DateTime.UtcNow.AddSeconds(Math.Max(5, waitSeconds));
            while (DateTime.UtcNow < deadline)
            {
                var code = await TryReadCodeAsync(receiverEmail, mailAccountEmail, mailAccountPassword, provider)
                    .ConfigureAwait(false);
                if (!string.IsNullOrEmpty(code)) return code;
                await Task.Delay(3000).ConfigureAwait(false);
            }
            return "";
        }

        public async Task<string> GetCodeFromAccountAsync(EmailAccount mail, string receiverEmail, int waitSeconds = 30)
        {
            var password = CredentialVault.Unprotect(mail.PasswordEnc);
            return await GetCodeAsync(receiverEmail, mail.Email, password, mail.Provider, waitSeconds).ConfigureAwait(false);
        }

        private async Task<string> TryReadCodeAsync(string receiverEmail, string login, string password, string provider)
        {
            try
            {
                if (!Providers.TryGetValue(provider ?? "", out var cfg)) cfg = Providers["zoho"];
                using var client = new ImapClient();
                client.ServerCertificateValidationCallback = (s, c, ch, e) => true;
                await client.ConnectAsync(cfg.host, cfg.port, SecureSocketOptions.SslOnConnect)
                    .ConfigureAwait(false);
                await client.AuthenticateAsync(login, password).ConfigureAwait(false);
                await client.Inbox.OpenAsync(FolderAccess.ReadOnly).ConfigureAwait(false);

                var senderQuery = SearchQuery.Or(
                    SearchQuery.FromContains(FacebookSenders[0]),
                    SearchQuery.FromContains(FacebookSenders[1]));
                var query = SearchQuery.And(senderQuery, SearchQuery.ToContains(receiverEmail));
                var ids = await client.Inbox.SearchAsync(query).ConfigureAwait(false);
                foreach (var id in ids.OrderByDescending(x => x).Take(10))
                {
                    var msg = await client.Inbox.GetMessageAsync(id).ConfigureAwait(false);
                    var text = (msg.Subject ?? "") + "\n" +
                               (msg.TextBody ?? "") + "\n" +
                               (msg.HtmlBody ?? "");
                    var code = ExtractCode(text);
                    if (!string.IsNullOrEmpty(code)) return code;
                }
                await client.DisconnectAsync(true).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warn("EmailOtp", $"IMAP read failed ({login}): {ex.Message}");
            }
            return "";
        }

        public static string ExtractCode(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            // Prefer "code" context: "Your code is 123456" / "security code: 123456"
            var contextual = Regex.Match(text,
                @"(?:code|Code|CODE)[^0-9]{0,20}\b(\d{6})\b",
                RegexOptions.Singleline);
            if (contextual.Success) return contextual.Groups[1].Value;
            var any = Regex.Match(text, @"\b(\d{6})\b");
            return any.Success ? any.Groups[1].Value : "";
        }
    }
}
