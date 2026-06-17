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
        private readonly List<Cid> markedForDelete = new();

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
                if (DeleteIfMarkedAndUnpinned(file)) continue;
                MarkIfUnpinned(file);
            }
        }

        private bool DeleteIfMarkedAndUnpinned(FileMetadata file)
        {
            if (markedForDelete.Contains(file.Cid))
            {
                markedForDelete.Remove(file.Cid);

                if (file.PinIds.Count == 0)
                {
                    deleteService.DeleteFile(file);
                    return true;
                }
                else
                {
                    logger.LogTrace("Delete of {0} was cancelled.", file.Cid);
                }
            }
            return false;
        }

        private void MarkIfUnpinned(FileMetadata file)
        {
            if (file.PinIds.Count == 0)
            {
                markedForDelete.Add(file.Cid);
                logger.LogTrace("{0} is marked for delete.", file.Cid);
            }
        }

        private List<FileMetadata> GetAllFiles()
        {
            var result = new List<FileMetadata>();
            objectStoreService.IterateObjects<FileMetadata>(HashService.CidPrefix, result.Add);
            return result;
        }
    }
}
