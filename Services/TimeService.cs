namespace ContentTower.Services
{
    public interface ITimeService
    {
        DateTime UtcNow();
    }

    public class TimeService : ITimeService
    {
        public DateTime UtcNow()
        {
            return DateTime.UtcNow;
        }
    }
}
