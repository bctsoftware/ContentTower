namespace ContentTower.IntegrationTests
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var ctAddress = Environment.GetEnvironmentVariable("ContentTowerEndpoint");
            if (string.IsNullOrEmpty(ctAddress)) throw new Exception("Missing environment variable 'ContentTowerEndpoint'");

            var testLogsFolder = Environment.GetEnvironmentVariable("TestLogsFolder");
            if (string.IsNullOrEmpty(testLogsFolder)) testLogsFolder = "testlogs";

            var output = new ConsoleOutput();
            output.Log("Content-Tower Integration Tests");
            output.Log($"Using: '{ctAddress}'");

            EnsureContentTowerOnline(ctAddress, output);

            var p = new Program(output, new DataHelper(), ctAddress, testLogsFolder);
            p.Run();
        }

        private static void EnsureContentTowerOnline(string ctAddress, ILog log)
        {
            var client = new Client(ctAddress, log);
            client.Initialize();
            log.Log("ContentTower online.");
        }

        private readonly ConsoleOutput log;
        private readonly DataHelper dataHelper;
        private readonly string ctAddress;
        private readonly string logsFolder;

        public Program(ConsoleOutput log, DataHelper dataHelper, string ctAddress, string logsFolder)
        {
            this.log = log;
            this.dataHelper = dataHelper;
            this.ctAddress = ctAddress;
            this.logsFolder = logsFolder;
        }

        public void Run()
        {
            Directory.CreateDirectory(logsFolder);

            var tests = DiscoverTests();
            foreach (var test in tests)
            {
                RunTest(test);
            }

            ProcessResults(tests);
            log.Log("Done!");
        }

        private void RunTest(ITest test)
        {
            try
            {
                var testFileLog = new FileOutput(Path.Combine(logsFolder, $"{test.GetType().Name}.log"));
                var testLog = new TimestampPrefixer(new MuxingLog(log, testFileLog));

                var client = new Client(ctAddress, testLog);
                var options = client.Initialize();
                
                test.Initialize(client, testLog, dataHelper, options);
                test.RunTest();
                log.Log("");
            }
            catch (Exception ex)
            {
                log.Log($"Exception raised by '{test.GetType().Name}' = {ex}");
                Environment.Exit(1);
            }
        }

        private void ProcessResults(ITest[] tests)
        {
            var successfulls = tests.Count(t => t.Failures.Length == 0);
            var failures = tests.Length - successfulls;

            foreach (var t in tests)
            {
                PrintFailures(t);
            }
            log.Log("");
            log.Log("");
            log.Log("Results:");
            log.Log($"Failed tests: {failures}");
            log.Log($"Successful tests: {successfulls}");
        }

        private void PrintFailures(ITest t)
        {
            foreach (var f in t.Failures)
            {
                log.Log($" - {f}");
            }
        }

        private static ITest[] DiscoverTests()
        {
            var assembly = typeof(Program).Assembly;
            return assembly.GetTypes()
                .Where(t => typeof(ITest).IsAssignableFrom(t))
                .Where(t => !t.IsAbstract)
                .Select(Activator.CreateInstance)
                .Cast<ITest>()
                .ToArray();
        }
    }
}
