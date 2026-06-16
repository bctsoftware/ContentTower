using ContentTower.Services.CleanupWorkers;
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
        private readonly ITime timeService;
        private readonly IPinCleanupWorker pinCleanupWorker;
        private readonly IContentCleanupWorker contentCleanupWorker;
        private readonly IDatafileCleanupWorker datafileCleanupWorker;

        public CleanupService(ILogger<CleanupService> logger, IOptions<StorageOptions> options, IHostApplicationLifetime  appLifetime, ITime timeService,
            IPinCleanupWorker pinCleanupWorker,
            IContentCleanupWorker contentCleanupWorker,
            IDatafileCleanupWorker datafileCleanupWorker)
        {
            this.logger = logger;
            this.appLifetime = appLifetime;
            this.timeService = timeService;
            this.pinCleanupWorker = pinCleanupWorker;
            this.contentCleanupWorker = contentCleanupWorker;
            this.datafileCleanupWorker = datafileCleanupWorker;
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
                await timeService.Sleep(longSleep, Ct);
            }
        }

        private async Task Step()
        {
            logger.LogTrace("Cleanup steps activating...");

            pinCleanupWorker.Step(Ct);
            await timeService.Sleep(stepSleep, Ct);
            if (Ct.IsCancellationRequested) return;

            contentCleanupWorker.Step(Ct);
            await timeService.Sleep(stepSleep, Ct);
            if (Ct.IsCancellationRequested) return;

            datafileCleanupWorker.Step(Ct);
            await timeService.Sleep(stepSleep, Ct);
        }

        private CancellationToken Ct => appLifetime.ApplicationStopping;
    }
}
