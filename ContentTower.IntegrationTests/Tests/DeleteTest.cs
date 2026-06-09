using ContentTowerOpenAPIClient;

namespace ContentTower.IntegrationTests.Tests
{
    public class DeleteTest : BaseTest
    {
        public override void Run()
        {
            var (_, _, defaultCid) = UploadRandom(StoreRequestType.Default);
            var (_, _, tempCid) = UploadRandom(StoreRequestType.TemporaryFile);

            Check(() => Ct.Check(defaultCid) == true);
            Check(() => Ct.Check(tempCid) == true);

            Ct.Delete(defaultCid);
            Ct.Delete(tempCid);

            Check(() => Ct.Check(defaultCid) == false);
            Check(() => Ct.Check(tempCid) == false);
        }
    }
}
