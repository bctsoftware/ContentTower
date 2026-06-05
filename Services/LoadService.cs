namespace ContentTower.Services
{
    public interface ILoadService
    {
        Task<FileMetadata> ReadMetadata(Cid cid);
        Task<Stream> ReadData(Cid cid);
    }

    public class LoadService : ILoadService
    {
        private readonly IFileSystemService fs;

        public LoadService(IFileSystemService fs)
        {
            this.fs = fs;
        }

        public async Task<FileMetadata> ReadMetadata(Cid cid)
        {
            return await fs.ReadObject<FileMetadata>(cid);
        }

        public async Task<Stream> ReadData(Cid cid)
        {
            return await fs.ReadData(cid);
        }
    }
}
