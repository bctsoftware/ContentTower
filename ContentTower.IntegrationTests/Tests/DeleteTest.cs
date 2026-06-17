using ContentTowerOpenAPIClient;

namespace ContentTower.IntegrationTests.Tests
{
    public class DeleteTest : BaseTest
    {
        public override void Run()
        {
            var (_, _, defaultCid, defaultPinId) = UploadRandom(StoreType.Default);
            var (_, _, tempCid, tempPinId) = UploadRandom(StoreType.Temporary);

            Check(() => Ct.Check(defaultCid) == true);
            Check(() => Ct.Check(defaultPinId) == true);
            Check(() => Ct.Check(tempCid) == true);
            Check(() => Ct.Check(tempPinId) == true);

            Ct.Delete(defaultPinId);
            Ct.Delete(tempPinId);

            Log("Both default and temp pins are deleted immediately at request.");
            Check(() => Ct.Check(defaultCid) == true);
            Check(() => Ct.Check(defaultPinId) == false);
            Check(() => Ct.Check(tempCid) == true);
            Check(() => Ct.Check(tempPinId) == false);

            SleepCleanupInterval();

            Log("The content is cleaned up later.");
            Check(() => Ct.Check(defaultCid) == false);
            Check(() => Ct.Check(defaultPinId) == false);
            Check(() => Ct.Check(tempCid) == false);
            Check(() => Ct.Check(tempPinId) == false);
        }
    }
}
