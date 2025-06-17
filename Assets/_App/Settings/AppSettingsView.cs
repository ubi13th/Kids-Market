using System;
using System.Globalization;
using System.Linq;
using _App.AdminDashboard;
using _App.Dashboard;
using _App.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _App.Settings
{
    public class AppSettingsView : MonoBehaviour //, ISettingsView
    {
        [Header("Panels")]
        [SerializeField] private GameObject settingsMainPanel;
        [SerializeField] private GameObject familyCanvas;
        [SerializeField] private EditSelectedUserView editProfileCanvas;
        
        [Header("Settings Buttons")]
        [SerializeField] private Button familyButton;
        [SerializeField] private Button familyPanelBackButton;
        [SerializeField] private Button editProfilePanelBackButton;
        
        [Header("Week Start")]
        [SerializeField] private GameObject weekStartsBlock;
        [SerializeField] private TMP_Dropdown weekStartDropdown;

        [Header("Family Members")]
        [SerializeField] private Transform adultsContainer;
        [SerializeField] private Transform kidsContainer;
        [SerializeField] private GameObject profileButtonPrefab;

        [Header("Edit Profile")]
        [SerializeField] private TMP_InputField titleInputField;
        [SerializeField] private Button moneyButton, pointsButton, noneButton;
        [SerializeField] private TMP_Text joinCodeText;
        [SerializeField] private Button deleteAccountButton;
        [SerializeField] private Button editNameButton;
        [SerializeField] private Image profileIcon;
        
        private IDashboardPresenter  _presenter;
        private IAdminDashboardPresenter  _adminPresenter;

        private bool _isAdmin;

        public void Initialize(IDashboardPresenter presenter)
        {
            // Only set if the presenter supports admin settings
            if (presenter is IAdminDashboardPresenter admin)
                _adminPresenter = admin;
            
            _isAdmin = UserSession.IsAdmin;
            
            // Hook up UI events
            familyButton.onClick.AddListener(OpenFamilyProfilePanel);
            familyPanelBackButton.onClick.AddListener(CloseFamilyProfilePanel);
            InitializeWeekStartDropdown();
        }

        private void OnEnable()
        {
            familyButton.gameObject.SetActive(_isAdmin);
            weekStartsBlock.SetActive(_isAdmin);
        }

        public void OpenFamilyProfilePanel()
        {
            familyCanvas.SetActive(true);

            _adminPresenter?.BuildFamilyModelAsync(ShowFamilySetup);
        }
        
        private void CloseFamilyProfilePanel()
        {
            familyCanvas.SetActive(false);
        }
        
        public void ShowFamilySetup(FamilyModel family)
        {
            // Clear containers
            foreach (Transform child in adultsContainer)
                Destroy(child.gameObject);
            foreach (Transform child in kidsContainer)
                Destroy(child.gameObject);
            
            foreach (var adult in family.Adults)
            {
                var button = Instantiate(profileButtonPrefab, adultsContainer);
                button.GetComponent<SettingsProfileButton>().Initialize(
                    adult.Uid, adult.DisplayName, adult.AvatarPath, OnProfileSelected, true);
            }

            foreach (var kid in family.Kids)
            {
                var button = Instantiate(profileButtonPrefab, kidsContainer);
                button.GetComponent<SettingsProfileButton>().Initialize(
                    kid.Uid, kid.DisplayName, kid.AvatarPath, OnProfileSelected, false);
            }
        }
        
        private void OnProfileSelected(string childId)
        {
            var child = _adminPresenter.GetAllChildren().FirstOrDefault(c => c.Uid == childId);
            if (child == null)
            {
                Debug.LogWarning($"❌ Child not found: {childId}");
                return;
            }

            CloseFamilyProfilePanel();

            editProfileCanvas.gameObject.SetActive(true);
            editProfileCanvas.LoadChildForEdit(child); // ✅ correct usage
            Debug.Log($"Selected profile for editing: {child.DisplayName}");
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