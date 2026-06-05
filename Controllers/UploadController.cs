using ContentTower.Services;
using Microsoft.AspNetCore.Mvc;

namespace ContentTower.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class UploadController : ControllerBase
    {
        private readonly ISaveService saveService;
        private readonly IQuotaService quotaService;

        public UploadController(ISaveService saveService, IQuotaService quotaService)
        {
            this.saveService = saveService;
            this.quotaService = quotaService;
        }

        [HttpPost]
        [RequestSizeLimit(long.MaxValue)]
        public async Task<IActionResult> Upload(UploadRequest request)
        {
            if (quotaService.IsFull()) return BadRequest("Storage quota is full.");

            var cid = await saveService.Handle(request);
            return Ok(new UploadResponse
            {
                ContentId = cid.Hash
            });
        }
    }

    public class UploadRequest
    {
        public StoreRequestType StoreType { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public byte[] Data { get; set; } = Array.Empty<byte>();
    }

    public enum StoreRequestType
    {
        Default,
        TemporaryFile,
        PermanentFile
    }

    public class UploadResponse
    {
        public string ContentId { get; set; } = string.Empty;
    }
}
