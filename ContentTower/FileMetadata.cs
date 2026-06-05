using ContentTower.Services;

namespace ContentTower
{
    public enum StoreRequestType
    {
        Default,
        TemporaryFile,
        PermanentFile
    }

    public class FileMetadata
    {
        public Cid Cid { get; set; } = new Cid(string.Empty);
        public string Name { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long Length { get; set; }
        public StoreRequestType StoreType { get; set; }
        public DateTime UploadUtc { get; set; }
        public DateTime LastActivityUtc { get; set; }
    }
}
