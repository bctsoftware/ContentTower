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

            Log("Touching a default store does not affect storage time.");
            Check(() => Ct.Check(cid) == true);
            Check(() => Ct.Check(pinId) == true);
            Check(() => Ct.Download(cid).Length > 0);

            Sleep(half);
            SleepCleanupInterval();

            Log("It is cleaned up after the full default nominal store duration.");
            Check(() => Ct.Check(cid) == false);
            Check(() => Ct.Check(pinId) == false);
        }
    }
}
