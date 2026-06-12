using System.Text.Json.Serialization;

namespace ContentTower
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum StoreType
    {
        Default,
        TemporaryFile,
        PermanentFile
    }

    public interface IId
    {
        string Id { get; }
    }

    public class Cid : IId
    {
        public Cid(string id)
        {
            Id = id;
        }

        public string Id { get; set; }

        public override string ToString()
        {
            return $"'{Id.Substring(0, 5)}..{Id.Substring(Id.Length - 3)}'";
        }
    }

    public class PinId : IId
    {
        public PinId(string id)
        {
            Id = id;
        }

        public string Id { get; set; }

        public override string ToString()
        {
            return $"'{Id}'";
        }
    }

    public class FileMetadata
    {
        public Cid Cid { get; set; } = new Cid(string.Empty);
        public PinId[] PinIds { get; set; } = Array.Empty<PinId>();

        public string Name { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long Length { get; set; }
    }

    public class PinData
    {
        public PinId PinId { get; set; } = new PinId(string.Empty);
        public Cid[] Cids { get; set; } = Array.Empty<Cid>();

        public StoreType StoreType { get; set; }
        public DateTime CreateUtc { get; set; }
        public DateTime LastActivityUtc { get; set; }
    }
}
