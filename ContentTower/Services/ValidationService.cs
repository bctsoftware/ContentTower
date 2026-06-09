using ContentTower.System;
using Microsoft.Extensions.Options;

namespace ContentTower.Services
{
    public interface IValidationService
    {
        void ValidateOptions();
    }

    public class ValidationService : IValidationService
    {
        private const int MinDefaultTimespanSeconds = 60;
        private const int MinTempTimespanSeconds = 20;
        private readonly StorageOptions options;
        private readonly IFileSystem fs;

        public ValidationService(IOptions<StorageOptions> options, IFileSystem fs)
        {
            this.options = options.Value;
            this.fs = fs;
        }

        public void ValidateOptions()
        {
            var faults = new List<string>();
            if (string.IsNullOrEmpty(options.DataPath)) faults.Add("DataPath not provided.");
            if (!fs.CheckCreateDir(options.DataPath)) faults.Add("Unable to create DataPath");
            if (options.Quota < 1024 * 1024) faults.Add("Quota must be at least 1 MB (1048576)");
            ValidateTimespan(faults, options.CleanupInterval, 1, nameof(StorageOptions.CleanupInterval));
            ValidateTimespan(faults, options.StoreDurationDefaultNominal, MinDefaultTimespanSeconds, nameof(StorageOptions.StoreDurationDefaultNominal));
            ValidateTimespan(faults, options.StoreDurationDefaultPressure, MinDefaultTimespanSeconds, nameof(StorageOptions.StoreDurationDefaultPressure));
            ValidateTimespan(faults, options.StoreDurationTemporaryNominal, MinTempTimespanSeconds, nameof(StorageOptions.StoreDurationTemporaryNominal));
            ValidateTimespan(faults, options.StoreDurationTemporaryPressure, MinTempTimespanSeconds, nameof(StorageOptions.StoreDurationTemporaryPressure));

            if (faults.Any())
            {
                throw new Exception($"Invalid configuration: {string.Join(", ", faults)}");
            }
        }

        private static void ValidateTimespan(List<string> faults, TimeSpan t, int minSeconds, string name)
        {
            if (t.TotalSeconds < minSeconds) faults.Add($"Timespan for {name} is too short. Must be at least {minSeconds} seconds.");
        }
    }
}
