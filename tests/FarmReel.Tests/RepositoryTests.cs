using FarmReel.Core.Data;
using FarmReel.Core.Models;
using Xunit;

namespace FarmReel.Tests
{
    public class RepositoryTests : IDisposable
    {
        private readonly string _dbPath;

        public RepositoryTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), "fr_test_" + Guid.NewGuid().ToString("N") + ".db");
            Db.Initialize(_dbPath);
        }

        public void Dispose()
        {
            foreach (var f in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" })
                try { File.Delete(f); } catch { }
        }

        [Fact]
        public void AccountRepository_SaveGetUpdateDelete()
        {
            var repo = new AccountRepository();
            var acc = new Account
            {
                Email = "a@b.com",
                Name = "Test User",
                Status = AccountStatus.Live,
                DeviceId = 3,
                Phone = "+123",
                DateOfBirth = "1990-01-01"
            };
            var id = repo.Save(acc);
            Assert.True(id > 0);

            var loaded = repo.Get(id);
            Assert.Equal("a@b.com", loaded.Email);
            Assert.Equal("Test User", loaded.Name);
            Assert.Equal(AccountStatus.Live, loaded.Status);
            Assert.Equal(3, loaded.DeviceId);

            loaded.Name = "Renamed";
            repo.Save(loaded);
            Assert.Equal("Renamed", repo.Get(id).Name);

            repo.Delete(id);
            Assert.Null(repo.Get(id));
        }

        [Fact]
        public void PostJobRepository_RoundTripsAllFields()
        {
            var repo = new PostJobRepository();
            var job = new PostJob
            {
                PageId = 7,
                DeviceId = 2,
                PageName = "My Page",
                PostType = PostType.Reel,
                ContentFolder = @"C:\videos",
                NumberOfPosts = 3,
                CaptionFromFile = true,
                CaptionFile = "caps.txt",
                CaptionText = "fallback",
                AiCaption = true,
                HashtagsFromFile = true,
                HashtagsText = "ht.txt",
                CommentEnabled = true,
                CommentRandom = true,
                CommentFile = "cmt.txt",
                CommentDeleteAfterUse = true,
                AmazonTag = "myTag",
                AudioEnabled = true,
                AiLabel = true,
                StoryWithLink = true,
                ShareGroupsCount = 4,
                CheckInLocation = true,
                LocationName = "Phnom Penh",
                AutoLocation = true,
                Audience = "friends",
                ScheduleMode = ScheduleMode.Weekly,
                ScheduleCron = "mon 09:00",
                DailyLimit = 5,
                State = "done",
                PostsToday = 2,
                LastFile = "x.mp4"
            };
            var id = repo.Save(job);
            var loaded = repo.GetAll().First(j => j.Id == id);

            Assert.Equal(PostType.Reel, loaded.PostType);
            Assert.Equal(@"C:\videos", loaded.ContentFolder);
            Assert.True(loaded.CaptionFromFile);
            Assert.True(loaded.AiCaption);
            Assert.True(loaded.CommentDeleteAfterUse);
            Assert.Equal("myTag", loaded.AmazonTag);
            Assert.True(loaded.AudioEnabled);
            Assert.True(loaded.AiLabel);
            Assert.Equal(4, loaded.ShareGroupsCount);
            Assert.True(loaded.CheckInLocation);
            Assert.Equal("Phnom Penh", loaded.LocationName);
            Assert.Equal(ScheduleMode.Weekly, loaded.ScheduleMode);
            Assert.Equal("mon 09:00", loaded.ScheduleCron);
            Assert.Equal(5, loaded.DailyLimit);
            Assert.Equal("done", loaded.State);
            Assert.Equal(2, loaded.PostsToday);

            repo.Delete(id);
            Assert.Empty(repo.GetAll().Where(j => j.Id == id));
        }

        [Fact]
        public void SettingsRepository_SetGet()
        {
            var repo = new SettingsRepository();
            repo.Set("k", "v");
            Assert.Equal("v", repo.Get("k"));
            Assert.Equal("def", repo.Get("missing", "def"));
            Assert.Equal(7, repo.GetInt("int_k", 7));
            repo.Set("bool_k", "true");
            Assert.True(repo.GetBool("bool_k"));
        }

        [Fact]
        public void LogRepository_InsertAndRead()
        {
            var repo = new LogRepository();
            repo.Insert(new LogEntry { Action = "test", Message = "hello", Level = "INFO" });
            var entries = repo.GetRecent(10);
            Assert.Contains(entries, e => e.Action == "test" && e.Message == "hello");
        }

        [Fact]
        public void EmailRepository_RoundTrip()
        {
            var repo = new EmailRepository();
            var email = new EmailAccount { Email = "x@zoho.com", Provider = "zoho", IsTrusted = true };
            var id = repo.Save(email);
            var loaded = repo.GetAll().First(e => e.Id == id);
            Assert.Equal("x@zoho.com", loaded.Email);
            Assert.True(loaded.IsTrusted);
        }
    }
}
