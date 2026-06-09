using ContentTower.Controllers;
using ContentTower.System;
using Microsoft.Extensions.Options;

namespace ContentTower.Services
{
    public interface ICleanupWorker
    {
        Task ProcessItem(FileMetadata item);
    }

    public class CleanupWorker : ICleanupWorker
    {
        private readonly StorageOptions options;
        private readonly IQuotaService quotaService;
        private readonly ITime timeService;
        private readonly IDeleteService deleteService;
        private readonly Dictionary<QuotaState, Dictionary<StoreRequestType, Func<TimeSpan>>> timespanSelectors = new();

        public CleanupWorker(IOptions<StorageOptions> options, IQuotaService quotaService, ITime timeService, IDeleteService deleteService)
        {
            this.options = options.Value;
            this.quotaService = quotaService;
            this.timeService = timeService;
            this.deleteService = deleteService;
            CreateTimespanSelectors();
        }

        public async Task ProcessItem(FileMetadata item)
        {
            await ProcessItemInternal(item);
        }

        private async Task ProcessItemInternal(FileMetadata item)
        {
            if (item.StoreType == StoreRequestType.PermanentFile) return;

            var state = quotaService.GetQuotaStatus().State;
            var span = timespanSelectors[state][item.StoreType]();
            var fileUtc = GetFileUtc(item);

            if (timeService.UtcNow() > fileUtc + span)
            {
                await deleteService.DeleteFile(item);
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
