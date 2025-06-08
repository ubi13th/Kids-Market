using System;

public static class DateTimeExtensions
{
    public static DateTime StartOfWeek(this DateTime date, DayOfWeek desiredStart)
    {
        int diff = (7 + (date.DayOfWeek - desiredStart)) % 7;
        return date.AddDays(-1 * diff).Date;
    }
}