using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FarmReel.Core.Utils;

namespace FarmReel.Core.Flow
{
    /// <summary>An element matcher used by waitFor / tap / verify steps.</summary>
    public class ElementMatcher
    {
        public string Text { get; set; }
        public string Regex { get; set; }
        public string ResourceId { get; set; }
        public string Class { get; set; }
        public bool Exact { get; set; }
        public bool Any { get; set; }
    }

    public class FlowStep
    {
        public string Op { get; set; } = "";
        public ElementMatcher Find { get; set; }
        public string Text { get; set; } = "";
        public int TimeoutSec { get; set; } = 30;
        public bool Optional { get; set; }
        public string OrOp { get; set; } = "";
        public Dictionary<string, string> Params { get; set; } = new Dictionary<string, string>();
        public int X { get; set; }
        public int Y { get; set; }
        public int X2 { get; set; }
        public int Y2 { get; set; }
        public int DurationMs { get; set; } = 300;
    }

    public class FlowDefinition
    {
        public string Name { get; set; } = "";
        public string FbVersionMin { get; set; } = "";
        public List<FlowStep> Steps { get; set; } = new List<FlowStep>();

        public static FlowDefinition Parse(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var def = new FlowDefinition
            {
                Name = GetStr(root, "name"),
                FbVersionMin = GetStr(root, "fb_version_min")
            };
            if (root.TryGetProperty("steps", out var steps))
            {
                foreach (var s in steps.EnumerateArray())
                {
                    var step = new FlowStep
                    {
                        Op = GetStr(s, "op"),
                        Text = GetStr(s, "text"),
                        TimeoutSec = GetInt(s, "timeout", 30),
                        Optional = GetBool(s, "optional"),
                        OrOp = GetStr(s, "or"),
                        X = GetInt(s, "x"),
                        Y = GetInt(s, "y"),
                        X2 = GetInt(s, "x2"),
                        Y2 = GetInt(s, "y2"),
                        DurationMs = GetInt(s, "duration", 300)
                    };
                    if (s.TryGetProperty("find", out var f))
                    {
                        step.Find = new ElementMatcher
                        {
                            Text = GetStr(f, "text"),
                            Regex = GetStr(f, "regex"),
                            ResourceId = GetStr(f, "resourceId"),
                            Class = GetStr(f, "class"),
                            Exact = GetBool(f, "exact"),
                            Any = GetBool(f, "any")
                        };
                    }
                    if (s.TryGetProperty("params", out var p))
                    {
                        foreach (var kv in p.EnumerateObject())
                            step.Params[kv.Name] = kv.Value.GetString() ?? "";
                    }
                    def.Steps.Add(step);
                }
            }
            return def;
        }

        private static string GetStr(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

        private static int GetInt(JsonElement e, string name, int def) =>
            e.TryGetProperty(name, out var v) && v.TryGetInt32(out var i) ? i : def;

        private static bool GetBool(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;
    }

    /// <summary>Host primitives implemented by the Automation layer (ADB + vision).</summary>
    public interface IFlowHost
    {
        Task<bool> StartAppAsync(string package, bool fresh);
        Task ForceStopAsync(string package);
        Task ClearAppDataAsync(string package);
        Task<string> GetScreenTextAsync();
        Task<ElementMatch> FindElementAsync(ElementMatcher matcher, int timeoutSec);
        Task<bool> TapAsync(ElementMatcher matcher, int x, int y);
        Task<bool> TapIfAsync(ElementMatcher matcher, int timeoutSec);
        Task<bool> LongPressAsync(ElementMatcher matcher);
        Task SwipeAsync(int x1, int y1, int x2, int y2, int durationMs);
        Task ScrollToAsync(ElementMatcher matcher, string direction, int maxScrolls);
        Task TypeAsync(ElementMatcher matcher, string text);
        Task<bool> VerifyAsync(ElementMatcher matcher, int timeoutSec);
        Task BackAsync();
        Task OpenLinkAsync(string uri);
        Task<bool> PullAppDataAsync(string package, string destDir);
        Task<bool> SelectFilesAsync(IReadOnlyList<string> fileNames, string mediaType);
        Task<bool> CommentOnPostAsync(string text, string photoPath);
        Task<string> CaptureScreenshotAsync(string savePath);
        Task<bool> IsScreenTextPresentAsync(string regex, int timeoutSec);
    }

    public class ElementMatch
    {
        public bool Found { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string Text { get; set; } = "";
        public string ResourceId { get; set; } = "";
    }

    /// <summary>Executes declarative flow definitions. Flows are data, not code.</summary>
    public class FlowEngine
    {
        private readonly IFlowHost _host;

        public FlowEngine(IFlowHost host) { _host = host; }

        public async Task<(bool ok, string message)> ExecuteAsync(FlowDefinition flow, Dictionary<string, string> vars, CancellationToken ct)
        {
            var log = new System.Text.StringBuilder();
            foreach (var step in flow.Steps)
            {
                if (ct.IsCancellationRequested) return (false, "Cancelled");
                var ok = await ExecuteStepAsync(step, vars, log).ConfigureAwait(false);
                if (!ok)
                {
                    var msg = $"Flow '{flow.Name}' failed at step '{step.Op}'" +
                              (step.Find?.Text != null ? $" (text: {step.Find.Text})" : "");
                    Log.Warn("Flow", msg);
                    return (false, msg + "\n" + log);
                }
            }
            return (true, log.ToString());
        }

        private async Task<bool> ExecuteStepAsync(FlowStep step, Dictionary<string, string> vars, System.Text.StringBuilder log)
        {
            var op = step.Op;
            if (op.StartsWith("{{", StringComparison.Ordinal) || op.StartsWith("$", StringComparison.Ordinal))
            {
                op = Resolve(op, vars);
            }
            string text = Resolve(step.Text, vars);
            string or = Resolve(step.OrOp, vars);

            switch (op)
            {
                case "startApp":
                    return await _host.StartAppAsync(Resolve(text, vars) ?? "com.facebook.katana", false).ConfigureAwait(false);
                case "startAppFresh":
                    return await _host.StartAppAsync(Resolve(text, vars) ?? "com.facebook.katana", true).ConfigureAwait(false);
                case "forceStop":
                    await _host.ForceStopAsync(Resolve(text, vars) ?? "com.facebook.katana").ConfigureAwait(false);
                    return true;
                case "clearData":
                    await _host.ClearAppDataAsync(Resolve(text, vars) ?? "com.facebook.katana").ConfigureAwait(false);
                    return true;
                case "waitFor":
                {
                    var m = await _host.FindElementAsync(step.Find, step.TimeoutSec).ConfigureAwait(false);
                    return m.Found;
                }
                case "waitForText":
                    return await _host.IsScreenTextPresentAsync(text ?? ".", step.TimeoutSec).ConfigureAwait(false);
                case "tap":
                {
                    if (step.Find != null)
                        return await _host.TapAsync(step.Find, 0, 0).ConfigureAwait(false);
                    if (step.X > 0 || step.Y > 0)
                        return await _host.TapAsync(null, step.X, step.Y).ConfigureAwait(false);
                    return false;
                }
                case "tapIf":
                    return await _host.TapIfAsync(step.Find, step.TimeoutSec).ConfigureAwait(false);
                case "tapIfTrue":
                {
                    // only act when the resolved flag variable equals "true"
                    var flag = Resolve(step.Text, vars);
                    if (string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase))
                        return await _host.TapIfAsync(step.Find, step.TimeoutSec).ConfigureAwait(false);
                    return true;
                }
                case "longPress":
                    return await _host.LongPressAsync(step.Find).ConfigureAwait(false);
                case "swipe":
                    await _host.SwipeAsync(step.X, step.Y, step.X2, step.Y2, step.DurationMs).ConfigureAwait(false);
                    return true;
                case "scrollTo":
                    return await _host.ScrollToAsync(step.Find, Resolve(step.Params.GetValueOrDefault("direction"), vars) ?? "down",
                        step.Params.TryGetValue("maxScrolls", out var ms) && int.TryParse(ms, out var n) ? n : 20).ConfigureAwait(false);
                case "type":
                    return await _host.TypeAsync(step.Find, text).ConfigureAwait(false);
                case "verify":
                    return await _host.VerifyAsync(step.Find ?? new ElementMatcher { Regex = text }, step.TimeoutSec).ConfigureAwait(false);
                case "back":
                    await _host.BackAsync().ConfigureAwait(false);
                    return true;
                case "openLink":
                    await _host.OpenLinkAsync(text).ConfigureAwait(false);
                    return true;
                case "selectFiles":
                {
                    var names = (Resolve(step.Params.GetValueOrDefault("names"), vars) ?? text)
                        ?.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
                    return await _host.SelectFilesAsync(names,
                        Resolve(step.Params.GetValueOrDefault("media"), vars) ?? "photo").ConfigureAwait(false);
                }
                case "commentOnPost":
                    return await _host.CommentOnPostAsync(text, Resolve(step.Params.GetValueOrDefault("photo"), vars)).ConfigureAwait(false);
                case "screenshot":
                    await _host.CaptureScreenshotAsync(Resolve(step.Params.GetValueOrDefault("path"), vars) ?? "").ConfigureAwait(false);
                    return true;
                case "sleep":
                    await Task.Delay(step.TimeoutSec * 1000).ConfigureAwait(false);
                    return true;
                case "or":
                {
                    // try both finders
                    var ok1 = await _host.FindElementAsync(step.Find, Math.Min(step.TimeoutSec, 10)).ConfigureAwait(false);
                    if (ok1.Found) return true;
                    if (!string.IsNullOrEmpty(or))
                        return await _host.IsScreenTextPresentAsync(or, Math.Min(step.TimeoutSec, 10)).ConfigureAwait(false);
                    return false;
                }
                case "log":
                    log.AppendLine(text);
                    return true;
                case "dumpText":
                {
                    var screen = await _host.GetScreenTextAsync().ConfigureAwait(false);
                    log.AppendLine("-----SCREEN TEXT-----");
                    log.AppendLine(screen);
                    return true;
                }
                case "pullAppData":
                {
                    var dest = Resolve(step.Params.GetValueOrDefault("dest"), vars) ?? "";
                    return await _host.PullAppDataAsync(Resolve(text, vars) ?? "com.facebook.katana", dest)
                        .ConfigureAwait(false);
                }
                default:
                    Log.Warn("Flow", $"Unknown step op '{step.Op}'");
                    return step.Optional;
            }
        }

        public static string Resolve(string template, Dictionary<string, string> vars)
        {
            if (string.IsNullOrEmpty(template) || vars == null || vars.Count == 0) return template;
            foreach (var kv in vars)
                template = template.Replace("{{" + kv.Key + "}}", kv.Value);
            return template;
        }
    }
}
