using ContentTower.Services;
using Microsoft.AspNetCore.Mvc;

namespace ContentTower.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DeleteController : ControllerBase
    {
        private readonly IPresenceService presenceService;
        private readonly IDeleteService deleteService;
        private readonly ILoadService loadService;

        public DeleteController(IPresenceService presenceService, IDeleteService deleteService, ILoadService loadService)
        {
            this.presenceService = presenceService;
            this.deleteService = deleteService;
            this.loadService = loadService;
        }

        [HttpDelete]
        [Route("{cid}")]
        public async Task<IActionResult> Delete([FromRoute] string cid)
        {
            if (!IsValid(cid)) return BadRequest();
            var contentId = new Cid(cid);

            return await DeleteInternal(contentId, force: false);
        }

        [HttpDelete]
        [Route("force/{cid}")]
        public async Task<IActionResult> DeleteForce([FromRoute] string cid)
        {
            if (!IsValid(cid)) return BadRequest();
            var contentId = new Cid(cid);

            return await DeleteInternal(contentId, force: true);
        }

        private async Task<IActionResult> DeleteInternal(Cid cid, bool force)
        {
            if (!presenceService.IsPresent(cid)) return NotFound();

            var metadata = await loadService.ReadMetadata(cid);
            if (metadata.StoreType == StoreRequestType.PermanentFile && !force)
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
}
