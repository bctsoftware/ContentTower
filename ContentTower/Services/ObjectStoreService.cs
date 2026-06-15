using ContentTower.System;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace ContentTower.Services
{
    public interface IObjectStoreService
    {
        bool Exists(IId id);
        void CreateOrUpdateObject<T>(IId id, Action<T> onObj) where T : new();
        T ReadObject<T>(IId id);
        void DeleteObject(IId id);
        void IterateObjects<T>(string prefix, Action<T> onObject);
    }

    public class ObjectStoreService : IObjectStoreService
    {
        private readonly ILogger<ObjectStoreService> logger;
        private readonly IFileSystem fs;
        private readonly StorageOptions options;
        private readonly Lock mutateLock = new Lock();

        public ObjectStoreService(ILogger<ObjectStoreService> logger, IFileSystem fs, IOptions<StorageOptions> options)
        {
            this.logger = logger;
            this.fs = fs;
            this.options = options.Value;
        }

        public bool Exists(IId id)
        {
            return fs.Exists(GetJsonFilepath(id));
        }

        public void DeleteObject(IId id)
        {
            lock (mutateLock)
            {
                fs.DeleteFile(GetJsonFilepath(id));
            }
        }

        public void IterateObjects<T>(string prefix, Action<T> onObject)
        {
            var files = fs.DirectoryGetFiles(options.DataPath);
            foreach (var file in files)
            {
                if (file.StartsWith(prefix) && file.ToLowerInvariant().EndsWith(".json"))
                {
                    TryJson(file, onObject);
                }
            }
        }

        public T ReadObject<T>(IId id)
        {
            var obj = GetJson<T>(GetJsonFilepath(id));
            if (obj == null) throw new Exception("Failed to load object for " + id);
            return obj;
        }

        public void CreateOrUpdateObject<T>(IId id, Action<T> onObj) where T : new()
        {
            lock (mutateLock)
            {
                var obj = GetOrCreate<T>(id);
                onObj(obj);
                fs.WriteAllText(GetJsonFilepath(id), JsonConvert.SerializeObject(obj));
            }
        }

        private T GetOrCreate<T>(IId id) where T : new()
        {
            if (Exists(id)) return ReadObject<T>(id);
            return new T();
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
                logger.LogWarning(ex, "Exception deserializing object of type {0}", typeof(T));
            }
        }

        private static T? GetJson<T>(string file)
        {
            return JsonConvert.DeserializeObject<T>(File.ReadAllText(file));
        }

        private string GetJsonFilepath(IId id)
        {
            if (string.IsNullOrEmpty(id.Id)) throw new Exception("Invalid CID");
            return Path.Combine(options.DataPath, id.Id + ".json");
        }
    }
}
