using System.Collections.Generic;
using System.Threading.Tasks;
using FarmReel.Core.Models;

namespace FarmReel.Core.Services
{
    public class FlowResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public string ScreenshotPath { get; set; } = "";
        public Dictionary<string, string> Data { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>What a flow needs to know about the device it runs on.</summary>
    public class FlowContext
    {
        public DeviceInstance Device { get; set; }
        public Account Account { get; set; }
        public Page Page { get; set; }
        public string FlowName { get; set; } = "";
        public Dictionary<string, string> Params { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Implemented by the Automation layer. Core services only depend on this interface,
    /// so the whole product can be driven without the UI knowing the details.
    /// </summary>
    public interface IFlowRunner
    {
        Task<FlowResult> RunAsync(FlowContext ctx);
        Task<FlowResult> LoginAsync(DeviceInstance device, Account account, string otpCode);
        Task<FlowResult> PullAccountNamesAsync(DeviceInstance device);
        Task<FlowResult> PullPageNamesAsync(DeviceInstance device, Account account);
        Task<FlowResult> PostAsync(DeviceInstance device, Account account, Page page, PostJob job, string mediaPath,
                                   string caption, string comment, string commentPhoto);
        Task<FlowResult> InteractAsync(DeviceInstance device, Account account, Page page, InteractionJob job,
                                       string action, string text);
        Task<FlowResult> RunActionAsync(DeviceInstance device, Account account, string action, Dictionary<string, string> parameters);
        Task<bool> IsDeviceReadyAsync(DeviceInstance device);
        Task<string> CaptureScreenshotAsync(DeviceInstance device);
    }

    /// <summary>Implemented by the Devices layer so the orchestrator can boot/stop devices.</summary>
    public interface IDeviceCoordinator
    {
        Task<bool> EnsureStartedAsync(DeviceInstance device);
        Task<bool> EnsureStoppedAsync(DeviceInstance device);
        Task<List<string>> ListAvailableAsync();
    }
}
