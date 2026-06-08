namespace ContentTower.IntegrationTests
{
    public class DataHelper
    {
        private readonly string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        private static readonly Random r = new Random();

        public int GetRandomNumber(int inclMin, int exclMax)
        {
            return r.Next(inclMin, exclMax);
        }

        public byte[] GetRandomData(int length)
        {
            var result = new byte[length];
            r.NextBytes(result);
            return result;
        }

        public string GetRandomString()
        {
            var length = r.Next(7, 21);
            var result = string.Empty;
            while (result.Length < length)
            {
                result += chars[r.Next(0, chars.Length)];
            }
            return result;
        }
    }
}
