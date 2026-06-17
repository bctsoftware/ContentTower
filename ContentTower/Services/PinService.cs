using ContentTower.System;

namespace ContentTower.Services
{
    public interface IPinService
    {
        void Attach(PinId[] pinIds, Cid cid);
        void Attach(PinId pinId, Cid[] cids);
        void Detach(PinId pinId, Cid[] cids);
        PinId Create(StoreType type, Cid[] cids);
        PinId[] Create(StoreType[] types, Cid cid);
        bool Exists(PinId pinId);
        void RegisterActivity(Cid cid);
        PinData Get(PinId pinId);
        void Delete(PinId pinId);
    }

    public class PinService : IPinService
    {
        public static readonly string PinIdPrefix = "p";
        private readonly ILogger<PinService> logger;
        private readonly IObjectStoreService objectStoreService;
        private readonly ITime timeService;
        private readonly IPresenceService presenceService;

        public PinService(ILogger<PinService> logger, IObjectStoreService objectStoreService, ITime timeService, IPresenceService presenceService)
        {
            this.logger = logger;
            this.objectStoreService = objectStoreService;
            this.timeService = timeService;
            this.presenceService = presenceService;
        }

        public void Attach(PinId[] pinIds, Cid cid)
        {
            if (pinIds.Length == 0) return;
            AddPinsToFile(pinIds, cid);
            foreach (var pinId in pinIds)
            {
                AddCidToPin(pinId, cid);
            }
            logger.LogInformation("Attached {0} pins to cid {1}", pinIds.Length, cid);
        }

        public void Attach(PinId pinId, Cid[] cids)
        {
            if (cids.Length == 0) return;
            AddCidsToPin(pinId, cids);
            foreach (var cid in cids)
            {
                AddPinToCid(pinId, cid);
            }
            logger.LogInformation("Attached pin {0} to {1} cids", pinId, cids.Length);
        }

        public void Detach(PinId pinId, Cid[] cids)
        {
            if (cids.Length == 0) return;
            RemoveCidsFromPin(pinId, cids);
            foreach (var cid in cids)
            {
                RemovePinFromCid(pinId, cid);
            }
            logger.LogInformation("Detached pin {0} from cids {1}", pinId, cids);
        }

        public PinId Create(StoreType type, Cid[] cids)
        {
            return CreateNewPin(type, cids);
        }

        public PinId[] Create(StoreType[] types, Cid cid)
        {
            var result = new List<PinId>();
            foreach (var type in types)
            {
                result.Add(CreateNewPin(type, [cid]));
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

        public PinData Get(PinId pinId)
        {
            return objectStoreService.ReadObject<PinData>(pinId);
        }

        public void Delete(PinId pinId)
        {
            var pin = Get(pinId);
            foreach (var cid in pin.Cids)
            {
                RemovePinFromCid(pinId, cid);
            }
            presenceService.ClearPresence(pinId);
            objectStoreService.DeleteObject(pinId);
            logger.LogInformation("Deleted pin {0} from {1} contents.", pinId, pin.Cids.Count);
        }

        private PinId CreateNewPin(StoreType type, Cid[] cids)
        {
            var pinId = CreateNewPinId();
            objectStoreService.CreateOrUpdateObject<PinData>(pinId, pin =>
            {
                pin.PinId = pinId;
                pin.Cids = cids.ToList();
                pin.CreateUtc = timeService.UtcNow();
                pin.LastActivityUtc = timeService.UtcNow();
                pin.StoreType = type;
            });
            presenceService.SetPresence(pinId);
            foreach (var cid in cids)
            {
                AddPinToCid(pinId, cid);
            }
            logger.LogInformation("Created new pin {0} of type {1} for {2} contents.", pinId, type, cids.Length);
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

        private void AddCidsToPin(PinId pinId, Cid[] cids)
        {
            try
            {
                objectStoreService.CreateOrUpdateObject<PinData>(pinId, pin =>
                {
                    pin.Cids.AddRange(cids);
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to add cids {0} to pin {1}.", cids, pinId);
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

        private void AddPinToCid(PinId pinId, Cid cid)
        {
            try
            {
                objectStoreService.CreateOrUpdateObject<FileMetadata>(cid, file =>
                {
                    file.PinIds.Add(pinId);
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to add pin {0} to cid {1}.", pinId, cid);
                throw;
            }
        }

        private void RemovePinFromCid(PinId pinId, Cid cid)
        {
            if (!objectStoreService.Exists(cid)) return;
            try
            {
                objectStoreService.CreateOrUpdateObject<FileMetadata>(cid, file =>
                {
                    file.PinIds.Remove(pinId);
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to remove pin {0} from cid {1}.", pinId, cid);
                throw;
            }
        }

        private void RemoveCidsFromPin(PinId pinId, Cid[] cids)
        {
            try
            {
                objectStoreService.CreateOrUpdateObject<PinData>(pinId, pin =>
                {
                    foreach (var c in cids) pin.Cids.Remove(c);
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to remove cids {0} from pin {1}.", cids, pinId);
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
