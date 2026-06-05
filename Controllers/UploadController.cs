using Microsoft.AspNetCore.Mvc;

namespace ContentTower.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class UploadController : ControllerBase
    {
        [HttpPost]
        [RequestSizeLimit(long.MaxValue)]
        public async Task<UploadResponse> Upload(UploadRequest request)
        {
            await Task.CompletedTask;
            return new UploadResponse();
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
        public long BytesStored { get; set; }
        public string ContentId { get; set; } = string.Empty;
    }
}
