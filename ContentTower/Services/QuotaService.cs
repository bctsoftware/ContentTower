using ContentTower.Controllers;
using ContentTower.System;
using Microsoft.Extensions.Options;

namespace ContentTower.Services
{
    public interface IQuotaService
    {
        Task Initialize();
        QuotaResponse GetQuotaStatus();
        bool IsFull();
        void AddUsedBytes(long bytes);
        void RemoveUsedBytes(long bytes);
    }

    public class QuotaService : IQuotaService
    {
        private readonly QuotaResponse status = new QuotaResponse();
        private readonly IOptions<StorageOptions> options;
        private readonly IFileSystem fs;
        private readonly ILogger<QuotaService> logger;
        private long nominalLimit = 0;

        public QuotaService(ILogger<QuotaService> logger, IOptions<StorageOptions> options, IFileSystem fs)
        {
            this.logger = logger;
            this.options = options;
            this.fs = fs;
        }

        public async Task Initialize()
        {
            logger.LogInformation("Initializing...");

            status.Quota = options.Value.Quota;
            status.Used = 0;
            status.State = QuotaState.Nominal;

            double q = status.Quota;
            nominalLimit = Convert.ToInt64(q * 0.8);

            await fs.IterateObjects<FileMetadata>(CountUpUsedBytes);

            UpdateState();

            logger.LogInformation("Initialized Quota with {0} used bytes of {1} total bytes and state {2} (pressure limit: {4})", status.Used, status.Quota, status.State, nominalLimit);
        }

        public QuotaResponse GetQuotaStatus()
        {
            return status;
        }
        
        public bool IsFull()
        {
            return status.State == QuotaState.Full;
        }

        public void AddUsedBytes(long bytes)
        {
            status.Used += bytes;
            UpdateState();
        }

        public void RemoveUsedBytes(long bytes)
        {
            status.Used -= bytes;
            if (status.Used < 0)
            {
                status.Used = 0;
                logger.LogWarning("Quota skew: Reached negative used-bytes. :/");
            }
            UpdateState();
        }

        private void UpdateState()
        {
            var newState = GetNewState();

            if (newState == QuotaState.Pressure && status.State == QuotaState.Nominal)
            {
                logger.LogWarning("ContentTower storage reaches pressure level. Cleaning up of old data will speed up.");
            }
            else if (newState == QuotaState.Nominal && status.State != QuotaState.Nominal)
            {
                logger.LogWarning("ContentTower storage pressure resolved. Cleaning speed reduced to normal.");
            }

            status.State = newState;
        }

        private QuotaState GetNewState()
        {
            if (status.Used > status.Quota) return QuotaState.Full;
            if (status.Used > nominalLimit) return QuotaState.Pressure;
            return QuotaState.Nominal;
        }

        private void CountUpUsedBytes(FileMetadata metadata)
        {
            status.Used += metadata.Length;
        }
    }
}
