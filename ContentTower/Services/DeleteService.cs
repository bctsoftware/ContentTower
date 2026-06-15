namespace ContentTower.Services
{
    public interface IDeleteService
    {
        void DeleteFile(FileMetadata file);
    }

    public class DeleteService : IDeleteService
    {
        private readonly ILogger<DeleteService> logger;
        private readonly IObjectStoreService objectStoreService;
        private readonly IDataStoreService dataStoreService;
        private readonly IPresenceService presenceService;
        private readonly IQuotaService quotaService;

        public DeleteService(ILogger<DeleteService> logger, IObjectStoreService objectStoreService, IDataStoreService dataStoreService, IPresenceService presenceService, IQuotaService quotaService)
        {
            this.logger = logger;
            this.objectStoreService = objectStoreService;
            this.dataStoreService = dataStoreService;
            this.presenceService = presenceService;
            this.quotaService = quotaService;
        }

        public void DeleteFile(FileMetadata file)
        {
            logger.LogTrace("Deleting {0}...", file.Cid);
            try
            {
                dataStoreService.DeleteData(file.Cid);
                objectStoreService.DeleteObject(file.Cid);
                presenceService.ClearPresence(file.Cid);
                quotaService.RemoveUsedBytes(file.Length);
                logger.LogInformation("Successfully deleted {0}.", file.Cid);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fatal: Failed to delete file.");
                throw;
            }
        }
    }
}
