using System.Security.Cryptography;

namespace ContentTower.Services
{
    public interface IHashService
    {
        Cid GetHash(byte[] data);
    }

    public class HashService : IHashService
    {
        public static string CidPrefix = "ct";

        public Cid GetHash(byte[] data)
        {
            var algorithm = SHA256.Create();
            var hash = algorithm.ComputeHash(data);
            var encoded = SimpleBase.Base58.Bitcoin.Encode(hash);
            return new Cid(CidPrefix + encoded);
        }
    }
}
