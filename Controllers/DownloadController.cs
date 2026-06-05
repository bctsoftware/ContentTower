using ContentTower.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace ContentTower.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DownloadController : ControllerBase
    {
        private readonly IPresenceService presenceService;
        private readonly ILoadService loadService;

        public DownloadController(IPresenceService presenceService, ILoadService loadService)
        {
            this.presenceService = presenceService;
            this.loadService = loadService;
        }

        [HttpGet]
        [Route("get/{cid}")]
        public async Task<IActionResult> Download([FromRoute] string cid)
        {
            if (!IsValid(cid)) return BadRequest("Invalid CID");
            var contentId = new Cid(cid);
            if (!presenceService.IsPresent(contentId)) return NotFound();

            var metadata = await loadService.ReadMetadata(contentId);
            var stream = await loadService.ReadData(contentId);
            return new FileStreamResult(stream, new MediaTypeHeaderValue(metadata.ContentType))
            {
                FileDownloadName = metadata.Name
            };
        }

        [HttpGet]
        [Route("check/{cid}")]
        public async Task<IActionResult> Check([FromRoute] string cid)
        {
            if (!IsValid(cid)) return BadRequest("Invalid CID");

            var contentId = new Cid(cid);
            if (!presenceService.IsPresent(contentId)) return NotFound();
            return Ok();
        }

        private static bool IsValid(string cid)
        {
            return cid.StartsWith(HashService.CidPrefix);
        }
    }
}
