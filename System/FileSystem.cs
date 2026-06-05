using ContentTower.Services;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace ContentTower.System
{
    public interface IFileSystem
    {
        Task WriteObject<T>(Cid cid, T obj);
        Task WriteData(Cid cid, byte[] data);
        Task<T> ReadObject<T>(Cid cid);
        Task<Stream> ReadData(Cid cid);
        Task DeleteObject(Cid cid);
        Task DeleteData(Cid cid);
        Task IterateObjects<T>(Action<T> onObject);
        bool Exists(Cid cid);
        bool CheckCreateDir(string dataPath);
    }

    public class FileSystem : IFileSystem
    {
        private readonly StorageOptions options;
        private readonly ILogger<FileSystem> logger;

        public FileSystem(ILogger<FileSystem> logger, IOptions<StorageOptions> options)
        {
            this.options = options.Value;
            this.logger = logger;
        }

        public bool CheckCreateDir(string path)
        {
            if (Directory.Exists(path)) return true;
            Directory.CreateDirectory(path);
            return Directory.Exists(path);
        }

        public async Task DeleteData(Cid cid)
        {
            File.Delete(GetDataFilepath(cid));
        }

        public async Task DeleteObject(Cid cid)
        {
            File.Delete(GetJsonFilepath(cid));
        }

        public bool Exists(Cid cid)
        {
            return
                File.Exists(GetJsonFilepath(cid)) &&
                File.Exists(GetDataFilepath(cid));
        }

        public async Task IterateObjects<T>(Action<T> onObject)
        {
            var files = Directory.GetFiles(options.DataPath);
            foreach (var file in files)
            {
                if (file.ToLowerInvariant().EndsWith(".json"))
                {
                    TryJson(file, onObject);
                }
            }
        }

        public async Task<Stream> ReadData(Cid cid)
        {
            return File.OpenRead(GetDataFilepath(cid));
        }

        public async Task<T> ReadObject<T>(Cid cid)
        {
            var obj = GetJson<T>(GetJsonFilepath(cid));
            if (obj == null) throw new Exception("Failed to load object for " + cid);
            return obj;
        }

        public async Task WriteData(Cid cid, byte[] data)
        {
            File.WriteAllBytes(GetDataFilepath(cid), data);
        }

        public async Task WriteObject<T>(Cid cid, T obj)
        {
            File.WriteAllText(GetJsonFilepath(cid), JsonConvert.SerializeObject(obj));
        }

        private void TryJson<T>(string file, Action<T> onObject)
        {
            try
            {
                var obj = GetJson<T>(file);
                if (obj != null) onObject(obj);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Exception in filesystem abstraction");
            }
        }

        private T? GetJson<T>(string file)
        {
            return JsonConvert.DeserializeObject<T>(File.ReadAllText(file));
        }

        private string GetJsonFilepath(Cid cid)
        {
            return Path.Combine(options.DataPath, cid.Hash + ".json");
        }

        private string GetDataFilepath(Cid cid)
        {
            return Path.Combine(options.DataPath, cid.Hash + ".data");
        }
    }
}
