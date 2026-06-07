namespace ContentTower.System
{
    public interface ITime
    {
        DateTime UtcNow();
        Task Sleep(TimeSpan span, CancellationToken ct);
    }

    public class Time : ITime
    {
        public DateTime UtcNow()
        {
            return DateTime.UtcNow;
        }

        public async Task Sleep(TimeSpan span, CancellationToken ct)
        {
            await Task.Delay(span, ct);
        }
    }
}
