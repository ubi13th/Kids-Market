using System;
using System.Globalization;
using System.Linq;
using _App.AdminDashboard;
using _App.Services;
using TMPro;
using UnityEngine;

namespace _App.Settings
{
    public class AppSettings : MonoBehaviour
    {
        [SerializeField] private TMP_Dropdown weekStartDropdown;
        private AdminDashboardPresenter _presenter;
        
        public void Initialize(AdminDashboardPresenter presenter) => 
            _presenter = presenter;

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
            if (_presenter != null)
                _presenter.SaveWeekStartsOnData(newStartDay);
            else
                Debug.LogWarning("⚠️ AdminDashboardPresenter not set. Calendar UI not refreshed.");
        }
    }
}