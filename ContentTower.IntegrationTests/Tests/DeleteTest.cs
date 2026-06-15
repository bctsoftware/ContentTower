using ContentTowerOpenAPIClient;

namespace ContentTower.IntegrationTests.Tests
{
    public class DeleteTest : BaseTest
    {
        public override void Run()
        {
            var (_, _, defaultCid, defautPinId) = UploadRandom(StoreType.Default);
            var (_, _, tempCid, tempPinId) = UploadRandom(StoreType.Temporary);

            Check(() => Ct.Check(defaultCid) == true);
            Check(() => Ct.Check(tempCid) == true);

            Ct.Delete(defaultCid);
            Ct.Delete(tempCid);

            Check(() => Ct.Check(defaultCid) == false);
            Check(() => Ct.Check(tempCid) == false);
        }
    }
}
