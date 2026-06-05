namespace ContentTower.System
{
    public interface ITime
    {
        DateTime UtcNow();
    }

    public class Time : ITime
    {
        public DateTime UtcNow()
        {
            return DateTime.UtcNow;
        }
    }
}
