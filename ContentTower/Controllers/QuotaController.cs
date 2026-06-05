using ContentTower.Services;
using Microsoft.AspNetCore.Mvc;

namespace ContentTower.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class QuotaController : ControllerBase
    {
        private readonly IQuotaService quotaService;

        public QuotaController(IQuotaService quotaService)
        {
            this.quotaService = quotaService;
        }

        [HttpGet]
        public async Task<QuotaResponse> Get()
        {
            return quotaService.GetQuotaStatus();
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
