using ContentTowerOpenAPIClient;

namespace ContentTower.IntegrationTests.Tests
{
    public class OnePinManyFilesTest : BaseTest
    {
        public override void Run()
        {
            var pinId = Ct.CreatePin(StoreType.Default);

            var cid1 = Ct.UploadAttachPin("one", "one", DataHelper.GetRandomData(1024), pinId);
            var cid2 = Ct.UploadAttachPin("two", "two", DataHelper.GetRandomData(1024), pinId);
            var cid3 = Ct.UploadAttachPin("three", "three", DataHelper.GetRandomData(1024), pinId);

            Check(() => Ct.Check(pinId) == true);
            var pin = Ct.Pin(pinId);
            Check(() => pin.Cids.Count == 3);
            Check(() => pin.Cids.Contains(cid1.Id));
            Check(() => pin.Cids.Contains(cid2.Id));
            Check(() => pin.Cids.Contains(cid3.Id));

            Check(() => Ct.Check(cid1) == true);
            Check(() => Ct.Check(cid2) == true);
            Check(() => Ct.Check(cid3) == true);
            var view1 = Ct.Metadata(cid1);
            var view2 = Ct.Metadata(cid2);
            var view3 = Ct.Metadata(cid3);

            Check(() => view1.PinIds.Count == 1);
            Check(() => view1.PinIds.Single() == pinId.Id);
            Check(() => view2.PinIds.Count == 1);
            Check(() => view2.PinIds.Single() == pinId.Id);
            Check(() => view3.PinIds.Count == 1);
            Check(() => view3.PinIds.Single() == pinId.Id);

            Sleep(TimeSpan.FromSeconds(Options.StoreDurationDefaultNominalSeconds));
            SleepCleanupInterval();

            Check(() => Ct.Check(pinId) == false);
            Check(() => Ct.Check(cid1) == false);
            Check(() => Ct.Check(cid2) == false);
            Check(() => Ct.Check(cid3) == false);
        }
    }
}
