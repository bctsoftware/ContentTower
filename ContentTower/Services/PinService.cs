namespace ContentTower.Services
{
    public interface IPinService
    {
        bool Exists(PinId pinId);
        Task Attach(Cid cid, PinId[] pinIds);
        Task<PinId[]> Create(StoreType[] types, Cid cid);
    }

    public class PinService : IPinService
    {
    }
}
