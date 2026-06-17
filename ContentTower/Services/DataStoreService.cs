using ContentTower.System;
using Microsoft.Extensions.Options;

namespace ContentTower.Services
{
    public interface IDataStoreService
    {
        bool Exists(IId id);
        void WriteData(IId id, byte[] data);
        Stream ReadData(IId id);
        void DeleteData(IId id);
    }

    public class DataStoreService : IDataStoreService
    {
        private readonly IFileSystem fs;
        private readonly StorageOptions options;

        public DataStoreService(IFileSystem fs, IOptions<StorageOptions> options)
        {
            this.fs = fs;
            this.options = options.Value;
        }

        public bool Exists(IId id)
        {
            return fs.Exists(GetDataFilepath(id));
        }

        public void DeleteData(IId id)
        {
            fs.DeleteFile(GetDataFilepath(id));
        }

        public Stream ReadData(IId id)
        {
            return fs.OpenRead(GetDataFilepath(id));
        }

        public void WriteData(IId id, byte[] data)
        {
            fs.WriteAllBytes(GetDataFilepath(id), data);
        }

        private string GetDataFilepath(IId id)
        {
            if (string.IsNullOrEmpty(id.Id)) throw new Exception("Invalid CID");
            return Path.Combine(options.DataPath, id.Id + ".data");
        }
    }
}
