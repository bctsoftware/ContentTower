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
            var (cid, pinId)= Ct.UploadNewPin(name, type, data);

            var metadata = Ct.Metadata(cid);
            Check(() => metadata.Cid == cid.Id);
            Check(() => metadata.Name == name);
            Check(() => metadata.ContentType == type);
            Check(() => metadata.Length == data.Length);
            Check(() => metadata.PinIds.Count == 1);
            Check(() => metadata.PinIds.Single() == pinId.Id);

            var pin = Ct.Pin(pinId);
            Check(() => IsCloseTo(pin.CreateUtc, uploadUtc));
            Check(() => IsCloseTo(pin.LastActivityUtc, uploadUtc));
            Check(() => pin.Cids.Count == 1);
            Check(() => pin.Cids.Single() == cid.Id);

            var download = Ct.Download(cid);

            Check(() => IsEqual(data, download));
        }
    }
}
