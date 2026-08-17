using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace FarmReel.Automation
{
    public static class ProcessRunner
    {
        public static (int exitCode, string stdout, string stderr) Run(string exe, string args, int timeoutMs = 60000)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
                using var p = Process.Start(psi);
                var stdoutTask = p?.StandardOutput.ReadToEndAsync() ?? Task.FromResult("");
                var stderrTask = p?.StandardError.ReadToEndAsync() ?? Task.FromResult("");
                if (!p.WaitForExit(timeoutMs))
                {
                    try { p.Kill(true); } catch { }
                    return (124, "", "Timeout");
                }
                return (p.ExitCode, stdoutTask.Result, stderrTask.Result);
            }
            catch (Exception ex)
            {
                return (-1, "", ex.Message);
            }
        }

        public static string Quote(string arg)
        {
            if (string.IsNullOrEmpty(arg)) return "\"\"";
            return "\"" + arg.Replace("\"", "\\\"") + "\"";
        }
    }
}
