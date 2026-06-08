using ContentTower.IntegrationTests.Tests;

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

            var client = new Client(ctAddress, new Prefixer(output, "(client)"));
            var options = client.Initialize();

            // start all tests
            var a = new UploadDownloadTest(client, new Prefixer(output, "(UploadDownloadTest)"), new DataHelper());

            // wait all tests finished
            for (var i = 0; i < 100; i++)
            {
                a.Run();
            }

            // process test results

            output.Log("Done!");
        }
    }
}
