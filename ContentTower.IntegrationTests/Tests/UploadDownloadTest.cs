using System.Linq.Expressions;

namespace ContentTower.IntegrationTests.Tests
{
    public class UploadDownloadTest
    {
        private readonly Client client;
        private readonly ILog log;
        private readonly DataHelper dataHelper;

        public UploadDownloadTest(Client client, ILog log, DataHelper dataHelper)
        {
            this.client = client;
            this.log = log;
            this.dataHelper = dataHelper;
        }

        public void Run()
        {
            var data = dataHelper.GetRandomData(123);
            var name = "testName";
            var type = "testType";

            var uploadUtc = DateTime.UtcNow;
            var cid = client.Upload(name, type, data);

            log.Log("uploaded to " + cid);

            var metadata = client.Metadata(cid);

            Check(() => metadata.Cid.Hash == cid.Hash);
            Check(() => metadata.Name == name);
            Check(() => metadata.ContentType == type);
            Check(() => metadata.Length == data.Length);
            Check(() => IsCloseTo(metadata.UploadUtc, uploadUtc));
            Check(() => IsCloseTo(metadata.LastActivityUtc, uploadUtc));

            var download = client.Download(cid);

            Check(() => IsEqual(data, download));
        }

        private bool IsEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }

        private bool IsCloseTo(DateTimeOffset a, DateTime b)
        {
            return Math.Abs((a - b).TotalSeconds) < 60.0;
        }

        private void Check(Expression<Func<bool>> expression)
        {
            var str = expression.Body.ToString();
            var activated = expression.Compile();
            var result = activated();

            if (result) log.Log($"Check '{str}' = OK");
            else
            {
                log.Log($"Check '{str}' = Failed");
                // add to report!
            }
        }
    }
}
