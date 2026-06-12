using ContentTower.System;

namespace ContentTower.Services
{
    public interface ISaveService
    {
        Task<Cid> Save(SaveRequest request);
    }

    public class SaveRequest
    {
        public SaveRequest(string name, string contentType, byte[] data)
        {
            Name = name;
            ContentType = contentType;
            Data = data;
        }

        public string Name { get; }
        public string ContentType { get; }
        public byte[] Data { get; }
    }

    public class SaveService : ISaveService
    {
        private readonly ILogger<SaveService> logger;
        private readonly IFileSystem fs;
        private readonly IHashService hashService;
        private readonly IPresenceService presenceService;
        private readonly IQuotaService quotaService;

        public SaveService(ILogger<SaveService> logger, IFileSystem fs, IHashService hashService, IPresenceService presenceService, IQuotaService quotaService)
        {
            this.logger = logger;
            this.fs = fs;
            this.hashService = hashService;
            this.presenceService = presenceService;
            this.quotaService = quotaService;
        }

        public async Task<Cid> Save(SaveRequest request)
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
                PinIds = Array.Empty<PinId>(),
                Name = request.Name,
                ContentType = request.ContentType,
                Length = request.Data.Length,
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
