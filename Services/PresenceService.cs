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
        private readonly HashSet<string> exists = new HashSet<string>();
        private readonly HashSet<string> doesntExist = new HashSet<string>();
        private readonly IFileSystemService fs;

        public PresenceService(IFileSystemService fs)
        {
            this.fs = fs;
        }

        public bool IsPresent(Cid cid)
        {
            if (exists.Contains(cid.Hash)) return true;
            if (ExistsOnFs(cid))
            {
                exists.Add(cid.Hash);
                return true;
            }
            return false;
        }

        public void ClearPresence(Cid cid)
        {
            doesntExist.Add(cid.Hash);
            exists.Remove(cid.Hash);

            CheckCaches();
        }

        public void SetPresence(Cid cid)
        {
            exists.Add(cid.Hash);
            doesntExist.Remove(cid.Hash);

            CheckCaches();
        }

        private bool ExistsOnFs(Cid cid)
        {
            return fs.Exists(cid);
        }

        private void CheckCaches()
        {
            if (exists.Count > 100000) exists.Clear();
            if (doesntExist.Count > 100000) doesntExist.Clear();
        }
    }
}
