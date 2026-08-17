using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FarmReel.Automation;
using FarmReel.Core.Flow;
using Xunit;

namespace FarmReel.Tests
{
    /// <summary>Fake IFlowHost that records the ops executed by the FlowEngine.</summary>
    public class FakeFlowHost : IFlowHost
    {
        public List<string> Ops { get; } = new List<string>();
        public string PresentText { get; set; } = "present element";
        public bool FindResult { get; set; } = true;

        public Task<bool> StartAppAsync(string package, bool fresh) { Ops.Add("startApp:" + package); return Task.FromResult(true); }
        public Task ForceStopAsync(string package) { Ops.Add("forceStop"); return Task.CompletedTask; }
        public Task ClearAppDataAsync(string package) { Ops.Add("clearData"); return Task.CompletedTask; }
        public Task<string> GetScreenTextAsync() { Ops.Add("getScreenText"); return Task.FromResult(PresentText); }
        public Task<ElementMatch> FindElementAsync(ElementMatcher matcher, int timeoutSec)
        {
            Ops.Add("find:" + (matcher?.Text ?? matcher?.Regex ?? "any"));
            return Task.FromResult(new ElementMatch
            {
                Found = FindResult,
                X = 100, Y = 200,
                Text = matcher?.Text ?? ""
            });
        }
        public Task<bool> TapAsync(ElementMatcher matcher, int x, int y) { Ops.Add($"tap:{(matcher?.Text ?? "")} {x},{y}"); return Task.FromResult(true); }
        public Task<bool> TapIfAsync(ElementMatcher matcher, int timeoutSec) { Ops.Add($"tapIf:{matcher?.Text ?? ""}"); return Task.FromResult(true); }
        public Task<bool> LongPressAsync(ElementMatcher matcher) { Ops.Add("longPress"); return Task.FromResult(true); }
        public Task SwipeAsync(int x1, int y1, int x2, int y2, int durationMs) { Ops.Add("swipe"); return Task.CompletedTask; }
        public Task<bool> ScrollToAsync(ElementMatcher matcher, string direction, int maxScrolls) { Ops.Add("scrollTo"); return Task.FromResult(true); }
        public Task<bool> TypeAsync(ElementMatcher matcher, string text) { Ops.Add($"type:{text}"); return Task.FromResult(true); }
        public Task<bool> VerifyAsync(ElementMatcher matcher, int timeoutSec) { Ops.Add("verify"); return Task.FromResult(true); }
        public Task BackAsync() { Ops.Add("back"); return Task.CompletedTask; }
        public Task OpenLinkAsync(string uri) { Ops.Add("openLink:" + uri); return Task.CompletedTask; }
        public Task<bool> PullAppDataAsync(string package, string destDir) { Ops.Add("pullAppData"); return Task.FromResult(true); }
        public Task<bool> SelectFilesAsync(IReadOnlyList<string> fileNames, string mediaType) { Ops.Add("selectFiles"); return Task.FromResult(true); }
        public Task<bool> CommentOnPostAsync(string text, string photoPath) { Ops.Add("commentOnPost:" + text); return Task.FromResult(true); }
        public Task<string> CaptureScreenshotAsync(string savePath) { Ops.Add("screenshot"); return Task.FromResult(""); }
        public Task<bool> IsScreenTextPresentAsync(string regex, int timeoutSec) { Ops.Add("isText:" + regex); return Task.FromResult(true); }
    }

    public class FlowEngineTests
    {
        [Fact]
        public async Task Execute_BasicFlow_RunsAllSteps()
        {
            var host = new FakeFlowHost();
            var engine = new FlowEngine(host);
            var flow = FlowDefinition.Parse(@"
            {
              ""name"": ""t"",
              ""steps"": [
                { ""op"": ""startApp"", ""text"": ""com.facebook.katana"", ""timeout"": 10 },
                { ""op"": ""waitFor"", ""find"": { ""text"": ""present element"" }, ""timeout"": 5 },
                { ""op"": ""tap"", ""find"": { ""text"": ""present element"" }, ""timeout"": 5 },
                { ""op"": ""type"", ""find"": { ""text"": ""field"" }, ""text"": ""hello {{name}}"", ""timeout"": 5 },
                { ""op"": ""log"", ""text"": ""done"" }
              ]
            }");

            var vars = new Dictionary<string, string> { ["name"] = "world" };
            var (ok, _) = await engine.ExecuteAsync(flow, vars, default);

            Assert.True(ok);
            Assert.Contains("startApp:com.facebook.katana", host.Ops);
            Assert.Contains("find:present element", host.Ops);
            Assert.Contains("tap:present element 100,200", host.Ops);
            Assert.Contains("type:hello world", host.Ops);
        }

        [Fact]
        public async Task Execute_FailedStep_ReturnsFailure()
        {
            var host = new FakeFlowHost { FindResult = false };
            var engine = new FlowEngine(host);
            var flow = FlowDefinition.Parse(@"
            {
              ""name"": ""t"",
              ""steps"": [
                { ""op"": ""waitFor"", ""find"": { ""text"": ""missing"" }, ""timeout"": 1 }
              ]
            }");
            var (ok, _) = await engine.ExecuteAsync(flow, new Dictionary<string, string>(), default);
            Assert.False(ok);
        }

        [Fact]
        public async Task Execute_TapIfTrue_FalseFlagSkips()
        {
            var host = new FakeFlowHost();
            var engine = new FlowEngine(host);
            var flow = FlowDefinition.Parse(@"
            {
              ""name"": ""t"",
              ""steps"": [
                { ""op"": ""tapIfTrue"", ""text"": ""{{flag}}"", ""find"": { ""text"": ""x"" }, ""timeout"": 3 },
                { ""op"": ""log"", ""text"": ""end"" }
              ]
            }");
            var (ok, _) = await engine.ExecuteAsync(flow, new Dictionary<string, string> { ["flag"] = "false" }, default);
            Assert.True(ok);
            Assert.DoesNotContain(host.Ops, o => o.StartsWith("tapIf:"));

            host.Ops.Clear();
            var (ok2, _) = await engine.ExecuteAsync(flow, new Dictionary<string, string> { ["flag"] = "true" }, default);
            Assert.True(ok2);
            Assert.Contains(host.Ops, o => o.StartsWith("tapIf:"));
        }

        [Fact]
        public async Task Execute_UnknownOp_OptionalReturnsOk()
        {
            var host = new FakeFlowHost();
            var engine = new FlowEngine(host);
            var flow = FlowDefinition.Parse(@"
            {
              ""name"": ""t"",
              ""steps"": [
                { ""op"": ""notARealOp"", ""timeout"": 1, ""optional"": true },
                { ""op"": ""log"", ""text"": ""end"" }
              ]
            }");
            var (ok, _) = await engine.ExecuteAsync(flow, new Dictionary<string, string>(), default);
            Assert.True(ok);
        }

        [Fact]
        public void DefaultFlows_AllBuiltInFlowsParse()
        {
            var map = FarmReel.Automation.DefaultFlows.Build();
            foreach (var kv in FarmReel.Automation.DefaultFlows.BuildExtra())
                map[kv.Key] = kv.Value;

            Assert.True(map.Count >= 50);
            foreach (var kv in map)
            {
                var flow = FlowDefinition.Parse(kv.Value);
                Assert.Equal(kv.Key, flow.Name);
                Assert.True(flow.Steps.Count > 0, kv.Key + " has no steps");
            }
        }
    }
}
