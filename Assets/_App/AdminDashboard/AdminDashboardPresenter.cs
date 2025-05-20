using System;
using System.Collections.Generic;
using System.Linq;
using _App.Bootstrap;
using _App.Services;
using Firebase.Database;
using UnityEngine;

namespace _App.AdminDashboard
{
    public class AdminDashboardPresenter
    {
        private readonly IAdminDashboardView _view;
        private readonly IChildService _childService;
        private readonly IContractService _contractService;
        private readonly IRewardService _rewardService;
        private readonly IDateService _dateService;
        private readonly IAdminContractListenerService _contractListenerService;
        
        private string _adminUID;
        private ChildModel _currentChild;
        private List<ChildModel> _children = new();
        private DateTime _selectedDay;
        private List<SmartContractModel> _allContracts = new();
        public List<ChildModel> GetAllChildren() => _children;
        
        public string AdminUID => _adminUID;
        public DateTime SelectedDay => _selectedDay;
        
        public ChildModel CurrentChild => _currentChild;

        public AdminDashboardPresenter(
            IAdminDashboardView view,
            IChildService childService,
            IContractService contractService,
            IRewardService rewardService,
            IDateService dateService,
            IAdminContractListenerService contractListenerService
        )
        {
            _view = view;
            _childService = childService;
            _contractService = contractService;
            _rewardService = rewardService;
            _dateService = dateService;
            _contractListenerService = contractListenerService;
        }

        public void Initialize(string adminUID)
        {
            _adminUID = adminUID;
            _selectedDay = _dateService.GetCurrentDay();
            _view.ShowDaySelection(_selectedDay);

            _childService.ListenToChildren(adminUID, OnChildrenUpdated);
            _contractListenerService.ListenToAdminContracts(adminUID, OnContractsChanged);

            var isAdmin = UserSession.IsAdmin;

            new DailyContractStateUpdater().Run(adminUID, isAdmin: true);
        }

        private void OnChildrenUpdated(List<ChildModel> children)
        {
            if (children == null || children.Count == 0)
            {
                _view.ShowExtraRewardStatus("No children linked.");
                return;
            }

            _children = children;
            _view.ShowChildren(children);

            if (_currentChild == null)
            {
                SetCurrentChild(children.First());
                _view.SelectToday(); // 👈 move here
            }
            else
            {
                var existing = children.FirstOrDefault(c => c.Uid == _currentChild.Uid);
                if (existing != null)
                {
                    SetCurrentChild(existing);
                    _view.SelectToday(); // 👈 also works here
                }
            }
        }

        private void OnContractsChanged(List<SmartContractModel> allContracts)
        {
            _allContracts = allContracts ?? new List<SmartContractModel>();

            _view.UpdateCalendarColors(_allContracts, _currentChild.Uid);

            if (_currentChild != null)
                FilterAndShowContracts();
        }
        
        private void FilterAndShowContracts()
        {
            if (_currentChild == null)
                return;

            var selectedDay = _selectedDay.Date;
            var today = DateTime.Today;

            var visibleContracts = new List<SmartContractModel>();
            var addedIds = new HashSet<string>();

            foreach (var contract in _allContracts)
            {
                if (contract.AssignedToUid != _currentChild.Uid)
                    continue;

                contract.LoadStateHistory();
                
                if (contract.GetStateOnDate(selectedDay) == SmartContractState.Hidden)
                    continue;

                // ✅ Skip ONCE or AS_NEEDED if selectedDay is before today
                if (selectedDay < today &&
                    contract.RepeatMode is RepeatType.Once or RepeatType.AsNeeded)
                    continue;

                if (contract.ShouldAppearInEveryDayGroup(selectedDay))
                {
                    if (addedIds.Add(contract.Id))
                        visibleContracts.Add(contract);
                }
                else if (contract.IsVisibleOn(selectedDay))
                {
                    if (addedIds.Add(contract.Id))
                        visibleContracts.Add(contract);
                }
            }

            // Grouping
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
        
        public void SetCurrentChild(ChildModel child)
        {
            _currentChild = child;
            _view.ShowCurrentChild(child);
            _view.ShowChildBalance(child.Balance);
            _view.CloseProfileSelector();
            _view.UpdateCalendarColors(_allContracts, _currentChild.Uid);
            FilterAndShowContracts();
            CheckExtraRewardEligibility();
        }

        public void OnDaySelected(DateTime day)
        {
            _selectedDay = day;
            _view.ShowDaySelection(day);
            _view.ShowSelectedDay(day);
            FilterAndShowContracts();
        }

        public void OnAddContractButtonPressed()
        {
            if (_currentChild == null) return;

            SmartContractDraft.Reset(_currentChild.Uid);
            SmartContractDraft.StartDate = _selectedDay;

            _view.OpenContractCreator();
        }

        public void PrepareNewContractDraft()
        {
            if (_currentChild == null)
            {
                Debug.LogWarning("No child selected.");
                return;
            }

            SmartContractDraft.Reset(_currentChild.Uid);
            SmartContractDraft.StartDate = _selectedDay;
        }

        public void OnAdjustBalanceButtonPressed() => _view.OpenAdjustBalancePanel();

        public void OnRewardButtonPressed() => _view.OpenRewardPanel();

        public void OnSelectProfileButtonPressed() => _view.OpenProfileSelector();

        public void OnExitSelectProfileButtonPressed() => _view.CloseProfileSelector();

        private SmartContractModel CreateCopyCompletedToday(SmartContractModel parent, DateTime date)
        {
            if (parent == null) return null;

            var copy = new SmartContractModel
            {
                Id = Guid.NewGuid().ToString(),
                ParentId = parent.Id,
                Title = parent.Title,
                IconPath = parent.IconPath,
                RewardAmount = parent.RewardAmount,
                RequirePhotoProof = parent.RequirePhotoProof,
                RequireParentalApproval = parent.RequireParentalApproval,
                RequireNotificationOnThisDevice = parent.RequireNotificationOnThisDevice,
                DueTime = parent.DueTime,
                AssignedToUid = parent.AssignedToUid,
                AdminUID = parent.AdminUID,
                IsCopy = true,

                // 👇 Optional but safe to keep consistent format
                RepeatMode = RepeatType.AsNeeded, // OR RepeatType.Once — does not affect grouping now
                RepeatDays = new List<DayOfWeek>()
            };

            copy.SetStartDate(date);
            copy.SetStateOnDate(date, SmartContractState.Completed);

            return copy;
        }
        
         public void ConfirmContract(string contractId)
        {
            _contractService.GetContractById(contractId, contract =>
            {
                if (contract == null)
                {
                    Debug.LogWarning($"❌ Contract not found: {contractId}");
                    return;
                }

                if (contract.IsCopy)
                    return;

                // ✅ AsNeeded → Create copy
                if (contract.RepeatMode == RepeatType.AsNeeded)
                {
                    string parentId = contract.Id;

                    // ✅ Check if a copy already exists for this parent and selected day
                    var existingCopy = _allContracts.FirstOrDefault(c =>
                        c.IsCopy &&
                        c.ParentId == parentId &&
                        c.AssignedToUid == contract.AssignedToUid &&
                        c.GetStartDate().Date == _selectedDay.Date);

                    if (existingCopy != null)
                    {
                        // 🟢 Just mark it as completed
                        existingCopy.SetStateOnDate(_selectedDay, SmartContractState.Completed);

                        _contractService.SaveContract(existingCopy, success =>
                        {
                            if (!success)
                            {
                                Debug.LogWarning("❌ Failed to update existing AsNeeded copy.");
                                return;
                            }

                            UpdateChildBalance(contract.RewardAmount);
                        });
                    }
                    else
                    {
                        // 🆕 No copy yet — create one
                        var copy = CreateCopyCompletedToday(contract, _selectedDay);

                        _contractService.SaveContract(copy, success =>
                        {
                            if (!success)
                            {
                                Debug.LogWarning("❌ Failed to save AsNeeded copy.");
                                return;
                            }

                            UpdateChildBalance(contract.RewardAmount);
                        });
                    }

                    return;
                }

                // ✅ Default path: mark directly
                _contractService.SetContractStateOnDate(contract.Id, _selectedDay, SmartContractState.Completed, success =>
                {
                    if (!success)
                    {
                        Debug.LogWarning("❌ Failed to update contract state.");
                        return;
                    }
                    UpdateChildBalance(contract.RewardAmount);
                });
            });
        }

        public void UndoConfirmContract(string contractId)
        {
            _contractService.GetContractById(contractId, contract =>
            {
                if (contract == null)
                {
                    Debug.LogWarning($"❌ Undo failed: Contract not found: {contractId}");
                    return;
                }

                if (contract.IsCopy)
                {
                    if (contract.HasStateOnDate(_selectedDay, SmartContractState.Completed))
                    {
                        contract.RemoveStateOnDate(_selectedDay);

                        // After removing, check if the copy has any other day left
                        contract.LoadStateHistory(); // just to be sure it's up-to-date

                        if (contract.StateHistory.Count == 0)
                        {
                            // ✅ Delete the whole copy if it's now empty
                            _contractService.DeleteContract(contract.Id, success =>
                            {
                                if (!success)
                                {
                                    Debug.LogError("❌ Failed to delete empty AsNeeded copy.");
                                    return;
                                }

                                UpdateChildBalance(-contract.RewardAmount);
                            });
                        }
                        else
                        {
                            // ✅ Just save the updated state
                            _contractService.SaveContract(contract, success =>
                            {
                                if (!success)
                                {
                                    Debug.LogError("❌ Failed to update AsNeeded copy after undo.");
                                    return;
                                }

                                UpdateChildBalance(-contract.RewardAmount);
                            });
                        }
                    }
                    
                    return;
                }

                var state = contract.AssignedToUid == FirebaseInit.CurrentUserId
                    ? SmartContractState.ReadyToSell
                    : SmartContractState.ReadyToConfirm;
                
                _contractService.SetContractStateOnDate(contract.Id, _selectedDay, state, success =>
                {
                    if (!success)
                    {
                        Debug.LogError("❌ Failed to revert contract state.");
                        return;
                    }
                    UpdateChildBalance(-contract.RewardAmount);
                });
            });
        }
        
        private void UpdateChildBalance(float delta)
        {
            if (_currentChild == null)
                return;

            float newBalance = _currentChild.Balance + delta;

            _childService.UpdateBalance(_currentChild.Uid, newBalance, success =>
            {
                if (success)
                {
                    _currentChild.Balance = newBalance;
                    _view.ShowChildBalance(newBalance);
                }
                else
                {
                    Debug.LogWarning("❌ Failed to update child balance.");
                }
            });
        }
        
        public void EditContract(string contractId)
        {
            _contractService.GetContractById(contractId, contract =>
            {
                if (contract == null)
                {
                    Debug.LogWarning("❌ Failed to load contract for editing.");
                    return;
                }

                SmartContractDraft.LoadFromModel(contract); // optional helper method
                _view.OpenEditContractPanel(); // shows step 2 directly
            });
        }
        
        public void SaveContract(SmartContractModel contract)
        {
            contract.AdminUID = _adminUID;

            _contractService.SaveContract(contract, success =>
            {
                if (!success)
                    Debug.LogWarning("❌ Failed to save contract");
            });
        }

        public void DeleteContract(string contractId)
        {
            _contractService.DeleteContract(contractId, success =>
            {
                if (!success)
                    Debug.LogWarning("❌ Failed to delete contract");
            });
        }

        private void CheckExtraRewardEligibility()
        {
            if (_currentChild == null)
                return;

            var weekStart = _dateService.GetWeekStart(_selectedDay);
            _rewardService.CheckExtraRewardEligibility(_currentChild.Uid, weekStart, eligible =>
            {
                _view.ShowExtraRewardEligible(eligible);
            });
        }

        public void PayoutExtraReward()
        {
            if (_currentChild == null)
                return;

            _rewardService.PayoutReward(_currentChild.Uid, reward =>
            {
                if (reward != null)
                    _view.ShowRewardPayout(reward);
            });
        }

        public void Cleanup() => _contractListenerService.StopListening();
    }
}
