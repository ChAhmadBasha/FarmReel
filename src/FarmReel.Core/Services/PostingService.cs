using System;
using System.IO;
using System.Threading.Tasks;
using FarmReel.Core.Models;
using FarmReel.Core.Utils;

namespace FarmReel.Core.Services
{
    /// <summary>Resolves captions/comments/hashtags for post jobs (file -> random -> AI).</summary>
    public class PostingService
    {
        private readonly AiService _ai;

        public PostingService(AiService ai) { _ai = ai; }

        public string ResolveCaption(PostJob job, string folder, string mediaPath, Account account, Page page)
        {
            string caption = null;

            // 1. per-file caption (filename.txt / filename_caption.txt)
            if (job.CaptionFromFile)
                caption = FileNameParser.CaptionFor(mediaPath, folder) ?? FileNameParser.MultiCaptionFor(folder, mediaPath);

            // 2. caption from caption file (random line)
            if (string.IsNullOrEmpty(caption) && job.CaptionFromFile && !string.IsNullOrEmpty(job.CaptionFile))
                caption = RandomText.PickLine(job.CaptionFile);

            // 3. AI caption (only if none provided, like Bob Prime 1.5.83)
            if (string.IsNullOrEmpty(caption) && job.AiCaption)
                caption = TryAi(() => _ai.GenerateCaptionAsync(
                    string.IsNullOrEmpty(job.AiCaptionPrompt) ? Path.GetFileName(mediaPath) : job.AiCaptionPrompt,
                    page?.Name ?? "").GetAwaiter().GetResult());

            // 4. inline caption text
            if (string.IsNullOrEmpty(caption) && !string.IsNullOrEmpty(job.CaptionText))
                caption = RandomText.Expand(job.CaptionText, account?.Name ?? "", page?.Name ?? "");

            // hashtags
            var hashtags = job.HashtagsFromFile && !string.IsNullOrEmpty(job.HashtagsText)
                ? RandomText.PickLine(job.HashtagsText)
                : job.HashtagsText;

            if (caption == null) caption = "";
            if (!string.IsNullOrEmpty(hashtags)) caption = (caption + " " + hashtags).Trim();
            return caption;
        }

        public string ResolveComment(PostJob job, string folder, string mediaPath, Account account, Page page)
        {
            string comment = null;
            if (job.CommentEnabled)
            {
                // per-file comment
                comment = FileNameParser.CommentFor(mediaPath, folder);
                if (string.IsNullOrEmpty(comment) && job.CommentRandom && !string.IsNullOrEmpty(job.CommentFile))
                    comment = RandomText.PickLine(job.CommentFile);
                if (string.IsNullOrEmpty(comment) && !string.IsNullOrEmpty(job.CommentText))
                    comment = RandomText.Expand(job.CommentText, account?.Name ?? "", page?.Name ?? "");
            }
            if (string.IsNullOrEmpty(comment)) return "";
            if (!string.IsNullOrEmpty(job.AmazonTag))
            {
                comment = comment.Replace("{amazon_tag}", job.AmazonTag);
                if (comment.Contains("{product_link}") == false && comment.Contains("amzn.to") == false)
                    comment += " " + (job.AmazonTag.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                        ? job.AmazonTag
                        : "https://www.amazon.com/?tag=" + job.AmazonTag);
            }
            return comment;
        }

        public string BuildStatusText(PostJob job, Account account, Page page)
        {
            if (!string.IsNullOrEmpty(job.CaptionFile) && System.IO.File.Exists(job.CaptionFile))
            {
                var line = RandomText.PickLine(job.CaptionFile);
                if (!string.IsNullOrEmpty(line)) return RandomText.Expand(line, account?.Name ?? "", page?.Name ?? "");
            }
            if (job.AiCaption)
            {
                var ai = TryAi(() => _ai.GenerateStatusAsync(
                    string.IsNullOrEmpty(job.AiCaptionPrompt) ? "daily life update" : job.AiCaptionPrompt)
                    .GetAwaiter().GetResult());
                if (!string.IsNullOrEmpty(ai)) return ai;
            }
            return RandomText.Expand(job.CaptionText, account?.Name ?? "", page?.Name ?? "");
        }

        private static string TryAi(Func<string> fn)
        {
            try { return fn(); }
            catch (Exception ex) { Log.Warn("AI", "AI generation failed, fallback used: " + ex.Message); return ""; }
        }
    }
}
