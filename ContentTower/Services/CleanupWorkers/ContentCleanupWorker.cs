namespace ContentTower.Services.CleanupWorkers
{
    public interface IContentCleanupWorker
    {
        void Step(CancellationToken ct);
    }

    public class ContentCleanupWorker : IContentCleanupWorker
    {
        private readonly ILogger<ContentCleanupWorker> logger;
        private readonly IObjectStoreService objectStoreService;
        private readonly IDeleteService deleteService;
        private readonly List<Cid> markedForDelete = new List<Cid>();

        public ContentCleanupWorker(ILogger<ContentCleanupWorker> logger, IObjectStoreService objectStoreService, IDeleteService deleteService)
        {
            this.logger = logger;
            this.objectStoreService = objectStoreService;
            this.deleteService = deleteService;
        }

        public void Step(CancellationToken ct)
        {
            var files = GetAllFiles();
            logger.LogTrace("Checking {0} cids...", files.Count);
            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();
                DeleteIfMarked(file);
            }
        }

        private void DeleteIfMarked(FileMetadata file)
        {

            var span = timespanSelector.Get(pin.StoreType);
            var startUtc = GetPinStartUtc(pin);
            var expiryUtc = startUtc + span;

            if (timeService.UtcNow() > expiryUtc)
            {
                logger.LogTrace("Deleting expired pin {0}...", pin.PinId);
                pinService.Delete(pin.PinId);
            }
        }

        private List<FileMetadata> GetAllFiles()
        {
            var result = new List<FileMetadata>();
            objectStoreService.IterateObjects<FileMetadata>(result.Add);
            return result;
        }
    }
}
