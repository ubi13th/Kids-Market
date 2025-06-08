using System;
using System.Collections.Generic;
using _App.Models;

namespace _App.Services
{
    public interface IDateService
    {
        DateTime GetCurrentDay();
        List<DateTime> GetCurrentWeekDays();
        DateTime GetWeekStart(DateTime referenceDay);
        bool IsSameWeek(DateTime a, DateTime b);
        DayOfWeek WeekStartsOn { get; }

        // ✅ Newly added:
        List<DateTime> GetFutureVisibleDates(SmartContractModel contract, int maxDays = 365);
        List<DateTime> GetWeekStartDatesRange(DateTime from, DateTime to);
        DateTime GetPreviousWeekStart(DateTime current);
        DateTime GetNextWeekStart(DateTime current);
    }
}