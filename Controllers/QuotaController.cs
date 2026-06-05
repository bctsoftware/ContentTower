using Microsoft.AspNetCore.Mvc;

namespace ContentTower.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class QuotaController : ControllerBase
    {
        [HttpGet]
        public async Task<QuotaResponse> Get()
        {
            return new QuotaResponse();
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
