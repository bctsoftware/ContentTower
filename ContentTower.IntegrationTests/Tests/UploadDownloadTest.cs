namespace ContentTower.IntegrationTests.Tests
{
    public class UploadDownloadTest : BaseTest
    {
        public override void Run()
        {
            var data = DataHelper.GetRandomData(123);
            var name = "testName";
            var type = "testType";

            var uploadUtc = DateTime.UtcNow;
            var cid = Ct.Upload(name, type, data);

            var metadata = Ct.Metadata(cid);

            Check(() => metadata.Cid.Hash == cid.Hash);
            Check(() => metadata.Name == name);
            Check(() => metadata.ContentType == type);
            Check(() => metadata.Length == data.Length);
            Check(() => IsCloseTo(metadata.UploadUtc, uploadUtc));
            Check(() => IsCloseTo(metadata.LastActivityUtc, uploadUtc));

            var download = Ct.Download(cid);

            Check(() => IsEqual(data, download));
        }
    }
}
