using ContentTowerOpenAPIClient;

namespace ContentTower.IntegrationTests
{
    public class Client
    {
        private readonly openapiClient client;
        private readonly ILog log;
        private static readonly Lock _lock = new Lock();

        public Client(string baseUrl, ILog log)
        {
            client = new openapiClient(new HttpClient());
            client.BaseUrl = baseUrl;
            this.log = new Prefixer(log, "(Client)");
        }

        public OptionsView Initialize()
        {
            var tries = 5;
            while (tries > 0)
            {
                try
                {
                    return InitializeInternal();
                }
                catch (Exception ex)
                {
                    log.Log(ex.ToString());
                }

                Thread.Sleep(3000);
                tries--;
            }
            log.Log("Failed to connect to ContentTower.");
            throw new Exception("Failed to connect");
        }

        private OptionsView InitializeInternal()
        {
            var config = On(api => api.ConfigAsync());
            if (config.CleanupIntervalSeconds > 0)
            {
                log.Log("ContentTower connection OK.");
                return config;
            }
            throw new Exception("Invalid configuration received");
        }

        public (Cid, PinId) UploadNewPin(string name, string type, byte[] data, StoreType storeType = StoreType.Default)
        {
            var response = On(api => api.UploadAsync(new UploadRequest
            {
                Name = name,
                ContentType = type,
                Data = data,
                AttachExistingPinIds = Array.Empty<string>(),
                CreateNewPins = new[]
                {
                    storeType
                }
            }));
            var cid = new Cid(response.Cid);
            var pinId = new PinId(response.NewPinIds.Single());
            log.Log($"Uploaded {data.Length} bytes '{name}' with type {storeType} => {cid},{pinId}");
            return (cid, pinId);
        }

        public Cid UploadAttachPin(string name, string type, byte[] data, PinId pinId)
        {
            var response = On(api => api.UploadAsync(new UploadRequest
            {
                Name = name,
                ContentType = type,
                Data = data,
                AttachExistingPinIds = new[] { pinId.Id },
                CreateNewPins = Array.Empty<StoreType>()
            }));
            var cid = new Cid(response.Cid);
            log.Log($"Uploaded and attached {data.Length} bytes '{name}' to {pinId} => {cid}");
            return cid;
        }

        public bool Check(Cid cid)
        {
            var result = On(api => api.CheckAsync(cid.Id));
            log.Log($"Checked {cid} => {result}");
            return result;
        }

        public bool Check(PinId pinId)
        {
            var result = On(api => api.Check2Async(pinId.Id));
            log.Log($"Checked {pinId} => {result}");
            return result;
        }

        public ContentView Metadata(Cid cid)
        {
            var result = On(api => api.MetadataAsync(cid.Id));
            log.Log($"Content {cid} => '{result.Name}'");
            return result;
        }

        public PinId CreatePin(StoreType type)
        {
            return CreatePin(type, Array.Empty<Cid>());
        }

        public PinId CreatePin(StoreType type, Cid[] cids)
        {
            var response = On(api => api.PinPOSTAsync(new CreatePinRequest
            {
                StoreType = type,
                Cids = cids.Select(c => c.Id).ToArray()
            }));
            var pinId = new PinId(response.PinId);
            log.Log($"CreatePin of type {type} with {cids.Length} cids => {pinId}");
            return pinId;
        }

        public void PatchPin(PinId pinId, Cid[] addCids, Cid[] removeCids)
        {
            On(api => api.PinPATCHAsync(new UpdatePinRequest
            {
                PinId = pinId.Id,
                AddCids = addCids.Select(c => c.Id).ToArray(),
                RemoveCids = removeCids.Select(c => c.Id).ToArray()
            }));
            log.Log($"PatchPin");
        }

        public PinView Pin(PinId pinId)
        {
            var result = On(api => api.PinGETAsync(pinId.Id));
            log.Log($"Pin {pinId} => '{string.Join(",", result.Cids)}'");
            return result;
        }

        public byte[] Download(Cid cid)
        {
            using var stream = new MemoryStream();
            var fileResponse = On(api => api.DownloadAsync(cid.Id));
            fileResponse.Stream.CopyTo(stream);
            var data = stream.ToArray();
            log.Log($"Downloaded {cid} => {data.Length} bytes");
            return data;
        }

        public void Delete(PinId pinId, bool force = false)
        {
            if (force) On(api => api.ForceAsync(pinId.Id));
            else On(api => api.PinDELETEAsync(pinId.Id));
        }

        public T On<T>(Func<openapiClient, Task<T>> action)
        {
            lock (_lock)
            {
                try
                {
                    var task = action(client);
                    task.Wait();
                    return task.Result;
                }
                catch (Exception ex)
                {
                    log.Log("Exception: " + ex);
                    throw;
                }
            }
        }

        public void On(Func<openapiClient, Task> action)
        {
            lock (_lock)
            {
                try
                {
                    var task = action(client);
                    task.Wait();
                }
                catch (Exception ex)
                {
                    log.Log("Exception: " + ex);
                    throw;
                }
            }
        }
    }

    public abstract class BaseId
    {
        public BaseId(string id)
        {
            Id = id;
        }

        public string Id { get; }

        public override string ToString()
        {
            return $"'{Id.Substring(0, 5)}..{Id.Substring(Id.Length - 3)}'";
        }
    }

    public class Cid : BaseId
    {
        public Cid(string id) : base(id)
        {
        }
    }

    public class PinId : BaseId
    {
        public PinId(string id) : base(id)
        {
        }
    }
}
