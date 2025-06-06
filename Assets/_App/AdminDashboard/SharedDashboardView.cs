using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using _App.Balance;
using _App.Bootstrap;
using _App.ChildDashboard;
using _App.Dashboard;
using _App.Models;
using _App.Services;
using _App.Services.BalanceService;
using _App.Settings;
using _App.UI.Scripts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _App.AdminDashboard
{
    public class SharedDashboardView : MonoBehaviour, IDashboardView
    {
        [SerializeField] private AppSettingsView appSettingsView;
        [SerializeField] private EditSelectedUserView editSelectedUserView;
        [SerializeField] private AddChildFromAdminUI addChildFromAdminUI;
        [SerializeField] private BalanceDashboardView balanceDashboardView;
        [SerializeField] private HistoryPresenter historyPresenter;
        [Header("Profile & Child")]
        [SerializeField] private Button profileAvatarButton;
        [SerializeField] private Image profileAvatarImage;
        [SerializeField] private TextMeshProUGUI profileNameText;
        [SerializeField] private TextMeshProUGUI balanceText;
        [SerializeField] private Transform childSelectorGrid;
        [SerializeField] private GameObject childSelectorItemPrefab;
        [SerializeField] private GameObject adminDoubleAvatar;
        [SerializeField] private GameObject infoRewardIcon;
        [SerializeField] private GameObject editRewardIcon;
        [SerializeField] private GameObject smartContractPlusIcon;

        [Header("Calendar")]
        [SerializeField] private Transform[] calendarDayButtonsContainers;
        [SerializeField] private Button calendarDayButtonPrefab;

        [Header("Contracts")]
        [SerializeField] private GameObject groupHeaderPrefab;
        [SerializeField] private Transform contractListContainer;
        [SerializeField] private GameObject contractEntryPrefab;
        [SerializeField] private Button addContractButton;

        [Header("Panels")]
        [SerializeField] private SmartContractCreationStep1 contractCreatorPanel;
        [SerializeField] private GameObject contractCreatorIconAvatarPanel;
        [SerializeField] private GameObject contractCreatorSettingsPanel;
        [SerializeField] private Transform childSelectorPanel;
        [SerializeField] private Transform newProfileCreatorPanel;
        [SerializeField] private GameObject rewardPanel;
        [SerializeField] private GameObject adjustBalancePanel;

        [Header("Buttons")]
        [SerializeField] private Button childSelectionExitButton;
        [SerializeField] private Button rewardButton;
        [SerializeField] private Button adjustBalanceButton;
        [SerializeField] private Button surpriseContractButton;
        [SerializeField] private Button addNewChildButton;
        [SerializeField] private TextMeshProUGUI dateText;
        [SerializeField] private TextMeshProUGUI extraRewardStatusText;
        
        [Header("Other UI")]
        [SerializeField] private Scrollbar  dashboardScScrollbar;
        private bool _hasTriggeredRefresh = false;
        private const float PullThreshold = 1.2f; // drag beyond 1 = pull gesture

        private bool _hasSelectedToday = false;
        private bool _didAutoSelectToday = false;

        [SerializeField] private Color redColor, lightRedColor, blueColor, greenColor, lightGreyColor, greyColor;

        private IDashboardPresenter _presenter;
        private readonly List<(Button button, DateTime date)> _calendarButtonData = new();
        private readonly Dictionary<string, GameObject> _childItemMap = new();

        private bool _isAdmin;
        
        private void Update()
        {
            if (dashboardScScrollbar == null) return;
        
            float position = dashboardScScrollbar.value;
        
            if (!_hasTriggeredRefresh && position > PullThreshold)
            {
                _hasTriggeredRefresh = true;
                //Debug.Log("📥 Pulled past threshold — refreshing contract list...");
                _presenter.OnDaySelected(_presenter.SelectedDay); // call with proper grouped data
            }
        
            // Reset flag when user scrolls up again
            if (_hasTriggeredRefresh && position < 1.2f) 
                _hasTriggeredRefresh = false;
        }
        private async void Start()
        {
            await FirebaseInit.WaitUntilReady();

            if (UserSession.IsAdmin)
            {
                _presenter = new AdminDashboardPresenter(
                    this,
                    new FirebaseChildService(),
                    new FirebaseContractService(),
                    new FirebaseRewardService(),
                    new FirebaseSettingsService(),
                    new DateService(),
                    new FirebaseAdminContractListenerService(),
                    new FirebaseBalanceService(),
                    new FirebaseBalanceListenerService()
                );
            }
            else
            {
                _presenter = new ChildDashboardPresenter(
                    this,
                    new FirebaseChildService(),
                    new FirebaseContractService(),
                    new FirebaseRewardService(),
                    new FirebaseSettingsService(),
                    new DateService(),
                    new FirebaseChildContractListenerService(),
                    new FirebaseBalanceService(),
                    new FirebaseBalanceListenerService()
                );
            }
            
            try
            {
                string uid = UserSession.IsAdmin
                    ? FirebaseInit.Auth.CurrentUser.UserId         // ✅ Admin uses Firebase UID
                    : PlayerPrefs.GetString(AppConstants.ChildUID); // ✅ Child uses saved custom UID

                try
                {
                    _presenter.Initialize(uid);
                    appSettingsView.Initialize(_presenter);
                    editSelectedUserView.Initialize(_presenter);
                    addChildFromAdminUI.Initialize(_presenter);
                    balanceDashboardView.Initialize(_presenter);
                }
                catch (Exception ex)
                {
                    Debug.LogError("❌ Crash during DelayedInitialize: " + ex);
                }

                appSettingsView.Initialize(_presenter);
            }
            catch (Exception ex)
            {
                Debug.LogError("❌ Crash during presenter init: " + ex);
            }
            
            _isAdmin = UserSession.IsAdmin;
            adminDoubleAvatar.SetActive(_isAdmin);
            infoRewardIcon.SetActive(!_isAdmin);
            editRewardIcon.SetActive(_isAdmin);
            smartContractPlusIcon.SetActive(_isAdmin);

            if (_isAdmin)
            {
                surpriseContractButton.onClick.AddListener(() => ((IAdminDashboardPresenter)_presenter).OnAdminSurpriseButtonPressed());
                childSelectionExitButton.onClick.AddListener(() => ((IAdminDashboardPresenter)_presenter).OnExitSelectProfileButtonPressed());
                addContractButton.onClick.AddListener(() => ((IAdminDashboardPresenter)_presenter).OnAddContractButtonPressed());
                rewardButton.onClick.AddListener(() => ((IAdminDashboardPresenter)_presenter).OnRewardButtonPressed());
                adjustBalanceButton.onClick.AddListener(() => ((IAdminDashboardPresenter)_presenter).OnAdjustBalanceButtonPressed());
                profileAvatarButton.onClick.AddListener(() => ((IAdminDashboardPresenter)_presenter).OnSelectProfileButtonPressed());
                addNewChildButton.onClick.AddListener(OpenNewProfileCreator);
            }
            else
            {
                surpriseContractButton.onClick.AddListener(() => _presenter.OnChildSurpriseButtonPressed());
                childSelectionExitButton.onClick.RemoveAllListeners();
                addContractButton.onClick.RemoveAllListeners();
                rewardButton.onClick.RemoveAllListeners();
                adjustBalanceButton.onClick.RemoveAllListeners();
                profileAvatarButton.onClick.RemoveAllListeners();
            }
            
            SetupCalendarButtons();
        }
        
        public void OnOpenHistoryTab()
        {
            ChildModel child = _presenter?.CurrentChild;
            var childUid = _presenter?.CurrentChild?.Uid; // From your presenter or model
            historyPresenter.Initialize(child, childUid);
        }
        
        public void SetupCalendarButtons()
        {
            _calendarButtonData.Clear();

            // Clear all old children from containers
            foreach (var container in calendarDayButtonsContainers)
            {
                foreach (Transform child in container)
                    Destroy(child.gameObject);
            }

            var dateService = new DateService();
            var today = dateService.GetCurrentDay();
            
            // Get week starts based on custom WeekStartsOn (e.g. Monday)
            DateTime thisWeekStart = dateService.GetWeekStart(today);
            DateTime prevWeekStart = thisWeekStart.AddDays(-7);
            DateTime nextWeekStart = thisWeekStart.AddDays(7);

            // Generate full weeks
            List<DateTime> pastWeek = Enumerable.Range(0, 7).Select(i => prevWeekStart.AddDays(i)).ToList();
            List<DateTime> presentWeek = Enumerable.Range(0, 7).Select(i => thisWeekStart.AddDays(i)).ToList();
            List<DateTime> futureWeek = Enumerable.Range(0, 7).Select(i => nextWeekStart.AddDays(i)).ToList();

            // Populate into UI containers
            PopulateCalendarSection(pastWeek, calendarDayButtonsContainers[0]);
            PopulateCalendarSection(presentWeek, calendarDayButtonsContainers[1]);
            PopulateCalendarSection(futureWeek, calendarDayButtonsContainers[2]);
            
            _presenter.OnDaySelected(today);
        }
        
        private void PopulateCalendarSection(List<DateTime> days, Transform container)
        {
            foreach (var day in days)
            {
                var button = Instantiate(calendarDayButtonPrefab, container);
                var dayText = button.transform.Find("Day")?.GetComponent<TextMeshProUGUI>();
                var numberText = button.transform.Find("Number")?.GetComponent<TextMeshProUGUI>();
                var line = button.transform.Find("Line");

                if (dayText != null) dayText.text = day.ToString("ddd");
                if (numberText != null) numberText.text = day.Day.ToString();
                if (line != null)
                {
                    DayOfWeek lastDay = (DayOfWeek)(((int)DateService.WeekStartsOn + 6) % 7);
                    if (day.DayOfWeek == lastDay)
                        line.gameObject.SetActive(false);
                }
                
                _calendarButtonData.Add((button, day));
                button.onClick.AddListener(() => _presenter.OnDaySelected(day));
            }
        }

        public void UpdateCalendarColors(List<SmartContractModel> allContracts, string selectedChildId)
        {
            foreach (var (button, date) in _calendarButtonData)
            {
                var bg = button.GetComponent<Image>();
                var line = button.transform.Find("Line")?.GetComponent<Image>();
                if (bg == null || line == null) continue;

                if (date > DateTime.Today)
                {
                    bg.color = greyColor;
                    line.color = greyColor;
                    continue;
                }

                if (date.Date == DateTime.Today)
                {
                    bg.color = blueColor;
                    line.color = blueColor;
                    continue;
                }

                // Filter for selected child and visible contracts
                var contractsForDay = allContracts
                    .Where(c => c.AssignedToUid == selectedChildId && c.IsVisibleOn(date))
                    .Where(c => c.ShouldAppearInEveryDayGroup(date)) // ✅ Only EveryDay group
                    .Where(c =>
                    {
                        // Exclude Once and AsNeeded in the past
                        if (date < DateTime.Today)
                        {
                            if (c.RepeatMode == RepeatType.Once || c.RepeatMode == RepeatType.AsNeeded)
                                return false;
                        }
                        return true;
                    })
                    .ToList();


                if (contractsForDay.Count == 0)
                {
                    bg.color = greyColor;
                    line.color = greyColor;
                    continue;
                }

                bool allDone = contractsForDay.All(c =>
                {
                    var state = c.GetStateOnDate(date, isAdmin: true);
                    return state == SmartContractState.Completed || state == SmartContractState.Purchased;
                });

                bool anyReadyToBuy = contractsForDay.Any(c =>
                    c.GetStateOnDate(date, isAdmin: true) == SmartContractState.ReadyToBuy);

                if (anyReadyToBuy)
                {
                    bg.color = redColor;
                    line.color = redColor;
                }
                else if (allDone)
                {
                    bg.color = greenColor;
                    line.color = greenColor;
                }
                else
                {
                    bg.color = lightGreyColor;
                    line.color = lightGreyColor;
                }

            }
        }

        public void ShowChildren(List<ChildModel> children)
        {
            foreach (Transform child in childSelectorGrid)
                Destroy(child.gameObject);
            _childItemMap.Clear();

            foreach (var child in children)
            {
                var item = Instantiate(childSelectorItemPrefab, childSelectorGrid);
                item.GetComponentInChildren<TextMeshProUGUI>().text = child.DisplayName;
                item.transform.Find("Avatar").GetComponent<Image>().sprite = AvatarLoader.LoadAvatar(child.AvatarPath);
                _childItemMap[child.Uid] = item;
                item.GetComponent<Button>().onClick.AddListener(() =>
                {
                    _presenter.SetCurrentChild(child);
                    HighlightCurrentChild(child.Uid);
                });
            }

            HighlightCurrentChild(_presenter.CurrentChild?.Uid);
        }

        private void HighlightCurrentChild(string selectedUid)
        {
            foreach (var kvp in _childItemMap)
            {
                var bg = kvp.Value.GetComponent<Image>();
                var avatarBg = kvp.Value.transform.Find("Bg")?.GetComponent<Image>();
                bool isSelected = kvp.Key == selectedUid;
                if (bg) bg.color = isSelected ? redColor : greyColor;
                if (avatarBg) avatarBg.color = isSelected ? lightGreyColor : greyColor;
            }
        }

        public void ShowCurrentChild(ChildModel child)
        {
            profileNameText.text = child.DisplayName;
            profileAvatarImage.sprite = AvatarLoader.LoadAvatar(child.AvatarPath);
            
            balanceDashboardView.OnChildSet(child);
        }

        public void ShowChildBalance(float balance) =>
            balanceText?.SetText($"{balance:F2}");
        
        private void ClearContractUI()
        {
            foreach (Transform child in contractListContainer)
                Destroy(child.gameObject);
        }
        
        public void ShowGroupedContracts(Dictionary<RepeatType, List<SmartContractModel>> groupedContracts)
        {
            if (groupedContracts == null)
            {
                Debug.LogError("❌ groupedContracts is null");
                return;
            }

            var selectedDay = _presenter.SelectedDay;
            string keyPrefix = selectedDay.ToString("yyyy-MM-dd");

            var mergedEveryDay = new List<SmartContractModel>();
            var addedIds = new HashSet<string>();

            ClearContractUI();

            void AddToEveryDay(SmartContractModel contract)
            {
                if (addedIds.Add(contract.Id))
                    mergedEveryDay.Add(contract);
            }

            // 🔁 Step 1: Contracts with native daily grouping or AsNeeded pseudo-copies
            foreach (var group in groupedContracts.Values)
            {
                foreach (var contract in group)
                {
                    if (contract.IsCopy && contract.RepeatMode == RepeatType.AsNeeded)
                    {
                        contract.LoadStateHistory();
                        var keysToday = contract.StateHistory.Keys
                            .Where(k => k.StartsWith(keyPrefix + "#"))
                            .ToList();

                        foreach (var queueKey in keysToday)
                        {
                            var pseudoCopy = CreateVisualInstanceForQueue(contract, queueKey);
                            AddToEveryDay(pseudoCopy);
                        }
                    }
                    else if (contract.ShouldAppearInEveryDayGroup(selectedDay))
                    {
                        AddToEveryDay(contract);
                    }
                }
            }

            ShowGroup("", mergedEveryDay, showHeader: false);

            // 🧭 Step 2: Non-completed Once contracts
            if (groupedContracts.TryGetValue(RepeatType.Once, out var onceGroup))
            {
                var onceVisible = onceGroup
                    .Where(c => !c.IsCopy &&
                                c.IsVisibleOn(selectedDay) &&
                                (!c.StateHistory.TryGetValue(keyPrefix, out var stateList) ||
                                 !stateList.Any(r => r.State == SmartContractState.Completed)))
                    .ToList();

                if (onceVisible.Count > 0)
                    ShowGroup("Once", onceVisible, showHeader: true);
            }

            // 🧭 Step 3: Show AsNeeded originals
            if (groupedContracts.TryGetValue(RepeatType.AsNeeded, out var asNeededGroup))
            {
                var mainAsNeeded = asNeededGroup
                    .Where(c => !c.IsCopy && c.IsVisibleOn(selectedDay))
                    .ToList();

                if (mainAsNeeded is { Count: > 0 })
                    ShowGroup("As Needed", mainAsNeeded, showHeader: true);
            }

            if (!_didAutoSelectToday)
            {
                _didAutoSelectToday = true;
                SelectToday();
            }
        }

        private SmartContractModel CreateVisualInstanceForQueue(SmartContractModel original, string queueKey)
        {
            return new SmartContractModel
            {
                Id = $"{original.Id}_{queueKey}",
                Title = original.Title,
                IconPath = original.IconPath,
                RewardAmount = original.RewardAmount,
                IsCopy = true,
                ParentId = original.ParentId,
                AssignedToUid = original.AssignedToUid,
                AdminUID = original.AdminUID,
                RepeatMode = RepeatType.AsNeeded,
                StartDate = original.StartDate,
                RequireNotificationOnThisDevice = original.RequireNotificationOnThisDevice,
                RequireParentalApproval = original.RequireParentalApproval,
                RequirePhotoProof = original.RequirePhotoProof,
                DueTime = original.DueTime,
                RepeatDays = new List<DayOfWeek>(),
                StateHistory = new Dictionary<string, List<SmartContractModel.StateRecord>>
                {
                    [queueKey] = original.StateHistory[queueKey]
                },
                stateHistoryRaw = $"{queueKey}:{(int)original.StateHistory[queueKey].First().State}"
            };
        }
        
        private void CreateGroupHeader(string label)
        {
            if (groupHeaderPrefab == null)
            {
                Debug.LogWarning("Group header prefab not set.");
                return;
            }

            var header = Instantiate(groupHeaderPrefab, contractListContainer);
            var text = header.GetComponentInChildren<TextMeshProUGUI>();
            if (text) text.text = label;
        }

        private void ShowGroup(string label, List<SmartContractModel> contracts, bool showHeader)
        {
            if (contracts == null || contracts.Count == 0)
                return;
            
            //Debug.Log($"🔽 Showing group: {label} | Total contracts: {contracts.Count}");

            if (showHeader)
                CreateGroupHeader(label);

            DateTime selectedDay = _presenter.SelectedDay;

            // ✅ Sort by custom daily state priority
            contracts = contracts.OrderBy(c =>
            {
                //var state = c.GetStateOnDate(selectedDay, _isAdmin);
                var state = c.GetLatestStateOnDate(selectedDay, _isAdmin);
                
                return state switch
                {
                    SmartContractState.ReadyToBuy                       => 0,
                    SmartContractState.ReadyToSell    when !_isAdmin => 1,
                    SmartContractState.ReadyToConfirm when !_isAdmin  => 2,
                    SmartContractState.ReadyToConfirm when _isAdmin  => 1,
                    SmartContractState.ReadyToSell    when _isAdmin => 2,
                    SmartContractState.Purchased                        => 3,
                    SmartContractState.Completed                        => 4,
                    _                                                   => 5
                };
            }).ToList();

            foreach (var contract in contracts)
            {
                //Debug.Log($"🧱 Instantiating: {contract.Title} | IsCopy: {contract.IsCopy} | Repeat: {contract.RepeatMode}");
                InstantiateContractUI(contract);
            }
        }
        
        private void InstantiateContractUI(SmartContractModel contract)
        {
            GameObject item = Instantiate(contractEntryPrefab, contractListContainer);
            var view = item.GetComponent<SmartContractView>();
            
            if (view == null)
            {
                Debug.LogError($"❌ Missing SmartContractView on prefab: {contract.Title}");
                return;
            }
            
            view?.Setup(_presenter);
            view?.Initialize(contract);
        }
         
        public void HighlightDayInCalendar(DateTime selectedDay)
        {
            foreach (var (button, date) in _calendarButtonData)
            {
                var dayText = button.transform.Find("Day")?.GetComponent<TextMeshProUGUI>();
                var numberText = button.transform.Find("Number")?.GetComponent<TextMeshProUGUI>();
                var pointer = button.transform.Find("Pointer")?.gameObject;
                bool isSelected = date.Date == selectedDay.Date;
                if (dayText != null) dayText.color = isSelected ? Color.white : Color.grey;
                if (numberText != null) numberText.color = isSelected ? Color.white : Color.black;
                if (pointer != null) pointer.SetActive(isSelected);
            }
        }

        public void SelectToday()
        {
            if (_hasSelectedToday) return; // ✅ prevent recursion
            _hasSelectedToday = true;

            var today = DateTime.Today;
            ShowSelectedDay(today);

            foreach (var (button, date) in _calendarButtonData)
            {
                if (date.Date == today)
                {
                    button.onClick.Invoke(); // triggers OnDaySelected(today)
                    break;
                }
            }
        }

        public void ShowSelectedDay(DateTime selectedDay)
        {
            if (dateText != null)
                dateText.text = selectedDay.ToString("dd MMM");
        }
        
        public void OpenAdjustBalancePanel()
        {
            adjustBalancePanel.SetActive(true);
        }
        
        public void OpenEditContractPanel()
        {
            if (_presenter is IAdminDashboardPresenter adminPresenter)
            {
                contractCreatorPanel.Initialize(adminPresenter);
                contractCreatorPanel.gameObject.SetActive(true);
                contractCreatorIconAvatarPanel.gameObject.SetActive(false);
                contractCreatorSettingsPanel.gameObject.SetActive(true);
            }
            else
            {
                Debug.LogWarning("❌ Attempted to open edit panel with non-admin presenter.");
            }
        }

        public void ShowExtraRewardStatus(string message)
        {
            if (extraRewardStatusText != null)
                extraRewardStatusText.text = message;
        }
        
        public void OpenContractCreator()
        {
            if (_presenter is IAdminDashboardPresenter adminPresenter)
            {
                adminPresenter.PrepareNewContractDraft();
                contractCreatorPanel.Initialize(adminPresenter);
                contractCreatorPanel.gameObject.SetActive(true);
                contractCreatorIconAvatarPanel.gameObject.SetActive(true);
            }
            else
            {
                Debug.LogWarning("❌ Attempted to open contract creator with non-admin presenter.");
            }
        }
        
        public void OnAdminSurpriseButtonClick()
        {
            Debug.Log("On Child surprise contract clicked");
        }

        public void OnChildSurpriseButtonClick()
        {
            Debug.Log("On Admin surprise contract clicked");
        }

        public void OpenProfileSelector() => 
            childSelectorPanel.gameObject.SetActive(true);
        
        public void CloseProfileSelector() => 
            childSelectorPanel.gameObject.SetActive(false);

        private void OpenNewProfileCreator()
        {
            CloseProfileSelector();
            newProfileCreatorPanel.gameObject.SetActive(true);
        }
        
        public void OpenRewardPanel() => 
            rewardPanel.SetActive(true);

        public void ShowExtraRewardEligible(bool eligible) => 
            rewardButton.interactable = eligible;
        
        public void ShowRewardPayout(RewardModel reward)
        {
            ShowExtraRewardStatus($"Reward given: {(reward.Type == RewardType.Event ? reward.Description : reward.Amount.ToString())}");
        }
    }
}