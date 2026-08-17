using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FarmReel.Core.Flow;
using FarmReel.Automation.Devices;
using FarmReel.Automation.Vision;

namespace FarmReel.Automation.Elements
{
    /// <summary>
    /// Finds UI elements on a device: first via uiautomator XML hierarchy,
    /// then via OCR text (handles Facebook's canvas-rendered UI).
    /// </summary>
    public class ElementFinder
    {
        private readonly IDeviceBackend _backend;
        private readonly Func<string> _ocr;

        public ElementFinder(IDeviceBackend backend, Func<string> ocr)
        {
            _backend = backend;
            _ocr = ocr;
        }

        public string DumpXml() => _backend.Adb.DumpUi();

        public List<ElementMatch> FindInXml(string xml, ElementMatcher matcher)
        {
            var results = new List<ElementMatch>();
            if (string.IsNullOrEmpty(xml)) return results;
            try
            {
                var doc = XDocument.Parse(xml);
                foreach (var node in doc.Descendants("node"))
                {
                    var text = (string)node.Attribute("text") ?? "";
                    var desc = (string)node.Attribute("content-desc") ?? "";
                    var resId = (string)node.Attribute("resource-id") ?? "";
                    var cls = (string)node.Attribute("class") ?? "";
                    var bounds = (string)node.Attribute("bounds") ?? "";

                    var haystack = (text + " " + desc).Trim();
                    bool match = false;
                    if (matcher == null) match = true;
                    else
                    {
                        if (matcher.Any) match = true;
                        if (!string.IsNullOrEmpty(matcher.Text))
                            match = matcher.Exact
                                ? haystack.Equals(matcher.Text, StringComparison.OrdinalIgnoreCase)
                                : haystack.IndexOf(matcher.Text, StringComparison.OrdinalIgnoreCase) >= 0;
                        if (!match && !string.IsNullOrEmpty(matcher.Regex))
                            match = Regex.IsMatch(haystack, matcher.Regex, RegexOptions.IgnoreCase);
                        if (!match && !string.IsNullOrEmpty(matcher.ResourceId))
                            match = resId.Equals(matcher.ResourceId, StringComparison.OrdinalIgnoreCase);
                        if (!match && !string.IsNullOrEmpty(matcher.Class))
                            match = cls.Equals(matcher.Class, StringComparison.OrdinalIgnoreCase);
                    }
                    if (!match) continue;

                    var (x, y, w, h) = ParseBounds(bounds);
                    results.Add(new ElementMatch
                    {
                        Found = true,
                        X = x + w / 2,
                        Y = y + h / 2,
                        Width = w,
                        Height = h,
                        Text = text,
                        ResourceId = resId
                    });
                }
            }
            catch { /* malformed XML: fall through to OCR */ }
            return results;
        }

        public ElementMatch Find(ElementMatcher matcher, string screenshotPath)
        {
            var xml = DumpXml();
            var inXml = FindInXml(xml, matcher);
            if (inXml.Count > 0) return inXml[0];

            // OCR fallback on the captured screenshot
            if (matcher != null && !string.IsNullOrEmpty(screenshotPath) && File.Exists(screenshotPath))
            {
                var text = _ocr?.Invoke(screenshotPath) ?? "";
                var haystack = text;
                bool found = false;
                if (!string.IsNullOrEmpty(matcher.Text))
                    found = matcher.Exact
                        ? haystack.Equals(matcher.Text, StringComparison.OrdinalIgnoreCase)
                        : haystack.IndexOf(matcher.Text, StringComparison.OrdinalIgnoreCase) >= 0;
                if (!found && !string.IsNullOrEmpty(matcher.Regex))
                    found = Regex.IsMatch(haystack, matcher.Regex, RegexOptions.IgnoreCase);
                if (found)
                {
                    // center-tap fallback: we know the text exists but not its exact position
                    using var bmp = ScreenService.Load(screenshotPath);
                    return new ElementMatch
                    {
                        Found = true,
                        X = bmp != null ? bmp.Width / 2 : 540,
                        Y = bmp != null ? bmp.Height / 2 : 960,
                        Width = 0, Height = 0, Text = matcher.Text
                    };
                }
            }
            return new ElementMatch { Found = false };
        }

        public bool IsTextPresent(string regexOrText, string screenshotPath, bool isRegex)
        {
            var xml = DumpXml();
            var m = new ElementMatcher { Regex = isRegex ? regexOrText : null, Text = isRegex ? null : regexOrText };
            if (FindInXml(xml, m).Count > 0) return true;
            var text = _ocr?.Invoke(screenshotPath) ?? "";
            return isRegex
                ? Regex.IsMatch(text, regexOrText, RegexOptions.IgnoreCase)
                : text.IndexOf(regexOrText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static (int x, int y, int w, int h) ParseBounds(string bounds)
        {
            // format: "[l,t][r,b]"
            var m = Regex.Match(bounds ?? "", @"\[(\d+),(\d+)\]\[(\d+),(\d+)\]");
            if (!m.Success) return (0, 0, 0, 0);
            int l = int.Parse(m.Groups[1].Value), t = int.Parse(m.Groups[2].Value);
            int r = int.Parse(m.Groups[3].Value), b = int.Parse(m.Groups[4].Value);
            return (l, t, Math.Max(0, r - l), Math.Max(0, b - t));
        }
    }
}
