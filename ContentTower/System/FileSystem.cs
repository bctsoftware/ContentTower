namespace ContentTower.System
{
    public interface IFileSystem
    {
        bool Exists(string path);
        bool CheckCreateDir(string dataPath);
        void DeleteFile(string path);
        Stream OpenRead(string path);
        void WriteAllBytes(string path, byte[] data);
        string[] DirectoryGetFiles(string path);
        void WriteAllText(string path, string text);
    }

    public class FileSystem : IFileSystem
    {
        public bool CheckCreateDir(string path)
        {
            if (Directory.Exists(path)) return true;
            Directory.CreateDirectory(path);
            return Directory.Exists(path);
        }

        public void DeleteFile(string path)
        {
            File.Delete(path);
        }

        public string[] DirectoryGetFiles(string path)
        {
            return Directory.GetFiles(path);
        }

        public bool Exists(string path)
        {
            return File.Exists(path);
        }

        public Stream OpenRead(string path)
        {
            return File.OpenRead(path);
        }

        public void WriteAllBytes(string path, byte[] data)
        {
            File.WriteAllBytes(path, data);
        }

        public void WriteAllText(string path, string text)
        {
            File.WriteAllText(path, text);
        }
    }
}
