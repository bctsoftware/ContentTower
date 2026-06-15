using ContentTowerOpenAPIClient;

namespace ContentTower.IntegrationTests.Tests
{
    public class TempfileCleanupTest : BaseTest
    {
        public override void Run()
        {
            var (name, type, cid, pinId) = UploadRandom(StoreType.Temporary);

            var span = TimeSpan.FromSeconds(Options.StoreDurationTemporaryNominalSeconds);

            Sleep(span);
            SleepCleanupInterval();

            // Untouching temp store cleans up quickly.
            Check(() => Ct.Check(cid) == false);
        }
    }
}
