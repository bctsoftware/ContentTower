using ContentTowerOpenAPIClient;

namespace ContentTower.IntegrationTests.Tests
{
    public class ActivityExtendsTempFileTest : BaseTest
    {
        public override void Run()
        {
            var (name, type, cid, pinId) = UploadRandom(StoreType.Temporary);

            var span = TimeSpan.FromSeconds(Options.StoreDurationTemporaryNominalSeconds);
            var half = span / 2.0;

            // Each touch resets temp storage.
            Sleep(half);
            Check(() => Ct.Download(cid).Length > 0);

            Sleep(half);
            Check(() => Ct.Download(cid).Length > 0);

            Sleep(half);
            Check(() => Ct.Download(cid).Length > 0);

            Check(() => Ct.Check(pinId) == true);
            Check(() => Ct.Check(cid) == true);

            Sleep(span);
            SleepCleanupInterval();

            // Then it cleans up.
            Check(() => Ct.Check(pinId) == false);
            Check(() => Ct.Check(cid) == false);
        }
    }
}
