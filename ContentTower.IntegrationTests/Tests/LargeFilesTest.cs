namespace ContentTower.IntegrationTests.Tests
{
    public class LargeFilesTest : BaseTest
    {
        public override void Run()
        {
            Run(1024); // 1kb
            Run(1024 * 1024); // 1mb
            Run(1024 * 1024 * 100); // 100mb
            Run(1024 * 1024 * 300); // 300mb
        }

        private void Run(int length)
        {
            var uploaded = DataHelper.GetRandomData(length);
            var (cid, pinId) = Ct.UploadNewPin("name", "type", uploaded);

            Check(() => Ct.Check(cid));

            var downloaded = Ct.Download(cid);

            Check(() => IsEqual(uploaded, downloaded));
            Log($"LargeFile Check passed for {length} bytes");

            Ct.Delete(pinId);
        }
    }
}
