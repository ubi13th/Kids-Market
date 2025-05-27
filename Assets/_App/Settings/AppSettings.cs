using System;
using System.Globalization;
using System.Linq;
using _App.AdminDashboard;
using _App.Dashboard;
using _App.Services;
using TMPro;
using UnityEngine;

namespace _App.Settings
{
    public class AppSettings : MonoBehaviour
    {
        [SerializeField] private TMP_Dropdown weekStartDropdown;
        private IAdminDashboardPresenter  _adminPresenter;
        
        public void Initialize(IDashboardPresenter presenter)
        {
            // Only set if the presenter supports admin settings
            if (presenter is IAdminDashboardPresenter admin)
                _adminPresenter = admin;
        }

        private void Start()
        {
            InitializeWeekStartDropdown();
        }
        
        private void InitializeWeekStartDropdown()
        {
            weekStartDropdown.ClearOptions();

            // ✅ Localized full names (e.g., "Monday", "Tuesday", ...)
            var days = Enumerable.Range(0, 7)
                .Select(i => CultureInfo.CurrentCulture.DateTimeFormat.GetDayName((DayOfWeek)i))
                .ToList();

            // ✅ OR: Short 3-letter format (e.g., "Mon", "Tue", ...)
            // var days = Enumerable.Range(0, 7)
            //     .Select(i => CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedDayName((DayOfWeek)i))
            //     .ToList();

            weekStartDropdown.AddOptions(days);

            int selectedIndex = (int)DateService.WeekStartsOn;
            weekStartDropdown.SetValueWithoutNotify(selectedIndex);
            weekStartDropdown.onValueChanged.AddListener(OnWeekStartChanged);
        }
        
        private void OnWeekStartChanged(int newIndex)
        {
            var newStartDay = (DayOfWeek)newIndex;
            DateService.SaveWeekStartDay(newStartDay);

            Debug.Log($"📆 Week now starts on: {newStartDay}");

            // ✅ Rebuild calendar if active
            if (_adminPresenter != null)
                _adminPresenter.SaveWeekStartsOnData(newStartDay);
            else
                Debug.LogWarning("⚠️ AdminDashboardPresenter not set. Calendar UI not refreshed.");
        }
    }
}