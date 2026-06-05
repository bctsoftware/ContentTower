using ContentTower.Controllers;
using ContentTower.System;
using Microsoft.Extensions.Options;

namespace ContentTower.Services
{
    public interface ICleanupService
    {
        Task Start();
        Task Stop();
    }

    public class CleanupService : ICleanupService
    {
        private readonly TimeSpan stepSleep = TimeSpan.FromSeconds(10);
        private readonly TimeSpan longSleep = TimeSpan.FromMinutes(10);
        private readonly ILogger<CleanupService> logger;
        private readonly StorageOptions options;
        private readonly IFileSystem fs;
        private readonly IPresenceService presenceService;
        private readonly IQuotaService quotaService;
        private readonly ITime timeService;
        private readonly List<FileMetadata> queue = new List<FileMetadata>();
        private readonly Dictionary<QuotaState, Dictionary<StoreRequestType, Func<TimeSpan>>> timespanSelectors = new();
        private CancellationTokenSource cts = new CancellationTokenSource();
        private Task worker = Task.CompletedTask;

        public CleanupService(ILogger<CleanupService> logger, IOptions<StorageOptions> options, IFileSystem fs, IPresenceService presenceService, IQuotaService quotaService, ITime timeService)
        {
            this.logger = logger;
            this.options = options.Value;
            this.fs = fs;
            this.presenceService = presenceService;
            this.quotaService = quotaService;
            this.timeService = timeService;
            CreateTimespanSelectors();
        }

        public async Task Start()
        {
            logger.LogInformation("Cleanup service starting...");
            cts = new CancellationTokenSource();
            worker = Task.Run(Worker);
        }

        public async Task Stop()
        {
            cts.Cancel();
            worker.Wait();
            logger.LogInformation("Cleanup service stopped.");
        }

        private async Task Worker()
        {
            try
            {
                await InternalWorker();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fatal: Cleanup worker stopped.");
                Environment.Exit(1);
            }
        }

        private async Task InternalWorker()
        {
            while (!cts.IsCancellationRequested)
            {
                await Step();
            }
        }

        private async Task Step()
        {
            if (queue.Count == 0) await FillQueue();
            else
            {
                var item = queue.First();
                queue.RemoveAt(0);
                await ProcessItem(item);
            }
        }

        private async Task FillQueue()
        {
            await fs.IterateObjects<FileMetadata>(queue.Add);
            await Sleep(longSleep);
        }

        private async Task ProcessItem(FileMetadata item)
        {
            if (item.StoreType == StoreRequestType.PermanentFile) return;

            var state = quotaService.GetQuotaStatus().State;
            var span = timespanSelectors[state][item.StoreType]();
            var fileUtc = GetFileUtc(item);

            if (fileUtc + span > timeService.UtcNow())
            {
                await DeleteFile(item);
            }

            await Sleep(stepSleep);
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

        private async Task Sleep(TimeSpan span)
        {
            await Task.Delay(span, cts.Token);
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
