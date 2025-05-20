using System;
using System.Collections.Generic;
using System.Linq;
using _App.Bootstrap;
using _App.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _App.AdminDashboard
{
    public class AdminDashboardView : MonoBehaviour, IAdminDashboardView
    {
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

        [Header("Calendar")]
        [SerializeField] private Transform calendarDayButtonsContainer;
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
        [SerializeField] private GameObject rewardPanel;
        [SerializeField] private GameObject adjustBalancePanel;

        [Header("Buttons")]
        [SerializeField] private Button childSelectionExitButton;
        [SerializeField] private Button rewardButton;
        [SerializeField] private Button adjustBalanceButton;
        [SerializeField] private TextMeshProUGUI dateText;
        [SerializeField] private TextMeshProUGUI extraRewardStatusText;

        private bool _hasSelectedToday = false;
        private bool _didAutoSelectToday = false;

        [SerializeField] private Color redColor, lightRedColor, blueColor, greenColor, lightGreyColor, greyColor;

        private AdminDashboardPresenter _presenter;
        private readonly List<(Button button, DateTime date)> _calendarButtonData = new();
        private readonly Dictionary<string, GameObject> _childItemMap = new();

        private async void Start()
        {
            await FirebaseInit.WaitUntilReady();
            _presenter = new AdminDashboardPresenter(
                this,
                new FirebaseChildService(),
                new FirebaseContractService(),
                new FirebaseRewardService(),
                new DateService(),
                new FirebaseAdminContractListenerService()
            );

            string adminUid = FirebaseInit.Auth.CurrentUser.UserId;
            _presenter.Initialize(adminUid);
            
            var isAdmin = UserSession.IsAdmin;
            adminDoubleAvatar.SetActive(isAdmin);
            infoRewardIcon.SetActive(!isAdmin);
            editRewardIcon.SetActive(isAdmin);

            childSelectionExitButton.onClick.AddListener(() => _presenter.OnExitSelectProfileButtonPressed());
            addContractButton.onClick.AddListener(() => _presenter.OnAddContractButtonPressed());
            rewardButton.onClick.AddListener(() => _presenter.OnRewardButtonPressed());
            adjustBalanceButton.onClick.AddListener(() => _presenter.OnAdjustBalanceButtonPressed());
            profileAvatarButton.onClick.AddListener(_presenter.OnSelectProfileButtonPressed);

            SetupCalendarButtons();
        }

        private void SetupCalendarButtons()
        {
            _calendarButtonData.Clear();
            var weekDays = new DateService().GetCurrentWeekDays();

            foreach (var day in weekDays)
            {
                Button button = Instantiate(calendarDayButtonPrefab, calendarDayButtonsContainer);
                var dayText = button.transform.Find("Day")?.GetComponent<TextMeshProUGUI>();
                if (dayText != null)
                    dayText.text = day.ToString("ddd");

                var numberText = button.transform.Find("Number")?.GetComponent<TextMeshProUGUI>();
                if (numberText != null)
                    numberText.text = day.Day.ToString();
                
                _calendarButtonData.Add((button, day));
                if (day.DayOfWeek == DayOfWeek.Sunday)
                    button.transform.Find("Line")?.gameObject.SetActive(false);
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

                bool allCompleted = contractsForDay.All(c =>
                    c.GetStateOnDate(date, isAdmin: true) == SmartContractState.Completed);

                bool anyReadyToBuy = contractsForDay.Any(c =>
                    c.GetStateOnDate(date, isAdmin: true) == SmartContractState.ReadyToBuy);

                if (anyReadyToBuy)
                {
                    bg.color = redColor;
                    line.color = redColor;
                }
                else if (allCompleted)
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
        }

        public void ShowChildBalance(float balance) =>
            balanceText.text = balance.ToString();
        
        private void ClearContractUI()
        {
            foreach (Transform child in contractListContainer)
                Destroy(child.gameObject);
        }
        
        public void ShowGroupedContracts(Dictionary<RepeatType, List<SmartContractModel>> groupedContracts)
        {
            ClearContractUI();

            var selectedDay = _presenter.SelectedDay;
            var isAdmin = UserSession.IsAdmin;
            string key = selectedDay.ToString("yyyy-MM-dd");

            var mergedEveryDay = new List<SmartContractModel>();
            var addedIds = new HashSet<string>();

            void AddToEveryDay(SmartContractModel contract)
            {
                if (addedIds.Add(contract.Id))
                {
                    mergedEveryDay.Add(contract);
                    //Debug.Log($"✅ [EveryDay] {contract.Title} | IsCopy: {contract.IsCopy} | Repeat: {contract.RepeatMode} | State: {contract.GetStateOnDate(selectedDay, isAdmin)}");
                }
            }

            // 🔁 Add all contracts that should appear in EveryDay group
            foreach (var group in groupedContracts.Values)
            {
                foreach (var contract in group)
                {
                    if (contract.ShouldAppearInEveryDayGroup(selectedDay))
                        AddToEveryDay(contract);
                }
            }

            //Debug.Log($"📦 Merged EveryDay total: {mergedEveryDay.Count}");
            ShowGroup("", mergedEveryDay, showHeader: false);

            // 🧭 Show pending Once contracts (not completed today)
            if (groupedContracts.TryGetValue(RepeatType.Once, out var onceGroup))
            {
                var onceVisible = onceGroup
                    .Where(c =>
                    {
                        if (c.IsCopy) return false;

                        c.LoadStateHistory();

                        bool completedToday = c.StateHistory.TryGetValue(key, out var state) && state == SmartContractState.Completed;
                        return c.IsVisibleOn(selectedDay) && !completedToday;
                    })
                    .ToList();

                if (onceVisible.Count > 0)
                    ShowGroup("Once", onceVisible, showHeader: true);
            }

            // 🧭 Show main AsNeeded contracts
            if (groupedContracts.TryGetValue(RepeatType.AsNeeded, out var asNeededGroup))
            {
                var asNeededMain = asNeededGroup
                    .Where(c => !c.IsCopy && c.IsVisibleOn(selectedDay))
                    .ToList();

                if (asNeededMain.Count > 0)
                    ShowGroup("As Needed", asNeededMain, showHeader: true);
            }

            if (!_didAutoSelectToday)
            {
                _didAutoSelectToday = true;
                SelectToday();
            }
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

            bool isAdmin = UserSession.IsAdmin;
            DateTime selectedDay = _presenter.SelectedDay;

            // ✅ Sort by custom daily state priority
            contracts = contracts.OrderBy(c =>
            {
                var state = c.GetStateOnDate(selectedDay, isAdmin);

                return state switch
                {
                    SmartContractState.ReadyToBuy                        => 0,
                    SmartContractState.ReadyToSell    when !isAdmin => 1,
                    SmartContractState.ReadyToConfirm when isAdmin  => 1,
                    SmartContractState.Purchased                        => 2,
                    SmartContractState.Completed                        => 3,
                    _                                                   => 4
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
         
        public void ShowDaySelection(DateTime selectedDay)
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

        public void OpenEditContractPanel()
        {
            contractCreatorPanel.Initialize(_presenter);
            contractCreatorPanel.gameObject.SetActive(true);
            contractCreatorIconAvatarPanel.gameObject.SetActive(false);
            contractCreatorSettingsPanel.gameObject.SetActive(true);
        }

        public void ShowExtraRewardStatus(string message)
        {
            if (extraRewardStatusText != null)
                extraRewardStatusText.text = message;
        }

        public void OpenContractCreator()
        {
            _presenter.PrepareNewContractDraft();
            contractCreatorPanel.Initialize(_presenter);
            contractCreatorPanel.gameObject.SetActive(true);
            contractCreatorIconAvatarPanel.gameObject.SetActive(true);
        }

        public void OpenProfileSelector() => childSelectorPanel.gameObject.SetActive(true);
        public void CloseProfileSelector() => childSelectorPanel.gameObject.SetActive(false);
        public void OpenRewardPanel() => rewardPanel.SetActive(true);
        public void OpenAdjustBalancePanel() => adjustBalancePanel.SetActive(true);
        public void ShowExtraRewardEligible(bool eligible) => rewardButton.interactable = eligible;
        public void ShowRewardPayout(RewardModel reward)
        {
            ShowExtraRewardStatus($"Reward given: {(reward.Type == RewardType.Event ? reward.Description : reward.Amount.ToString())}");
        }
    }
}






















/*using System;
using System.Collections.Generic;
using System.Linq;
using _App.Bootstrap;
using _App.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _App.AdminDashboard
{
    public class AdminDashboardView : MonoBehaviour, IAdminDashboardView
    {
        [Header("Profile & Child")]
        [SerializeField] private Button profileAvatarButton;
        [SerializeField] private Image profileAvatarImage;
        [SerializeField] private TextMeshProUGUI profileNameText;
        [SerializeField] private TextMeshProUGUI balanceText;
        [SerializeField] private Transform childSelectorGrid;
        [SerializeField] private GameObject childSelectorItemPrefab;

        [Header("Calendar")]
        [SerializeField] private Transform calendarDayButtonsContainer; // 7 buttons (Mon–Sun)
        [SerializeField] private Button calendarDayButtonPrefab;

        [Header("Contracts")]
        [SerializeField] private Transform contractListContainer;
        [SerializeField] private GameObject contractEntryPrefab;
        [SerializeField] private Button addContractButton;

        [Header("Panels")]
        [SerializeField] private SmartContractCreationStep1 contractCreatorPanel;
        [SerializeField] private Transform childSelectorPanel;
        [SerializeField] private GameObject rewardPanel;
        [SerializeField] private GameObject adjustBalancePanel;

        [Header("Buttons")]
        [SerializeField] private Button childSelectionExitButton;
        [SerializeField] private Button rewardButton;
        [SerializeField] private Button adjustBalanceButton;
        [SerializeField] private TextMeshProUGUI dateText;
        [SerializeField] private TextMeshProUGUI extraRewardStatusText;
        
        [SerializeField] private Color redColor = new Color(0.9921f, 0.1607f, 0.1607f);
        [SerializeField] private Color lightRedColor = new Color(1f, 0.3215f, 0.3215f);
        [SerializeField] private Color blueColor = new Color(0.1647f, 0.5254f, 1f);
        [SerializeField] private Color greenColor = new Color(0.1764f, 0.7803f, 0.3411f);
        [SerializeField] private Color lightGreyColor = new Color(0.7985f, 0.7845f, 0.8113f);
        [SerializeField] private Color greyColor = new Color(0.3725f, 0.3411f, 0.4235f);
        
        private AdminDashboardPresenter _presenter;
        private readonly List<(Button button, DateTime date)> _calendarButtonData = new();
        private readonly Dictionary<string, GameObject> _childItemMap = new();
        
        private async void Start()
        {
            await FirebaseInit.WaitUntilReady();
            // Instantiate presenter with injected services
            _presenter = new AdminDashboardPresenter(
                this,
                new FirebaseChildService(),
                new FirebaseContractService(),
                new FirebaseRewardService(),
                new DateService(),
                new FirebaseAdminContractListenerService()
            );

            string adminUID = FirebaseInit.Auth.CurrentUser.UserId;
            _presenter.Initialize(adminUID);

            childSelectionExitButton.onClick.AddListener(() => _presenter.OnExitSelectProfileButtonPressed());
            addContractButton.onClick.AddListener(() => _presenter.OnAddContractButtonPressed());
            rewardButton.onClick.AddListener(() => _presenter.OnRewardButtonPressed());
            adjustBalanceButton.onClick.AddListener(() => _presenter.OnAdjustBalanceButtonPressed());
            profileAvatarButton.onClick.AddListener(_presenter.OnSelectProfileButtonPressed);
            
            SetupCalendarButtons();
            SelectToday();
        }
        
        private void SetupCalendarButtons()
        {
            _calendarButtonData.Clear();

            var weekDays = new DateService().GetCurrentWeekDays(); // returns 7 DateTimes (Mon–Sun)

            foreach (var day in weekDays)
            {
                Button button = Instantiate(calendarDayButtonPrefab, calendarDayButtonsContainer);

                var dayText = button.transform.Find("Day")?.GetComponent<TextMeshProUGUI>();
                var numberText = button.transform.Find("Number")?.GetComponent<TextMeshProUGUI>();

                if (dayText != null)
                    dayText.text = day.ToString("ddd"); // "Mon", "Tue", ...
                if (numberText != null)
                    numberText.text = day.Day.ToString(); // "6", "12", etc.

                // 🗓 Store for later interaction
                _calendarButtonData.Add((button, day));

                // 🚫 Disable future days
                var isFuture = day.Date > DateTime.Today;
                //button.interactable = !isFuture;

                // 🕊️ Hide background if Sunday
                if (day.DayOfWeek == DayOfWeek.Sunday)
                {
                    var bg = button.transform.Find("Line");
                    if (bg != null) bg.gameObject.SetActive(false);
                }
                
                button.onClick.AddListener(() => { _presenter.OnDaySelected(day); });

                // ✅ Add listener only if valid
                if (!isFuture)
                {
                    //button.onClick.AddListener(() => { _presenter.OnDaySelected(day); });
                }
            }
        }
        
        public void UpdateCalendarColors(List<SmartContractModel> allContracts)
        {
            foreach (var (button, date) in _calendarButtonData)
            {
                Image bg = button.transform?.GetComponent<Image>();
                if (bg == null) continue;
                
                Image line = button.transform.Find("Line")?.GetComponent<Image>();
                if (line == null) continue;

                // Future = Grey
                if (date > DateTime.Today)
                {
                    bg.color = greyColor;
                    line.color = greyColor;
                    continue;
                }

                // Today = Blue
                if (date.Date == DateTime.Today)
                {
                    bg.color = blueColor;
                    line.color = blueColor;
                    continue;
                }

                // Past = check contract states
                var contractsForDay = allContracts
                    .Where(c => c.DueDate == date.ToString("yyyy-MM-dd"))
                    .ToList();

                if (contractsForDay.Count == 0)
                {
                    bg.color = greyColor; // or skip, if you prefer
                    line.color = greyColor;
                    continue;
                }

                bool allCompleted = contractsForDay.All(c =>
                    c.State == SmartContractState.Completed ||
                    c.State == SmartContractState.Purchased);

                bool anyReadyToBuy = contractsForDay.Any(c => c.State == SmartContractState.ReadyToBuy);

                if (anyReadyToBuy)
                {
                    bg.color = redColor;
                    line.color = redColor;
                }
                else if (allCompleted)
                {
                    bg.color = greenColor;
                    line.color = greenColor;
                }
                else
                {
                    bg.color = lightGreyColor; // fallback
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
                GameObject item = Instantiate(childSelectorItemPrefab, childSelectorGrid);
                item.GetComponentInChildren<TextMeshProUGUI>().text = child.DisplayName;
                item.transform.Find("Avatar").GetComponent<Image>().sprite = AvatarLoader.LoadAvatar(child.AvatarPath);
                
                _childItemMap[child.Uid] = item;

                //string uid = child.Uid;
                item.GetComponent<Button>().onClick.AddListener(() =>
                {
                    _presenter.SetCurrentChild(child);
                    HighlightCurrentChild(child.Uid); // 👈 highlight on click
                });
                
                // highlight initially (if already selected)
                HighlightSelectedChildItem(item, _presenter.CurrentChild?.Uid == child.Uid);
                
                if (_presenter.CurrentChild != null)
                    HighlightCurrentChild(_presenter.CurrentChild.Uid);
            }
        }
        
        private void HighlightCurrentChild(string selectedUid)
        {
            foreach (var kvp in _childItemMap)
            {
                bool isSelected = kvp.Key == selectedUid;
                HighlightSelectedChildItem(kvp.Value, isSelected);
            }
        }
        
        private void HighlightSelectedChildItem(GameObject item, bool isSelected)
        {
            var bg = item.transform.GetComponent<Image>();
            if (bg != null)
                bg.color = isSelected ? redColor : greyColor;
            
            var avatarBg = item.transform.Find("Bg").GetComponent<Image>();
            if (avatarBg != null)
                avatarBg.color = isSelected ? lightGreyColor : greyColor;
        }

        public void ShowCurrentChild(ChildModel child)
        {
            profileNameText.text = child.DisplayName;
            profileAvatarImage.sprite = AvatarLoader.LoadAvatar(child.AvatarPath);
        }

        public void ShowChildBalance(float balance)
        {
            balanceText.text = $"{balance}";
        }
        
        private int GetSortOrder(SmartContractState state)
        {
            return state switch
            {
                SmartContractState.ReadyToBuy     => 0,
                SmartContractState.ReadyToSell    => 1,
                SmartContractState.ReadyToConfirm => 2,
                SmartContractState.Purchased      => 3,
                SmartContractState.Completed      => 4,
                _ => 5
            };
        }

        public void ShowContracts(List<SmartContractModel> contracts)
        {
            // Sort contracts by custom priority
            contracts = contracts.OrderBy(c => GetSortOrder(c.State)).ToList();

            foreach (Transform child in contractListContainer)
                Destroy(child.gameObject);
            
            foreach (var contract in contracts)
            {
                GameObject item = Instantiate(contractEntryPrefab, contractListContainer);
                
                bool isFuture = contract.GetDueDate().Date > DateTime.Today;

                var icon = item.transform.Find("Icon").GetComponent<Image>();
                var titleText = item.transform.Find("Title")?.GetComponent<TextMeshProUGUI>();
                var rewardText = item.transform.Find("Reward")?.GetComponent<TextMeshProUGUI>();
                var dueTimeText = item.transform.Find("DueTime")?.GetComponent<TextMeshProUGUI>();
                //var dueDateText = item.transform.GetChild(2).GetChild(2)?.GetComponent<TextMeshProUGUI>();

                //var photoProofIcon = item.transform.Find("PhotoProofIcon")?.gameObject;
                //var approvalIcon = item.transform.Find("ApprovalIcon")?.gameObject;

                var sellButton = item.transform.Find("SellButton")?.GetComponent<Button>();
                var editButton = item.transform.Find("EditButton")?.GetComponent<Button>();
                var deleteButton = item.transform.Find("DeleteButton")?.GetComponent<Button>();
                
                if (icon != null)
                    icon.sprite = ContractIconLoader.Load(contract.IconPath);

                if (titleText != null)
                    titleText.text = contract.Title;

                if (rewardText != null)
                    rewardText.text = $"{contract.RewardAmount}";

                sellButton.gameObject.SetActive(isFuture);
                if (sellButton != null)
                    sellButton.onClick.AddListener(() => _presenter.ConfirmContract(contract.Id));
                
                if (editButton != null)
                    editButton.onClick.AddListener(() => _presenter.EditContract(contract.Id));

                if (deleteButton != null)
                    deleteButton.onClick.AddListener(() => _presenter.DeleteContract(contract.Id));
                
                Color buttonColor;

                switch (contract.State)
                {
                    case SmartContractState.ReadyToSell:
                        item.transform.GetComponent<CanvasGroup>().alpha = 1f;
                        if (sellButton != null)
                        {
                            sellButton.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = "Buy";
                            sellButton.transform.GetChild(1).gameObject.SetActive(false); // coin icon
                            sellButton.transform.GetChild(2).gameObject.SetActive(false); // check icon
                        }
                        buttonColor = Color.green;
                        break;

                    case SmartContractState.ReadyToBuy:
                        item.transform.GetComponent<CanvasGroup>().alpha = 1f;
                        if (sellButton != null)
                        {
                            sellButton.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = "Buy";
                            sellButton.transform.GetChild(1).gameObject.SetActive(false); // coin icon
                            sellButton.transform.GetChild(2).gameObject.SetActive(false); // check icon
                        }
                        buttonColor = Color.green;
                        break;

                    case SmartContractState.ReadyToConfirm:
                        item.transform.GetComponent<CanvasGroup>().alpha = 1f;
                        if (sellButton != null)
                        {
                            sellButton.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = "Buy";
                            sellButton.transform.GetChild(1).gameObject.SetActive(false); // coin icon
                            sellButton.transform.GetChild(2).gameObject.SetActive(false); // check icon
                        }
                        buttonColor = Color.green;
                        break;
                    
                    case SmartContractState.Completed:
                        item.transform.GetComponent<CanvasGroup>().alpha = 0.5f;
                        if (sellButton != null)
                        {
                            sellButton.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = "";
                            sellButton.transform.GetChild(1).gameObject.SetActive(false); // coin icon
                            sellButton.transform.GetChild(2).gameObject.SetActive(true); // check icon
                        }
                        buttonColor = Color.grey;
                        break;
                    
                    case SmartContractState.Purchased:
                        item.transform.GetComponent<CanvasGroup>().alpha = 0.5f;
                        if (sellButton != null)
                        {
                            sellButton.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = "";
                            sellButton.transform.GetChild(1).gameObject.SetActive(true); // coin icon
                            sellButton.transform.GetChild(2).gameObject.SetActive(true); // check icon
                        }
                        buttonColor = Color.grey;
                        break;

                    default:
                        if(sellButton != null)
                            sellButton.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = "Error";
                        buttonColor = Color.gray;
                        break;
                }
                
                if(sellButton != null)
                    sellButton.GetComponent<Image>().color = buttonColor;

                if (dueTimeText != null && !string.IsNullOrEmpty(contract.DueTime) && contract.DueTime != "00:00")
                    dueTimeText.text = $"{contract.DueTime}";
                else if (dueTimeText != null && !string.IsNullOrEmpty(contract.DueTime))
                    dueTimeText.gameObject.SetActive(false); // clear if not used
                
                /*if (dueDateText != null && !string.IsNullOrEmpty(contract.DueDate))
                {
                    if (DateTime.TryParse(contract.DueDate, out var dueDate))
                    {
                        dueDateText.text = $"Due day: {dueDate:ddd}, {dueDate:dd MMM}";
                        dueDateText.gameObject.SetActive(true);
                    }
                    else
                    {
                        Debug.LogWarning($"⚠️ Could not parse DueDate: {contract.DueDate}");
                        dueDateText.gameObject.SetActive(false);
                    }
                }
                else
                {
                    dueDateText?.gameObject.SetActive(false);
                }#1#


                //if (photoProofIcon != null)
                    //photoProofIcon.SetActive(contract.RequirePhotoProof);

                //if (approvalIcon != null)
                    //approvalIcon.SetActive(contract.RequireParentalApproval);
            }
        }

        public void ShowDaySelection(DateTime selectedDay)
        {
            foreach (var (button, date) in _calendarButtonData)
            {
                var dayText = button.transform.Find("Day")?.GetComponent<TextMeshProUGUI>();
                var numberText = button.transform.Find("Number")?.GetComponent<TextMeshProUGUI>();
                var pointer = button.transform.Find("Pointer")?.gameObject;

                if (dayText != null)
                    dayText.text = date.ToString("ddd");

                if (numberText != null)
                    numberText.text = date.Day.ToString();

                bool isSelected = date.Date == selectedDay.Date;

                if (numberText != null)
                    numberText.color = isSelected ? Color.white : Color.black;

                if (dayText != null)
                    dayText.color = isSelected ? Color.white : Color.grey;

                if (pointer != null)
                    pointer.SetActive(isSelected);
            }
        }

        private void SelectToday()
        {
            DateTime today = DateTime.Today;

            ShowSelectedDay(today);

            foreach (var (button, date) in _calendarButtonData)
            {
                if (date.Date == today)
                {
                    //Debug.Log($"📅 Auto-selecting today: {today:yyyy-MM-dd}");
                    button.onClick.Invoke(); // simulate button press
                    break;
                }
            }
        }

        public void ShowSelectedDay(DateTime selectedDay)
        {
            if (dateText != null)
                dateText.text = selectedDay.ToString("dd MMM"); // e.g. "06 May"
        }
        
        public void ShowExtraRewardStatus(string message)
        {
            if (extraRewardStatusText != null)
                extraRewardStatusText.text = message;
        }

        public void OpenContractCreator()
        {
            _presenter.PrepareNewContractDraft(); // ✅ let presenter handle UID and date
            contractCreatorPanel.Initialize(_presenter);
            contractCreatorPanel.gameObject.SetActive(true);
        }

        public void OpenProfileSelector()
        {
            childSelectorPanel.gameObject.SetActive(true);
            
            HighlightCurrentChild(_presenter.CurrentChild?.Uid);
        }
        
        public void CloseProfileSelector()
        {
            childSelectorPanel.gameObject.SetActive(false);
        }

        public void OpenRewardPanel()
        {
            rewardPanel.SetActive(true);
        }

        public void OpenAdjustBalancePanel()
        {
            adjustBalancePanel.SetActive(true);
        }

        public void ShowExtraRewardEligible(bool eligible)
        {
            rewardButton.interactable = eligible;
        }

        public void ShowRewardPayout(RewardModel reward)
        {
            ShowExtraRewardStatus($"Reward given: {(reward.Type == RewardType.Event ? reward.Description : reward.Amount.ToString())}");
        }
    }
}*/