using FarmReel.Core.Utils;
using Xunit;

namespace FarmReel.Tests
{
    public class FileNameParserTests : IDisposable
    {
        private readonly string _dir;

        public FileNameParserTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "fr_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, true); } catch { }
        }

        private string W(string name, string content)
        {
            var p = Path.Combine(_dir, name);
            File.WriteAllText(p, content);
            return p;
        }

        [Fact]
        public void CaptionFor_ReadsFileCaption()
        {
            var media = W("photo.jpg", "x");
            W("photo.txt", "A nice caption");
            Assert.Equal("A nice caption", FileNameParser.CaptionFor(media, _dir));
        }

        [Fact]
        public void CaptionFor_FallsBackToCaptionSuffix()
        {
            var media = W("photo.jpg", "x");
            W("photo_caption.txt", "Fallback caption");
            Assert.Equal("Fallback caption", FileNameParser.CaptionFor(media, _dir));
        }

        [Fact]
        public void CaptionFor_ReturnsNullWhenMissing()
        {
            var media = W("photo.jpg", "x");
            Assert.Null(FileNameParser.CaptionFor(media, _dir));
        }

        [Fact]
        public void CommentFor_ReadsCommentFile()
        {
            var media = W("photo.jpg", "x");
            W("photo_comment.txt", "Nice!");
            Assert.Equal("Nice!", FileNameParser.CommentFor(media, _dir));
        }

        [Fact]
        public void CommentPhotoFor_FindsPhotoInCommentFolder()
        {
            var media = W("video.mp4", "x");
            Directory.CreateDirectory(Path.Combine(_dir, ".comment_photos"));
            W(".comment_photos/video.png", "img");
            var found = FileNameParser.CommentPhotoFor(media, _dir);
            Assert.NotNull(found);
            Assert.EndsWith("video.png", found);
        }

        [Fact]
        public void LinkFor_PicksRandomLine()
        {
            var media = W("1.jpg", "x");
            W("1_link.txt", "https://a.com\nhttps://b.com");
            var link = FileNameParser.LinkFor(media, _dir);
            Assert.True(link == "https://a.com" || link == "https://b.com");
        }

        [Fact]
        public void ResolvePostFiles_DetectsMultiPhoto()
        {
            W("trip.jpg", "x");
            W("trip_M1.jpg", "x");
            W("trip_M2.jpg", "x");
            W("trip_M3.jpg", "x");
            var files = FileNameParser.ResolvePostFiles(_dir, Path.Combine(_dir, "trip.jpg"));
            Assert.Equal(3, files.Count);
        }

        [Fact]
        public void ResolvePostFiles_CapsAtNine()
        {
            for (int i = 1; i <= 12; i++) W($"p_M{i}.jpg", "x");
            var files = FileNameParser.ResolvePostFiles(_dir, Path.Combine(_dir, "p_M1.jpg"));
            Assert.Equal(9, files.Count);
        }

        [Fact]
        public void MultiCaptionFor_ReadsGroupCaption()
        {
            W("p_M1.jpg", "x");
            W("p_M.txt", "group caption");
            Assert.Equal("group caption", FileNameParser.MultiCaptionFor(_dir, Path.Combine(_dir, "p_M1.jpg")));
        }

        [Fact]
        public void MoveToLifecycleFolder_MovesPosted()
        {
            var media = W("photo.jpg", "x");
            FileNameParser.MoveToLifecycleFolder(media, _dir, posted: true);
            Assert.False(File.Exists(Path.Combine(_dir, "photo.jpg")));
            Assert.True(File.Exists(Path.Combine(_dir, ".posted", "photo.jpg")));
        }

        [Fact]
        public void MoveToLifecycleFolder_MovesFailed()
        {
            var media = W("photo.jpg", "x");
            FileNameParser.MoveToLifecycleFolder(media, _dir, posted: false);
            Assert.True(File.Exists(Path.Combine(_dir, ".failed", "photo.jpg")));
        }

        [Fact]
        public void ScanMedia_RespectsFiltersAndSkipsHidden()
        {
            W("a.jpg", "x");
            W("b.mp4", "x");
            W(".posted", "");
            W("c.txt", "x"); // not media
            W("d_M1.jpg", "x"); // multi-photo, excluded from scan
            var files = FileNameParser.ScanMedia(_dir);
            Assert.Equal(2, files.Count);

            var filtered = FileNameParser.ScanMedia(_dir, false, "a", "");
            Assert.Single(filtered);
        }
    }
}
