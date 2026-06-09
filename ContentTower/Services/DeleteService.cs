using ContentTower.System;

namespace ContentTower.Services
{
    public interface IDeleteService
    {
        Task DeleteFile(FileMetadata item);
    }

    public class DeleteService : IDeleteService
    {
        private readonly ILogger<DeleteService> logger;
        private readonly IFileSystem fs;
        private readonly IPresenceService presenceService;
        private readonly IQuotaService quotaService;

        public DeleteService(ILogger<DeleteService> logger, IFileSystem fs, IPresenceService presenceService, IQuotaService quotaService)
        {
            this.logger = logger;
            this.fs = fs;
            this.presenceService = presenceService;
            this.quotaService = quotaService;
        }

        public async Task DeleteFile(FileMetadata item)
        {
            logger.LogTrace("Cleaning up {0}...", item.Cid);
            try
            {
                await fs.DeleteData(item.Cid);
                await fs.DeleteObject(item.Cid);
                presenceService.ClearPresence(item.Cid);
                quotaService.RemoveUsedBytes(item.Length);
                logger.LogTrace("Successfully cleaned up {0}.", item.Cid);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fatal: Failed to delete file.");
                throw;
            }
        }
    }
}
