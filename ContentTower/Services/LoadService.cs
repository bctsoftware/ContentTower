namespace ContentTower.Services
{
    public interface ILoadService
    {
        FileMetadata ReadMetadata(Cid cid);
        Stream ReadData(Cid cid);
    }

    public class LoadService : ILoadService
    {
        private readonly IObjectStoreService objectStoreService;
        private readonly IDataStoreService dataStoreService;

        public LoadService(IObjectStoreService objectStoreService, IDataStoreService dataStoreService)
        {
            this.objectStoreService = objectStoreService;
            this.dataStoreService = dataStoreService;
        }

        public FileMetadata ReadMetadata(Cid cid)
        {
            return objectStoreService.ReadObject<FileMetadata>(cid);
        }

        public Stream ReadData(Cid cid)
        {
            return dataStoreService.ReadData(cid);
        }
    }
}
