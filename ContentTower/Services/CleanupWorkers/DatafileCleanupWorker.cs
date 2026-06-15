using ContentTower.System;
using Microsoft.Extensions.Options;

namespace ContentTower.Services.CleanupWorkers
{
    public interface IDatafileCleanupWorker
    {
        void Step(CancellationToken ct);
    }

    public class DatafileCleanupWorker : IDatafileCleanupWorker
    {
        private readonly ILogger<DatafileCleanupWorker> logger;
        private readonly IFileSystem fs;
        private readonly ITime timeService;
        private readonly StorageOptions options;

        public DatafileCleanupWorker(ILogger<DatafileCleanupWorker> logger, IFileSystem fs, IOptions<StorageOptions> options, ITime timeService)
        {
            this.logger = logger;
            this.fs = fs;
            this.timeService = timeService;
            this.options = options.Value;
        }

        public void Step(CancellationToken ct)
        {
            var files = fs.DirectoryGetFiles(options.DataPath);
            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();
                CheckMetadataExists(file);
                timeService.Sleep(TimeSpan.FromMilliseconds(100), ct);
            }
        }

        private void CheckMetadataExists(string file)
        {
            if (file.EndsWith(".data"))
            {
                var expectedMetadataFile = file.Substring(0, file.Length - 5) + ".json";
                if (!fs.Exists(expectedMetadataFile))
                {
                    logger.LogWarning($"Found data file '{0}' without matching metadata file. Deleting...", file);
                    try
                    {
                        fs.DeleteFile(file);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to delete data file '{0}' that has no matching metadata file.", file);
                    }
                }
            }
        }
    }
}
