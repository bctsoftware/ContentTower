using ContentTower.Controllers;
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
        private readonly IFileSystemService fs;
        private readonly ILogger<QuotaService> logger;
        private long nominalLimit = 0;

        public QuotaService(ILogger<QuotaService> logger, IOptions<StorageOptions> options, IFileSystemService fs)
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
            status.State = QuotaState.Full;

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
            if (status.Used > status.Quota) status.State = QuotaState.Full;
            else if (status.Used > nominalLimit) status.State = QuotaState.Pressure;
            else status.State = QuotaState.Nominal;
        }

        private void CountUpUsedBytes(FileMetadata metadata)
        {
            status.Used += metadata.Length;
        }
    }
}
