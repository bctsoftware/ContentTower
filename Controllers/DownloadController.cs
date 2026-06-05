using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace ContentTower.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DownloadController : ControllerBase
    {
        [HttpGet]
        public async Task<FileStreamResult> Download(DownloadRequest request)
        {
            var stream = new MemoryStream();
            return new FileStreamResult(stream, new MediaTypeHeaderValue("text/plain"))
            {
                FileDownloadName = "test.txt"
            };
        }
    }

    public class DownloadRequest
    {
        public string ContentId { get; set; } = string.Empty;
    }
}
