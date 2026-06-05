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
            return new Cid(CidPrefix + Convert.ToBase64String(hash));
        }
    }

    public class Cid
    {
        public Cid(string hash)
        {
            Hash = hash;
        }

        public string Hash { get; }

        public override string ToString()
        {
            return $"'{Hash.Substring(0, 5)}..{Hash.Last()}'";
        }
    }
}
