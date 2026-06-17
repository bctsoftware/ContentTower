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
        private readonly IPinService pinService;

        public UploadController(ISaveService saveService, IQuotaService quotaService, IPinService pinService)
        {
            this.saveService = saveService;
            this.quotaService = quotaService;
            this.pinService = pinService;
        }

        [HttpPost]
        [RequestSizeLimit(long.MaxValue)]
        [EndpointDescription("Stores content in ContentTower. Creates new pins for the content if requested. Attaches existing pins to the content if requested.")]
        public UploadResponse Upload(UploadRequest request)
        {
            if (quotaService.IsFull()) throw new BadHttpRequestException("Storage quota is full.");

            var errors = GetPinningInstructionErrors(request);
            if (!string.IsNullOrEmpty(errors)) throw new BadHttpRequestException(errors);

            var cid = saveService.Save(new SaveRequest(
                name: request.Name,
                contentType: request.ContentType,
                data: request.Data
            ));

            pinService.Attach(GetAttachPinIds(request), cid);
            var newPins = pinService.Create(GetNewPinTypes(request), cid);

            return new UploadResponse
            {
                Cid = cid.Id,
                NewPinIds = newPins.Select(p => p.Id).ToArray()
            };
        }

        private StoreType[] GetNewPinTypes(UploadRequest request)
        {
            if (request.CreateNewPins == null) return Array.Empty<StoreType>();
            return request.CreateNewPins;
        }

        private PinId[] GetAttachPinIds(UploadRequest request)
        {
            if (request.AttachExistingPinIds == null) return Array.Empty<PinId>();
            return request.AttachExistingPinIds.Select(p => new PinId(p)).ToArray();
        }

        private string GetPinningInstructionErrors(UploadRequest request)
        {
            if (
                (request.AttachExistingPinIds == null || request.AttachExistingPinIds.Length == 0) &&
                (request.CreateNewPins == null || request.CreateNewPins.Length == 0)
            )
            {
                return $"Both '{nameof(UploadRequest.AttachExistingPinIds)}' and '{nameof(UploadRequest.CreateNewPins)}' are null or empty. You must provide at least one. " +
                    $"The new content must be attached to at least 1 pin.";
            }
            return GetAttachPinsErrors(request.AttachExistingPinIds);
        }

        private string GetAttachPinsErrors(string[]? pinIds)
        {
            var result = string.Empty;
            if (pinIds == null) return result;
            foreach (var pin in pinIds)
            {
                if (!pinService.Exists(new PinId(pin))) result += $" pinId '{pin}' does not exist.";
            }
            return result;
        }
    }

    public class UploadRequest
    {
        public string Name { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public byte[] Data { get; set; } = Array.Empty<byte>();

        public string[] AttachExistingPinIds { get; set; } = Array.Empty<string>();
        public StoreType[] CreateNewPins { get; set; } = Array.Empty<StoreType>();
    }

    public class UploadResponse
    {
        public string Cid { get; set; } = string.Empty;
        public string[] NewPinIds { get; set; } = Array.Empty<string>();
    }
}
