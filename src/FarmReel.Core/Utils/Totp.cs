using System;
using System.Security.Cryptography;
using System.Text;

namespace FarmReel.Core.Utils
{
    /// <summary>RFC 6238 TOTP (Google Authenticator compatible).</summary>
    public static class Totp
    {
        public static string Generate(string base32Secret, int digits = 6, int periodSeconds = 30, long? unixTimeSeconds = null)
        {
            var secret = Base32Decode(base32Secret);
            var counter = (unixTimeSeconds ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds()) / periodSeconds;
            var counterBytes = BitConverter.GetBytes(counter);
            if (BitConverter.IsLittleEndian) Array.Reverse(counterBytes);

            using var hmac = new HMACSHA1(secret);
            var hash = hmac.ComputeHash(counterBytes);
            int offset = hash[hash.Length - 1] & 0x0f;
            int binary =
                ((hash[offset] & 0x7f) << 24) |
                ((hash[offset + 1] & 0xff) << 16) |
                ((hash[offset + 2] & 0xff) << 8) |
                (hash[offset + 3] & 0xff);
            var otp = binary % (int)Math.Pow(10, digits);
            return otp.ToString().PadLeft(digits, '0');
        }

        public static string GenerateRandomSecret()
        {
            var bytes = RandomNumberGenerator.GetBytes(20);
            return Base32Encode(bytes);
        }

        private static string Base32Encode(byte[] data)
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
            var sb = new StringBuilder();
            int bits = 0, value = 0;
            foreach (var b in data)
            {
                value = (value << 8) | b;
                bits += 8;
                while (bits >= 5)
                {
                    sb.Append(alphabet[(value >> (bits - 5)) & 0x1f]);
                    bits -= 5;
                }
            }
            if (bits > 0) sb.Append(alphabet[(value << (5 - bits)) & 0x1f]);
            return sb.ToString();
        }

        private static byte[] Base32Decode(string input)
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
            input = input.ToUpperInvariant().Replace(" ", "").Replace("-", "");
            var bytes = new System.Collections.Generic.List<byte>();
            int bits = 0, value = 0;
            foreach (var c in input)
            {
                int idx = alphabet.IndexOf(c);
                if (idx < 0) continue;
                value = (value << 5) | idx;
                bits += 5;
                if (bits >= 8)
                {
                    bytes.Add((byte)((value >> (bits - 8)) & 0xff));
                    bits -= 8;
                }
            }
            return bytes.ToArray();
        }
    }
}
