using ContentTower.Services;
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
        private readonly IPinService pinService;

        public DownloadController(IPresenceService presenceService, ILoadService loadService, IPinService pinService)
        {
            this.presenceService = presenceService;
            this.loadService = loadService;
            this.pinService = pinService;
        }

        [HttpGet]
        [Route("download/{cid}")]
        [EndpointDescription("Downloads content as a stream.")]
        [ProducesResponseType<Stream>(StatusCodes.Status200OK, MediaTypeNames.Application.Octet)]
        public Stream Download([FromRoute] string cid)
        {
            if (!IsValid(cid)) throw new BadHttpRequestException("Invalid CID");
            var contentId = new Cid(cid);
            if (!presenceService.IsPresent(contentId)) throw new BadHttpRequestException("Not found");

            var metadata = loadService.ReadMetadata(contentId);
            var stream = loadService.ReadData(contentId);
            UpdateLastActivity(metadata);
            return stream;
        }

        [HttpGet]
        [Route("metadata/{cid}")]
        [EndpointDescription("Retrieves metadata for content.")]
        public ContentView Metadata([FromRoute] string cid)
        {
            if (!IsValid(cid)) throw new BadHttpRequestException("Invalid CID");
            var contentId = new Cid(cid);
            if (!presenceService.IsPresent(contentId)) throw new BadHttpRequestException("Not found");
            return Map(loadService.ReadMetadata(contentId));
        }

        [HttpGet]
        [Route("check/{cid}")]
        [EndpointDescription("Checks whether the content exists in this service.")]
        public bool Check([FromRoute] string cid)
        {
            if (!IsValid(cid)) throw new BadHttpRequestException("Invalid CID");

            var contentId = new Cid(cid);
            if (!presenceService.IsPresent(contentId)) return false;
            return true;
        }

        private static ContentView Map(FileMetadata file)
        {
            return new ContentView
            {
                Cid = file.Cid.Id,
                Name = file.Name,
                ContentType = file.ContentType,
                Length = file.Length,
                PinIds = file.PinIds.Select(p => p.Id).ToArray()
            };
        }

        private void UpdateLastActivity(FileMetadata metadata)
        {
            pinService.RegisterActivity(metadata.Cid);
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
