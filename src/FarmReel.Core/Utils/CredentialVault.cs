using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace FarmReel.Core.Utils
{
    /// <summary>
    /// Encrypts secrets with Windows DPAPI (machine scope). For portable export,
    /// supports AES-256-GCM with a user passphrase (PBKDF2-derived key).
    /// </summary>
    public static class CredentialVault
    {
        public static string Protect(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return "";
            try
            {
                var bytes = Encoding.UTF8.GetBytes(plain);
                var enc = ProtectedData.Protect(bytes, null, DataProtectionScope.LocalMachine);
                return Convert.ToBase64String(enc);
            }
            catch
            {
                // Fallback: obfuscated base64 (still better than plaintext in DB)
                var bytes = Encoding.UTF8.GetBytes(plain);
                return "b64:" + Convert.ToBase64String(bytes);
            }
        }

        public static string Unprotect(string enc)
        {
            if (string.IsNullOrEmpty(enc)) return "";
            try
            {
                if (enc.StartsWith("b64:", StringComparison.Ordinal))
                    return Encoding.UTF8.GetString(Convert.FromBase64String(enc.Substring(4)));
                var bytes = Convert.FromBase64String(enc);
                var dec = ProtectedData.Unprotect(bytes, null, DataProtectionScope.LocalMachine);
                return Encoding.UTF8.GetString(dec);
            }
            catch
            {
                return "";
            }
        }

        /// <summary>Export an encrypted bundle that can be moved between machines.</summary>
        public static byte[] ExportWithPassphrase(string plain, string passphrase)
        {
            if (string.IsNullOrEmpty(passphrase)) throw new ArgumentException("Passphrase required");
            var salt = RandomNumberGenerator.GetBytes(16);
            var key = DeriveKey(passphrase, salt);
            var nonce = RandomNumberGenerator.GetBytes(12);
            var plainBytes = Encoding.UTF8.GetBytes(plain ?? "");
            var cipher = new byte[plainBytes.Length];
            using (var aes = new AesGcm(key, 16))
            {
                aes.Encrypt(nonce, plainBytes, cipher, out var tag);
                var result = new byte[16 + 12 + 16 + cipher.Length];
                Buffer.BlockCopy(salt, 0, result, 0, 16);
                Buffer.BlockCopy(nonce, 0, result, 16, 12);
                Buffer.BlockCopy(tag, 0, result, 28, 16);
                Buffer.BlockCopy(cipher, 0, result, 44, cipher.Length);
                return result;
            }
        }

        public static string ImportWithPassphrase(byte[] bundle, string passphrase)
        {
            if (bundle.Length < 44) throw new ArgumentException("Invalid bundle");
            var salt = new byte[16]; var nonce = new byte[12]; var tag = new byte[16];
            Buffer.BlockCopy(bundle, 0, salt, 0, 16);
            Buffer.BlockCopy(bundle, 16, nonce, 0, 12);
            Buffer.BlockCopy(bundle, 28, tag, 0, 16);
            var cipher = new byte[bundle.Length - 44];
            Buffer.BlockCopy(bundle, 44, cipher, 0, cipher.Length);
            var key = DeriveKey(passphrase, salt);
            var plain = new byte[cipher.Length];
            using (var aes = new AesGcm(key, 16))
            {
                aes.Decrypt(nonce, cipher, tag, plain);
            }
            return Encoding.UTF8.GetString(plain);
        }

        private static byte[] DeriveKey(string passphrase, byte[] salt)
        {
            using var pbkdf2 = new Rfc2898DeriveBytes(passphrase, salt, 100_000, HashAlgorithmName.SHA256);
            return pbkdf2.GetBytes(32);
        }
    }
}
