namespace ContentTower.IntegrationTests
{
    public interface ILog
    {
        void Log(string msg);
    }

    public class Output : ILog
    {
        public void Log(string msg)
        {
            Console.WriteLine($"{DateTime.UtcNow.ToString("u")} {msg}");
        }
    }

    public class Prefixer : ILog
    {
        private readonly ILog backingLog;
        private readonly string prefix;

        public Prefixer(ILog backingLog, string prefix)
        {
            this.backingLog = backingLog;
            this.prefix = prefix;
        }

        public void Log(string msg)
        {
            backingLog.Log($"{prefix} {msg}");
        }
    }
}
