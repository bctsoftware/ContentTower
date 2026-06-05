using ContentTower.Controllers;

namespace ContentTower.Services
{
    public interface ISaveService
    {
        Task<Cid> Handle(UploadRequest request);
    }

    public class SaveService : ISaveService
    {
        private readonly ILogger<SaveService> logger;
        private readonly IFileSystemService fs;
        private readonly IHashService hashService;
        private readonly IPresenceService presenceService;
        private readonly IQuotaService quotaService;
        private readonly ITimeService timeService;

        public SaveService(ILogger<SaveService> logger, IFileSystemService fs, IHashService hashService, IPresenceService presenceService, IQuotaService quotaService, ITimeService timeService)
        {
            this.logger = logger;
            this.fs = fs;
            this.hashService = hashService;
            this.presenceService = presenceService;
            this.quotaService = quotaService;
            this.timeService = timeService;
        }

        public async Task<Cid> Handle(UploadRequest request)
        {
            logger.LogTrace($"Handling request...");
            var cid = hashService.GetHash(request.Data);
            if (presenceService.IsPresent(cid))
            {
                logger.LogInformation("Content {0} already present.", cid);
                return cid;
            }

            var metadata = new FileMetadata
            {
                Cid = cid,
                Name = request.Name,
                ContentType = request.ContentType,
                Length = request.Data.Length,
                StoreType = request.StoreType,
                UploadUtc = timeService.UtcNow(),
                LastActivityUtc = timeService.UtcNow(),
            };
            await fs.WriteObject(cid, metadata);

            try
            {
                await fs.WriteData(cid, request.Data);
                logger.LogInformation("Saved new content for {0}.", cid);
                presenceService.SetPresence(cid);
                quotaService.AddUsedBytes(request.Data.Length);
                return cid;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to save content for {0}.", cid);
                await fs.DeleteObject(cid);
                throw;
            }
        }
    }
}
