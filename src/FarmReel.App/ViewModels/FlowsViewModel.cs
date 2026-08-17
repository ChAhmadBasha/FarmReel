using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using FarmReel.Automation;
using FarmReel.Core.Utils;

namespace FarmReel.App.ViewModels
{
    /// <summary>
    /// Flow builder: lists all built-in + custom flows, lets the user view/edit the JSON,
    /// validate it and save it as a custom override (flows are data, not code).
    /// </summary>
    public class FlowsViewModel : ObservableObject
    {
        private readonly ServiceLocator _svc;
        public ObservableCollection<string> FlowNames { get; } = new ObservableCollection<string>();

        private string _currentName = "";
        private string _currentJson = "";
        private string _status = "Select a flow on the left, or press New.";

        public string CurrentName { get => _currentName; set { _currentName = value ?? ""; OnPropertyChanged(); } }
        public string CurrentJson { get => _currentJson; set { _currentJson = value ?? ""; OnPropertyChanged(); } }
        public string Status { get => _status; set { _status = value ?? ""; OnPropertyChanged(); } }

        public FlowsViewModel(ServiceLocator svc)
        {
            _svc = svc;
            RefreshList();
        }

        public void RefreshList()
        {
            FlowNames.Clear();
            var map = AllFlows();
            foreach (var name in map.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
                FlowNames.Add(name);
            var dir = _svc.Settings.FlowsDir;
            if (Directory.Exists(dir))
            {
                foreach (var f in Directory.GetFiles(dir, "*.json"))
                {
                    var name = Path.GetFileNameWithoutExtension(f);
                    if (!FlowNames.Contains(name)) FlowNames.Add(name);
                }
            }
        }

        private static Dictionary<string, string> AllFlows()
        {
            var map = DefaultFlows.Build();
            foreach (var kv in DefaultFlows.BuildExtra()) map[kv.Key] = kv.Value;
            return map;
        }

        public void Load(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            CurrentName = name;
            var custom = Path.Combine(_svc.Settings.FlowsDir, name + ".json");
            if (File.Exists(custom))
            {
                CurrentJson = File.ReadAllText(custom);
                Status = "Loaded custom flow (overrides built-in of the same name).";
                return;
            }
            if (AllFlows().TryGetValue(name, out var json))
            {
                CurrentJson = json;
                Status = "Loaded built-in flow. Save to create a custom override.";
                return;
            }
            Status = "Flow not found.";
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(CurrentJson)) { Status = "Empty JSON."; return; }
            try
            {
                using var doc = JsonDocument.Parse(CurrentJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("steps", out var steps) && steps.GetArrayLength() > 0)
                    Status = "Valid flow JSON with " + steps.GetArrayLength() + " step(s).";
                else
                    Status = "JSON is valid but has no steps.";
            }
            catch (Exception ex)
            {
                Status = "Invalid JSON: " + ex.Message;
            }
        }

        public void Save()
        {
            if (string.IsNullOrWhiteSpace(CurrentName))
            {
                Status = "Enter a flow name first.";
                return;
            }
            try { JsonDocument.Parse(CurrentJson); }
            catch (Exception ex) { Status = "Cannot save invalid JSON: " + ex.Message; return; }

            var dir = _svc.Settings.FlowsDir;
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, CurrentName + ".json");
            File.WriteAllText(path, CurrentJson);
            Status = "Saved to " + path + " - this override is used instead of the built-in flow.";
            RefreshList();
        }

        public void NewFlow()
        {
            CurrentName = "";
            CurrentJson = "{\n  \"name\": \"my_flow\",\n  \"fb_version_min\": \"\",\n  \"steps\": [\n    { \"op\": \"startApp\", \"text\": \"{{package}}\", \"timeout\": 10 },\n    { \"op\": \"log\", \"text\": \"Hello from my flow\" }\n  ]\n}";
            Status = "New flow template. Set a name, edit the JSON, Validate, then Save.";
        }

        public void Delete()
        {
            if (string.IsNullOrWhiteSpace(CurrentName)) return;
            var path = Path.Combine(_svc.Settings.FlowsDir, CurrentName + ".json");
            if (File.Exists(path))
            {
                File.Delete(path);
                Status = "Deleted " + path;
                RefreshList();
            }
            else
            {
                Status = "Only custom flows can be deleted (built-ins ship with the app).";
            }
        }
    }
}
