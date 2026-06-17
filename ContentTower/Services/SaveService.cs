namespace ContentTower.Services
{
    public interface ISaveService
    {
        Cid Save(SaveRequest request);
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
        private readonly IObjectStoreService objectStoreService;
        private readonly IDataStoreService dataStoreService;
        private readonly IHashService hashService;
        private readonly IPresenceService presenceService;
        private readonly IQuotaService quotaService;

        public SaveService(ILogger<SaveService> logger, IObjectStoreService objectStoreService, IDataStoreService dataStoreService, IHashService hashService, IPresenceService presenceService, IQuotaService quotaService)
        {
            this.logger = logger;
            this.objectStoreService = objectStoreService;
            this.dataStoreService = dataStoreService;
            this.hashService = hashService;
            this.presenceService = presenceService;
            this.quotaService = quotaService;
        }

        public Cid Save(SaveRequest request)
        {
            logger.LogTrace($"Handling request...");
            var cid = hashService.GetHash(request.Data);
            if (presenceService.IsPresent(cid))
            {
                logger.LogInformation("Content {0} already present.", cid);
                return cid;
            }

            objectStoreService.CreateOrUpdateObject<FileMetadata>(cid, file =>
            {
                file.Cid = cid;
                file.PinIds = new List<PinId>();
                file.Name = request.Name;
                file.ContentType = request.ContentType;
                file.Length = request.Data.Length;
            });

            try
            {
                dataStoreService.WriteData(cid, request.Data);
                logger.LogInformation("Saved new content for {0}.", cid);
                presenceService.SetPresence(cid);
                quotaService.AddUsedBytes(request.Data.Length);
                return cid;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to save content for {0}.", cid);
                objectStoreService.DeleteObject(cid);
                throw;
            }
        }
    }
}
