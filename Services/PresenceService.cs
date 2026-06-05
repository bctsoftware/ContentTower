namespace ContentTower.Services
{
    public interface IPresenceService
    {
        bool IsPresent(Cid cid);
        void SetPresence(Cid cid);
        void ClearPresence(Cid cid);
    }

    public class PresenceService : IPresenceService
    {
        public bool IsPresent(Cid cid)
        {
            throw new NotImplementedException();
        }

        public void ClearPresence(Cid cid)
        {
            throw new NotImplementedException();
        }

        public void SetPresence(Cid cid)
        {
            throw new NotImplementedException();
        }
    }
}
