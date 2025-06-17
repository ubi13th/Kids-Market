using System;
using System.Collections.Generic;
using System.Globalization;
using _App.Services;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace _App.ExtraReward
{
    public class DaySelectorUI : MonoBehaviour
    {
        [SerializeField] private Transform dayButtonContainer;
        [SerializeField] private Button dayButtonPrefab;
        [SerializeField] private Color selectedColor, unselectedColor;

        private readonly Dictionary<DayOfWeek, Button> _dayButtons = new();
        private readonly HashSet<DayOfWeek> _selectedDays = new();

        public event Action<HashSet<DayOfWeek>> OnSelectionChanged;

        private bool _isAdmin;

        public IReadOnlyCollection<DayOfWeek> SelectedDays => _selectedDays;

        public void Initialize(bool isAdmin, IEnumerable<DayOfWeek> preselectedDays = null)
        {
            _isAdmin = isAdmin;
            
            _selectedDays.Clear();
            foreach (Transform child in dayButtonContainer)
                Destroy(child.gameObject);

            DayOfWeek lastDay = (DayOfWeek)(((int)DateService.WeekStartsOn + 6) % 7);

            foreach (DayOfWeek day in DateService.OrderedDaysOfWeek)
            {
                var button = Instantiate(dayButtonPrefab, dayButtonContainer);
                var dayText = button.transform.Find("Day")?.GetComponent<TextMeshProUGUI>();
                var line = button.transform.Find("Line").GetComponent<Image>();

                if (dayText != null)
                    dayText.text = day.ToString().Substring(0, 1); // ✅ "Monday" → "M"

                if (line != null)
                    line.gameObject.SetActive(day != lastDay); // ✅ Hide line if it's the last day

                bool isSelected = preselectedDays != null && new HashSet<DayOfWeek>(preselectedDays).Contains(day);
                if (isSelected)
                    _selectedDays.Add(day);

                HighlightDayButton(button, line, isSelected);

                var capturedDay = day;

                if (isAdmin)
                {
                    button.onClick.AddListener(() =>
                    {
                        if (_selectedDays.Contains(capturedDay))
                            _selectedDays.Remove(capturedDay);
                        else
                            _selectedDays.Add(capturedDay);

                        HighlightDayButton(button, line, _selectedDays.Contains(capturedDay));
                        OnSelectionChanged?.Invoke(_selectedDays);
                    });
                }
                else
                {
                    button.onClick.RemoveAllListeners();
                }

                _dayButtons[day] = button;
            }
        }

        private void HighlightDayButton(Button button, Image line, bool isSelected)
        {
            var bg = button.GetComponent<Image>();
            if (bg != null)
                bg.color = isSelected ? selectedColor : unselectedColor;
            
            if (line != null)
                line.color = isSelected ? selectedColor : unselectedColor;
        }
    }
}
