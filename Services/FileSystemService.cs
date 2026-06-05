namespace ContentTower.Services
{
    public interface IFileSystemService
    {
        Task WriteObject<T>(Cid cid, T obj);
        Task WriteData(Cid cid, byte[] data);
        Task<T> ReadObject<T>(Cid cid);
        Task<Stream> ReadData(Cid cid);
        Task DeleteObject(Cid cid);
        Task DeleteData(Cid cid);
        Task IterateObjects<T>(Action<T> onObject);
    }

    public class FileSystemService : IFileSystemService
    {
        public Task DeleteData(Cid cid)
        {
            throw new NotImplementedException();
        }

        public Task DeleteObject(Cid cid)
        {
            throw new NotImplementedException();
        }

        public Task<Stream> ReadData(Cid cid)
        {
            throw new NotImplementedException();
        }

        public Task<T> ReadObject<T>(Cid cid)
        {
            throw new NotImplementedException();
        }

        public Task WriteData(string name, byte[] data)
        {
            throw new NotImplementedException();
        }

        public Task WriteData(Cid cid, byte[] data)
        {
            throw new NotImplementedException();
        }

        public Task WriteObject<T>(string name, T obj)
        {
            throw new NotImplementedException();
        }

        public Task WriteObject<T>(Cid cid, T obj)
        {
            throw new NotImplementedException();
        }
    }
}
