using ContentTower.Services;
using Microsoft.AspNetCore.Mvc;

namespace ContentTower.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PinController : ControllerBase
    {
        private readonly IPinService pinService;

        public PinController(IPinService pinService)
        {
            this.pinService = pinService;
        }

        [HttpPost]
        [EndpointDescription("Creates a new pin and attaches it to the specified contents.")]
        public string Create([FromBody] CreatePinRequest createPinRequest)
        {
            var cids = Map(createPinRequest.Cids);
            var pinId = pinService.Create(createPinRequest.StoreType, cids);
            return pinId.Id;
        }

        [HttpPatch]
        [EndpointDescription("Attached content to or detaches content from the specified pin.")]
        public void Update([FromBody] UpdatePinRequest updatePinRequest)
        {
            if (!IsValidPinId(updatePinRequest.PinId)) throw new BadHttpRequestException($"Invalid PinId: '{updatePinRequest.PinId}'.");

            var pinId = new PinId(updatePinRequest.PinId);
            var toAdd = Map(updatePinRequest.AddCids);
            var toRemove = Map(updatePinRequest.RemoveCids);

            pinService.Attach(pinId, toAdd);
            pinService.Detach(pinId, toRemove);
        }

        [HttpGet]
        [Route("{pinId}")]
        [EndpointDescription("Retrieves pin details.")]
        public PinView Get([FromRoute] string pinId)
        {
            if (!IsValidPinId(pinId)) throw new BadHttpRequestException($"Invalid PinId: '{pinId}'.");
            var pid = new PinId(pinId);
            return Map(pinService.Get(pid));
        }

        [HttpDelete]
        [Route("{pinId}")]
        [EndpointDescription("Deletes a pin immediately. Not allowed for pins with StoreType 'Permanent'.")]
        public void Delete([FromRoute] string pinId)
        {
            if (!IsValidPinId(pinId)) throw new BadHttpRequestException($"Invalid PinId: '{pinId}'.");
            var pid = new PinId(pinId);
            DeleteInternal(pid, force: false);
        }

        [HttpDelete]
        [Route("force/{pinId}")]
        [EndpointDescription("Deletes a pin immediately. Explicit override for pins with StoreType 'Permanent'.")]
        public void DeleteForce([FromRoute] string pinId)
        {
            if (!IsValidPinId(pinId)) throw new BadHttpRequestException($"Invalid PinId: '{pinId}'.");
            var pid = new PinId(pinId);
            DeleteInternal(pid, force: true);
        }

        private static PinView Map(PinData pinData)
        {
            return new PinView
            {
                PinId = pinData.PinId.Id,
                StoreType = pinData.StoreType,
                CreateUtc = pinData.CreateUtc,
                LastActivityUtc = pinData.LastActivityUtc,
                Cids = pinData.Cids.Select(c => c.Id).ToArray()
            };
        }

        private void DeleteInternal(PinId pinId, bool force)
        {
            var pin = pinService.Get(pinId);
            if (pin.StoreType == StoreType.PermanentFile && !force) throw new BadHttpRequestException("Cannot delete permanent file.");
            pinService.Delete(pinId);
        }

        private static Cid[] Map(string[] cids)
        {
            foreach (var c in cids) if (!IsValidCid(c)) throw new BadHttpRequestException($"Invalid CID: '{c}'.");
            return cids.Select(c => new Cid(c)).ToArray();
        }

        private static bool IsValidCid(string cid)
        {
            return cid.StartsWith(HashService.CidPrefix);
        }

        private static bool IsValidPinId(string pinId)
        {
            return pinId.StartsWith(PinService.PinIdPrefix);
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
