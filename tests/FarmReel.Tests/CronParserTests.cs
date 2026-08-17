using FarmReel.Core.Utils;
using Xunit;

namespace FarmReel.Tests
{
    public class CronParserTests
    {
        [Theory]
        [InlineData("12:30", "2026-08-17 12:30:00", true)]
        [InlineData("12:30", "2026-08-17 12:31:00", false)]
        [InlineData("00:00", "2026-08-17 00:00:00", true)]
        [InlineData("", "2026-08-17 12:30:00", false)]
        public void Matches_HourMinute(string cron, string at, bool expected)
        {
            var dt = DateTime.Parse(at);
            Assert.Equal(expected, CronParser.Matches(cron, dt));
        }

        [Fact]
        public void Matches_WeeklyDayMask()
        {
            // Monday 2026-08-17
            var monday = new DateTime(2026, 8, 17, 9, 0, 0);
            var tuesday = new DateTime(2026, 8, 18, 9, 0, 0);
            Assert.True(CronParser.Matches("mon 09:00", monday));
            Assert.False(CronParser.Matches("mon 09:00", tuesday));
            Assert.True(CronParser.Matches("tue 09:00", tuesday));
        }

        [Fact]
        public void Matches_MultiDay()
        {
            var sun = new DateTime(2026, 8, 16, 9, 0, 0); // Sunday
            var mon = new DateTime(2026, 8, 17, 9, 0, 0);
            Assert.True(CronParser.Matches("sun,mon 09:00", sun));
            Assert.True(CronParser.Matches("sun,mon 09:00", mon));
            var tue = new DateTime(2026, 8, 18, 9, 0, 0);
            Assert.False(CronParser.Matches("sun,mon 09:00", tue));
        }
    }
}
