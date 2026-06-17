using ContentTowerOpenAPIClient;

namespace ContentTower.IntegrationTests.Tests
{
    public class TempfileCleanupTest : BaseTest
    {
        public override void Run()
        {
            var (name, type, cid, pinId) = UploadRandom(StoreType.Temporary);
            Log("We have a content with a temporary pin.");

            var span = TimeSpan.FromSeconds(Options.StoreDurationTemporaryNominalSeconds);

            Sleep(span);
            SleepCleanupInterval();

            Log("Untouching temporary pins are cleaned up quickly.");
            Check(() => Ct.Check(pinId) == false);
            Check(() => Ct.Check(cid) == false);
        }
    }
}
