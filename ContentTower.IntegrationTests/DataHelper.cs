namespace ContentTower.IntegrationTests
{
    public class DataHelper
    {
        private static readonly Random r = new Random();

        public byte[] GetRandomData(int length)
        {
            var result = new byte[length];
            r.NextBytes(result);
            return result;
        }
    }
}
