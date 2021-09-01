using System.Diagnostics;

namespace Dhaf.Core
{
    public static class Shell
    {
        public static ExecResults Exec(string fileName, string args = "")
        {
            var psi = new ProcessStartInfo(fileName, args)
            {
                RedirectStandardOutput = true,
            };

            var proc = Process.Start(psi);
            if (proc == null)
            {
                return new ExecResults { Success = false };
            }

            var output = proc.StandardOutput
                .ReadToEnd()
                .TrimEnd('\r', '\n');

            proc.WaitForExit();

            var totalExecTime = (proc.ExitTime - proc.StartTime).TotalMilliseconds;


            return new ExecResults
            {
                Success = true,
                ExitCode = proc.ExitCode,
                TotalExecuteTime = totalExecTime,
                Output = output
            };
        }
    }
}
