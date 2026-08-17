using FarmReel.Core.Services;
using Xunit;

namespace FarmReel.Tests
{
    public class ThreatClassifierTests
    {
        [Theory]
        [InlineData("You can't use this feature right now. Try again later.", "locked282")]
        [InlineData("You cannot use this feature at the moment", "locked282")]
        [InlineData("We noticed unusual activity from your account", "checkpoint")]
        [InlineData("Confirm your identity to continue", "checkpoint")]
        [InlineData("Your account has been disabled", "suspended")]
        [InlineData("Your account has been permanently disabled", "suspended")]
        [InlineData("You have been temporarily locked", "templock")]
        [InlineData("We'll get back to you within 24 hours", "inreview")]
        [InlineData("Just a normal feed", "unknown")]
        [InlineData("", "unknown")]
        public void Classify_VariousScreens(string text, string expected)
        {
            Assert.Equal(expected, ThreatClassifier.Classify(text));
        }
    }
}
