using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FarmReel.Core.Flow;
using FarmReel.Core.Models;
using FarmReel.Core.Services;
using FarmReel.Core.Utils;
using FarmReel.Automation.Devices;
using FarmReel.Automation.Vision;

namespace FarmReel.Automation
{
    /// <summary>
    /// Maps logical product actions to FlowScript flows. Built-in flows ship as JSON;
    /// users can drop overrides into the flows folder (and record new ones with the
    /// Flow Recorder) so Facebook UI changes never require a rebuild.
    /// </summary>
    public class FlowRunner : IFlowRunner
    {
        private readonly DeviceManager _devices;
        private readonly SettingsService _settings;
        private readonly Dictionary<string, string> _builtInFlows;

        public FlowRunner(DeviceManager devices, SettingsService settings)
        {
            _devices = devices;
            _settings = settings;
            _builtInFlows = new Dictionary<string, string>(DefaultFlows.Build());
            foreach (var kv in DefaultFlows.BuildExtra())
                _builtInFlows[kv.Key] = kv.Value;
        }

        private FlowEngine EngineFor(DeviceInstance device, out FlowHost host)
        {
            var backend = _devices.GetBackend(device);
            var ocr = CreateOcr();
            host = new FlowHost(backend, ocr, _settings.AppDataDir + "\\tmp");
            return new FlowEngine(host);
        }

        private IOcrEngine CreateOcr()
        {
            var mode = _settings.Get("ocr_engine", "none");
            if (mode == "external")
                return new ExternalOcrEngine(_settings.Get("ocr_exe_path", ""), _settings.Get("ocr_args", "{image}"));
            return new NoOcrEngine();
        }

        private FlowDefinition ResolveFlow(string name)
        {
            // 1. user override from flows folder
            var path = Path.Combine(_settings.FlowsDir, name + ".json");
            if (File.Exists(path))
            {
                try { return FlowDefinition.Parse(File.ReadAllText(path)); }
                catch (Exception ex) { Log.Warn("Flow", $"Bad flow file {path}: {ex.Message}"); }
            }
            // 2. built-in
            if (_builtInFlows.TryGetValue(name, out var json))
            {
                try { return FlowDefinition.Parse(json); }
                catch (Exception ex) { Log.Warn("Flow", $"Bad built-in flow {name}: {ex.Message}"); }
            }
            return null;
        }

        private async Task<FlowResult> RunNamedAsync(DeviceInstance device, string flowName, Dictionary<string, string> vars)
        {
            if (device == null) return new FlowResult { Success = false, Message = "No device assigned" };
            var flow = ResolveFlow(flowName);
            if (flow == null)
                return new FlowResult
                {
                    Success = false,
                    Message = $"Flow '{flowName}' is not defined. Add '{flowName}.json' to the flows folder or record it with the Flow Recorder."
                };
            var engine = EngineFor(device, out _);
            var (ok, message) = await engine.ExecuteAsync(flow, vars ?? new Dictionary<string, string>(), default)
                .ConfigureAwait(false);
            return new FlowResult { Success = ok, Message = message };
        }

        public async Task<FlowResult> RunAsync(FlowContext ctx)
            => await RunNamedAsync(ctx.Device, ctx.FlowName, ctx.Params).ConfigureAwait(false);

        public Task<FlowResult> LoginAsync(DeviceInstance device, Account account, string otpCode)
        {
            return RunNamedAsync(device, "login", new Dictionary<string, string>
            {
                ["email"] = account?.Email ?? "",
                ["password"] = account != null ? CredentialVault.Unprotect(account.PasswordEnc) : "",
                ["otp"] = otpCode ?? "",
                ["package"] = device?.PackageName ?? "com.facebook.katana"
            });
        }

        public async Task<FlowResult> PullAccountNamesAsync(DeviceInstance device)
        {
            var result = await RunNamedAsync(device, "pull_names", new Dictionary<string, string>()).ConfigureAwait(false);
            // OCR name capture is best-effort; on success store nothing here (caller saves).
            return result;
        }

        public Task<FlowResult> PullPageNamesAsync(DeviceInstance device, Account account)
            => RunNamedAsync(device, "pull_page_names", new Dictionary<string, string> { ["account"] = account?.Email ?? "" });

        public async Task<FlowResult> PostAsync(DeviceInstance device, Account account, Page page, PostJob job,
            string mediaPath, string caption, string comment, string commentPhoto)
        {
            var flowName = job.PostType switch
            {
                PostType.Photo => "post_photo",
                PostType.Video => job.ReelOrVideo ? "post_reel" : "post_video",
                PostType.Reel => job.ReelOrVideo ? "post_reel" : "post_video",
                PostType.Status => "post_status",
                PostType.Story => "post_story",
                _ => "post_photo"
            };

            // Resolve all files of the post (multi-photo _M1.._M9 supported)
            var postFiles = new List<string>();
            if (!string.IsNullOrEmpty(mediaPath) && File.Exists(mediaPath))
            {
                postFiles = FileNameParser.ResolvePostFiles(job?.ContentFolder ?? "", mediaPath);
                if (postFiles.Count == 0) postFiles = new List<string> { mediaPath };
            }

            var vars = new Dictionary<string, string>
            {
                ["package"] = device?.PackageName ?? "com.facebook.katana",
                ["media"] = mediaPath ?? "",
                ["mediaName"] = string.IsNullOrEmpty(mediaPath) ? "" : Path.GetFileName(mediaPath),
                ["mediaFiles"] = string.Join(";", postFiles.Select(f => Path.GetFileName(f))),
                ["mediaCount"] = Math.Max(1, postFiles.Count).ToString(),
                ["caption"] = caption ?? "",
                ["comment"] = comment ?? "",
                ["commentPhoto"] = commentPhoto ?? "",
                ["audio"] = (job?.AudioEnabled ?? false).ToString(),
                ["aiLabel"] = (job?.AiLabel ?? false).ToString(),
                ["locationFlag"] = (job?.CheckInLocation ?? false).ToString(),
                ["storyLink"] = job != null && job.StoryWithLink
                    ? FileNameParser.LinkFor(mediaPath, job.ContentFolder) ?? ""
                    : "",
                ["location"] = job != null && job.CheckInLocation
                    ? (job.AutoLocation ? "@auto" : job.LocationName ?? "")
                    : "",
                ["audience"] = job?.Audience ?? "public",
                ["amazonTag"] = job?.AmazonTag ?? ""
            };

            // Push media to the device so the gallery can pick it up
            foreach (var pf in postFiles)
                await PushToDeviceAsync(device, pf).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(commentPhoto) && File.Exists(commentPhoto))
                await PushToDeviceAsync(device, commentPhoto).ConfigureAwait(false);

            var postResult = await RunNamedAsync(device, flowName, vars).ConfigureAwait(false);
            if (!postResult.Success) return postResult;

            // auto-comment on each post
            if (!string.IsNullOrEmpty(comment) && (job?.CommentEnabled ?? false))
            {
                var engine = EngineFor(device, out _);
                var cmtFlow = ResolveFlow("comment_on_post");
                if (cmtFlow != null)
                {
                    await engine.ExecuteAsync(cmtFlow,
                        new Dictionary<string, string> { ["comment"] = comment, ["photo"] = commentPhoto ?? "" },
                        default).ConfigureAwait(false);
                }
                // optional: delete the just-posted comment (engagement inflation without spam)
                if (job.CommentDeleteAfterUse)
                {
                    await RunNamedAsync(device, "delete_last_comment",
                        new Dictionary<string, string>()).ConfigureAwait(false);
                }
            }

            // cross-post/share to groups
            if (job != null && job.ShareGroupsCount > 0)
            {
                await RunNamedAsync(device, "share_to_groups",
                    new Dictionary<string, string> { ["count"] = job.ShareGroupsCount.ToString() }).ConfigureAwait(false);
            }

            // invite collaborators
            if (job != null && !string.IsNullOrEmpty(job.Collaborators))
            {
                await RunNamedAsync(device, "invite_collaborators",
                    new Dictionary<string, string> { ["collaborators"] = job.Collaborators, ["count"] = job.CollaboratorCount.ToString() })
                    .ConfigureAwait(false);
            }

            return postResult;
        }

        public Task<FlowResult> InteractAsync(DeviceInstance device, Account account, Page page, InteractionJob job,
            string action, string text)
        {
            var flowName = action switch
            {
                "like" => "interact_like",
                "react" => "interact_react",
                "comment" => "interact_comment",
                "follow" => "interact_follow",
                "share" => "interact_share",
                "view" => "interact_view",
                "checkin" => "interact_checkin",
                "confirm_friend" => "confirm_friend",
                "add_friend" => "add_friend",
                "join_group" => "join_group",
                "group_comment" => "interact_comment",
                "post_to_group" => "post_to_group",
                "watch_live" => "watch_live",
                "view_story" => "view_story",
                _ => "interact_view"
            };
            return RunNamedAsync(device, flowName, new Dictionary<string, string> { ["text"] = text ?? "" });
        }

        public Task<FlowResult> RunActionAsync(DeviceInstance device, Account account, string action, Dictionary<string, string> parameters)
        {
            var vars = new Dictionary<string, string>(parameters ?? new Dictionary<string, string>())
            {
                ["package"] = device?.PackageName ?? "com.facebook.katana",
                ["igpackage"] = _settings.Get("instagram_package", "com.instagram.android"),
                ["account"] = account?.Email ?? ""
            };
            return RunNamedAsync(device, action, vars);
        }

        public async Task<bool> IsDeviceReadyAsync(DeviceInstance device)
        {
            var backend = _devices.GetBackend(device);
            return backend.IsOnline && backend.Adb.IsPackageInstalled(device.PackageName);
        }

        public async Task<string> CaptureScreenshotAsync(DeviceInstance device)
        {
            var backend = _devices.GetBackend(device);
            var path = Path.Combine(_settings.AppDataDir, "screenshots", $"shot_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            return backend.Adb.SaveScreenshot(path) ? path : "";
        }

        private async Task PushToDeviceAsync(DeviceInstance device, string localPath)
        {
            try
            {
                var remote = "/sdcard/Pictures/";
                await Task.Run(() =>
                {
                    var result = ProcessRunner.Run(
                        _settings.AdbPath,
                        $"-s {device.AdbSerial} push {ProcessRunner.Quote(localPath)} {remote}", 180000);
                    if (result.code != 0)
                        Log.Warn("Flow", $"adb push failed for {localPath}: {result.stderr}");
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warn("Flow", "Push to device failed: " + ex.Message);
            }
        }
    }

    /// <summary>Built-in FlowScript definitions. Tune these against your Facebook app version
    /// (or better: record your own with the Flow Recorder / Flows tab).</summary>
    public static partial class DefaultFlows
    {
        public static Dictionary<string, string> Build()
        {
            var map = new Dictionary<string, string>
            {
                ["login"] = @"
{
  ""name"": ""login"",
  ""steps"": [
    { ""op"": ""startAppFresh"", ""text"": ""{{package}}"", ""timeout"": 10 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""log in|login|log in to facebook"" }, ""timeout"": 40 },
    { ""op"": ""tap"", ""find"": { ""regex"": ""log in|login"" }, ""timeout"": 10 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""mobile number or email|email or phone"" }, ""timeout"": 30 },
    { ""op"": ""type"", ""find"": { ""regex"": ""mobile number or email|email or phone"" }, ""text"": ""{{email}}"", ""timeout"": 15 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""^password$|password"" }, ""timeout"": 15 },
    { ""op"": ""type"", ""find"": { ""regex"": ""^password$|password"" }, ""text"": ""{{password}}"", ""timeout"": 15 },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""log in"" }, ""timeout"": 10 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""enter code|check your|code|continue"" }, ""timeout"": 25, ""optional"": true },
    { ""op"": ""type"", ""find"": { ""regex"": ""enter code|code"" }, ""text"": ""{{otp}}"", ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""continue|confirm"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""verify"", ""find"": { ""regex"": ""what's on your mind|news feed|home"" }, ""timeout"": 60, ""optional"": true },
    { ""op"": ""log"", ""text"": ""Login flow finished"" }
  ]
}",
                ["post_photo"] = @"
{
  ""name"": ""post_photo"",
  ""steps"": [
    { ""op"": ""startApp"", ""text"": ""{{package}}"", ""timeout"": 10 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""what's on your mind|create post|write something"" }, ""timeout"": 40 },
    { ""op"": ""tap"", ""find"": { ""regex"": ""what's on your mind|create post"" }, ""timeout"": 10 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""photo|add photo"" }, ""timeout"": 20 },
    { ""op"": ""tap"", ""find"": { ""regex"": ""^photo$|add photo"" }, ""timeout"": 10 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""done|next|gallery|photos"" }, ""timeout"": 25 },
    { ""op"": ""selectFiles"", ""text"": ""{{mediaName}}"", ""params"": { ""media"": ""photo"", ""names"": ""{{mediaFiles}}"", ""count"": ""{{mediaCount}}"" } },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""done|next"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIfTrue"", ""text"": ""{{locationFlag}}"", ""find"": { ""regex"": ""check in|add location|location"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""type"", ""find"": { ""regex"": ""search for a place|where are you"" }, ""text"": ""{{location}}"", ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIfTrue"", ""text"": ""{{aiLabel}}"", ""find"": { ""regex"": ""ai label|generated with ai|ai info"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""type"", ""find"": { ""regex"": ""say something|write a caption|add a caption"" }, ""text"": ""{{caption}}"", ""timeout"": 15, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""^post$|share"" }, ""timeout"": 10 },
    { ""op"": ""verify"", ""find"": { ""regex"": ""is now on facebook|post shared|posted"" }, ""timeout"": 60, ""optional"": true }
  ]
}",
                ["post_reel"] = @"
{
  ""name"": ""post_reel"",
  ""steps"": [
    { ""op"": ""startApp"", ""text"": ""{{package}}"", ""timeout"": 10 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""what's on your mind|create|reel"" }, ""timeout"": 40 },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""reel"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""add|reel|video"" }, ""timeout"": 20 },
    { ""op"": ""tap"", ""find"": { ""regex"": ""^add$|select video|upload"" }, ""timeout"": 10 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""done|next|gallery"" }, ""timeout"": 25 },
    { ""op"": ""selectFiles"", ""text"": ""{{mediaName}}"", ""params"": { ""media"": ""video"", ""names"": ""{{mediaFiles}}"", ""count"": ""{{mediaCount}}"" } },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""next|done"" }, ""timeout"": 10 },
    { ""op"": ""tapIfTrue"", ""text"": ""{{audio}}"", ""find"": { ""regex"": ""add audio|add music|music"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""tapIfTrue"", ""text"": ""{{locationFlag}}"", ""find"": { ""regex"": ""check in|add location|location"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""type"", ""find"": { ""regex"": ""search for a place|where are you"" }, ""text"": ""{{location}}"", ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIfTrue"", ""text"": ""{{aiLabel}}"", ""find"": { ""regex"": ""ai label|generated with ai|ai info"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""type"", ""find"": { ""regex"": ""say something|caption"" }, ""text"": ""{{caption}}"", ""timeout"": 15, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""^post$|share"" }, ""timeout"": 10 },
    { ""op"": ""verify"", ""find"": { ""regex"": ""posted|is now on facebook"" }, ""timeout"": 90, ""optional"": true }
  ]
}",
                ["post_video"] = @"
{
  ""name"": ""post_video"",
  ""steps"": [
    { ""op"": ""startApp"", ""text"": ""{{package}}"", ""timeout"": 10 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""what's on your mind|create post"" }, ""timeout"": 40 },
    { ""op"": ""tap"", ""find"": { ""regex"": ""what's on your mind|create post"" }, ""timeout"": 10 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""video|add video"" }, ""timeout"": 20 },
    { ""op"": ""tap"", ""find"": { ""regex"": ""video|add video"" }, ""timeout"": 10 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""done|next|gallery"" }, ""timeout"": 25 },
    { ""op"": ""selectFiles"", ""text"": ""{{mediaName}}"", ""params"": { ""media"": ""video"", ""names"": ""{{mediaFiles}}"", ""count"": ""{{mediaCount}}"" } },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""next|done"" }, ""timeout"": 10 },
    { ""op"": ""tapIfTrue"", ""text"": ""{{audio}}"", ""find"": { ""regex"": ""add audio|add music|music"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""tapIfTrue"", ""text"": ""{{locationFlag}}"", ""find"": { ""regex"": ""check in|add location|location"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""type"", ""find"": { ""regex"": ""search for a place|where are you"" }, ""text"": ""{{location}}"", ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIfTrue"", ""text"": ""{{aiLabel}}"", ""find"": { ""regex"": ""ai label|generated with ai|ai info"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""type"", ""find"": { ""regex"": ""say something|caption"" }, ""text"": ""{{caption}}"", ""timeout"": 15, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""^post$|share"" }, ""timeout"": 10 },
    { ""op"": ""verify"", ""find"": { ""regex"": ""posted|is now on facebook"" }, ""timeout"": 90, ""optional"": true }
  ]
}",
                ["post_status"] = @"
{
  ""name"": ""post_status"",
  ""steps"": [
    { ""op"": ""startApp"", ""text"": ""{{package}}"", ""timeout"": 10 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""what's on your mind|create post"" }, ""timeout"": 40 },
    { ""op"": ""tap"", ""find"": { ""regex"": ""what's on your mind|create post"" }, ""timeout"": 10 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""say something|what's on your mind"" }, ""timeout"": 15 },
    { ""op"": ""type"", ""find"": { ""regex"": ""say something|what's on your mind"" }, ""text"": ""{{caption}}"", ""timeout"": 15 },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""^post$|share"" }, ""timeout"": 10 },
    { ""op"": ""verify"", ""find"": { ""regex"": ""posted|is now on facebook"" }, ""timeout"": 60, ""optional"": true }
  ]
}",
                ["post_story"] = @"
{
  ""name"": ""post_story"",
  ""steps"": [
    { ""op"": ""startApp"", ""text"": ""{{package}}"", ""timeout"": 10 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""story|your story|add to story"" }, ""timeout"": 40 },
    { ""op"": ""tap"", ""find"": { ""regex"": ""add to story|create story"" }, ""timeout"": 10 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""gallery|photos|done|next"" }, ""timeout"": 25 },
    { ""op"": ""selectFiles"", ""text"": ""{{mediaName}}"", ""params"": { ""media"": ""photo"", ""names"": ""{{mediaFiles}}"", ""count"": ""{{mediaCount}}"" } },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""done|next"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""link"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""type"", ""find"": { ""regex"": ""add a link|enter link"" }, ""text"": ""{{storyLink}}"", ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""done|save"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""share to story|^share$|your story"" }, ""timeout"": 10 },
    { ""op"": ""verify"", ""find"": { ""regex"": ""story shared|posted"" }, ""timeout"": 40, ""optional"": true }
  ]
}",
                ["comment_on_post"] = @"
{
  ""name"": ""comment_on_post"",
  ""steps"": [
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""comment"" }, ""timeout"": 15, ""optional"": true },
    { ""op"": ""type"", ""find"": { ""regex"": ""write a comment|add a comment|comment"" }, ""text"": ""{{comment}}"", ""timeout"": 15, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""^post$|send"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""back"", ""timeout"": 5 }
  ]
}",
                ["interact_like"] = @"
{
  ""name"": ""interact_like"",
  ""steps"": [
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""^like$|like"" }, ""timeout"": 10 },
    { ""op"": ""log"", ""text"": ""liked"" }
  ]
}",
                ["interact_react"] = @"
{
  ""name"": ""interact_react"",
  ""steps"": [
    { ""op"": ""longPress"", ""find"": { ""regex"": ""^like$|reaction"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""text"": ""{{text}}"", ""exact"": true }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""log"", ""text"": ""reacted {{text}}"" }
  ]
}",
                ["interact_comment"] = @"
{
  ""name"": ""interact_comment"",
  ""steps"": [
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""comment"" }, ""timeout"": 10 },
    { ""op"": ""type"", ""find"": { ""regex"": ""write a comment|add a comment|comment"" }, ""text"": ""{{text}}"", ""timeout"": 12 },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""^post$|send"" }, ""timeout"": 8 },
    { ""op"": ""back"", ""timeout"": 5 }
  ]
}",
                ["interact_follow"] = @"
{
  ""name"": ""interact_follow"",
  ""steps"": [
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""^follow$|follow"" }, ""timeout"": 10 },
    { ""op"": ""log"", ""text"": ""followed"" }
  ]
}",
                ["interact_share"] = @"
{
  ""name"": ""interact_share"",
  ""steps"": [
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""share"" }, ""timeout"": 10 },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""share now|share to feed"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""log"", ""text"": ""shared"" }
  ]
}",
                ["interact_view"] = @"
{
  ""name"": ""interact_view"",
  ""steps"": [
    { ""op"": ""swipe"", ""x"": 540, ""y"": 1400, ""x2"": 540, ""y2"": 500, ""duration"": 400 },
    { ""op"": ""log"", ""text"": ""viewed"" }
  ]
}",
                ["interact_checkin"] = @"
{
  ""name"": ""interact_checkin"",
  ""steps"": [
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""what's on your mind|create post"" }, ""timeout"": 10 },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""check in|location"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""type"", ""find"": { ""regex"": ""search|where are you"" }, ""text"": ""{{text}}"", ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""^post$|share"" }, ""timeout"": 10 },
    { ""op"": ""log"", ""text"": ""checked in"" }
  ]
}",
                ["share_to_groups"] = @"
{
  ""name"": ""share_to_groups"",
  ""steps"": [
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""share"" }, ""timeout"": 10 },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""share to a group|groups"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""log"", ""text"": ""shared to groups ({{count}})"" }
  ]
}",
                ["invite_collaborators"] = @"
{
  ""name"": ""invite_collaborators"",
  ""steps"": [
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""invite|collaborator"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""log"", ""text"": ""collaborators: {{collaborators}}"" }
  ]
}"
            };
            return map;
        }
    }
}
