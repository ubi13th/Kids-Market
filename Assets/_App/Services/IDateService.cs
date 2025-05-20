using System;
using System.Collections.Generic;

namespace _App.Services
{
    public interface IDateService
    {
        DateTime GetCurrentDay();
        List<DateTime> GetCurrentWeekDays();
        DateTime GetWeekStart(DateTime referenceDay);
        bool IsSameWeek(DateTime a, DateTime b);

        // Optional, but recommended for testability/configuration
        DayOfWeek WeekStartsOn { get; }
    }
}