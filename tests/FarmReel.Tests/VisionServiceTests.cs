using FarmReel.Automation;
using FarmReel.Automation.Vision;
using System.Drawing;
using Xunit;

namespace FarmReel.Tests
{
    public class VisionServiceTests
    {
        [Fact]
        public void TemplateMatcher_FindsExactCopy()
        {
            using var screen = new Bitmap(200, 200);
            using var g = Graphics.FromImage(screen);
            g.Clear(Color.White);
            using (var b = new SolidBrush(Color.Red)) g.FillRectangle(b, 40, 50, 60, 70);

            using var tpl = new Bitmap(60, 70);
            using var g2 = Graphics.FromImage(tpl);
            g2.Clear(Color.White);
            using (var b = new SolidBrush(Color.Red)) g2.FillRectangle(b, 0, 0, 60, 70);

            bool found = TemplateMatcher.Find(screen, tpl, out int x, out int y, threshold: 0.9);
            Assert.True(found);
            Assert.InRange(x, 60, 80);   // 40 + 30 (center)
            Assert.InRange(y, 75, 95);   // 50 + 35
        }

        [Fact]
        public void TemplateMatcher_NotPresent_ReturnsFalse()
        {
            using var screen = new Bitmap(100, 100);
            using var g = Graphics.FromImage(screen);
            g.Clear(Color.White);

            using var tpl = new Bitmap(20, 20);
            using var g2 = Graphics.FromImage(tpl);
            g2.Clear(Color.Black);

            bool found = TemplateMatcher.Find(screen, tpl, out _, out _, threshold: 0.9);
            Assert.False(found);
        }

        [Fact]
        public void ExternalOcr_NotAvailable_WhenMissing()
        {
            var ocr = new ExternalOcrEngine(@"C:\missing\ocr.exe", "{image}");
            Assert.False(ocr.IsAvailable);
            Assert.Equal("", ocr.Recognize("whatever.png"));
        }
    }
}
