namespace Pix2d.UI.Common.Extensions;

public static class TimeSpanExtensions
{
    public static string FormatTimespan(this TimeSpan period)
    {
        if (period.TotalSeconds <= 10)
            return "0-10s";

        if (period.TotalSeconds <= 30)
            return "10-30s";

        if (period.TotalSeconds <= 60)
            return "30s-1m";

        if (period.TotalMinutes <= 30)
            return "1-30m";

        if (period.TotalMinutes <= 60)
            return "30-1h";

        if (period.TotalHours <= 5)
            return "1-5h";

        if (period.TotalHours <= 24)
            return "5-24h";

        return Math.Round(period.TotalDays / 10) * 10 + "+days";
    }

}