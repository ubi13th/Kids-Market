using System;
using System.Collections.Generic;
using System.Linq;
using _App.Dashboard;
using _App.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _App.Reports
{
    public class ContractHistoryPresenter : MonoBehaviour
    {
        [Header("Setup")]
        [SerializeField] private TextMeshProUGUI reportsText;
        [SerializeField] private Transform contractsContainer;
        [SerializeField] private GameObject historyContractEntryPrefab;
        [SerializeField] private Sprite emptyIcon;
        [SerializeField] private Sprite fillIcon;
        [SerializeField] private GameObject contractsReadyToBuyBlock;
        [SerializeField] private TextMeshProUGUI contractsReadyToBuyBlockTitleText;
        [SerializeField] private Transform contractsReadyToBuyContainer;
        [SerializeField] private Button contractsReadyToBuyContainerExitButton;
        [SerializeField] private GameObject contractEntryPrefab;
        [SerializeField] private GameObject contractEntryHeaderPrefab;

        [SerializeField] private Color colorCompleted, colorMissed, colorPending, colorNotAssigned;

        private IDateService _dateService;
        
        private DateTime _weekStart;
        private List<SmartContractModel> _visibleContracts = new();
        private ChildModel _currentChild;
        private IDashboardPresenter _presenter;

        private int _completedContractsAmount;
        private RewardType _currentRewardType;
        
        public void Initialize(ChildModel currentChild, List<SmartContractModel> contracts, DateTime weekStart, IDashboardPresenter presenter)
        {
            _presenter = presenter;
            _currentChild = currentChild;
            _visibleContracts = contracts
                .Where(c => c.RepeatMode == RepeatType.EveryDay || c.RepeatMode == RepeatType.SpecificDays)
                .ToList();

            _weekStart = weekStart.Date;
            _currentRewardType = currentChild.RewardPreference;

            ShowWeeklyHistory();
        }


        /*
        public void Initialize(ChildModel currentChild, List<SmartContractModel> contracts, DateTime weekStart, IDashboardPresenter presenter)
        {
            _currentChild = currentChild;
            _presenter = presenter;
            _visibleContracts = contracts
                .Where(c => c.RepeatMode == RepeatType.EveryDay || c.RepeatMode == RepeatType.SpecificDays)
                .ToList();

            _weekStart = _dateService.GetWeekStart(weekStart);
            _currentRewardType = currentChild.RewardPreference;

            ShowWeeklyHistory();
        }
        */

        private void ShowWeeklyHistory()
        {
            foreach (Transform child in contractsContainer)
                Destroy(child.gameObject);

            reportsText.text = _currentChild.DisplayName.ToUpper() + " REPORTS";

            foreach (var contract in _visibleContracts)
            {
                var row = Instantiate(historyContractEntryPrefab, contractsContainer);

                var contractIcon = row.transform.Find("ContractContent/IconBg/Icon")?.GetComponent<Image>();
                if (contractIcon != null)
                    contractIcon.sprite = ContractIconLoader.Load(contract.IconPath);

                var titleText = row.transform.Find("ContractContent/Title")?.GetComponent<TextMeshProUGUI>();
                if (titleText != null)
                    titleText.text = contract.Title;

                var priceText = row.transform.Find("ContractContent/Price")?.GetComponent<TextMeshProUGUI>();
                if (priceText != null)
                    priceText.text = RewardAmountString(contract.RewardAmount);

                var rewardText = row.transform.Find("ContractContent/Reward")?.GetComponent<TextMeshProUGUI>();
                contract.LoadStateHistory();

                int completedCount = contract.StateHistory
                    .Where(kv =>
                        DateTime.TryParse(kv.Key.Split('#')[0], out var date) &&
                        date >= _weekStart && date <= _weekStart.AddDays(6))
                    .SelectMany(kv => kv.Value)
                    .Count(r => r.State is SmartContractState.Completed);

                float totalEarned = completedCount * contract.RewardAmount;
                if (rewardText != null)
                    rewardText.text = RewardAmountString(totalEarned);

                var btnContainer = row.transform.Find("ContractContent/SellBtnContainer");
                var orderedWeekDays = DateService.OrderedDaysOfWeek;

                for (int i = 0; i < 7; i++)
                {
                    DateTime currentDate = _weekStart.AddDays(i);
                    var btnSlot = btnContainer.GetChild(i);
                    var coinIcon = btnSlot.Find("CoinIcon")?.GetComponent<Image>();
                    var btn = btnSlot.GetComponent<Button>();
                    var btnIcon = btnSlot.GetComponent<Image>();

                    if (coinIcon == null || btn == null)
                        continue;

                    coinIcon.gameObject.SetActive(false);
                    btnIcon.sprite = fillIcon;
                    btn.interactable = false;

                    if (!contract.IsVisibleOn(currentDate))
                    {
                        btnIcon.sprite = emptyIcon;
                        btnIcon.color = colorNotAssigned;
                        continue;
                    }

                    var state = contract.GetStateOnDate(currentDate);

                    if (currentDate <= DateTime.Today)
                    {
                        switch (state)
                        {
                            case SmartContractState.Completed:
                                btnIcon.sprite = fillIcon;
                                btnIcon.color = colorCompleted;
                                break;
                            case SmartContractState.Purchased:
                                btnIcon.sprite = fillIcon;
                                btnIcon.color = colorCompleted;
                                coinIcon.gameObject.SetActive(true);
                                btn.interactable = true;
                                btn.onClick.RemoveAllListeners();
                                btn.onClick.AddListener(() => InstantiateSingleReadyToBuyEntryForDay(contract, currentDate, SmartContractState.Purchased));
                                break;
                            case SmartContractState.ReadyToBuy:
                                btnIcon.sprite = fillIcon;
                                btnIcon.color = colorMissed;
                                btn.interactable = true;
                                btn.onClick.RemoveAllListeners();
                                btn.onClick.AddListener(() => InstantiateSingleReadyToBuyEntryForDay(contract, currentDate, SmartContractState.ReadyToBuy));
                                break;
                            case SmartContractState.ReadyToSell:
                                btnIcon.sprite = fillIcon;
                                btnIcon.color = colorPending;
                                break;
                            case SmartContractState.ReadyToConfirm:
                                btnIcon.sprite = fillIcon;
                                btnIcon.color = colorCompleted;
                                break;
                            default:
                                btnIcon.sprite = fillIcon;
                                btnIcon.color = colorPending;
                                break;
                        }
                    }
                    else
                    {
                        btn.interactable = false;
                        btnIcon.sprite = fillIcon;
                        btnIcon.color = colorPending;
                    }
                }
            }
        }
        
        private void InstantiateSingleReadyToBuyEntryForDay(SmartContractModel contract, DateTime selectedDay, SmartContractState state)
        {
            // Clear previous items
            foreach (Transform child in contractsReadyToBuyContainer)
                Destroy(child.gameObject);

            // Check if contract is visible and ReadyToBuy on this day
            if (!contract.IsVisibleOn(selectedDay)) //  || contract.GetStateOnDate(selectedDay) != SmartContractState.ReadyToBuy
            {
                HideReadyToBuyBlock();
                return;
            }

            // Instantiate header
            GameObject header = Instantiate(contractEntryHeaderPrefab, contractsReadyToBuyContainer);
            var headerText = header.GetComponentInChildren<TextMeshProUGUI>();
            if (headerText != null)
                headerText.text = selectedDay.ToString("dddd, dd MMM");

            // Instantiate contract
            GameObject entry = Instantiate(contractEntryPrefab, contractsReadyToBuyContainer);
            var smartContractView = entry.GetComponent<SmartContractView>();

            if (smartContractView != null)
            {
                smartContractView.Setup(_presenter);
                smartContractView.Initialize(contract, selectedDay);
            }
            else
            {
                Debug.LogWarning("❌ SmartContractView not found on prefab.");
            }

            ShowReadyToBuyBlock(state);
            contractsReadyToBuyContainerExitButton.onClick.RemoveAllListeners();
            contractsReadyToBuyContainerExitButton.onClick.AddListener(HideReadyToBuyBlock);
        }
        
        private void ShowReadyToBuyBlock(SmartContractState state)
        {
            if(!contractsReadyToBuyBlock.gameObject.activeInHierarchy)
                contractsReadyToBuyBlock.gameObject.SetActive(true);

            contractsReadyToBuyBlockTitleText.text = state == SmartContractState.Purchased ? "Contract Purchased" : "Contract Incompleted";
        }
        
        public void HideReadyToBuyBlock()
        {
            if(contractsReadyToBuyBlock.gameObject.activeInHierarchy)
                contractsReadyToBuyBlock.gameObject.SetActive(false);
        }

        private string RewardAmountString(float savedAmount) =>
            _currentRewardType == RewardType.Money ? $"{savedAmount:F2}" : $"{savedAmount}";
    }
}










/*
using System;
using System.Collections.Generic;
using System.Linq;
using _App.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _App.Reports
{
    public class ContractHistoryPresenter : MonoBehaviour
    {
        [Header("Setup")]
        [SerializeField] private TextMeshProUGUI reportsText;
        [SerializeField] private Transform contractsContainer;
        [SerializeField] private GameObject historyContractEntryPrefab;
        [SerializeField] private Sprite emptyIcon;
        [SerializeField] private Sprite fillIcon;
        [SerializeField] private Transform contractsReadyToBuyContainer;
        [SerializeField] private GameObject contractEntryPrefab;

        [SerializeField] private Color colorCompleted, colorMissed, colorPending, colorNotAssigned;
        
        private DateTime _weekStart;
        private List<SmartContractModel> _visibleContracts = new();
        private ChildModel _currentChild;

        private int _completedContractsAmount;
        private RewardType _currentRewardType;

        public void Initialize(ChildModel currentChild, List<SmartContractModel> contracts, DateTime weekStart)
        {
            _currentChild = currentChild;
            
            _visibleContracts = contracts
                .Where(c => c.RepeatMode == RepeatType.EveryDay || c.RepeatMode == RepeatType.SpecificDays)
                .ToList();

            _weekStart = weekStart.Date;
            _currentChild.RewardPreference = currentChild.RewardPreference;

            ShowWeeklyHistory();
        }

        private void ShowWeeklyHistory()
        {
            foreach (Transform child in contractsContainer)
                Destroy(child.gameObject);

            reportsText.text = _currentChild.DisplayName.ToUpper() + " REPORTS";
            
            foreach (var contract in _visibleContracts)
            {
                var row = Instantiate(historyContractEntryPrefab, contractsContainer);

                // Optional UI: contract title and reward
                var contractIcon = row.transform.Find("ContractContent/IconBg/Icon")?.GetComponent<Image>();
                if (contractIcon != null) 
                    contractIcon.sprite = ContractIconLoader.Load(contract.IconPath);
                
                var titleText = row.transform.Find("ContractContent/Title")?.GetComponent<TextMeshProUGUI>();
                if (titleText != null) 
                    titleText.text = $"{contract.Title}";
                
                var priceText = row.transform.Find("ContractContent/Price")?.GetComponent<TextMeshProUGUI>();
                if (priceText != null) 
                    priceText.text = $"{contract.RewardAmount}";
                
                var rewardText = row.transform.Find("ContractContent/Reward")?.GetComponent<TextMeshProUGUI>();
                contract.LoadStateHistory();

                int completedOrPurchasedCount = contract.StateHistory
                    .Where(kv =>
                        DateTime.TryParse(kv.Key.Split('#')[0], out var date) &&
                        date >= _weekStart && date <= _weekStart.AddDays(6))
                    .SelectMany(kv => kv.Value)
                    .Count(r => r.State is SmartContractState.Completed or SmartContractState.Purchased);

                float totalEarned = completedOrPurchasedCount * contract.RewardAmount;

                if (rewardText != null)
                    rewardText.text = RewardAmountString(totalEarned);
                
                var btnContainer = row.transform.Find("ContractContent/SellBtnContainer");
                
                var orderedWeekDays = DateService.OrderedDaysOfWeek;

                for (var i = 0; i < 7; i++)
                {
                    DayOfWeek logicalDay = orderedWeekDays[i];
                    DateTime currentDate = _weekStart.StartOfWeek(logicalDay);
                    
                    currentDate = _weekStart.AddDays(i);
                    var btnSlot = btnContainer.GetChild(i);
                    var coinIcon = btnSlot.Find("CoinIcon")?.GetComponent<Image>();
                    var btn = btnSlot.GetComponent<Button>();
                    var btnIcon = btnSlot.GetComponent<Image>();

                    if (coinIcon == null || btn == null)
                        continue;
                    
                    coinIcon.gameObject.SetActive(false);
                    btnIcon.sprite = fillIcon;
                    btn.interactable = false;

                    if (!contract.IsVisibleOn(currentDate))
                    {
                        btnIcon.sprite = emptyIcon;
                        btnIcon.color = colorNotAssigned;
                        continue;
                    }
                    
                    var state = contract.GetStateOnDate(currentDate);

                    if (currentDate <= DateTime.Today)
                    {
                        // 🔙 Past or today — show actual state
                        switch (state)
                        {
                            case SmartContractState.Completed:
                                btnIcon.sprite = fillIcon;
                                btnIcon.color = colorCompleted;
                                break;
                            case SmartContractState.Purchased:
                                btnIcon.sprite = fillIcon;
                                btnIcon.color = colorCompleted;
                                coinIcon.gameObject.SetActive(true);
                                break;
                            case SmartContractState.ReadyToBuy:
                                btnIcon.sprite = fillIcon;
                                btnIcon.color = colorMissed;
                                btn.interactable = true;
                                DateTime selectedDay = currentDate;
                                var selectedContract = contract;
                                btn.onClick.RemoveAllListeners();
                                btn.onClick.AddListener(() => InstantiateReadyToBuyContractsForDay(selectedDay));

                                break;
                            case SmartContractState.ReadyToSell:
                                btnIcon.sprite = fillIcon;
                                btnIcon.color = colorPending;
                                break;
                            case SmartContractState.ReadyToConfirm:
                                btnIcon.sprite = fillIcon;
                                btnIcon.color = colorCompleted;
                                break;
                            default:
                                btnIcon.sprite = fillIcon;
                                btnIcon.color = colorPending;
                                break;
                        }
                    }
                    else
                    {
                        btn.interactable = false;
                        
                        // 🔮 Future — only show fill if it will appear on that date
                        if (contract.IsVisibleOn(currentDate))
                        {
                            btnIcon.sprite = fillIcon;
                            btnIcon.color = colorPending;
                        }
                        else
                        {
                            btnIcon.sprite = emptyIcon;
                            btnIcon.color = colorNotAssigned;
                        }
                    }
                }
            }
        }

        private void InstantiateReadyToBuyContractsForDay(DateTime selectedDay)
        {
            foreach (Transform child in contractsReadyToBuyContainer)
                Destroy(child.gameObject); // Clear previous entries

            var readyToBuyContracts = _visibleContracts
                .Where(c =>
                    c.GetStateOnDate(selectedDay) == SmartContractState.ReadyToBuy &&
                    c.IsVisibleOn(selectedDay))
                .ToList();

            foreach (var contract in readyToBuyContracts)
            {
                GameObject entry = Instantiate(contractEntryPrefab, contractsReadyToBuyContainer);

                var smartContractView = entry.GetComponent<SmartContractView>();
                if (smartContractView != null)
                {
                    smartContractView.Setup(_presenter);                      // 🔧 Set presenter (required!)
                    _presenter.SelectedDay = selectedDay;                     // 👈 Ensure presenter day is set
                    smartContractView.Initialize(contract);                  // 📦 Populate UI
                }
                else
                {
                    Debug.LogWarning("❌ SmartContractView not found on prefab.");
                }
            }

            // Optional: reveal panel if hidden
            contractsReadyToBuyContainer.gameObject.SetActive(true);
        }
        
        private string RewardAmountString(float savedAmount) => 
            _currentRewardType == RewardType.Money ? $"{savedAmount:F2}" : $"{savedAmount}";
    }
}
*/
