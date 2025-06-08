using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _App.Reports
{
    using System;
    using TMPro;
    using UnityEngine;
    using UnityEngine.UI;
    using _App.Services;

    public class WeekNavigator : MonoBehaviour
    {
        [SerializeField] private Button previousWeekButton;
        [SerializeField] private Button nextWeekButton;
        [SerializeField] private TextMeshProUGUI weekLabel;
        
        [SerializeField] private Transform daysContainer;
        [SerializeField] private GameObject historyDayPrefab;

        public Action<DateTime> OnWeekChanged;

        private int _weekOffset = 0;

        private void Start()
        {
            DateService.LoadSettings(); // 👈 Ensure saved start day is applied

            previousWeekButton.onClick.AddListener(() => ChangeWeek(-1));
            nextWeekButton.onClick.AddListener(() => ChangeWeek(1));

            UpdateLabel();
            OnWeekChanged?.Invoke(GetCurrentWeekStart());
        }

        public void Show(DateTime weekStart)
        {
            foreach (Transform child in daysContainer)
                Destroy(child.gameObject);

            var orderedDays = DateService.OrderedDaysOfWeek;

            foreach (var dayOfWeek in orderedDays)
            {
                DateTime day = weekStart.StartOfWeek(DateService.WeekStartsOn)
                    .AddDays((int)((7 + dayOfWeek - DateService.WeekStartsOn) % 7));

                GameObject go = Instantiate(historyDayPrefab, daysContainer);
                var label = go.GetComponentInChildren<TextMeshProUGUI>();

                label.text = $"{day:ddd}"; //\n{day:MM/dd}";
            }
        }

        private void ChangeWeek(int direction)
        {
            _weekOffset += direction;
            UpdateLabel();
            OnWeekChanged?.Invoke(GetCurrentWeekStart());
        }

        public DateTime GetCurrentWeekStart()
        {
            DateService ds = new DateService();
            var reference = DateTime.Today.AddDays(_weekOffset * 7);
            return ds.GetWeekStart(reference);
        }

        private void UpdateLabel()
        {
            var monday = GetCurrentWeekStart();
            var sunday = monday.AddDays(6);

            if (_weekOffset == 0)
                weekLabel.text = "This Week";
            else if (_weekOffset == -1)
                weekLabel.text = "Last Week";
            else if (_weekOffset == 1)
                weekLabel.text = "Next Week";
            else
                weekLabel.text = $"{monday:MMM d} – {sunday:MMM d}";
        }
    }
}