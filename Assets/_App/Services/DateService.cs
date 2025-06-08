using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using _App.Models; // Needed for SmartContractModel

namespace _App.Services
{
    public class DateService : IDateService
    {
        public DateTime GetCurrentDay() => DateTime.Today;

        public static DayOfWeek WeekStartsOn { get; private set; } = DayOfWeek.Monday;

        public static void LoadSettings()
        {
            if (PlayerPrefs.HasKey("WeekStartsOn"))
            {
                int savedValue = PlayerPrefs.GetInt("WeekStartsOn");
                WeekStartsOn = (DayOfWeek)savedValue;
            }
        }

        public static void SaveWeekStartDay(DayOfWeek day)
        {
            WeekStartsOn = day;
            PlayerPrefs.SetInt("WeekStartsOn", (int)day);
            PlayerPrefs.Save();
        }

        public List<DateTime> GetCurrentWeekDays()
        {
            DateTime today = DateTime.Today;
            int delta = WeekStartsOn - today.DayOfWeek;
            if (delta > 0) delta -= 7;

            DateTime firstDay = today.AddDays(delta);
            return Enumerable.Range(0, 7).Select(i => firstDay.AddDays(i)).ToList();
        }

        public DateTime GetWeekStart(DateTime referenceDay)
        {
            int diff = (7 + (referenceDay.DayOfWeek - WeekStartsOn)) % 7;
            return referenceDay.AddDays(-diff).Date;
        }

        public bool IsSameWeek(DateTime a, DateTime b) =>
            GetWeekStart(a) == GetWeekStart(b);

        public static DayOfWeek[] OrderedDaysOfWeek =>
            Enumerable.Range(0, 7)
                .Select(i => (DayOfWeek)(((int)WeekStartsOn + i) % 7))
                .ToArray();

        DayOfWeek IDateService.WeekStartsOn => WeekStartsOn;

        // 🔁 Get all visible dates in the future for a contract
        public List<DateTime> GetFutureVisibleDates(SmartContractModel contract, int maxDays = 365)
        {
            DateTime today = DateTime.Today;
            return Enumerable
                .Range(0, maxDays)
                .Select(offset => today.AddDays(offset))
                .Where(date => contract.IsVisibleOn(date))
                .ToList();
        }

        // 📅 Get week start dates from one point to another
        public List<DateTime> GetWeekStartDatesRange(DateTime from, DateTime to)
        {
            var start = GetWeekStart(from);
            var weeks = new List<DateTime>();

            while (start <= to)
            {
                weeks.Add(start);
                start = start.AddDays(7);
            }

            return weeks;
        }

        // ⬅️ Get previous week start
        public DateTime GetPreviousWeekStart(DateTime current) =>
            GetWeekStart(current).AddDays(-7);

        // ➡️ Get next week start
        public DateTime GetNextWeekStart(DateTime current) =>
            GetWeekStart(current).AddDays(7);
    }
}










/*using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace _App.Services
{
    public class DateService : IDateService
    {
        public DateTime GetCurrentDay() => DateTime.Today;

        public static DayOfWeek WeekStartsOn { get; private set; } = DayOfWeek.Monday;

        public static void LoadSettings()
        {
            if (PlayerPrefs.HasKey("WeekStartsOn"))
            {
                int savedValue = PlayerPrefs.GetInt("WeekStartsOn");
                WeekStartsOn = (DayOfWeek)savedValue;
            }
        }

        public static void SaveWeekStartDay(DayOfWeek day)
        {
            WeekStartsOn = day;
            PlayerPrefs.SetInt("WeekStartsOn", (int)day);
            PlayerPrefs.Save();
        }

        public List<DateTime> GetCurrentWeekDays()
        {
            DateTime today = DateTime.Today;
            int delta = WeekStartsOn - today.DayOfWeek;
            if (delta > 0) delta -= 7;

            DateTime firstDay = today.AddDays(delta);
            return Enumerable.Range(0, 7).Select(i => firstDay.AddDays(i)).ToList();
        }

        public DateTime GetWeekStart(DateTime referenceDay)
        {
            int diff = (7 + (referenceDay.DayOfWeek - WeekStartsOn)) % 7;
            return referenceDay.AddDays(-diff).Date;
        }

        public bool IsSameWeek(DateTime a, DateTime b) => 
            GetWeekStart(a) == GetWeekStart(b);
        
        public static DayOfWeek[] OrderedDaysOfWeek
        {
            get
            {
                return Enumerable.Range(0, 7)
                    .Select(i => (DayOfWeek)(((int)WeekStartsOn + i) % 7))
                    .ToArray();
            }
        }

        DayOfWeek IDateService.WeekStartsOn => WeekStartsOn;
    }
}*/