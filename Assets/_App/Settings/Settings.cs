using System;
using System.Linq;
using _App.Services;
using TMPro;
using UnityEngine;

namespace _App.Settings
{
    public class Settings : MonoBehaviour
    {
        [SerializeField] private TMP_Dropdown weekStartDropdown;

        private void Start()
        {
            weekStartDropdown.ClearOptions();
            weekStartDropdown.AddOptions(Enum.GetNames(typeof(DayOfWeek)).ToList());

            int current = (int)DateService.WeekStartsOn;
            weekStartDropdown.value = current;
            weekStartDropdown.RefreshShownValue();

            weekStartDropdown.onValueChanged.AddListener(index =>
            {
                DayOfWeek selected = (DayOfWeek)index;
                DateService.SaveWeekStartDay(selected);
            }); 
        }
    }
}