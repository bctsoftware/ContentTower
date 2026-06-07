using ContentTower.System;

namespace ContentTower.Services
{
    public interface ICleanupService
    {
        Task Start();
        Task Stop();
    }

    public class CleanupService : ICleanupService
    {
        private readonly TimeSpan longSleep = TimeSpan.FromMinutes(10);
        private readonly ILogger<CleanupService> logger;
        private readonly IFileSystem fs;
        private readonly ITime timeService;
        private readonly ICleanupWorker cleanupWorker;
        private readonly List<FileMetadata> queue = new List<FileMetadata>();
        private CancellationTokenSource cts = new CancellationTokenSource();
        private Task worker = Task.CompletedTask;

        public CleanupService(ILogger<CleanupService> logger, IFileSystem fs, ITime timeService, ICleanupWorker cleanupWorker)
        {
            this.logger = logger;
            this.fs = fs;
            this.timeService = timeService;
            this.cleanupWorker = cleanupWorker;
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
                await cleanupWorker.ProcessItem(item, cts.Token);
            }
        }

        private async Task FillQueue()
        {
            await fs.IterateObjects<FileMetadata>(queue.Add);
            await timeService.Sleep(longSleep, cts.Token);
        }
    }
}
