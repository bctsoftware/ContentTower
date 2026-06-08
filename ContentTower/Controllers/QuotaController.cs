using ContentTower.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ContentTower.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class QuotaController : ControllerBase
    {
        private readonly IQuotaService quotaService;
        private readonly StorageOptions options;

        public QuotaController(IQuotaService quotaService, IOptions<StorageOptions> options)
        {
            this.quotaService = quotaService;
            this.options = options.Value;
        }

        [HttpGet]
        public async Task<QuotaResponse> Get()
        {
            return quotaService.GetQuotaStatus();
        }

        [HttpGet]
        [Route("config")]
        public async Task<OptionsView> GetConfig()
        {
            return new OptionsView(options);
        }
    }

    public class QuotaResponse
    {
        public long Quota { get; set; }
        public long Used { get; set; }
        public QuotaState State { get; set; }
    }

    public enum QuotaState
    {
        Nominal,
        Pressure,
        Full
    }

    public class OptionsView
    {
        public OptionsView(StorageOptions options)
        {
            CleanupIntervalSeconds = options.CleanupIntervalSeconds;
            StoreDurationDefaultNominalSeconds = options.StoreDurationDefaultNominalSeconds;
            StoreDurationDefaultPressureSeconds = options.StoreDurationDefaultPressureSeconds;
            StoreDurationTemporaryNominalSeconds = options.StoreDurationTemporaryNominalSeconds;
            StoreDurationTemporaryPressureSeconds = options.StoreDurationTemporaryPressureSeconds;
        }

        public int CleanupIntervalSeconds { get; set; }
        public int StoreDurationDefaultNominalSeconds { get; set; }
        public int StoreDurationDefaultPressureSeconds { get; set; }
        public int StoreDurationTemporaryNominalSeconds { get; set; }
        public int StoreDurationTemporaryPressureSeconds { get; set; }
    }
}
