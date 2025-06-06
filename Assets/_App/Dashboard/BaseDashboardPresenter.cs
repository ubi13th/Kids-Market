using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using _App.ChildDashboard;
using _App.Models;
using _App.Services;
using _App.Services.BalanceService;
using UnityEngine;

namespace _App.Dashboard
{
    public abstract class BaseDashboardPresenter
    {
        protected readonly IDashboardView _view;
        protected readonly IChildService _childService;
        protected readonly IContractService _contractService;
        protected readonly IRewardService _rewardService;
        protected readonly IAppSettingsService _appSettingsService;
        protected readonly IDateService _dateService;
        protected readonly IBalanceService _balanceService;

        protected string _adminUID;
        protected ChildModel _currentChild;
        protected DateTime _selectedDay;
        protected List<ChildModel> _children = new();
        protected List<SmartContractModel> _allContracts = new();

        public string AdminUID => _adminUID;
        public ChildModel CurrentChild => _currentChild;
        public DateTime SelectedDay => _selectedDay;
        public List<ChildModel> GetAllChildren() => _children;
        
        private readonly Dictionary<string, float> _temporarilyHiddenParents = new(); // parentId → hideUntilTime

        public BaseDashboardPresenter(
            IDashboardView view,
            IChildService childService,
            IContractService contractService,
            IRewardService rewardService,
            IAppSettingsService settingsService,
            IDateService dateService,
            IBalanceService balanceService)
        {
            _view = view;
            _childService = childService;
            _contractService = contractService;
            _rewardService = rewardService;
            _appSettingsService = settingsService;
            _dateService = dateService;
            _balanceService = balanceService;
        }

        public virtual void OnDaySelected(DateTime day)
        {
            _selectedDay = day;
            _view.HighlightDayInCalendar(day);
            _view.ShowSelectedDay(day);
        }

        public virtual void SetCurrentChild(ChildModel child)
        {
            _currentChild = child;
            _view.ShowCurrentChild(child);
            _view.ShowChildBalance(child.Balance);
            _view.CloseProfileSelector();
            //_view.ShowBalanceHistory(child.BalanceHistory ?? new List<BalanceHistoryRecord>());
        }
        
        public void TemporarilyHideParentContract(string parentId, float seconds)
        {
            _temporarilyHiddenParents[parentId] = Time.realtimeSinceStartup + seconds;

            if (_view is MonoBehaviour mb)
                mb.StartCoroutine(RunDelayedContractRefresh(seconds));
        }

        private IEnumerator RunDelayedContractRefresh(float delay)
        {
            yield return new WaitForSeconds(delay);
            FilterAndShowContracts();
        }

        public virtual void FilterAndShowContracts()
        {
            if (_currentChild == null) return;

            var selectedDay = _selectedDay.Date;
            var today = DateTime.Today;

            var visibleContracts = new List<SmartContractModel>();
            var addedIds = new HashSet<string>();

            foreach (var contract in _allContracts)
            {
                if (contract.AssignedToUid != _currentChild.Uid)
                    continue;

                if (!contract.IsCopy && contract.RepeatMode == RepeatType.AsNeeded)
                {
                    if (_temporarilyHiddenParents.TryGetValue(contract.Id, out float until))
                    {
                        if (Time.realtimeSinceStartup < until)
                            continue;
                        else
                            _temporarilyHiddenParents.Remove(contract.Id);
                    }
                }

                contract.LoadStateHistory();

                if (selectedDay < today && !contract.IsCopy &&
                    contract.RepeatMode is RepeatType.Once or RepeatType.AsNeeded)
                    continue;

                if (contract.ShouldAppearInEveryDayGroup(selectedDay) || contract.IsVisibleOn(selectedDay))
                {
                    if (addedIds.Add(contract.Id))
                        visibleContracts.Add(contract);
                }
            }

            var grouped = new Dictionary<RepeatType, List<SmartContractModel>>
            {
                [RepeatType.EveryDay] = new(),
                [RepeatType.SpecificDays] = new(),
                [RepeatType.Once] = new(),
                [RepeatType.AsNeeded] = new()
            };

            foreach (var contract in visibleContracts)
            {
                if (contract.ShouldAppearInEveryDayGroup(selectedDay))
                    grouped[RepeatType.EveryDay].Add(contract);
                else
                    grouped[contract.RepeatMode].Add(contract);
            }

            _view.ShowGroupedContracts(grouped);
        }
        
        protected void BuyContract(string contractId)
        {
            if (_selectedDay == default)
            {
                Debug.LogWarning("❌ No selected day selected.");
                return;
            }

            _contractService.GetContractById(contractId, contract =>
            {
                if (contract == null)
                {
                    Debug.LogWarning($"❌ Contract not found: {contractId}");
                    return;
                }

                if (contract.IsCopy)
                {
                    Debug.LogWarning($"🚫 Cannot buy a copy contract: {contract.Title}");
                    return;
                }

                if (!contract.HasStateOnDate(_selectedDay, SmartContractState.ReadyToBuy))
                {
                    Debug.Log($"ℹ️ Contract is not ReadyToBuy on {_selectedDay:yyyy-MM-dd}");
                    return;
                }

                _contractService.SetContractStateOnDate(contract.Id, _selectedDay, SmartContractState.Purchased, success =>
                {
                    if (!success)
                    {
                        Debug.LogWarning($"❌ Failed to set state to Purchased for contract: {contract.Title}");
                        return;
                    }

                    _balanceService.AdjustBalance(
                        _currentChild.Uid,
                        -contract.RewardAmount,
                        $"Contract '{contract.Title}' purchased",
                        recordHistory: false
                    );

                    Debug.Log($"✅ Contract purchased: {contract.Title} | Amount: -{contract.RewardAmount}");
                });
            });
        }

        protected int GetNextAsNeededQueueIndex(SmartContractModel parentContract, DateTime date)
        {
            string prefix = date.ToString("yyyy-MM-dd#");
            int maxIndex = -1;

            foreach (var key in parentContract.StateHistory.Keys)
            {
                if (key.StartsWith(prefix) && TryParseQueueIndex(key, out int index))
                {
                    maxIndex = Mathf.Max(maxIndex, index);
                }
            }

            return maxIndex + 1;
        }

        private bool TryParseQueueIndex(string key, out int index)
        {
            index = 0;
            var parts = key.Split('#');
            return parts.Length == 2 && int.TryParse(parts[1], out index);
        }
        
        public int GetLastQueueIndexForDay(SmartContractModel contract, DateTime day)
        {
            string keyPrefix = day.ToString("yyyy-MM-dd") + "#";

            var matchingKeys = contract.StateHistory.Keys
                .Where(k => k.StartsWith(keyPrefix))
                .ToList();

            if (matchingKeys.Count == 0)
                return -1;

            return matchingKeys
                .Select(ExtractQueueIndex)
                .Max();
        }
        
        private int ExtractQueueIndex(string key)
        {
            var parts = key.Split('#');
            if (parts.Length != 2) return -1;

            var queuePart = parts[1].Split(':')[0]; // strips ":3"
            return int.TryParse(queuePart, out int result) ? result : -1;
        }
        
        protected void UpdateChildBalance(float delta, string reason = "No Notes", bool recordHistory = false)
        {
            if (_currentChild == null) return;

            _balanceService.AdjustBalance(_currentChild.Uid, delta, reason, recordHistory, success =>
            {
                if (success)
                {
                    _currentChild.Balance += delta;
                    _view.ShowChildBalance(_currentChild.Balance);
                }
            });
        }

        public virtual void PayoutExtraReward()
        {
            if (_currentChild == null) return;

            _rewardService.PayoutReward(_currentChild.Uid, reward =>
            {
                if (reward != null)
                    _view.ShowRewardPayout(reward);
            });
        }

        public virtual void CheckExtraRewardEligibility()
        {
            if (_currentChild == null) return;

            var weekStart = _dateService.GetWeekStart(_selectedDay);
            _rewardService.CheckExtraRewardEligibility(_currentChild.Uid, weekStart, eligible =>
            {
                _view.ShowExtraRewardEligible(eligible);
            });
        }

        public virtual void OnExitSelectProfileButtonPressed() => _view.CloseProfileSelector();
        public virtual void OnSelectProfileButtonPressed() => _view.OpenProfileSelector();
    }
}
