using ContentTower.Services;
using ContentTower.System;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;

namespace ContentTower.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DownloadController : ControllerBase
    {
        private readonly IPresenceService presenceService;
        private readonly ILoadService loadService;
        private readonly IFileSystem fs;
        private readonly ITime timeService;

        public DownloadController(IPresenceService presenceService, ILoadService loadService, IFileSystem fs, ITime timeService)
        {
            this.presenceService = presenceService;
            this.loadService = loadService;
            this.fs = fs;
            this.timeService = timeService;
        }

        [HttpGet]
        [Route("download/{cid}")]
        [ProducesResponseType<Stream>(StatusCodes.Status200OK, MediaTypeNames.Application.Octet)]
        public async Task<Stream> Download([FromRoute] string cid)
        {
            if (!IsValid(cid)) throw new BadHttpRequestException("Invalid CID");
            var contentId = new Cid(cid);
            if (!presenceService.IsPresent(contentId)) throw new BadHttpRequestException("Not found");

            var metadata = await loadService.ReadMetadata(contentId);
            var stream = await loadService.ReadData(contentId);
            await UpdateLastActivity(metadata);
            return stream;
        }

        [HttpGet]
        [Route("metadata/{cid}")]
        public async Task<ContentView> Metadata([FromRoute] string cid)
        {
            if (!IsValid(cid)) throw new BadHttpRequestException("Invalid CID");
            var contentId = new Cid(cid);
            if (!presenceService.IsPresent(contentId)) throw new BadHttpRequestException("Not found");
            return await loadService.ReadMetadata(contentId);
        }

        [HttpGet]
        [Route("check/{cid}")]
        public async Task<bool> Check([FromRoute] string cid)
        {
            if (!IsValid(cid)) throw new BadHttpRequestException("Invalid CID");

            var contentId = new Cid(cid);
            if (!presenceService.IsPresent(contentId)) return false;
            return true;
        }

        private async Task UpdateLastActivity(FileMetadata metadata)
        {
            metadata.LastActivityUtc = timeService.UtcNow();
            await fs.WriteObject(metadata.Cid, metadata);
        }

        private static bool IsValid(string cid)
        {
            return cid.StartsWith(HashService.CidPrefix);
        }
    }

    public class ContentView
    {
        public string Cid { get; set; } = string.Empty;
        public string[] PinIds { get; set; } = Array.Empty<string>();

        public string Name { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long Length { get; set; }
    }
}
