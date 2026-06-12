using ContentTower.System;
using Microsoft.Extensions.Options;

namespace ContentTower.Services
{
    public interface ICleanupService
    {
        void Start();
    }

    public class CleanupService : ICleanupService
    {
        private readonly TimeSpan longSleep;
        private readonly TimeSpan stepSleep = TimeSpan.FromMilliseconds(100);
        private readonly ILogger<CleanupService> logger;
        private readonly IHostApplicationLifetime appLifetime;
        private readonly IObjectStoreService objectStoreService;
        private readonly ITime timeService;
        private readonly ICleanupWorker cleanupWorker;
        private readonly List<FileMetadata> queue = new List<FileMetadata>();

        public CleanupService(ILogger<CleanupService> logger, IOptions<StorageOptions> options, IHostApplicationLifetime  appLifetime, IObjectStoreService objectStoreService, ITime timeService, ICleanupWorker cleanupWorker)
        {
            this.logger = logger;
            this.appLifetime = appLifetime;
            this.objectStoreService = objectStoreService;
            this.timeService = timeService;
            this.cleanupWorker = cleanupWorker;

            longSleep = options.Value.CleanupInterval;
        }

        public void Start()
        {
            logger.LogInformation("Cleanup service starting with interval {0}...", Utils.FormatDuration(longSleep));
            Task.Run(Worker);
        }

        private async Task Worker()
        {
            try
            {
                await InternalWorker();
            }
            catch (TaskCanceledException)
            {
                // Graceful shutdown
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fatal: Cleanup worker stopped.");
                Environment.Exit(1);
            }
        }

        private async Task InternalWorker()
        {
            while (!Ct.IsCancellationRequested)
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
                await cleanupWorker.ProcessItem(item);
                await timeService.Sleep(stepSleep, Ct);
            }
        }

        private async Task FillQueue()
        {
            objectStoreService.IterateObjects<FileMetadata>(queue.Add);
            logger.LogTrace("Scheduled {0} items for evaluation.", queue.Count);
            await timeService.Sleep(longSleep, Ct);
        }

        private CancellationToken Ct => appLifetime.ApplicationStopping;
    }
}
