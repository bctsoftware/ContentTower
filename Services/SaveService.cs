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

        public SaveService(ILogger<SaveService> logger, IFileSystemService fs, IHashService hashService, IPresenceService presenceService)
        {
            this.logger = logger;
            this.fs = fs;
            this.hashService = hashService;
            this.presenceService = presenceService;
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
                Name = request.Name,
                ContentType = request.ContentType,
                Length = request.Data.Length,
                UploadUtc = DateTime.UtcNow,
                LastActivityUtc = DateTime.UtcNow,
            };
            await fs.WriteObject(cid, metadata);

            try
            {
                await fs.WriteData(cid, request.Data);
                logger.LogInformation("Saved new content for {0}.", cid);
                presenceService.SetPresence(cid);
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
