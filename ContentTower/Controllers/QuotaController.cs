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
        [Route("interval")]
        public async Task<IActionResult> GetInterval()
        {
            return Ok(options.CleanupIntervalSeconds);
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
}
