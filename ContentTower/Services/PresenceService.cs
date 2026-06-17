namespace ContentTower.Services
{
    public interface IPresenceService
    {
        bool IsPresent(IId id);
        void SetPresence(IId id);
        void ClearPresence(IId id);
    }

    public class PresenceService : IPresenceService
    {
        private readonly HashSet<string> exists = new HashSet<string>();
        private readonly HashSet<string> doesntExist = new HashSet<string>();
        private readonly IObjectStoreService objectStoreService;

        public PresenceService(IObjectStoreService objectStoreService)
        {
            this.objectStoreService = objectStoreService;
        }

        public bool IsPresent(IId id)
        {
            if (exists.Contains(id.Id)) return true;
            if (ExistsOnFs(id))
            {
                exists.Add(id.Id);
                return true;
            }
            return false;
        }

        public void ClearPresence(IId id)
        {
            doesntExist.Add(id.Id);
            exists.Remove(id.Id);

            CheckCaches();
        }

        public void SetPresence(IId id)
        {
            exists.Add(id.Id);
            doesntExist.Remove(id.Id);

            CheckCaches();
        }

        private bool ExistsOnFs(IId id)
        {
            return objectStoreService.Exists(id);
        }

        private void CheckCaches()
        {
            if (exists.Count > 100000) exists.Clear();
            if (doesntExist.Count > 100000) doesntExist.Clear();
        }
    }
}
