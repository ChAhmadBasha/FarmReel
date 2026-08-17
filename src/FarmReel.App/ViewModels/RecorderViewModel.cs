using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FarmReel.Automation;
using FarmReel.Automation.Devices;
using FarmReel.Automation.Elements;
using FarmReel.Automation.Vision;
using FarmReel.Core.Flow;
using FarmReel.Core.Utils;

namespace FarmReel.App.ViewModels
{
    /// <summary>
    /// Flow Recorder: watch a live device screen, record screen transitions as
    /// waitFor steps and your clicks (on the preview) as tap steps, then save the
    /// result as a FlowScript JSON - no rebuild needed to fix Facebook UI changes.
    /// </summary>
    public class RecorderViewModel : ObservableObject
    {
        private readonly ServiceLocator _svc;
        private readonly List<FlowStep> _steps = new List<FlowStep>();
        private readonly HashSet<string> _seen = new HashSet<string>();
        private DispatcherTimer _timer;
        private string _lastShot = "";
        private int _lastW, _lastH;
        private bool _busy;
        private bool _isRecording;

        public ObservableCollection<DeviceRow> Devices { get; } = new ObservableCollection<DeviceRow>();
        public ObservableCollection<string> Steps { get; } = new ObservableCollection<string>();

        public RecorderViewModel(ServiceLocator svc)
        {
            _svc = svc;
            RefreshDevices();
        }

        public void RefreshDevices()
        {
            Devices.Clear();
            foreach (var d in _svc.Devices.GetAll()) Devices.Add(new DeviceRow { Model = d });
        }

        private DeviceRow _selectedDevice;
        public DeviceRow SelectedDevice
        {
            get => _selectedDevice;
            set { _selectedDevice = value; OnPropertyChanged(); }
        }

        private string _flowName = "";
        public string FlowName { get => _flowName; set { _flowName = value ?? ""; OnPropertyChanged(); } }

        public bool IsRecording
        {
            get => _isRecording;
            set { _isRecording = value; OnPropertyChanged(); }
        }

        public ImageSource Preview { get => _preview; set { _preview = value; OnPropertyChanged(); } }
        private ImageSource _preview;

        public int PreviewWidth => _lastW;
        public int PreviewHeight => _lastH;

        public void Start()
        {
            if (SelectedDevice == null) { Log.Warn("Recorder", "Select a device first"); return; }
            if (IsRecording) return;

            _steps.Clear(); Steps.Clear(); _seen.Clear();
            _steps.Add(new FlowStep { Op = "startApp", Text = "{{package}}", TimeoutSec = 10 });
            Steps.Add("startApp {{package}}");

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
            _timer.Tick += (s, e) => OnTick();
            _timer.Start();
            IsRecording = true;
            Log.Info("Recorder", "Recording started - drive the emulator manually. Click the preview to record taps.");
        }

        public void Stop()
        {
            _timer?.Stop();
            _timer = null;
            IsRecording = false;
            var name = string.IsNullOrWhiteSpace(FlowName) ? "untitled" : FlowName.Trim();
            _steps.Add(new FlowStep { Op = "log", Text = $"recorded flow: {name}" });
            Steps.Add($"log end ({_steps.Count - 1} steps)");
            Log.Info("Recorder", "Recording stopped");
        }

        public void Save()
        {
            var name = FlowName?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                Log.Warn("Recorder", "Enter a flow name before saving");
                return;
            }
            if (_steps.Count == 0)
            {
                Log.Warn("Recorder", "Nothing to save");
                return;
            }

            var flow = new
            {
                name,
                fb_version_min = "",
                steps = _steps
            };
            var json = JsonSerializer.Serialize(flow, new JsonSerializerOptions { WriteIndented = true });

            // validate
            try { JsonDocument.Parse(json); }
            catch (Exception ex) { Log.Error("Recorder", "Generated flow is invalid: " + ex.Message); return; }

            var dir = _svc.Settings.FlowsDir;
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, name + ".json");
            File.WriteAllText(path, json);
            Log.Info("Recorder", "Flow saved to " + path);
        }

        public void Clear()
        {
            _steps.Clear(); Steps.Clear(); _seen.Clear();
            Preview = null;
        }

        /// <summary>Record a tap at device coordinates (from a click on the preview).</summary>
        public void RecordTap(int x, int y)
        {
            if (!IsRecording) return;
            _steps.Add(new FlowStep { Op = "tap", X = x, Y = y });
            Steps.Add($"tap {x},{y}");
        }

        private async void OnTick()
        {
            if (_busy) return;
            _busy = true;
            try
            {
                var dev = SelectedDevice?.Model;
                if (dev == null) return;
                var backend = _svc.DeviceManager.GetBackend(dev);

                var shot = Path.Combine(Path.GetTempPath(), $"fr_rec_{Guid.NewGuid():N}.png");
                var ok = await Task.Run(() => backend.Adb.SaveScreenshot(shot)).ConfigureAwait(true);
                if (!ok) return;

                using (var bmp = ScreenService.Load(shot))
                {
                    if (bmp != null) { _lastW = bmp.Width; _lastH = bmp.Height; }
                }
                Preview = LoadImage(shot);
                try { if (!string.IsNullOrEmpty(_lastShot)) File.Delete(_lastShot); } catch { }
                _lastShot = shot;

                // capture visible texts (uiautomator) and record new ones as waitFor steps
                var texts = await Task.Run(() =>
                {
                    var finder = new ElementFinder(backend, _ => "");
                    var xml = backend.Adb.DumpUi();
                    return finder.FindInXml(xml, new ElementMatcher { Any = true })
                        .Select(m => m.Text)
                        .Where(t => !string.IsNullOrWhiteSpace(t))
                        .ToList();
                }).ConfigureAwait(true);

                var fresh = texts.Where(t => !_seen.Contains(t)).ToList();
                _seen.UnionWith(texts);
                var best = fresh
                    .Where(t => t.Length >= 4 && !long.TryParse(t, out _))
                    .OrderByDescending(t => t.Length)
                    .FirstOrDefault();
                if (best != null && (_steps.Count == 0 || _steps[_steps.Count - 1].Find?.Text != best))
                {
                    _steps.Add(new FlowStep { Op = "waitFor", Find = new ElementMatcher { Text = best }, TimeoutSec = 30 });
                    Steps.Add($"waitFor \"{best}\"");
                }
            }
            catch { }
            finally { _busy = false; }
        }

        private static ImageSource LoadImage(string path)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(path);
                bmp.EndInit();
                return bmp;
            }
            catch { return null; }
        }
    }
}
