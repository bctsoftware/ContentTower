using ContentTower.Controllers;
using ContentTower.System;
using Microsoft.Extensions.Options;

namespace ContentTower.Services
{
    public interface ICleanupWorker
    {
        Task ProcessItem(FileMetadata item, CancellationToken ct);
    }

    public class CleanupWorker : ICleanupWorker
    {
        private readonly ILogger<CleanupService> logger;
        private readonly StorageOptions options;
        private readonly IQuotaService quotaService;
        private readonly ITime timeService;
        private readonly IPresenceService presenceService;
        private readonly IFileSystem fs;
        private readonly TimeSpan stepSleep = TimeSpan.FromSeconds(10);
        private readonly Dictionary<QuotaState, Dictionary<StoreRequestType, Func<TimeSpan>>> timespanSelectors = new();

        public CleanupWorker(ILogger<CleanupService> logger, IOptions<StorageOptions> options, IQuotaService quotaService, ITime timeService, IPresenceService presenceService, IFileSystem fs)
        {
            this.logger = logger;
            this.options = options.Value;
            this.quotaService = quotaService;
            this.timeService = timeService;
            this.presenceService = presenceService;
            this.fs = fs;
            CreateTimespanSelectors();
        }

        public async Task ProcessItem(FileMetadata item, CancellationToken ct)
        {
            await ProcessItemInternal(item, ct);
            await timeService.Sleep(stepSleep, ct);
        }

        private async Task ProcessItemInternal(FileMetadata item, CancellationToken ct)
        {
            if (item.StoreType == StoreRequestType.PermanentFile) return;

            var state = quotaService.GetQuotaStatus().State;
            var span = timespanSelectors[state][item.StoreType]();
            var fileUtc = GetFileUtc(item);

            if (timeService.UtcNow() > fileUtc + span)
            {
                await DeleteFile(item);
            }
        }

        private DateTime GetFileUtc(FileMetadata item)
        {
            // Temporary files are measured from last-activity.
            // Default files are measure from upload.
            if (item.StoreType == StoreRequestType.TemporaryFile) return item.LastActivityUtc;
            if (item.StoreType == StoreRequestType.Default) return item.UploadUtc;
            throw new InvalidOperationException("Attempt to get fileUTC for unknown store type: " + item.StoreType);
        }

        private async Task DeleteFile(FileMetadata item)
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

        private void CreateTimespanSelectors()
        {
            var nominalSet = new Dictionary<StoreRequestType, Func<TimeSpan>>();
            var pressureSet = new Dictionary<StoreRequestType, Func<TimeSpan>>();

            nominalSet.Add(StoreRequestType.Default, () => options.StoreDurationDefaultNominal);
            nominalSet.Add(StoreRequestType.TemporaryFile, () => options.StoreDurationTemporaryNominal);
            nominalSet.Add(StoreRequestType.PermanentFile, () => throw new InvalidOperationException("Attempt to get timespan for permanent file in nominal state."));

            pressureSet.Add(StoreRequestType.Default, () => options.StoreDurationDefaultPressure);
            pressureSet.Add(StoreRequestType.TemporaryFile, () => options.StoreDurationTemporaryPressure);
            pressureSet.Add(StoreRequestType.PermanentFile, () => throw new InvalidOperationException("Attempt to get timespan for permanent file in pressure state."));

            timespanSelectors.Add(QuotaState.Nominal, nominalSet);
            timespanSelectors.Add(QuotaState.Pressure, pressureSet);
            timespanSelectors.Add(QuotaState.Full, pressureSet);
        }
    }
}
