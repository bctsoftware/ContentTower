namespace ContentTower.IntegrationTests
{
    public interface ILog
    {
        void Log(string msg);
    }

    public class ConsoleOutput : ILog
    {
        public void Log(string msg)
        {
            Console.WriteLine($"{DateTime.UtcNow.ToString("u")} {msg}");
        }
    }

    public class FileOutput : ILog
    {
        private readonly string filepath;

        public FileOutput(string filepath)
        {
            this.filepath = filepath;
        }

        public void Log(string msg)
        {
            try
            {
                File.AppendAllLines(filepath, [msg]);
            }
            catch
            {
            }
        }
    }

    public class MuxingLog : ILog
    {
        private readonly ILog[] backingLogs;

        public MuxingLog(params ILog[] backingLogs)
        {
            this.backingLogs = backingLogs;
        }

        public void Log(string msg)
        {
            foreach (var l in backingLogs) l.Log(msg);
        }
    }

    public class Prefixer : ILog
    {
        private readonly ILog backingLog;
        private readonly Func<string> getPrefix;

        public Prefixer(ILog backingLog, string prefix)
            : this(backingLog, () => prefix)
        {
        }

        public Prefixer(ILog backingLog, Func<string> getPrefix)
        {
            this.backingLog = backingLog;
            this.getPrefix = getPrefix;
        }

        public void Log(string msg)
        {
            backingLog.Log($"{getPrefix()} {msg}");
        }
    }

    public class TimestampPrefixer : Prefixer
    {
        public TimestampPrefixer(ILog backingLog)
            : base(backingLog, GetTimestamp)
        {
        }

        private static string GetTimestamp()
        {
            return $"[{DateTime.UtcNow.ToString("u")}]";
        }
    }
}
