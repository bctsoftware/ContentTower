using ContentTower.Controllers;
using Microsoft.Extensions.Options;

namespace ContentTower.Services.CleanupWorkers
{
    public interface ITimespanSelector
    {
        TimeSpan Get(StoreType storeType);
    }

    public class TimespanSelector : ITimespanSelector
    {
        private readonly StorageOptions options;
        private readonly IQuotaService quotaService;
        private readonly Dictionary<QuotaState, Dictionary<StoreType, Func<TimeSpan>>> timespanSelectors = new();

        public TimespanSelector(IOptions<StorageOptions> options, IQuotaService quotaService)
        {
            this.options = options.Value;
            this.quotaService = quotaService;
            
            CreateTimespanSelectors();
        }

        public TimeSpan Get(StoreType storeType)
        {
            var state = quotaService.GetQuotaStatus().State;
            return timespanSelectors[state][storeType]();
        }

        private void CreateTimespanSelectors()
        {
            var nominalSet = new Dictionary<StoreType, Func<TimeSpan>>();
            var pressureSet = new Dictionary<StoreType, Func<TimeSpan>>();

            nominalSet.Add(StoreType.Default, () => options.StoreDurationDefaultNominal);
            nominalSet.Add(StoreType.Temporary, () => options.StoreDurationTemporaryNominal);
            nominalSet.Add(StoreType.Permanent, () => throw new InvalidOperationException("Attempt to get timespan for permanent file in nominal state."));

            pressureSet.Add(StoreType.Default, () => options.StoreDurationDefaultPressure);
            pressureSet.Add(StoreType.Temporary, () => options.StoreDurationTemporaryPressure);
            pressureSet.Add(StoreType.Permanent, () => throw new InvalidOperationException("Attempt to get timespan for permanent file in pressure state."));

            timespanSelectors.Add(QuotaState.Nominal, nominalSet);
            timespanSelectors.Add(QuotaState.Pressure, pressureSet);
            timespanSelectors.Add(QuotaState.Full, pressureSet);
        }
    }
}
