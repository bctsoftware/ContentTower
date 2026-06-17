namespace ContentTower.IntegrationTests.Tests
{
    public class UploadRequiresPin : BaseTest
    {
        public override void Run()
        {
            Log("When uploading data, it is required to provide at least 1 pinId, OR");
            Log("provide info to create at least 1 pin. If neither are provided ");
            Log("the upload is rejected.");

            var uploadSuccessful = true;
            try
            {
                Ct.On(api => api.UploadAsync(new ContentTowerOpenAPIClient.UploadRequest()
                {
                    Name = "not-allowed-upload",
                    ContentType = "justData",
                    Data = DataHelper.GetRandomData(1024),
                    AttachExistingPinIds = [],
                    CreateNewPins = []
                }));

                uploadSuccessful = true;
            }
            catch
            {
                uploadSuccessful = false;
            }

            Check(() => uploadSuccessful == false);
        }
    }
}
