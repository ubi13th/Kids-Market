using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using _App.AdminDashboard;
using _App.Bootstrap;
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
    //[SerializeField] private GameObject weekDaysToggleBlock;
    [SerializeField] private GameObject iconNamePanel;
    [SerializeField] private GameObject contractCreatorPanel;
    [SerializeField] private GameObject rewardBlock;
    
    [SerializeField] private ScrollRect hourScroll;
    [SerializeField] private RectTransform hourContent;
    [SerializeField] private GameObject hourItemPrefab;
    [SerializeField] private ScrollRect minuteScroll;
    [SerializeField] private RectTransform minuteContent;
    [SerializeField] private GameObject minuteItemPrefab;
    
    [SerializeField] private InfiniteScrollTimePicker hourPicker;
    [SerializeField] private InfiniteScrollTimePicker minutePicker;

    private float _hourItemHeight;
    private float _minuteItemHeight;
    //private int _selectedHour = 0;
    //private int _selectedMinute = 0;
    private bool _snappingHour;
    private bool _snappingMinute;
    private bool _isMinuteDragging;

    [SerializeField] private SmartContractCreationStep1 step1;
    [SerializeField] private ContractIconPickerUI contractIconPickerUI;

    private float _childBalance;
    private float _rewardAmount;
    private RewardType _currentRewardType;

    private DateTime _dueTime;
    private AdminDashboardPresenter _presenter;
    private DateTime _shownMonth;
    
    private readonly Dictionary<string, bool> _assignedChildrenList = new(); // key = UID
    
    private Coroutine _delayedRedraw;

    //private readonly List<(Button button, DateTime date)> _calendarButtonData = new();
    //private List<DayOfWeek> _sharedRepeatDays = new();

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

    public void Initialize(AdminDashboardPresenter presenter)
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
        dueTimeSlider.SetActive(false);
        
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

        dueTimeSlider.SetActive(SmartContractDraft.DueTime != TimeSpan.Zero);
        CloseDueTimeSelectionPanel();
        SetTime(SmartContractDraft.DueTime.Hours, SmartContractDraft.DueTime.Minutes);
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
    
    // private void OpenCloseAssignsSelectionButtonsBlock()
    // {
    //     string originalAssignedUid = SmartContractDraft.AssignedToUid;
    //
    //     // Get selected UIDs (multi-child support)
    //     var selected = _assignedChildrenList.Where(kv => kv.Value).Select(kv => kv.Key).ToList();
    //     assignsSelectionButtonsBlock.SetActive(selected.Count > 1 && !string.IsNullOrEmpty(originalAssignedUid));
    //
    //     if (selected.Count > 1 && !string.IsNullOrEmpty(originalAssignedUid))
    //         OnEveryoneSelected();
    // }
    
    //private void HighlightButtons(Image buttonBg, bool isSelected) => 
        //buttonBg.color = isSelected ? _greyColor : _darkGreyColor;

    /*private void OnRotateSelected(List<ChildModel> allChildren)
    {
        assignsOptionDescriptionText.text = "Takes turns on specific days.";
        childRotateContainer.gameObject.SetActive(true);
        SmartContractDraft.RepeatDaysPerChild.Clear();

        foreach (Transform child in childRotateContainer)
            Destroy(child.gameObject);

        foreach (var child in allChildren)
        {
            if (!_assignedChildrenList.TryGetValue(child.Uid, out bool isSelected) || !isSelected)
                continue;

            var rotateBlock = Instantiate(childRotateBlockPrefab, childRotateContainer);
            var avatar = Instantiate(assignChildAvatarRotatePrefab, rotateBlock.transform.GetChild(0));
            avatar.GetComponentInChildren<TextMeshProUGUI>().text = child.DisplayName;

            var dayButtonRow = rotateBlock.transform.GetChild(1); // for days container
            SmartContractDraft.RepeatDaysPerChild[child.Uid] = new();
            
            DayOfWeek[] orderedDays = new[]
            {
                DayOfWeek.Monday,
                DayOfWeek.Tuesday,
                DayOfWeek.Wednesday,
                DayOfWeek.Thursday,
                DayOfWeek.Friday,
                DayOfWeek.Saturday,
                DayOfWeek.Sunday
            };

            foreach (DayOfWeek day in orderedDays)
            {
                var btn = Instantiate(weekDaysButtonPrefab, dayButtonRow);
                btn.transform.Find("Day")?.GetComponent<TextMeshProUGUI>().SetText(day.ToString().Substring(0, 1));
                HighlightDayButton(btn, false);

                btn.onClick.AddListener(() =>
                {
                    var list = SmartContractDraft.RepeatDaysPerChild[child.Uid];
                    if (list.Contains(day))
                        list.Remove(day);
                    else
                        list.Add(day);

                    HighlightDayButton(btn, list.Contains(day));
                });
            }
        }
    }*/
    
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

        DayOfWeek[] orderedDays = new[]
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
            DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
        };

        foreach (var day in orderedDays)
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

    
    
    
    
    
    
    /*private void OpenWeekDayTogglesBlock(List<DayOfWeek> preselectedDays = null)
    {
        weekDaysToggleBlock.SetActive(true);

        foreach (Transform child in weekDaysToggleBlock.transform)
            Destroy(child.gameObject);

        _calendarButtonData.Clear();

        // ✅ Use provided days or fallback
        _sharedRepeatDays = preselectedDays != null 
            ? new List<DayOfWeek>(preselectedDays) 
            : GetSharedRepeatDays();

        DayOfWeek[] orderedDays = new[]
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
            DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
        };

        foreach (DayOfWeek day in orderedDays)
        {
            var button = Instantiate(weekDaysButtonPrefab, weekDaysToggleBlock.transform);
            button.transform.Find("Day")?.GetComponent<TextMeshProUGUI>().SetText(day.ToString().Substring(0, 1));

            _calendarButtonData.Add((button, DateTime.Today)); // dummy

            HighlightDayButton(button, _sharedRepeatDays.Contains(day));

            DayOfWeek capturedDay = day;

            button.onClick.AddListener(() =>
            {
                if (_sharedRepeatDays.Contains(capturedDay))
                    _sharedRepeatDays.Remove(capturedDay);
                else
                    _sharedRepeatDays.Add(capturedDay);

                ApplyRepeatDaysToAllSelectedChildren(_sharedRepeatDays);
                HighlightDayButton(button, _sharedRepeatDays.Contains(capturedDay));
                UpdateRepeatLabelFromSelectedDays(_sharedRepeatDays);
            });
        }

        UpdateRepeatLabelFromSelectedDays(_sharedRepeatDays);
    }*/
    
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


            // case RepeatType.SpecificDays:
            //     var sharedDays = GetSharedRepeatDays();
            //     OpenWeekDayTogglesBlock(sharedDays);
            //     break;
        }
    }
    
    // private void ApplyRepeatDaysToAllSelectedChildren(List<DayOfWeek> days)
    // {
    //     foreach (var kvp in _assignedChildrenList)
    //     {
    //         if (!kvp.Value) continue; // only selected children
    //         SmartContractDraft.RepeatDaysPerChild[kvp.Key] = new List<DayOfWeek>(days);
    //     }
    // }
    
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

    private void SetTime(int hour, int minute)
    {
        hourPicker.ScrollToValue(hour);
        minutePicker.ScrollToValue(minute);
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (dueTimeText != null)
            dueTimeText.text = $"{hourPicker.GetSelectedValue():D2}:{minutePicker.GetSelectedValue():D2}";
    }

    private void Update()
    {
        UpdateDisplay();
    }
    
    private void OnDueTimeToggleChanged(bool isOn)
    {
        dueTimeSlider.SetActive(isOn);
        dueTimeSelectorPanel.SetActive(isOn);

        if (isOn)
        {
            hourPicker.enabled = true;
            minutePicker.enabled = true;
            // Optionally: set picker to current saved time
        }
    }

    private void CloseDueTimeSelectionPanel() => 
        dueTimeSelectorPanel.SetActive(false);

    private void SaveDueTime()
    {
        var hour = hourPicker.GetSelectedValue();
        var minute = minutePicker.GetSelectedValue();
        SmartContractDraft.SetDueTime(new TimeSpan(hour, minute, 0));
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

            var state = UserSession.IsAdmin
                ? SmartContractState.ReadyToConfirm
                : SmartContractState.ReadyToSell;

            contract.SetStateOnDate(SmartContractDraft.StartDate, state);
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
    }
    
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
