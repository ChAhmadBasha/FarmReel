using System;
using System.Collections.Generic;

namespace FarmReel.Core.Models
{
    public enum PostType
    {
        Photo = 0,
        Video = 1,
        Reel = 2,
        Status = 3,
        Story = 4
    }

    public enum ScheduleMode
    {
        WhenRun = 0,
        Clock = 1,
        Weekly = 2
    }

    public class PostJob
    {
        public long Id { get; set; }
        public long PageId { get; set; }
        public long DeviceId { get; set; }
        public string PageName { get; set; } = "";
        public PostType PostType { get; set; } = PostType.Photo;
        public bool Enabled { get; set; } = true;

        // content
        public string ContentFolder { get; set; } = "";
        public int NumberOfPosts { get; set; } = 1;
        public bool RandomFolder { get; set; }
        public string RandomFolderRoot { get; set; } = "";
        public string FilenameFilterFirst { get; set; } = "";
        public string FilenameFilterLast { get; set; } = "";
        public bool ReelOrVideo { get; set; }             // if PostType == Reel: choose reel or video per file

        // caption & hashtags
        public bool CaptionFromFile { get; set; }
        public string CaptionFile { get; set; } = "";
        public string CaptionText { get; set; } = "";
        public bool AiCaption { get; set; }
        public string AiCaptionPrompt { get; set; } = "";
        public bool HashtagsFromFile { get; set; }
        public string HashtagsText { get; set; } = "";

        // comment
        public bool CommentEnabled { get; set; }
        public string CommentText { get; set; } = "";
        public bool CommentRandom { get; set; }
        public string CommentFile { get; set; } = "";
        public bool CommentWithPhoto { get; set; }
        public string CommentPhotoFolder { get; set; } = "";
        public bool CommentDeleteAfterUse { get; set; }
        public string AmazonTag { get; set; } = "";

        // extras
        public bool AudioEnabled { get; set; }
        public bool MuteOriginalAudio { get; set; } = true;
        public bool AiLabel { get; set; }
        public bool StoryWithLink { get; set; }
        public string Collaborators { get; set; } = "";   // names/ids separated by lines
        public int CollaboratorCount { get; set; }
        public int ShareGroupsCount { get; set; }
        public bool CheckInLocation { get; set; }
        public string LocationName { get; set; } = "";
        public bool AutoLocation { get; set; }
        public string Audience { get; set; } = "public";   // public | friends | onlyme

        // schedule
        public ScheduleMode ScheduleMode { get; set; } = ScheduleMode.WhenRun;
        public string ScheduleCron { get; set; } = "";    // "HH:mm" or "dow HH:mm" e.g. "12:30", "sun,mon 09:00"
        public int DailyLimit { get; set; }               // 0 = unlimited

        // lifecycle
        public string State { get; set; } = "idle";       // idle | queued | running | done | failed
        public string LastError { get; set; } = "";
        public DateTime? LastRunAt { get; set; }
        public DateTime? NextRunAt { get; set; }
        public long PostsToday { get; set; }
        public string LastFile { get; set; } = "";

        public string Display => $"{PostType} -> {PageName} [{Enabled}]";
    }

    public class InteractionJob
    {
        public long Id { get; set; }
        public long DeviceId { get; set; }
        public long PageId { get; set; }                  // 0 = account, else page identity
        public string Kind { get; set; } = "feed";        // feed | watch | reels | live | story | friends | groups
        public bool Enabled { get; set; } = true;
        public int MaxActions { get; set; } = 30;
        public int MaxLikes { get; set; } = 20;
        public int MaxComments { get; set; } = 5;
        public int MaxFollows { get; set; } = 5;
        public int MaxShares { get; set; } = 3;
        public int MinDelaySec { get; set; } = 3;
        public int MaxDelaySec { get; set; } = 12;
        public bool ReactRandom { get; set; } = true;
        public bool CommentRandom { get; set; } = true;
        public string CommentFile { get; set; } = "";
        public bool CommentDeleteAfterUse { get; set; }
        public bool LikeAndFollow { get; set; } = true;
        public bool CheckInPost { get; set; }
        public bool AutoLocation { get; set; }
        public string PolicyJson { get; set; } = "{}";
        public string State { get; set; } = "idle";
        public string LastError { get; set; } = "";
        public DateTime? LastRunAt { get; set; }

        public string Display => $"{Kind} on device {DeviceId} [{Enabled}]";
    }

    public class ScheduledTask
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public string Cron { get; set; } = "";            // "HH:mm" or "dow HH:mm"
        public string TargetType { get; set; } = "post";  // post | active | backup | shutdown
        public long TargetId { get; set; }
        public bool Enabled { get; set; } = true;
        public DateTime? LastRunAt { get; set; }
        public DateTime? NextRunAt { get; set; }
    }
}
