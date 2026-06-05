namespace ContentTower
{
    public class FileMetadata
    {
        public string Name { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long Length { get; set; }
        public DateTime UploadUtc { get; set; }
        public DateTime LastActivityUtc { get; set; }
    }
}
