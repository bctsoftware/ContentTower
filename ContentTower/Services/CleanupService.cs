using ContentTower.System;

namespace ContentTower.Services
{
    public interface ICleanupService
    {
        void Start();
    }

    public class CleanupService : ICleanupService
    {
        private readonly TimeSpan longSleep = TimeSpan.FromMinutes(10);
        private readonly ILogger<CleanupService> logger;
        private readonly IHostApplicationLifetime appLifetime;
        private readonly IFileSystem fs;
        private readonly ITime timeService;
        private readonly ICleanupWorker cleanupWorker;
        private readonly List<FileMetadata> queue = new List<FileMetadata>();

        public CleanupService(ILogger<CleanupService> logger, IHostApplicationLifetime  appLifetime, IFileSystem fs, ITime timeService, ICleanupWorker cleanupWorker)
        {
            this.logger = logger;
            this.appLifetime = appLifetime;
            this.fs = fs;
            this.timeService = timeService;
            this.cleanupWorker = cleanupWorker;
        }

        public void Start()
        {
            logger.LogInformation("Cleanup service starting...");
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
                await cleanupWorker.ProcessItem(item, Ct);
            }
        }

        private async Task FillQueue()
        {
            await fs.IterateObjects<FileMetadata>(queue.Add);
            await timeService.Sleep(longSleep, Ct);
        }

        private CancellationToken Ct => appLifetime.ApplicationStopping;
    }
}
