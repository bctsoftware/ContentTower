namespace ContentTower
{
    public static class Utils
    {
        public static string FormatDuration(TimeSpan d)
        {
            var result = "";
            if (d.Days > 0) result += $"{d.Days} days, ";
            if (d.Hours > 0) result += $"{d.Hours} hours, ";
            if (d.Minutes > 0) result += $"{d.Minutes} mins, ";
            if (d.Seconds > 0) result += $"{d.Seconds} secs";
            if (d.Seconds == 0 && d.Milliseconds > 0) result += $"{d.Milliseconds} ms";
            if (result == "") result = "0 secs";
            return result;
        }
    }
}
