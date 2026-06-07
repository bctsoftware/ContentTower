namespace ContentTower
{
    public class StorageOptions
    {
        public long Quota { get; set; }
        public string DataPath { get; set; } = string.Empty;

        public int CleanupIntervalSeconds { get; set; }
        public int StoreDurationDefaultNominalSeconds { get; set; }
        public int StoreDurationDefaultPressureSeconds { get; set; }
        public int StoreDurationTemporaryNominalSeconds { get; set; }
        public int StoreDurationTemporaryPressureSeconds { get; set; }

        public TimeSpan CleanupInterval => TimeSpan.FromSeconds(CleanupIntervalSeconds);
        public TimeSpan StoreDurationDefaultNominal => TimeSpan.FromSeconds(StoreDurationDefaultNominalSeconds);
        public TimeSpan StoreDurationDefaultPressure => TimeSpan.FromSeconds(StoreDurationDefaultPressureSeconds);
        public TimeSpan StoreDurationTemporaryNominal => TimeSpan.FromSeconds(StoreDurationTemporaryNominalSeconds);
        public TimeSpan StoreDurationTemporaryPressure => TimeSpan.FromSeconds(StoreDurationTemporaryPressureSeconds);
    }
}
