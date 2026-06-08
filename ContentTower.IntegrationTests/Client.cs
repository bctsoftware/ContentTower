using ContentTowerOpenAPIClient;

namespace ContentTower.IntegrationTests
{
    public class Client
    {
        private readonly openapiClient client;
        private readonly ILog log;
        private readonly Lock _lock = new Lock();

        public Client(string baseUrl, ILog log)
        {
            client = new openapiClient(new HttpClient());
            client.BaseUrl = baseUrl;
            this.log = new Prefixer(log, "(Client)");
        }

        public OptionsView Initialize()
        { 
            try
            {
                var config = On(api => api.ConfigAsync());
                if (config.CleanupIntervalSeconds > 0)
                {
                    log.Log("ContentTower connection OK.");
                    return config;
                }
            }
            catch { }
            log.Log("Failed to connect to ContentTower.");
            throw new Exception("Failed to connect");
        }

        public Cid Upload(string name, string type, byte[] data)
        {
            var response = On(api => api.UploadAsync(new UploadRequest
            {
                Name = name,
                ContentType = type,
                Data = data,
                StoreType = StoreRequestType.Default
            }));
            log.Log($"Uploaded {data.Length} bytes '{name}' => {response.ContentId}");
            return new Cid(response.ContentId);
        }

        public bool Check(Cid cid)
        {
            var result = On(api => api.CheckAsync(cid.Hash));
            log.Log($"Checked {cid} => {result}");
            return result;
        }

        public FileMetadata Metadata(Cid cid)
        {
            var result = On(api => api.MetadataAsync(cid.Hash));
            log.Log($"Metadata {cid} => '{result.Name}'");
            return result;
        }

        public byte[] Download(Cid cid)
        {
            using var stream = new MemoryStream();
            var fileResponse = On(api => api.DownloadAsync(cid.Hash));
            fileResponse.Stream.CopyTo(stream);
            var data = stream.ToArray();
            log.Log($"Downloaded {cid} => {data.Length} bytes");
            return data;
        }

        public T On<T>(Func<openapiClient, Task<T>> action)
        {
            lock (_lock)
            {
                var task = action(client);
                task.Wait();
                return task.Result;
            }
        }
    }

    public class Cid
    {
        public Cid(string hash)
        {
            Hash = hash;
        }

        public string Hash { get; }

        public override string ToString()
        {
            return $"'{Hash.Substring(0, 5)}..{Hash.Last()}'";
        }
    }
}
