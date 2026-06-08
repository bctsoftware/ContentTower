using ContentTower.IntegrationTests.Tests;
using ContentTowerOpenAPIClient;

namespace ContentTower.IntegrationTests
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var ctAddress = "http://localhost:5000"; // Environment.GetEnvironmentVariable("ContentTowerEndpoint");
            if (string.IsNullOrEmpty(ctAddress)) throw new Exception("Missing environment variable 'ContentTowerEndpoint'");

            var output = new Output();
            output.Log("Content-Tower Integration Tests");
            output.Log($"Using: '{ctAddress}'");

            var client = new Client(ctAddress, output);
            var options = client.Initialize();

            var p = new Program(client, output, new DataHelper(), options);
            p.Run();
        }

        private readonly Client client;
        private readonly Output output;
        private readonly DataHelper dataHelper;
        private readonly OptionsView options;

        public Program(Client client, Output output, DataHelper dataHelper, OptionsView options)
        {
            this.client = client;
            this.output = output;
            this.dataHelper = dataHelper;
            this.options = options;
        }

        public void Run()
        { 
            var tests = new ITest[]
            {
                new ActivityExtendsTempFileTest(),
                new DefaultCleanupTest(),
                new TempfileCleanupTest(),
                new UploadDownloadTest()
            };

            // start all tests
            var workers = new List<Task>();
            foreach (var test in tests)
            {
                workers.Add(Task.Run(() => RunTest(test)));
                Thread.Sleep(3000);
            }

            // wait all tests finished
            Task.WaitAll(workers);

            // process test results
            output.Log("Done! todo process results");
        }

        private void RunTest(ITest test)
        {
            try
            {
                test.Initialize(client, output, dataHelper, options);
                test.RunTest();
            }
            catch (Exception ex)
            {
                output.Log($"Exception raised by '{test.GetType().Name}' = {ex}");
                Environment.Exit(1);
            }
        }
    }
}
