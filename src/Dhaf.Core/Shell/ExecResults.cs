namespace Dhaf.Core
{
    public class ExecResults
    {
        public string Output { get; set; }
        public double TotalExecuteTime { get; set; }
        public int ExitCode { get; set; }

        public bool Success { get; set; }
    }
}
