using ContentTowerOpenAPIClient;

namespace ContentTower.IntegrationTests.Tests
{
    public class DeleteForcePermanentFilesTests : BaseTest
    {
        public override void Run()
        {
            var (_, _, cid, pinId) = UploadRandom(StoreType.Permanent);

            Check(() => Ct.Check(cid) == true);

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

            Ct.Delete(pinId, force: true);

            Check(() => Ct.Check(cid) == false);
        }
    }
}
