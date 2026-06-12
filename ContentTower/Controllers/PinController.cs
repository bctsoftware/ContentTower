using ContentTower.Services;
using Microsoft.AspNetCore.Mvc;

namespace ContentTower.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PinController : ControllerBase
    {
        private readonly IPresenceService presenceService;
        private readonly IDeleteService deleteService;
        private readonly ILoadService loadService;

        public PinController(IPresenceService presenceService, IDeleteService deleteService, ILoadService loadService)
        {
            this.presenceService = presenceService;
            this.deleteService = deleteService;
            this.loadService = loadService;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreatePinRequest createPinRequest)
        {
        }

        [HttpPatch]
        public async Task<IActionResult> Update([FromBody] UpdatePinRequest updatePinRequest)
        {
        }

        [HttpGet]
        [Route("{pinId}")]
        public async Task<PinView> Get([FromRoute] string pinId)
        {
        }

        [HttpDelete]
        [Route("{pinId}")]
        public async Task<IActionResult> Delete([FromRoute] string pinId)
        {
        }

        [HttpDelete]
        [Route("force/{pinId}")]
        public async Task<IActionResult> DeleteForce([FromRoute] string pinId)
        {
        }

        private async Task<IActionResult> DeleteInternal(Cid cid, bool force)
        {
            if (!presenceService.IsPresent(cid)) return NotFound();

            var metadata = await loadService.ReadMetadata(cid);
            if (metadata.StoreType == StoreType.PermanentFile && !force)
            {
                return BadRequest("Cannot delete permanent file.");
            }

            await deleteService.DeleteFile(metadata);
            return Ok();
        }

        private static bool IsValid(string cid)
        {
            return cid.StartsWith(HashService.CidPrefix);
        }
    }

    public class CreatePinRequest
    {
        public StoreType StoreType { get; set; }
        public string[] Cids { get; set; } = Array.Empty<string>();
    }

    public class UpdatePinRequest
    {
        public string PinId { get; set; } = string.Empty;
        public string[] AddCids { get; set; } = Array.Empty<string>();
        public string[] RemoveCids { get; set; } = Array.Empty<string>();
    }

    public class PinView
    {
        public string PinId { get; set; } = string.Empty;
        public StoreType StoreType { get; set; }
        public DateTime CreateUtc { get; set; }
        public DateTime LastActivityUtc { get; set; }
        public string[] Cids { get; set; } = Array.Empty<string>();
    }
}
