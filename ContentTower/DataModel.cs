using System.Text.Json.Serialization;

namespace ContentTower
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum StoreType
    {
        Default,
        Temporary,
        Permanent
    }

    public interface IId
    {
        string Id { get; }
    }

    public class BaseId : IEquatable<BaseId?>, IId
    {
        public BaseId(string id)
        {
            Id = id;
        }

        public string Id { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as BaseId);
        }

        public bool Equals(BaseId? other)
        {
            return other is not null &&
                   Id == other.Id;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id);
        }

        public override string ToString()
        {
            return $"'{Id.Substring(0, 5)}..{Id.Substring(Id.Length - 3)}'";
        }

        public static bool operator ==(BaseId? left, BaseId? right)
        {
            return EqualityComparer<BaseId>.Default.Equals(left, right);
        }

        public static bool operator !=(BaseId? left, BaseId? right)
        {
            return !(left == right);
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

    public class FileMetadata
    {
        public Cid Cid { get; set; } = new Cid(string.Empty);
        public List<PinId> PinIds { get; set; } = new List<PinId>();

        public string Name { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long Length { get; set; }
    }

    public class PinData
    {
        public PinId PinId { get; set; } = new PinId(string.Empty);
        public List<Cid> Cids { get; set; } = new List<Cid>();

        public StoreType StoreType { get; set; }
        public DateTime CreateUtc { get; set; }
        public DateTime LastActivityUtc { get; set; }
    }
}
