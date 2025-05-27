using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using _App.AdminDashboard;
using _App.Bootstrap;
using _App.Services;
using Firebase.Extensions;


public class SmartContractCreationStep2 : MonoBehaviour
{
    [Header("UI References")] 
    [SerializeField] private Image icon;
    [SerializeField] private TMP_InputField contractTitleText;
    [SerializeField] private TMP_Text rewardText;
    [SerializeField] private TMP_InputField rewardInputField;
    [SerializeField] private TMP_Text dueTimeText;
    [SerializeField] private TextMeshProUGUI startDateText;
    [SerializeField] private TextMeshProUGUI monthLabelText;
    [SerializeField] private TextMeshProUGUI dropDownLabelText;
    [SerializeField] private TextMeshProUGUI createEditScText;
    [Space(10)]
    [Header("UI Toggles")]
    [SerializeField] private TMP_Dropdown repeatDropdown;
    [SerializeField] private Toggle dueTimeToggle;
    [SerializeField] private Toggle photoProofToggle;
    [SerializeField] private Toggle parentalApprovalToggle;
    [SerializeField] private Toggle notifyOnThisDeviceToggle;
    [SerializeField] private Toggle saveAsPresetToggle;
    [Space(10)]
    [Header("UI Buttons")] 
    [SerializeField] private Button backButton;
    [SerializeField] private Button openIconPickerButton;
    [SerializeField] private Button rewardPlusButton;
    [SerializeField] private Button rewardMinusButton;
    [SerializeField] private Button dueTimeButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button deleteButton;
    [SerializeField] private Button weekDaysButtonPrefab;
    [SerializeField] private Button exitDueTimeSelectionPanelButton;
    [Space(10)]
    [SerializeField] private Button startDateButton;
    [SerializeField] private Button prevMonthButton;
    [SerializeField] private Button nextMonthButton;
    [Space(10)]
    [Header("UI Prefabs")] 
    [SerializeField] private Transform assignsContainerTop;
    [SerializeField] private GameObject currentChildAvatarPrefab;
    [SerializeField] private Transform assignsContainer;
    [SerializeField] private GameObject assignChildAvatarPrefab;
    [SerializeField] private Transform daysGridTransform;
    [SerializeField] private Button calendarDayPrefab;
    [SerializeField] private GameObject childRotateBlockPrefab;
    [SerializeField] private GameObject assignChildAvatarRotatePrefab;
    [Space(10)]
    [Header("UI Sliders")]
    [SerializeField] private GameObject dueTimeSlider;
    [SerializeField] private GameObject photoProofSlider;
    [SerializeField] private GameObject parentalProofSlider;
    [SerializeField] private GameObject remindOnThisDeviceSlider;
    [SerializeField] private GameObject saveAsPresetSlider;
    [Space(10)]
    [Header("UI Assigns")]
    [SerializeField] private GameObject assignsBlockText;
    [SerializeField] private GameObject assignsBlock;
    [SerializeField] private GameObject assignsSelectionButtonsBlock;
    [SerializeField] private Button everyoneButton;
    [SerializeField] private Button anyoneButton;
    [SerializeField] private Button rotateButton;
    [SerializeField] private TextMeshProUGUI assignsOptionDescriptionText;
    [SerializeField] private Transform childRotateContainer;
    [Space(10)]
    [Header("UI Blocks")]
    [SerializeField] private GameObject startDateBlock;
    [SerializeField] private GameObject calendarPanel;
    [SerializeField] private GameObject repeatDropdownBlock;
    [SerializeField] private GameObject dueTimeSelectorPanel;
    [SerializeField] private GameObject iconNamePanel;
    [SerializeField] private GameObject contractCreatorPanel;
    [SerializeField] private GameObject rewardBlock;
    [SerializeField] private ScrollMechanic hourScroll;
    [SerializeField] private ScrollMechanic minuteScroll;

    [SerializeField] private SmartContractCreationStep1 step1;
    [SerializeField] private ContractIconPickerUI contractIconPickerUI;

    [SerializeField] private Scrollbar scrollbar;
    
    [Header("Tick Sound")]
    [SerializeField] private AudioClip tickSound;
    [SerializeField] private AudioSource hourAudioSource;
    [SerializeField] private AudioSource minuteAudioSource;
    private int _lastDisplayedHour = -1;
    private int _lastDisplayedMinute = -1;

    private float _childBalance;
    private float _rewardAmount;
    private RewardType _currentRewardType;

    private bool _isDueTimeSelectorOn = false;
    
    private DateTime _dueTime;
    private IAdminDashboardPresenter _presenter;
    private DateTime _shownMonth;
    
    private readonly Dictionary<string, bool> _assignedChildrenList = new(); // key = UID
    
    private Coroutine _delayedRedraw;

    private DateTime _selectedDate;
    private readonly Color _orangeColor = new Color(1f, 0.6f, 0f);       // Today
    private readonly Color _greenColor = new Color(0.2f, 0.8f, 0.2f);    // Selected
    private readonly Color _whiteFaded = new Color(1f, 1f, 1f, 0.2f);    // Future
    
    //private readonly Color _greyColor = new Color(0.5529f, 0.5647f, 0.6039f);
    //private readonly Color _darkGreyColor = new Color(0.2823f, 0.2626f, 0.3333f);
    
    private void OnEnable()
    {
        InitializeUI();
    }

    private void OnDisable()
    {
        openIconPickerButton.onClick.RemoveAllListeners();
        rewardPlusButton.onClick.RemoveAllListeners();
        rewardMinusButton.onClick.RemoveAllListeners();
        dueTimeButton.onClick.RemoveAllListeners();
        saveButton.onClick.RemoveAllListeners();
        deleteButton.onClick.RemoveAllListeners();
        backButton.onClick.RemoveAllListeners();
        exitDueTimeSelectionPanelButton.onClick.RemoveAllListeners();
        
        everyoneButton.onClick.RemoveAllListeners();
        anyoneButton.onClick.RemoveAllListeners();
        rotateButton.onClick.RemoveAllListeners();
        
        startDateButton.onClick.RemoveAllListeners();
        prevMonthButton.onClick.RemoveAllListeners();
        nextMonthButton.onClick.RemoveAllListeners();

        dueTimeToggle.onValueChanged.RemoveAllListeners();
        photoProofToggle.onValueChanged.RemoveAllListeners();
        parentalApprovalToggle.onValueChanged.RemoveAllListeners();
        notifyOnThisDeviceToggle.onValueChanged.RemoveAllListeners();
        saveAsPresetToggle.onValueChanged.RemoveAllListeners();

        rewardInputField.onValueChanged.RemoveAllListeners();
        repeatDropdown.onValueChanged.RemoveAllListeners();
        rewardInputField.onValidateInput -= ValidateRewardInput;
    }

    public void Initialize(IAdminDashboardPresenter presenter)
    {
        _presenter = presenter;
    }

    private void InitializeUI()
    {
        // ✅ Show child assignment block (if applicable)
        ShowAssignsBlockIfMultipleChildren(_presenter.GetAllChildren());
        
        openIconPickerButton.onClick.AddListener(OpenContractIconPicker);
        rewardPlusButton.onClick.AddListener(() => AdjustReward(+1));
        rewardMinusButton.onClick.AddListener(() => AdjustReward(-1));
        dueTimeButton.onClick.AddListener(OpenCloseDueTimeSelector);
        saveButton.onClick.AddListener(SaveContract);
        deleteButton.onClick.AddListener(DeleteDraft);
        backButton.onClick.AddListener(CloseSettingsPanel);

        createEditScText.text = step1.isCreatingNewContract ? "New Smart Contract" : "Edit Smart Contract";

        if (SmartContractDraft.Id == null)
        {
            SmartContractDraft.StartDate = DateTime.Today;
            startDateBlock.SetActive(true);
        }
        else
            startDateBlock.SetActive(false);
        
        startDateButton.onClick.AddListener(() =>
        {
            calendarPanel.SetActive(true);
            RenderCalendar(DateTime.Today); // show current month
        });
        prevMonthButton.onClick.AddListener(() => ChangeMonth(-1));
        nextMonthButton.onClick.AddListener(() => ChangeMonth(1));

        exitDueTimeSelectionPanelButton.onClick.AddListener(CloseDueTimeSelectionPanel);

        dueTimeToggle.onValueChanged.AddListener(OnDueTimeToggleChanged);
        photoProofToggle.onValueChanged.AddListener(OnPhotoProofToggleChanged);
        parentalApprovalToggle.onValueChanged.AddListener(OnParentalProofToggleChanged);
        notifyOnThisDeviceToggle.onValueChanged.AddListener(OnNotifyMeOnThisDeviceToggleChanged);
        saveAsPresetToggle.onValueChanged.AddListener(OnSaveAsPresetToggleChanged);
        
        rewardInputField.onValidateInput += ValidateRewardInput;
        rewardInputField.onValueChanged.AddListener(OnRewardInputChanged);
        
        LoadChildRewardConfig(SmartContractDraft.AssignedToUid);

        icon.sprite = ContractIconLoader.Load(SmartContractDraft.IconPath);
        contractTitleText.text = SmartContractDraft.Title;
        
        saveButton.interactable = !string.IsNullOrWhiteSpace(contractTitleText.text);
        contractTitleText.onValueChanged.AddListener(text => 
            saveButton.interactable = !string.IsNullOrWhiteSpace(text));
        
        _rewardAmount = SmartContractDraft.RewardAmount;
        UpdateRewardDisplay();
        
        repeatDropdownBlock.SetActive(true);

        startDateText.text = SmartContractDraft.StartDate.ToLocalTime().ToString("MMMM dd, yyyy");
        
        _dueTime = SmartContractDraft.StartDate == default ? DateTime.UtcNow.AddDays(1) : SmartContractDraft.StartDate;
        dueTimeText.text = _dueTime.ToLocalTime().ToString(@"hh\:mm");
        dueTimeToggle.isOn = false;
        if(SmartContractDraft.DueTime != TimeSpan.Zero)
            SetTime(SmartContractDraft.DueTime.Hours, SmartContractDraft.DueTime.Minutes, true);
        
        // Toggles
        photoProofToggle.isOn = SmartContractDraft.RequiresPhotoProof;
        parentalApprovalToggle.isOn = SmartContractDraft.RequiresParentalApproval;
        notifyOnThisDeviceToggle.isOn = SmartContractDraft.RequireNotificationOnThisDevice;

        // Dropdown
        // If editing (contract was loaded with a specific RepeatMode)
        if (SmartContractDraft.RepeatMode != default)
        {
            repeatDropdown.value = (int)SmartContractDraft.RepeatMode;

            if (SmartContractDraft.RepeatMode == RepeatType.SpecificDays)
            {
                OpenRepeatDaysEditor();
                var sharedDays = GetSharedRepeatDays();
                UpdateRepeatLabelFromSelectedDays(sharedDays);
            }
            else
            {
                CloseWeekDayTogglesBlock();
                dropDownLabelText.text = SmartContractDraft.RepeatMode switch
                {
                    RepeatType.EveryDay => "Every day",
                    RepeatType.Once => "Once",
                    RepeatType.AsNeeded => "As needed",
                    _ => ""
                };
            }
        }
        else
        {
            // New contract (default = EveryDay)
            SmartContractDraft.RepeatMode = RepeatType.EveryDay;
            repeatDropdown.value = (int)RepeatType.EveryDay;
            dropDownLabelText.text = "Every day";
            CloseWeekDayTogglesBlock();
        }
        
        repeatDropdown.onValueChanged.AddListener(OnRepeatModeValueChanged);

        scrollbar.value = 1;
    }

    private void OpenContractIconPicker()
    {
        contractIconPickerUI.OnIconSelected = (iconName) =>
        {
            SmartContractDraft.IconPath = iconName;
            icon.sprite = ContractIconLoader.Load(iconName);
        };

        contractIconPickerUI.gameObject.SetActive(true);
        
        Debug.Log($"contractIconPickerUI ON    REWARD = {SmartContractDraft.RewardAmount}");
    }
    
    //---------------- Load Children -------------------

    private void ShowAssignsBlockIfMultipleChildren(List<ChildModel> allChildren)
    {
        // ✅ EDIT MODE: Only show assigned child (non-interactive)
        if (!step1.isCreatingNewContract || allChildren.Count <= 1)
        {
            assignsBlockText.SetActive(false);
            assignsBlock.SetActive(false);
            assignsContainerTop.gameObject.SetActive(true);
    
            // Clear previous UI
            foreach (Transform child in assignsContainerTop)
                Destroy(child.gameObject);
            foreach (Transform child in childRotateContainer)
                Destroy(child.gameObject);
    
            // Instantiate current child's avatar
            var childGo = Instantiate(currentChildAvatarPrefab, assignsContainerTop);
            childGo.GetComponentInChildren<TextMeshProUGUI>().text = _presenter.CurrentChild.DisplayName;
    
            _assignedChildrenList.Clear();
            _assignedChildrenList[_presenter.CurrentChild.Uid] = true;
    
            HighlightChildSelection(childGo, true);
            return;
        }
    
        // ✅ CREATION MODE: Allow multiple selection
        assignsContainerTop.gameObject.SetActive(false);
        assignsBlockText.SetActive(true);
        assignsBlock.SetActive(true);
    
        foreach (Transform child in assignsContainer)
            Destroy(child.gameObject);
        foreach (Transform child in childRotateContainer)
            Destroy(child.gameObject);
    
        _assignedChildrenList.Clear();
    
        foreach (var child in allChildren)
        {
            string uid = child.Uid;
            bool isInitiallySelected = _presenter.CurrentChild.Uid == uid;
    
            var childGo = Instantiate(assignChildAvatarPrefab, assignsContainer);
            childGo.GetComponentInChildren<TextMeshProUGUI>().text = child.DisplayName;
    
            _assignedChildrenList[uid] = isInitiallySelected;
            HighlightChildSelection(childGo, isInitiallySelected);
    
            Button button = childGo.GetComponent<Button>();
            button.onClick.AddListener(() =>
            {
                // Toggle selection
                _assignedChildrenList[uid] = !_assignedChildrenList[uid];
                HighlightChildSelection(childGo, _assignedChildrenList[uid]);
            });
        }
    }

    private void HighlightChildSelection(GameObject childGo, bool isSelected)
    {
        var border = childGo?.GetComponent<Image>();
        if (border)
            border.color = isSelected ? Color.green : Color.gray;

        if (SmartContractDraft.RepeatMode == RepeatType.SpecificDays)
        {
            // Debounce full redraw (e.g., wait until mouse-up or next frame)
            if (_delayedRedraw != null)
                StopCoroutine(_delayedRedraw);

            _delayedRedraw = StartCoroutine(RedrawDaysEditorDelayed());
        }
    }

    private IEnumerator RedrawDaysEditorDelayed()
    {
        yield return new WaitForEndOfFrame(); // or small delay
        OpenRepeatDaysEditor();
        var sharedDays = GetSharedRepeatDays();
        UpdateRepeatLabelFromSelectedDays(sharedDays);
    }
    
    //------------------- Children Selection Options -------------------------
    
    
    
    // ----------------- Reward ----------------------
    private void LoadChildRewardConfig(string childUid)
    {
        FirebaseInit.DbRef.Child(AppConstants.Children).Child(childUid).GetValueAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (!task.IsCompletedSuccessfully || !task.Result.Exists)
                    return;

                var snapshot = task.Result;
                Enum.TryParse(snapshot.Child(AppConstants.RewardPreference).Value?.ToString(), out _currentRewardType);
                UpdateRewardDisplay();
            });
    }

    private void AdjustReward(int direction)
    {
        float step = _currentRewardType == RewardType.Money ? 0.25f : 1f;
        _rewardAmount = Mathf.Max(0, _rewardAmount + direction * step);
        UpdateRewardDisplay();
    }
    
    private void OnRewardInputChanged(string input)
    {
        if (!float.TryParse(input, out float parsedValue)) return;

        float step = _currentRewardType == RewardType.Money ? 0.25f : 1f;
        parsedValue = Mathf.Round(parsedValue / step) * step;
        parsedValue = (float)Math.Round(parsedValue, 2); // ✅ Force two decimal places

        _rewardAmount = Mathf.Max(0, parsedValue);
        UpdateRewardDisplay();
    }
    
    private char ValidateRewardInput(string text, int charIndex, char addedChar)
    {
        return char.IsDigit(addedChar) || (_currentRewardType == RewardType.Money && addedChar == '.' && !text.Contains(".")) ? addedChar : '\0';
    }

    private void UpdateRewardDisplay()
    {
        rewardBlock.SetActive(_currentRewardType != RewardType.None);
        string rewardString = _currentRewardType == RewardType.Money
            ? ($"{_rewardAmount:F2}")
            : ($"{_rewardAmount}");

        rewardText.text = rewardString;
        if (rewardInputField.text != rewardString)
            rewardInputField.SetTextWithoutNotify(rewardString);

        SmartContractDraft.RewardAmount = _rewardAmount;
    }
    
    //----------------- Start Date -------------------------
    
    private void ChangeMonth(int delta)
    {
        _shownMonth = _shownMonth.AddMonths(delta);
        RenderCalendar(_shownMonth);
    }
    
    private void RenderCalendar(DateTime monthToShow)
    {
        _shownMonth = monthToShow;
        ClearOldButtons();

        DateTime firstDay = new DateTime(monthToShow.Year, monthToShow.Month, 1);
        int daysInMonth = DateTime.DaysInMonth(monthToShow.Year, monthToShow.Month);

        // Disable prev month button if trying to go before today
        prevMonthButton.interactable = monthToShow > new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        prevMonthButton.gameObject.SetActive(monthToShow > new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1));

        for (var i = 0; i < daysInMonth; i++)
        {
            DateTime day = firstDay.AddDays(i);
            var buttonGo = Instantiate(calendarDayPrefab, daysGridTransform);
            var buttonBg = buttonGo.GetComponent<Image>();
            var button = buttonGo.GetComponent<Button>();
            var label = buttonGo.GetComponentInChildren<TextMeshProUGUI>();

            label.text = day.Day.ToString();

            bool isPast = day.Date < DateTime.Today;
            bool isToday = day.Date == DateTime.Today;
            bool isSelected = SmartContractDraft.StartDate.Date == day.Date;

            if (isPast)
            {
                button.interactable = false;
                label.color = _whiteFaded;
            }
            else
            {
                button.interactable = true;

                if (isSelected)
                    buttonBg.color = _greenColor;
                else if (isToday)
                    buttonBg.color = _orangeColor;
                else
                    buttonBg.color = _whiteFaded;

                button.onClick.AddListener(() =>
                {
                    SmartContractDraft.StartDate = day;
                    startDateText.text = day.ToString("MMMM dd, yyyy");
                    calendarPanel.SetActive(false);
                    RenderCalendar(_shownMonth); // re-render to apply selection highlight
                    
                    Debug.Log($"🗓 StartDate saved as: {day} | UTC: {day.ToUniversalTime()} | Local: {day.ToLocalTime()}");
                });
            }
        }

        monthLabelText.text = monthToShow.ToString("MMMM yyyy");
    }

    private void ClearOldButtons()
    {
        foreach (Transform child in daysGridTransform)
            Destroy(child.gameObject);
    }
    
    //--------------------- Repeat ------------------------------
    
    private void OpenRepeatDaysEditor()
    {
        // 🟢 Edit mode → only show the assigned child
        if (!step1.isCreatingNewContract)
        {
            string assignedUid = SmartContractDraft.AssignedToUid;
            if (string.IsNullOrEmpty(assignedUid))
                return;

            string childName = _presenter.GetAllChildren()
                .FirstOrDefault(c => c.Uid == assignedUid)?.DisplayName ?? "Unknown";

            SmartContractDraft.RepeatDaysPerChild.TryGetValue(assignedUid, out var preselectedDays);
            preselectedDays ??= new List<DayOfWeek>();

            childRotateContainer.gameObject.SetActive(true);

            foreach (Transform child in childRotateContainer)
                Destroy(child.gameObject);

            CreateChildDayBlock(assignedUid, childName, preselectedDays);

            UpdateRepeatLabelFromSelectedDays(preselectedDays);
            return;
        }

        // 🔁 Creation mode → show blocks for all selected children
        var selectedChildren = _assignedChildrenList.Where(kv => kv.Value).ToList();

        if (selectedChildren.Count == 0)
            return;

        childRotateContainer.gameObject.SetActive(true);

        foreach (Transform child in childRotateContainer)
            Destroy(child.gameObject);

        foreach (var (uid, _) in selectedChildren)
        {
            string childName = _presenter.GetAllChildren()
                .FirstOrDefault(c => c.Uid == uid)?.DisplayName ?? "Unknown";

            SmartContractDraft.RepeatDaysPerChild.TryGetValue(uid, out var preselectedDays);
            preselectedDays ??= new List<DayOfWeek>();

            CreateChildDayBlock(uid, childName, preselectedDays);
        }

        var sharedDays = GetSharedRepeatDays();
        UpdateRepeatLabelFromSelectedDays(sharedDays);
    }

    private void CreateChildDayBlock(string childUid, string childName, List<DayOfWeek> preselectedDays)
    {
        var rotateBlock = Instantiate(childRotateBlockPrefab, childRotateContainer);

        // 👤 Set child name
        var avatar = Instantiate(assignChildAvatarRotatePrefab, rotateBlock.transform.GetChild(0));
        avatar.GetComponentInChildren<TextMeshProUGUI>().text = childName;

        // 🗓 Set up weekday buttons
        var dayButtonRow = rotateBlock.transform.GetChild(1);
        SmartContractDraft.RepeatDaysPerChild[childUid] = new List<DayOfWeek>(preselectedDays);
        
        foreach (DayOfWeek day in DateService.OrderedDaysOfWeek)
        {
            var btn = Instantiate(weekDaysButtonPrefab, dayButtonRow);
            btn.transform.Find("Day")?.GetComponent<TextMeshProUGUI>().SetText(day.ToString().Substring(0, 1));

            HighlightDayButton(btn, preselectedDays.Contains(day));

            btn.onClick.AddListener(() =>
            {
                var list = SmartContractDraft.RepeatDaysPerChild[childUid];
                if (list.Contains(day))
                    list.Remove(day);
                else
                    list.Add(day);

                HighlightDayButton(btn, list.Contains(day));

                var sharedDays = GetSharedRepeatDays();
                UpdateRepeatLabelFromSelectedDays(sharedDays);
            });
        }
    }
    
    private void OnRepeatModeValueChanged(int value)
    {
        SmartContractDraft.RepeatMode = (RepeatType)value;

        switch ((RepeatType)value)
        {
            case RepeatType.EveryDay:
                dropDownLabelText.text = "Every day";
                CloseWeekDayTogglesBlock();
                break;

            case RepeatType.Once:
                dropDownLabelText.text = "Once";
                CloseWeekDayTogglesBlock();
                break;

            case RepeatType.AsNeeded:
                dropDownLabelText.text = "As needed";
                CloseWeekDayTogglesBlock();
                break;
            
            case RepeatType.SpecificDays:
                OpenRepeatDaysEditor();
                break;
        }
    }
    
    private List<DayOfWeek> GetSharedRepeatDays()
    {
        var selectedUids = _assignedChildrenList
            .Where(kv => kv.Value)
            .Select(kv => kv.Key)
            .ToList();

        if (selectedUids.Count == 0)
            return new List<DayOfWeek>();

        // Start with the first selected child's days
        if (!SmartContractDraft.RepeatDaysPerChild.TryGetValue(selectedUids[0], out var shared))
            return new List<DayOfWeek>();

        var sharedDays = new HashSet<DayOfWeek>(shared);

        // Intersect with each subsequent selected child's days
        foreach (var uid in selectedUids.Skip(1))
        {
            if (!SmartContractDraft.RepeatDaysPerChild.TryGetValue(uid, out var days))
                return new List<DayOfWeek>(); // if one child has nothing, return empty

            sharedDays.IntersectWith(days);
        }

        return sharedDays.ToList();
    }
    
    private void UpdateRepeatLabelFromSelectedDays(List<DayOfWeek> days)
    {
        if (days == null || days.Count == 0)
        {
            dropDownLabelText.text = "Select days";
            return;
        }

        var sorted = days.OrderBy(d => d == DayOfWeek.Sunday ? 7 : (int)d).ToList();
        string dayString = string.Join(", ", sorted.Select(d => d.ToString().Substring(0, 3)));

        var selectedKids = _assignedChildrenList.Where(kv => kv.Value).Select(kv => kv.Key).ToList();

        if (selectedKids.Count > 1)
        {
            // For multi-child case, just say "Specific Days"
            dropDownLabelText.text = "Specific Days";
        }
        else
        {
            // For single child, show full day list
            dropDownLabelText.text = $"Every {dayString}";
        }
    }

    
    private void HighlightDayButton(Button button, bool isSelected)
    {
        var bg = button.GetComponent<Image>();
        if (bg != null)
            bg.color = isSelected ? Color.green : Color.gray;
    }

    private void CloseWeekDayTogglesBlock() => 
        childRotateContainer.gameObject.SetActive(false);

    //-------------------- Due Time ---------------------------
    
    private void OpenCloseDueTimeSelector()
    {
        if (_isDueTimeSelectorOn)
            CloseDueTimeSelectionPanel();
        else
            OpenDueTimeSelectionPanel();
    }

    private void SetTime(int hour, int minute, bool isSliderOn)
    {
        dueTimeToggle.isOn = isSliderOn;
        dueTimeText.text = $"{hour:D2}:{minute:D2}";
    }

    private void UpdateDueTimeDisplay()
    {
        if (dueTimeText != null)
        {
            int hour = hourScroll.GetCurrentValue();
            int minute = minuteScroll.GetCurrentValue();

            // 🟢 Check if value changed
            if (hour != _lastDisplayedHour)
            {
                // ✅ Play tick sound
                PlayHourTickSound();
                _lastDisplayedHour = hour;
            }
            
            if (minute != _lastDisplayedMinute)
            {
                // ✅ Play tick sound
                PlayMinuteTickSound();
                _lastDisplayedMinute = minute;
            }

            dueTimeText.text = $"{hour:D2}:{minute:D2}";
        }
    }

    private void PlayHourTickSound()
    {
        if (tickSound != null && hourAudioSource != null) 
            hourAudioSource.PlayOneShot(tickSound);
    }
    
    private void PlayMinuteTickSound()
    {
        if (tickSound != null && minuteAudioSource != null) 
            minuteAudioSource.PlayOneShot(tickSound);
    }

    private void Update()
    {
        if(!_isDueTimeSelectorOn)
            return;
        UpdateDueTimeDisplay();
    }
    
    private void OnDueTimeToggleChanged(bool isOn)
    {
        dueTimeSlider.SetActive(isOn);

        if (!isOn) 
            return;
        
        OpenDueTimeSelectionPanel();
    }
    
    private void OpenDueTimeSelectionPanel()
    {
        dueTimeSelectorPanel.SetActive(true);
        StartCoroutine(InitializeScrollValuesNextFrame());
    }
    
    private IEnumerator InitializeScrollValuesNextFrame()
    {
        yield return null; // Wait one frame for UI to rebuild
        yield return new WaitForSeconds(0.5f);

        if (SmartContractDraft.DueTime != TimeSpan.Zero)
        {
            int hour = SmartContractDraft.DueTime.Hours;
            int minute = SmartContractDraft.DueTime.Minutes;

            hourScroll.ScrollToValue(hour);
            minuteScroll.ScrollToValue(minute);
        }
        
        yield return new WaitForSeconds(0.5f);

        _isDueTimeSelectorOn = true;
    }

    private void CloseDueTimeSelectionPanel()
    {
        _isDueTimeSelectorOn = false;
        dueTimeSelectorPanel.SetActive(false);
    }

    private void SaveDueTime()
    {
        int hour = hourScroll.GetCurrentValue();     // E.g., returns 13 for "13"
        int minute = minuteScroll.GetCurrentValue(); // E.g., returns 30 for "30"

        SmartContractDraft.SetDueTime(new TimeSpan(hour, minute, 0));
        Debug.Log($"⏰ Saved DueTime: {hour:D2}:{minute:D2}");
    }

    //--------------------- Toggles ---------------------------

    private void OnPhotoProofToggleChanged(bool isOn)
    {
        SmartContractDraft.RequiresPhotoProof = isOn;
        photoProofSlider.SetActive(isOn);

        // Sync parental proof toggle state
        parentalApprovalToggle.interactable = !isOn;
        if (isOn)
        {
            SmartContractDraft.RequiresParentalApproval = true;
            parentalApprovalToggle.isOn = true;
        }

        parentalProofSlider.SetActive(SmartContractDraft.RequiresParentalApproval);
    }

    private void OnParentalProofToggleChanged(bool isOn)
    {
        SmartContractDraft.RequiresParentalApproval = isOn;
        parentalProofSlider.SetActive(isOn);
    }
    
    private void OnNotifyMeOnThisDeviceToggleChanged(bool isOn)
    {
        SmartContractDraft.RequireNotificationOnThisDevice = isOn;
        remindOnThisDeviceSlider.SetActive(isOn);
    }
    
    private void OnSaveAsPresetToggleChanged(bool isOn)
        {
            SmartContractDraft.SaveAsPreset = isOn;
            saveAsPresetSlider.SetActive(isOn);
        }

    //--------------------- Save ---------------------------
    
    private void SaveContract()
    {
        if (_rewardAmount <= 0f)
        {
            Debug.LogWarning("⚠️ Reward must be greater than 0.");
            return;
        }

        if (string.IsNullOrWhiteSpace(contractTitleText.text))
        {
            Debug.LogWarning("⚠️ Contract title is required.");
            return;
        }

        SmartContractDraft.Title = contractTitleText.text;
        SmartContractDraft.SetStartDate(SmartContractDraft.StartDate.Date);

        if (dueTimeToggle.isOn)
            SaveDueTime();

        string originalId = SmartContractDraft.Id;
        string originalAssignedUid = SmartContractDraft.AssignedToUid;

        var selectedKids = _assignedChildrenList
            .Where(kv => kv.Value)
            .Select(kv => kv.Key)
            .ToList();

        // If none selected, fallback to the originally assigned
        if (selectedKids.Count == 0 && !string.IsNullOrEmpty(originalAssignedUid))
            selectedKids.Add(originalAssignedUid);

        foreach (var childUid in selectedKids)
        {
            // In edit mode, skip unintended children
            if (!step1.isCreatingNewContract && childUid != originalAssignedUid)
                continue;

            var contract = BuildContract(childUid, originalAssignedUid, originalId);
            contract.SetStateOnDate(SmartContractDraft.StartDate, SmartContractState.ReadyToSell);
            _presenter.SaveContract(contract);
        }

        if (saveAsPresetToggle != null && saveAsPresetToggle.isOn)
            SavePreset();

        Debug.Log($"✅ Contract(s) saved for {selectedKids.Count} child(ren): {SmartContractDraft.Title}");

        SmartContractDraft.Reset();
        gameObject.SetActive(false);
        contractCreatorPanel.SetActive(false);
    }

    private SmartContractModel BuildContract(string childUid, string originalAssignedUid, string originalId)
    {
        var mode = SmartContractDraft.RepeatMode;

        var repeatDays = SmartContractDraft.RepeatDaysPerChild.TryGetValue(childUid, out var value)
            ? value
            : SmartContractDraft.RepeatDaysPerChild.Values.FirstOrDefault() ?? new List<DayOfWeek>();
        
        string dueTimeStr;
        try
        {
            dueTimeStr = SmartContractDraft.DueTime.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
        }
        catch (FormatException e)
        {
            Debug.LogWarning($"⚠️ Invalid due time format: {e.Message}. Defaulting to 00:00");
            dueTimeStr = "00:00";
        }

        return new SmartContractModel
        {
            Title = SmartContractDraft.Title,
            IconPath = SmartContractDraft.IconPath,
            RewardAmount = SmartContractDraft.RewardAmount,
            RequirePhotoProof = SmartContractDraft.RequiresPhotoProof,
            RequireParentalApproval = SmartContractDraft.RequiresParentalApproval,
            RequireNotificationOnThisDevice = SmartContractDraft.RequireNotificationOnThisDevice,
            RepeatMode = mode,
            RepeatDays = repeatDays,
            StartDate = SmartContractDraft.StartDate.ToString("yyyy-MM-dd"),
            DueTime = dueTimeStr,
            AssignedToUid = childUid,
            AdminUID = _presenter.AdminUID,
            Id = (childUid == originalAssignedUid && !string.IsNullOrEmpty(originalId)) ? originalId : null
        };
    }

    private void SavePreset()
    {
        string dueTimeStr;
        try
        {
            dueTimeStr = SmartContractDraft.DueTime.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
        }
        catch (FormatException e)
        {
            Debug.LogWarning($"⚠️ Invalid due time format: {e.Message}. Defaulting to 00:00");
            dueTimeStr = "00:00";
        }
        
        var preset = new SmartContractCustomPreset
        {
            title = SmartContractDraft.Title,
            iconPath = SmartContractDraft.IconPath,
            defaultReward = SmartContractDraft.RewardAmount,
            startDate = SmartContractDraft.StartDate.ToString("yyyy-MM-dd"),
            dueTime = dueTimeStr,
            repeatMode = SmartContractDraft.RepeatMode,
            repeatDays = new List<DayOfWeek>(
                SmartContractDraft.RepeatDaysPerChild.Values.FirstOrDefault() ?? new List<DayOfWeek>()
            ),
            requiresPhotoProof = SmartContractDraft.RequiresPhotoProof,
            requiresParentalApproval = SmartContractDraft.RequiresParentalApproval,
            requireNotificationOnThisDevice = SmartContractDraft.RequireNotificationOnThisDevice
        };

        PresetStorage.SavePreset(preset);
        SmartContractCreationStep1.OnPresetSaved?.Invoke();
    }

    /*private void SaveContract()
    {
        if (_rewardAmount <= 0f)
        {
            Debug.LogWarning("⚠️ Reward must be greater than 0.");
            return;
        }

        if (string.IsNullOrWhiteSpace(contractTitleText.text))
        {
            Debug.LogWarning("⚠️ Contract title is required.");
            return;
        }

        SmartContractDraft.Title = contractTitleText.text;
        SmartContractDraft.SetStartDate(SmartContractDraft.StartDate.Date);

        if (dueTimeToggle.isOn)
            SaveDueTime();

        string originalId = SmartContractDraft.Id;
        string originalAssignedUid = SmartContractDraft.AssignedToUid;

        var selectedKids = _assignedChildrenList
            .Where(kv => kv.Value)
            .Select(kv => kv.Key)
            .ToList();

        if (selectedKids.Count == 0 && !string.IsNullOrEmpty(originalAssignedUid))
            selectedKids.Add(originalAssignedUid);

        foreach (var childUid in selectedKids)
        {
            // ✅ Skip unintended children in edit mode
            if (!step1.isCreatingNewContract && childUid != SmartContractDraft.AssignedToUid)
                continue;
            
            var mode = SmartContractDraft.RepeatMode;
            
            var repeatDays =
                SmartContractDraft.RepeatDaysPerChild.TryGetValue(childUid, out var value)
                ? value
                : new List<DayOfWeek>(SmartContractDraft.RepeatDaysPerChild.Values.FirstOrDefault() ?? new());
            
            var contract = new SmartContractModel
            {
                Title = SmartContractDraft.Title,
                IconPath = SmartContractDraft.IconPath,
                RewardAmount = SmartContractDraft.RewardAmount,
                RequirePhotoProof = SmartContractDraft.RequiresPhotoProof,
                RequireParentalApproval = SmartContractDraft.RequiresParentalApproval,
                RequireNotificationOnThisDevice = SmartContractDraft.RequireNotificationOnThisDevice,
                RepeatMode = mode,
                RepeatDays = repeatDays,
                StartDate = SmartContractDraft.StartDate.ToString("yyyy-MM-dd"),
                DueTime = SmartContractDraft.DueTime.ToString(@"hh\:mm"),
                AssignedToUid = childUid,
                AdminUID = _presenter.AdminUID,
                Id = (childUid == originalAssignedUid && !string.IsNullOrEmpty(originalId)) ? originalId : null
            };
            
            contract.SetStateOnDate(SmartContractDraft.StartDate, SmartContractState.ReadyToSell);
            _presenter.SaveContract(contract);
        }

        if (saveAsPresetToggle != null && saveAsPresetToggle.isOn)
        {
            var preset = new SmartContractCustomPreset
            {
                title = SmartContractDraft.Title,
                iconPath = SmartContractDraft.IconPath,
                defaultReward = SmartContractDraft.RewardAmount,
                startDate = SmartContractDraft.StartDate.ToString("yyyy-MM-dd"),
                dueTime = SmartContractDraft.DueTime.ToString(@"hh\:mm"),
                repeatMode = SmartContractDraft.RepeatMode,
                repeatDays = new List<DayOfWeek>(SmartContractDraft.RepeatDaysPerChild.Values.FirstOrDefault() ?? new List<DayOfWeek>()),
                requiresPhotoProof = SmartContractDraft.RequiresPhotoProof,
                requiresParentalApproval = SmartContractDraft.RequiresParentalApproval,
                requireNotificationOnThisDevice = SmartContractDraft.RequireNotificationOnThisDevice
            };

            PresetStorage.SavePreset(preset);
            SmartContractCreationStep1.OnPresetSaved?.Invoke();
        }

        SmartContractDraft.Reset();
        gameObject.SetActive(false);
        contractCreatorPanel.SetActive(false);
    }*/
    
    //------------------------------------------------
    
    private void CloseSettingsPanel()
    {
        if (step1.isCreatingNewContract)
        {
            step1.isCreatingNewContract = false;
            gameObject.SetActive(false);
            iconNamePanel.SetActive(true);
        }
        else
        {
            gameObject.SetActive(false);
            iconNamePanel.SetActive(true);
            contractCreatorPanel.SetActive(false);
        }
    }

    private void DeleteDraft()
    {
        SmartContractDraft.Reset();
        SceneLoader.LoadHomeScene();
    }
}
