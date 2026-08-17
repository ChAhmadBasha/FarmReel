using FarmReel.Core.Utils;
using Xunit;

namespace FarmReel.Tests
{
    public class RandomTextTests
    {
        [Fact]
        public void PickLine_ReturnsOneOfTheLines()
        {
            var file = Path.Combine(Path.GetTempPath(), "fr_rand_" + Guid.NewGuid().ToString("N") + ".txt");
            File.WriteAllLines(file, new[] { "alpha", "beta", "gamma" });
            try
            {
                for (int i = 0; i < 20; i++)
                    Assert.Contains(RandomText.PickLine(file), new[] { "alpha", "beta", "gamma" });
            }
            finally { File.Delete(file); }
        }

        [Fact]
        public void PickLine_MissingFile_ReturnsEmpty()
        {
            Assert.Equal("", RandomText.PickLine(@"C:\definitely\missing.txt"));
        }

        [Fact]
        public void Expand_ReplacesPlaceholders()
        {
            var text = RandomText.Expand("%date% %name% %page% %random_number%", "alice", "my page");
            Assert.StartsWith(DateTime.Now.ToString("MMM d, yyyy"), text);
            Assert.Contains("alice", text);
            Assert.Contains("my page", text);
            Assert.Matches(@"\d", text);
        }

        [Fact]
        public void Expand_NullReturnsNull()
        {
            Assert.Null(RandomText.Expand(null));
        }

        [Fact]
        public void RandomDelay_WithinBounds()
        {
            for (int i = 0; i < 50; i++)
            {
                var d = RandomText.RandomDelay(3, 12);
                Assert.InRange(d, 3, 12);
            }
        }
    }
}
