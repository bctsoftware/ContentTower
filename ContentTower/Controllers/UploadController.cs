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
        public async Task<UploadResponse> Upload(UploadRequest request)
        {
            if (quotaService.IsFull()) throw new BadHttpRequestException("Storage quota is full.");

            var cid = await saveService.Handle(request);
            return new UploadResponse
            {
                ContentId = cid.Hash
            };
        }
    }

    public class UploadRequest
    {
        public string Name { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public byte[] Data { get; set; } = Array.Empty<byte>();

        public AttachToPinRequest? AttachPins { get; set; } = null;
        public UploadCreatePinsRequest? CreateNewPins { get; set; } = null;
    }

    public class AttachToPinRequest
    {
        public string[] PinIds { get; set; } = Array.Empty<string>();
    }

    public class UploadCreatePinsRequest
    {
        public StoreType[] StoreTypes { get; set; } = Array.Empty<StoreType>();
    }

    public class UploadResponse
    {
        public string Cid { get; set; } = string.Empty;
        public string[] PinIds { get; set; } = Array.Empty<string>();
    }
}
