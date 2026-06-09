using ContentTowerOpenAPIClient;

namespace ContentTower.IntegrationTests
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var ctAddress = Environment.GetEnvironmentVariable("ContentTowerEndpoint");
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
            var tests = DiscoverTests();

            foreach (var test in tests)
            {
                RunTest(test);
                output.Log("");
            }

            ProcessResults(tests);
            output.Log("Done!");
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

        private void ProcessResults(ITest[] tests)
        {
            var successfulls = tests.Count(t => t.Failures.Length == 0);
            var failures = tests.Length - successfulls;

            foreach (var t in tests)
            {
                PrintFailures(t);
            }
            output.Log("");
            output.Log("");
            output.Log("Results:");
            output.Log($"Failed tests: {failures}");
            output.Log($"Successful tests: {successfulls}");
        }

        private void PrintFailures(ITest t)
        {
            foreach (var f in t.Failures)
            {
                output.Log($" - {f}");
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
