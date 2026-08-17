using FarmReel.Core.Utils;
using Xunit;

namespace FarmReel.Tests
{
    public class CredentialVaultTests
    {
        [Fact]
        public void Protect_Unprotect_RoundTrips()
        {
            var secret = "s3cr3t-password!";
            var enc = CredentialVault.Protect(secret);
            Assert.NotEqual(secret, enc);
            Assert.Equal(secret, CredentialVault.Unprotect(enc));
        }

        [Fact]
        public void Protect_Empty_ReturnsEmpty()
        {
            Assert.Equal("", CredentialVault.Protect(""));
            Assert.Equal("", CredentialVault.Unprotect(""));
        }

        [Fact]
        public void ExportWithPassphrase_ImportWithPassphrase_RoundTrips()
        {
            var bundle = CredentialVault.ExportWithPassphrase("portable secret", "correct horse");
            var plain = CredentialVault.ImportWithPassphrase(bundle, "correct horse");
            Assert.Equal("portable secret", plain);
        }

        [Fact]
        public void ImportWithPassphrase_WrongPassphrase_Throws()
        {
            var bundle = CredentialVault.ExportWithPassphrase("x", "pw1");
            Assert.ThrowsAny<System.Security.Cryptography.CryptographicException>(() =>
                CredentialVault.ImportWithPassphrase(bundle, "pw2"));
        }
    }
}
