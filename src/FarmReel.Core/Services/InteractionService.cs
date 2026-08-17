using System;
using System.Collections.Generic;
using System.IO;
using FarmReel.Core.Models;
using FarmReel.Core.Utils;

namespace FarmReel.Core.Services
{
    public class ActionItem
    {
        public string Action { get; set; } = "";
        public string Text { get; set; } = "";
    }

    /// <summary>Builds randomized, quota-limited interaction action plans for each kind.</summary>
    public class InteractionService
    {
        private static readonly string[] DefaultComments =
        {
            "Nice post! 👍", "Great content 🔥", "Keep it up!", "Awesome!",
            "Love this ❤️", "So true!", "Amazing shot 📸", "Well said!",
            "Thanks for sharing 🙏", "This is great!"
        };

        public List<ActionItem> BuildPlan(InteractionJob job)
        {
            var rng = new Random();
            var plan = new List<ActionItem>();
            var maxTotal = job.MaxActions <= 0 ? 30 : job.MaxActions;
            var kind = (job.Kind ?? "feed").ToLowerInvariant();

            switch (kind)
            {
                case "friends":
                    for (int i = 0; i < Math.Min(maxTotal, 20); i++)
                        plan.Add(new ActionItem { Action = rng.Next(3) == 0 ? "confirm_friend" : "add_friend", Text = "" });
                    return plan;

                case "groups":
                    for (int i = 0; i < Math.Min(maxTotal, 15); i++)
                    {
                        var roll = rng.Next(10);
                        plan.Add(new ActionItem
                        {
                            Action = roll < 4 ? "join_group" : roll < 7 ? "group_comment" : "post_to_group",
                            Text = roll < 7 ? PickComment(job, rng) : ""
                        });
                    }
                    return plan;

                case "live":
                    for (int i = 0; i < Math.Min(maxTotal, 10); i++)
                        plan.Add(new ActionItem { Action = "watch_live", Text = "" });
                    return plan;

                case "story":
                    for (int i = 0; i < Math.Min(maxTotal, 15); i++)
                        plan.Add(new ActionItem { Action = "view_story", Text = "" });
                    return plan;

                case "watch":
                    for (int i = 0; i < maxTotal; i++)
                        plan.Add(new ActionItem { Action = "view", Text = "" });
                    return plan;

                case "reels":
                case "feed":
                default:
                    return BuildFeedPlan(job, rng, maxTotal, reels: kind == "reels");
            }
        }

        private List<ActionItem> BuildFeedPlan(InteractionJob job, Random rng, int maxTotal, bool reels)
        {
            var plan = new List<ActionItem>();
            int likes = 0, comments = 0, follows = 0, shares = 0, views = 0;

            var pools = new List<(string action, int max, double weight)>
            {
                ("view", 999, 0.35),
                ("like", job.MaxLikes, 0.25),
                ("follow", job.MaxFollows, 0.10),
                ("share", job.MaxShares, 0.08),
                ("comment", job.MaxComments, 0.12),
                ("react", job.MaxLikes, 0.10)
            };
            if (reels)
            {
                // reels loop: mostly view/swipe with occasional like/comment
                pools = new List<(string action, int max, double weight)>
                {
                    ("view", 999, 0.60),
                    ("like", job.MaxLikes, 0.25),
                    ("comment", job.MaxComments, 0.10),
                    ("react", job.MaxLikes, 0.05)
                };
            }

            for (int i = 0; i < maxTotal; i++)
            {
                var totalWeight = 0.0;
                foreach (var p in pools) totalWeight += p.weight;
                var roll = rng.NextDouble() * totalWeight;
                string chosen = "view";
                foreach (var p in pools)
                {
                    roll -= p.weight;
                    if (roll <= 0) { chosen = p.action; break; }
                }

                switch (chosen)
                {
                    case "like" when likes < job.MaxLikes:
                        likes++;
                        plan.Add(new ActionItem { Action = "like", Text = "" });
                        break;
                    case "react" when likes < job.MaxLikes:
                        likes++;
                        plan.Add(new ActionItem { Action = "react", Text = RandomReaction(rng) });
                        break;
                    case "comment" when comments < job.MaxComments:
                        comments++;
                        plan.Add(new ActionItem { Action = "comment", Text = PickComment(job, rng) });
                        break;
                    case "follow" when follows < job.MaxFollows:
                        follows++;
                        plan.Add(new ActionItem { Action = "follow", Text = "" });
                        break;
                    case "share" when shares < job.MaxShares:
                        shares++;
                        plan.Add(new ActionItem { Action = "share", Text = "" });
                        break;
                    default:
                        views++;
                        plan.Add(new ActionItem { Action = "view", Text = "" });
                        break;
                }
            }

            if (job.CheckInPost)
                plan.Insert(0, new ActionItem { Action = "checkin", Text = job.AutoLocation ? "@auto" : "" });

            return plan;
        }

        private static string PickComment(InteractionJob job, Random rng)
        {
            if (job.CommentRandom && !string.IsNullOrEmpty(job.CommentFile) && File.Exists(job.CommentFile))
            {
                var line = RandomText.PickLine(job.CommentFile);
                if (!string.IsNullOrEmpty(line)) return line;
            }
            return DefaultComments[rng.Next(DefaultComments.Length)];
        }

        private static string RandomReaction(Random rng)
        {
            var reactions = new[] { "like", "love", "care", "haha", "wow", "sad", "angry" };
            return reactions[rng.Next(reactions.Length)];
        }
    }
}
