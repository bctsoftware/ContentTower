using ContentTowerOpenAPIClient;

namespace ContentTower.IntegrationTests.Tests
{
    public class ActivityExtendsTempPinTest : BaseTest
    {
        public override void Run()
        {
            var (name, type, cid, pinId) = UploadRandom(StoreType.Temporary);

            var span = TimeSpan.FromSeconds(Options.StoreDurationTemporaryNominalSeconds);
            var half = span / 2.0;

            Log("Each touch resets temp storage remaining time.");
            Sleep(half);
            Check(() => Ct.Download(cid).Length > 0);

            Sleep(half);
            Check(() => Ct.Download(cid).Length > 0);

            Sleep(half);
            Check(() => Ct.Download(cid).Length > 0);


            Check(() => Ct.Check(pinId) == true);
            Check(() => Ct.Check(cid) == true);

            Log("Now let it expire.");
            Sleep(span);
            SleepCleanupInterval();

            Log("Then it cleans up.");
            Check(() => Ct.Check(pinId) == false);
            Check(() => Ct.Check(cid) == false);
        }
    }
}
