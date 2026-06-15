using ContentTowerOpenAPIClient;

namespace ContentTower.IntegrationTests.Tests
{
    public class DefaultCleanupTest : BaseTest
    {
        public override void Run()
        {
            var (name, type, cid, pinId) = UploadRandom(StoreType.Default);

            var span = TimeSpan.FromSeconds(Options.StoreDurationDefaultNominalSeconds);
            var half = span / 2.0;

            Sleep(half);
            
            // Touching a default store does not affect storage time.
            Check(() => Ct.Check(cid) == true);
            Check(() => Ct.Download(cid).Length > 0);

            Sleep(half);
            SleepCleanupInterval();

            Check(() => Ct.Check(cid) == false);
        }
    }
}
