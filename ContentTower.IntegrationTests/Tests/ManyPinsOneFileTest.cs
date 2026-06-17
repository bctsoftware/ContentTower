using ContentTowerOpenAPIClient;

namespace ContentTower.IntegrationTests.Tests
{
    public class ManyPinsOneFileTest : BaseTest
    {
        public override void Run()
        {
            var (cid, pin1Id) = Ct.UploadNewPin("data", "type", DataHelper.GetRandomData(1024), StoreType.Default);
            Log("Create a temp pin for the cid.");
            var pin2Id = Ct.CreatePin(StoreType.Temporary, [cid]);

            Log("Create a default pin without cid.");
            var pin3Id = Ct.CreatePin(StoreType.Default);
            Log("Then attach the cid to it.");
            Ct.PatchPin(pin3Id, addCids: [cid], removeCids: []);

            Log("We have 1 content with 3 pins, a temporary and two default ones.");
            Log("The content shows the 3 pins.");
            Check(() => Ct.Check(cid) == true);
            var view = Ct.Metadata(cid);
            Check(() => view.PinIds.Count == 3);
            Check(() => view.PinIds.Contains(pin1Id.Id));
            Check(() => view.PinIds.Contains(pin2Id.Id));
            Check(() => view.PinIds.Contains(pin3Id.Id));

            Check(() => Ct.Check(pin1Id) == true);
            Check(() => Ct.Check(pin2Id) == true);
            Check(() => Ct.Check(pin3Id) == true);
            var pin1 = Ct.Pin(pin1Id);
            var pin2 = Ct.Pin(pin2Id);
            var pin3 = Ct.Pin(pin3Id);

            Log("Each pin shows the content.");
            Check(() => pin1.Cids.Count == 1);
            Check(() => pin1.Cids.Single() == cid.Id);
            Check(() => pin2.Cids.Count == 1);
            Check(() => pin2.Cids.Single() == cid.Id);
            Check(() => pin3.Cids.Count == 1);
            Check(() => pin3.Cids.Single() == cid.Id);

            Sleep(TimeSpan.FromSeconds(Options.StoreDurationTemporaryNominalSeconds));
            SleepCleanupInterval();

            Log("the temp pin is cleaned up before anything else.");
            Check(() => Ct.Check(cid) == true);
            Check(() => Ct.Check(pin1Id) == true);
            Check(() => Ct.Check(pin2Id) == false);
            Check(() => Ct.Check(pin3Id) == true);

            Sleep(TimeSpan.FromSeconds(Options.StoreDurationDefaultNominalSeconds));
            SleepCleanupInterval();

            Log("Then everything is cleaned up.");
            Check(() => Ct.Check(cid) == false);
            Check(() => Ct.Check(pin1Id) == false);
            Check(() => Ct.Check(pin2Id) == false);
            Check(() => Ct.Check(pin3Id) == false);
        }
    }
}
