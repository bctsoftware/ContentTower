using ContentTower.System;

namespace ContentTower.Services
{
    public interface IPinService
    {
        Task Attach(PinId[] pinIds, Cid cid);
        Task<PinId[]> Create(StoreType[] types, Cid cid);
        bool Exists(PinId pinId);
        Task RegisterActivity(Cid cid);
    }

    public class PinService : IPinService
    {
        public static readonly string PinIdPrefix = "p";
        private readonly ILogger<PinService> logger;
        private readonly IFileSystem fs;
        private readonly ITime timeService;

        public PinService(ILogger<PinService> logger, IFileSystem fs, ITime timeService)
        {
            this.logger = logger;
            this.fs = fs;
            this.timeService = timeService;
        }

        public async Task Attach(PinId[] pinIds, Cid cid)
        {
            await AddPinsToFile(pinIds, cid);
            foreach (var pinId in pinIds)
            {
                await AddCidToPin(pinId, cid);
            }
            logger.LogInformation("Attached pins {0} to cid {1}", pinIds, cid);
        }

        public async Task<PinId[]> Create(StoreType[] types, Cid cid)
        {
            var result = new List<PinId>();
            foreach (var type in types)
            {
                result.Add(await CreateNewPin(type, cid));
            }
            return result.ToArray();
        }

        public bool Exists(PinId pinId)
        {
            return fs.Exists(pinId);
        }

        public async Task RegisterActivity(Cid cid)
        {
            var file = await fs.ReadObject<FileMetadata>(cid);
            foreach (var pin in file.PinIds)
            {
                await RegisterActivity(pin);
            }
        }

        private async Task<PinId> CreateNewPin(StoreType type, Cid cid)
        {
            var pin = new PinData
            {
                PinId = CreateNewPinId(),
                Cids = new List<Cid> { cid },
                CreateUtc = timeService.UtcNow(),
                LastActivityUtc = timeService.UtcNow(),
                StoreType = type
            };
            await fs.WriteObject(pin.PinId, pin);
            return pin.PinId;
        }

        private PinId CreateNewPinId()
        {
            return new PinId(PinIdPrefix + Guid.NewGuid().ToString());
        }

        private async Task AddPinsToFile(PinId[] pinIds, Cid cid)
        {
            try
            {
                var file = await fs.ReadObject<FileMetadata>(cid);
                file.PinIds.AddRange(pinIds);
                await fs.WriteObject(cid, file);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to add pins {0} to cid {1}.", pinIds, cid);
                throw;
            }
        }

        private async Task AddCidToPin(PinId pinId, Cid cid)
        {
            try
            {
                var pin = await fs.ReadObject<PinData>(pinId);
                pin.Cids.Add(cid);
                await fs.WriteObject(pinId, pin);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to add cid {0} to pin {1}.", cid, pinId);
                throw;
            }
        }

        private async Task RegisterActivity(PinId pinId)
        {
            try
            {
                var pin = await fs.ReadObject<PinData>(pinId);
                pin.LastActivityUtc = timeService.UtcNow();
                await fs.WriteObject(pinId, pin);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to update activity for pin {0}.", pinId);
                throw;
            }
        }
    }
}
