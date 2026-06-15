using ContentTower.System;

namespace ContentTower.Services.CleanupWorkers
{
    public interface IPinCleanupWorker
    {
        void Step(CancellationToken ct);
    }

    public class PinCleanupWorker : IPinCleanupWorker
    {
        private readonly ILogger<PinCleanupWorker> logger;
        private readonly IObjectStoreService objectStoreService;
        private readonly IPinService pinService;
        private readonly ITime timeService;
        private readonly ITimespanSelector timespanSelector;

        public PinCleanupWorker(ILogger<PinCleanupWorker> logger, IObjectStoreService objectStoreService, IPinService pinService, ITime timeService, ITimespanSelector timespanSelector)
        {
            this.logger = logger;
            this.objectStoreService = objectStoreService;
            this.pinService = pinService;
            this.timeService = timeService;
            this.timespanSelector = timespanSelector;
        }

        public void Step(CancellationToken ct)
        {
            var pins = GetAllPins();
            logger.LogTrace("Checking {0} pins...", pins.Count);
            foreach (var pin in pins)
            {
                ct.ThrowIfCancellationRequested();
                DeleteIfExpired(pin);
            }
        }

        private void DeleteIfExpired(PinData pin)
        {
            if (pin.StoreType == StoreType.Permanent) return;

            var span = timespanSelector.Get(pin.StoreType);
            var startUtc = GetPinStartUtc(pin);
            var expiryUtc = startUtc + span;

            if (timeService.UtcNow() > expiryUtc)
            {
                logger.LogTrace("Deleting expired pin {0}...", pin.PinId);
                pinService.Delete(pin.PinId);
            }
        }

        private List<PinData> GetAllPins()
        {
            var result = new List<PinData>();
            objectStoreService.IterateObjects<PinData>(PinService.PinIdPrefix, result.Add);
            return result;
        }

        private DateTime GetPinStartUtc(PinData item)
        {
            // Temporary storeTypes are measured from last-activity.
            // Default storeTypes are measure from upload.
            if (item.StoreType == StoreType.Temporary) return item.LastActivityUtc;
            if (item.StoreType == StoreType.Default) return item.CreateUtc;
            throw new InvalidOperationException("Attempt to get fileUTC for unknown store type: " + item.StoreType);
        }
    }
}
