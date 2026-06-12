using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace ContentTower.System
{
    public interface IFileSystem
    {
        Task WriteObject<T>(IId id, T obj);
        Task WriteData(IId id, byte[] data);
        Task<T> ReadObject<T>(IId id);
        Task<Stream> ReadData(IId id);
        Task DeleteObject(IId id);
        Task DeleteData(IId id);
        Task IterateObjects<T>(Action<T> onObject);
        bool Exists(IId id);
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

        public async Task DeleteData(IId id)
        {
            File.Delete(GetDataFilepath(id));
        }

        public async Task DeleteObject(IId id)
        {
            File.Delete(GetJsonFilepath(id));
        }

        public bool Exists(IId id)
        {
            return
                File.Exists(GetJsonFilepath(id)) &&
                File.Exists(GetDataFilepath(id));
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

        public async Task<Stream> ReadData(IId id)
        {
            return File.OpenRead(GetDataFilepath(id));
        }

        public async Task<T> ReadObject<T>(IId id)
        {
            var obj = GetJson<T>(GetJsonFilepath(id));
            if (obj == null) throw new Exception("Failed to load object for " + id);
            return obj;
        }

        public async Task WriteData(IId id, byte[] data)
        {
            File.WriteAllBytes(GetDataFilepath(id), data);
        }

        public async Task WriteObject<T>(IId id, T obj)
        {
            File.WriteAllText(GetJsonFilepath(id), JsonConvert.SerializeObject(obj));
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

        private string GetJsonFilepath(IId id)
        {
            if (string.IsNullOrEmpty(id.Id)) throw new Exception("Invalid CID");
            return Path.Combine(options.DataPath, id.Id + ".json");
        }

        private string GetDataFilepath(IId id)
        {
            if (string.IsNullOrEmpty(id.Id)) throw new Exception("Invalid CID");
            return Path.Combine(options.DataPath, id.Id + ".data");
        }
    }
}
