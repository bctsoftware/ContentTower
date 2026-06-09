using ContentTowerOpenAPIClient;

namespace ContentTower.IntegrationTests.Tests
{
    public class DeleteForcePermanentFilesTests : BaseTest
    {
        public override void Run()
        {
            var (_, _, cid) = UploadRandom(StoreRequestType.PermanentFile);

            Check(() => Ct.Check(cid) == true);

            var normalDeleteFailed = false;
            try
            {
                Ct.Delete(cid);
            }
            catch
            {
                normalDeleteFailed = true;
            }

            Check(() => normalDeleteFailed == true);
            Check(() => Ct.Check(cid) == true);

            Ct.Delete(cid, force: true);

            Check(() => Ct.Check(cid) == false);
        }
    }
}
