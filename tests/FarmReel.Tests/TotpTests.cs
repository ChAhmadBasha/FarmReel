using FarmReel.Core.Utils;
using Xunit;

namespace FarmReel.Tests
{
    public class TotpTests
    {
        // RFC 6238 test vector: ASCII secret "12345678901234567890" (base32 GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ),
        // time step 59 -> 8-digit code 94287082 (SHA1)
        [Fact]
        public void Generate_MatchesRfc6238Vector()
        {
            var code = Totp.Generate("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", digits: 8, periodSeconds: 30, unixTimeSeconds: 59);
            Assert.Equal("94287082", code);
        }

        [Fact]
        public void Generate_IsSixDigitsByDefault()
        {
            var code = Totp.Generate("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", unixTimeSeconds: 1234567890);
            Assert.Equal(6, code.Length);
            Assert.True(int.TryParse(code, out _));
        }

        [Fact]
        public void Generate_ChangesOverTime()
        {
            var secret = Totp.GenerateRandomSecret();
            var a = Totp.Generate(secret, unixTimeSeconds: 100);
            var b = Totp.Generate(secret, unixTimeSeconds: 130); // next window
            Assert.NotEqual(a, b);
        }

        [Fact]
        public void GenerateRandomSecret_IsBase32()
        {
            var secret = Totp.GenerateRandomSecret();
            Assert.All(secret, c => Assert.True(
                (c >= 'A' && c <= 'Z') || (c >= '2' && c <= '7')));
        }
    }
}
