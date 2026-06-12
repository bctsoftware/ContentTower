using ContentTower.System;

namespace ContentTower.Services
{
    public interface IPinService
    {
        void Attach(PinId[] pinIds, Cid cid);
        PinId[] Create(StoreType[] types, Cid cid);
        bool Exists(PinId pinId);
        void RegisterActivity(Cid cid);
    }

    public class PinService : IPinService
    {
        public static readonly string PinIdPrefix = "p";
        private readonly ILogger<PinService> logger;
        private readonly IObjectStoreService objectStoreService;
        private readonly ITime timeService;

        public PinService(ILogger<PinService> logger, IObjectStoreService objectStoreService, ITime timeService)
        {
            this.logger = logger;
            this.objectStoreService = objectStoreService;
            this.timeService = timeService;
        }

        public void Attach(PinId[] pinIds, Cid cid)
        {
            AddPinsToFile(pinIds, cid);
            foreach (var pinId in pinIds)
            {
                AddCidToPin(pinId, cid);
            }
            logger.LogInformation("Attached pins {0} to cid {1}", pinIds, cid);
        }

        public PinId[] Create(StoreType[] types, Cid cid)
        {
            var result = new List<PinId>();
            foreach (var type in types)
            {
                result.Add(CreateNewPin(type, cid));
            }
            return result.ToArray();
        }

        public bool Exists(PinId pinId)
        {
            return objectStoreService.Exists(pinId);
        }

        public void RegisterActivity(Cid cid)
        {
            var file = objectStoreService.ReadObject<FileMetadata>(cid);
            foreach (var pin in file.PinIds)
            {
                RegisterActivity(pin);
            }
        }

        private PinId CreateNewPin(StoreType type, Cid cid)
        {
            var pinId = CreateNewPinId();
            objectStoreService.CreateOrUpdateObject<PinData>(pinId, pin =>
            {
                pin.PinId = pinId;
                pin.Cids = new List<Cid> { cid };
                pin.CreateUtc = timeService.UtcNow();
                pin.LastActivityUtc = timeService.UtcNow();
                pin.StoreType = type;
            });
            return pinId;
        }

        private static PinId CreateNewPinId()
        {
            return new PinId(PinIdPrefix + Guid.NewGuid().ToString());
        }

        private void AddPinsToFile(PinId[] pinIds, Cid cid)
        {
            try
            {
                objectStoreService.CreateOrUpdateObject<FileMetadata>(cid, file =>
                {
                    file.PinIds.AddRange(pinIds);
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to add pins {0} to cid {1}.", pinIds, cid);
                throw;
            }
        }

        private void AddCidToPin(PinId pinId, Cid cid)
        {
            try
            {
                objectStoreService.CreateOrUpdateObject<PinData>(pinId, pin =>
                {
                    pin.Cids.Add(cid);
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to add cid {0} to pin {1}.", cid, pinId);
                throw;
            }
        }

        private void RegisterActivity(PinId pinId)
        {
            try
            {
                objectStoreService.CreateOrUpdateObject<PinData>(pinId, pin =>
                {
                    pin.LastActivityUtc = timeService.UtcNow();
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to update activity for pin {0}.", pinId);
                throw;
            }
        }
    }
}
