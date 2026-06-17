using ContentTowerOpenAPIClient;
using System.Linq.Expressions;

namespace ContentTower.IntegrationTests
{
    public interface ITest
    {
        void Initialize(Client client, ILog log, DataHelper dataHelper, OptionsView options);
        void RunTest();
        string[] Failures { get; }
    }

    public abstract class BaseTest : ITest
    {
        private readonly List<string> failures = new();
        private int checkCounter = 0;

        public void Initialize(Client client, ILog log, DataHelper dataHelper, OptionsView options)
        {
            Ct = client;
            Logger = new Prefixer(log, $"({GetType().Name})");
            DataHelper = dataHelper;
            Options = options;
        }

        public abstract void Run();

        public void RunTest()
        {
            Log("Start...");
            try
            {
                Run();
            }
            catch (Exception ex)
            {
                Fail("Exception: " + ex);
            }
            Log("");
            if (failures.Count > 0)
            {
                Log($"Finished with {failures.Count} failures:");
                foreach (var f in failures)
                {
                    Log($"   - {f}");
                }
            }
            else Log("Finished successfully.");
            Log("");
        }

        protected ILog Logger { get; private set; } = null!;
        protected Client Ct { get; private set; } = null!;
        protected DataHelper DataHelper { get; private set; } = null!;
        protected OptionsView Options { get; private set; } = null!;

        public string[] Failures => failures.ToArray();

        protected void Log(string msg)
        {
            Logger.Log(msg);
        }

        protected void Fail(string msg)
        {
            Log(msg);
            failures.Add(msg);
        }

        protected bool IsEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }

        protected bool IsCloseTo(DateTimeOffset a, DateTime b)
        {
            return Math.Abs((a - b).TotalSeconds) < 60.0;
        }

        protected void Check(Expression<Func<bool>> expression)
        {
            var str = expression.Body.ToString();
            var activated = expression.Compile();
            var result = activated();

            if (result) Log($"Check[{checkCounter}] = OK");
            else
            {
                Fail($"Check[{checkCounter}] '{str}' = Failed");
            }
            checkCounter++;
        }

        protected (string, string, Cid, PinId) UploadRandom(int length)
        {
            return UploadRandom(length, StoreType.Default);
        }

        protected (string, string, Cid, PinId) UploadRandom(StoreType storeType)
        {
            return UploadRandom(DataHelper.GetRandomNumber(1000, 50000), storeType);
        }

        protected (string, string, Cid, PinId) UploadRandom(int length, StoreType storeType)
        {
            var name = DataHelper.GetRandomString();
            var type = DataHelper.GetRandomString();
            var data = DataHelper.GetRandomData(length);
            var (cid, pinId) = Ct.UploadNewPin(name, type, data, storeType);
            return (name, type, cid, pinId);
        }

        protected void Sleep(TimeSpan span)
        {
            Log(nameof(Sleep) + ": " + span.TotalSeconds + " seconds");
            Thread.Sleep(span);
        }

        protected void SleepCleanupInterval()
        {
            Log(nameof(SleepCleanupInterval));
            Thread.Sleep(TimeSpan.FromSeconds(Options.CleanupIntervalSeconds) * 3.0);
        }
    }
}
