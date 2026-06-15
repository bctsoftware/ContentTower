using ContentTowerOpenAPIClient;

namespace ContentTower.IntegrationTests.Tests
{
    public class DeleteForcePermanentFilesTests : BaseTest
    {
        public override void Run()
        {
            var (_, _, cid, pinId) = UploadRandom(StoreType.Permanent);

            Check(() => Ct.Check(cid) == true);
            Check(() => Ct.Check(pinId) == true);

            var normalDeleteFailed = false;
            try
            {
                Ct.Delete(pinId);
            }
            catch
            {
                normalDeleteFailed = true;
            }

            Check(() => normalDeleteFailed == true);
            Check(() => Ct.Check(cid) == true);
            Check(() => Ct.Check(pinId) == true);

            Ct.Delete(pinId, force: true);

            Check(() => Ct.Check(cid) == true);
            Check(() => Ct.Check(pinId) == false);

            SleepCleanupInterval();

            Check(() => Ct.Check(cid) == false);
            Check(() => Ct.Check(pinId) == false);
        }
    }
}
