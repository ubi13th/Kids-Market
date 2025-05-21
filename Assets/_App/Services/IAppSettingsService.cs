using System;

namespace _App.Services
{
    public interface IAppSettingsService
    {
        void SaveWeekStartsOn(DayOfWeek day, string adminUid);
        void LoadWeekStartsOn(string adminUid, Action<DayOfWeek> onLoaded);
        void LoadAdminWeekStartsOn(string childUid, Action<DayOfWeek> onLoaded);
    }
}