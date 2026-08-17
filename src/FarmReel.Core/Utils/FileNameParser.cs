using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FarmReel.Core.Utils
{
    /// <summary>
    /// Implements the content-file naming conventions shared by Bob Prime and FarmReel:
    ///   photo.jpg          -> media file
    ///   photo.txt          -> caption for photo.jpg
    ///   photo_caption.txt  -> alternate caption file
    ///   photo_comment.txt  -> comment text for that file
    ///   photo_M1.jpg..M9   -> multi-photo selection (max 9)
    ///   photo_M.txt        -> caption for the multi-photo group
    ///   photo_link.txt     -> link to attach (stories)
    ///   .posted/ .failed/  -> lifecycle folders
    ///   .comment_photos/   -> comment photo attachments
    /// </summary>
    public static class FileNameParser
    {
        public static string StripExtension(string path)
        {
            var name = Path.GetFileName(path);
            var dot = name.LastIndexOf('.');
            return dot > 0 ? name.Substring(0, dot) : name;
        }

        public static string CaptionFor(string mediaPath, string folder)
        {
            var baseName = StripExtension(mediaPath);
            foreach (var candidate in new[]
            {
                Path.Combine(folder, baseName + ".txt"),
                Path.Combine(folder, baseName + "_caption.txt")
            })
            {
                if (File.Exists(candidate)) return File.ReadAllText(candidate).Trim();
            }
            return null;
        }

        public static string CommentFor(string mediaPath, string folder)
        {
            var baseName = StripExtension(mediaPath);
            var candidate = Path.Combine(folder, baseName + "_comment.txt");
            return File.Exists(candidate) ? File.ReadAllText(candidate).Trim() : null;
        }

        public static string CommentPhotoFor(string mediaPath, string folder)
        {
            // .comment_photos folder holds images named like the media file
            var baseName = StripExtension(mediaPath);
            var dir = Path.Combine(folder, ".comment_photos");
            if (!Directory.Exists(dir)) return null;
            var match = Directory.GetFiles(dir)
                .FirstOrDefault(f => StripExtension(f).Equals(baseName, StringComparison.OrdinalIgnoreCase));
            return match;
        }

        public static string LinkFor(string mediaPath, string folder)
        {
            var baseName = StripExtension(mediaPath);
            var candidate = Path.Combine(folder, baseName + "_link.txt");
            if (!File.Exists(candidate)) return null;
            var lines = File.ReadAllLines(candidate).Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            return lines.Count == 0 ? null : lines[new Random().Next(lines.Count)];
        }

        /// <summary>Returns the list of files forming one multi-photo post (or a single file).</summary>
        public static List<string> ResolvePostFiles(string folder, string mediaPath)
        {
            var baseName = StripExtension(mediaPath);
            var multi = Directory.GetFiles(folder)
                .Where(f => StripExtension(f).StartsWith(baseName + "_M", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => StripExtension(f), StringComparer.OrdinalIgnoreCase)
                .Take(9)
                .ToList();
            if (multi.Count > 0) return multi;
            return new List<string> { mediaPath };
        }

        public static string MultiCaptionFor(string folder, string mediaPath)
        {
            var baseName = StripExtension(mediaPath);
            var candidate = Path.Combine(folder, baseName + "_M.txt");
            return File.Exists(candidate) ? File.ReadAllText(candidate).Trim() : null;
        }

        public static void MoveToLifecycleFolder(string filePath, string folder, bool posted)
        {
            if (!File.Exists(filePath)) return;
            var target = Path.Combine(folder, posted ? ".posted" : ".failed");
            if (!Directory.Exists(target)) Directory.CreateDirectory(target);
            var dest = Path.Combine(target, Path.GetFileName(filePath));
            try { File.Move(filePath, dest, overwrite: true); }
            catch { }
        }

        public static void DeleteFile(string filePath)
        {
            try { if (File.Exists(filePath)) File.Delete(filePath); } catch { }
        }

        /// <summary>All media files in a folder matching extension whitelist, respecting filename filters.</summary>
        public static List<string> ScanMedia(string folder, bool includePosted = false, string filterFirst = "", string filterLast = "")
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return new List<string>();
            var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp",
                ".mp4", ".mov", ".mkv", ".avi", ".webm", ".3gp"
            };
            var files = Directory.GetFiles(folder)
                .Where(f =>
                {
                    if (!exts.Contains(Path.GetExtension(f))) return false;
                    if (!includePosted && Path.GetFileName(f).StartsWith(".", StringComparison.Ordinal)) return false;
                    if (StripExtension(f).Contains("_M", StringComparison.OrdinalIgnoreCase)) return false; // handled by multi logic
                    var name = Path.GetFileName(f);
                    if (!string.IsNullOrEmpty(filterFirst) && !name.StartsWith(filterFirst, StringComparison.OrdinalIgnoreCase)) return false;
                    if (!string.IsNullOrEmpty(filterLast) && !name.EndsWith(filterLast, StringComparison.OrdinalIgnoreCase)) return false;
                    return true;
                })
                .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                .ToList();
            return files;
        }
    }
}
