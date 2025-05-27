using System;
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
}