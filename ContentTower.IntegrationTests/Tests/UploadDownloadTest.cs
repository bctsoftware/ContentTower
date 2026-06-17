using ContentTowerOpenAPIClient;

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

            Log("After upload, the content shows the correct metadata.");
            var metadata = Ct.Metadata(cid);
            Check(() => Ct.Check(cid));
            Check(() => metadata.Cid == cid.Id);
            Check(() => metadata.Name == name);
            Check(() => metadata.ContentType == type);
            Check(() => metadata.Length == data.Length);
            Check(() => metadata.PinIds.Count == 1);
            Check(() => metadata.PinIds.Single() == pinId.Id);

            Log("After upload, the pin shows the correct store type and timing information");
            var pin = Ct.Pin(pinId);
            Check(() => Ct.Check(pinId));
            Check(() => pin.StoreType == StoreType.Default);
            Check(() => IsCloseTo(pin.CreateUtc, uploadUtc));
            Check(() => IsCloseTo(pin.LastActivityUtc, uploadUtc));
            Check(() => pin.Cids.Count == 1);
            Check(() => pin.Cids.Single() == cid.Id);

            Log("The downloaded content is binary-equal to the uploaded content.");
            var download = Ct.Download(cid);
            Check(() => IsEqual(data, download));
        }
    }
}
