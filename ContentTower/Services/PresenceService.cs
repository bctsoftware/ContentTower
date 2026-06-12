using ContentTower.System;

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
        private readonly IFileSystem fs;

        public PresenceService(IFileSystem fs)
        {
            this.fs = fs;
        }

        public bool IsPresent(Cid cid)
        {
            if (exists.Contains(cid.Id)) return true;
            if (ExistsOnFs(cid))
            {
                exists.Add(cid.Id);
                return true;
            }
            return false;
        }

        public void ClearPresence(Cid cid)
        {
            doesntExist.Add(cid.Id);
            exists.Remove(cid.Id);

            CheckCaches();
        }

        public void SetPresence(Cid cid)
        {
            exists.Add(cid.Id);
            doesntExist.Remove(cid.Id);

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
