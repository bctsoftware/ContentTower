using ContentTowerOpenAPIClient;

namespace ContentTower.IntegrationTests.Tests
{
    public class DeleteForcePermanentPinsTests : BaseTest
    {
        public override void Run()
        {
            var (_, _, cid, pinId) = UploadRandom(StoreType.Permanent);

            Check(() => Ct.Check(cid) == true);
            Check(() => Ct.Check(pinId) == true);

            var normalDeleteFailed = false;
            Log("Deleting a permanent pin is not allowed.");
            try
            {
                Ct.Delete(pinId);
            }
            catch
            {
                normalDeleteFailed = true;
            }

            Log("It survives the delete attempt.");
            Check(() => normalDeleteFailed == true);
            Check(() => Ct.Check(cid) == true);
            Check(() => Ct.Check(pinId) == true);

            Log("Use force-delete.");
            Ct.Delete(pinId, force: true);

            Log("Now the pin is deleted.");
            Check(() => Ct.Check(cid) == true);
            Check(() => Ct.Check(pinId) == false);

            SleepCleanupInterval();

            Log("Then the content is cleaned up.");
            Check(() => Ct.Check(cid) == false);
            Check(() => Ct.Check(pinId) == false);
        }
    }
}
