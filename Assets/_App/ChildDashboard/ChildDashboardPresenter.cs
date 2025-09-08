using System;
using System.Collections.Generic;
using System.Linq;
using _App.Bootstrap;
using _App.Dashboard;
using _App.Models;
using _App.Services;
using _App.Services.BalanceService;
using UnityEngine;
using _App.Services.Notifications;

namespace _App.ChildDashboard
{
    public class ChildDashboardPresenter : BaseDashboardPresenter, IDashboardPresenter
    {
        private readonly IChildContractListenerService _contractListenerService;
        private readonly IBalanceListenerService _balanceListenerService;
        private readonly IncomeDistributorService _distributorService = new();
        
        // instantiate once (DI or quick field)
        private readonly INotificationService _notificationService = new CloudFunctionNotificationService();
        
        private ExtraRewardModel _currentExtraReward;
        private RewardType _type;
        
        public List<SmartContractModel> GetAllContracts() => _allContracts;

        private readonly float _asNeededReSetDelay = 30f;

        public ChildDashboardPresenter(
            IDashboardView view,
            IChildService childService,
            IContractService contractService,
            IRewardService rewardService,
            IAppSettingsService appSettingsService,
            IDateService dateService,
            IChildContractListenerService contractListenerService,
            IBalanceService balanceService,
            IBalanceListenerService balanceListenerService
        ) : base(view, childService, contractService, rewardService, appSettingsService, dateService, balanceService)
        {
            _contractListenerService = contractListenerService;
            _balanceListenerService = balanceListenerService;
        }
        
        public void Initialize(string childUID)
        {
            _selectedDay = _dateService.GetCurrentDay();
            _view.HighlightDayInCalendar(_selectedDay);

            _childService.GetChildById(childUID, child =>
            {
                if (child == null)
                {
                    Debug.LogWarning($"❌ No child found for UID: {childUID}");
                    return;
                }

                Debug.Log($"✅ Loaded child: {child.DisplayName}");
                SetCurrentChild(child);

                // ⛑️ Load week start
                _appSettingsService.LoadWeekStartsOn(child.AdminUID, loadedDay =>
                {
                    DateService.SaveWeekStartDay(loadedDay);
                    _view.SetupCalendarButtons();
                    _view.UpdateCalendarColors(_allContracts, child.Uid);
                });

                // ✅ Contract listener
                _contractListenerService.ListenToChildContracts(childUID, OnContractsChanged);

                // ✅ Balance listener
                _balanceListenerService.StopListening(child.Uid);
                _balanceListenerService.ListenToBalance(child.Uid, newBalance =>
                {
                    _currentChild.Balance = newBalance;
                    _view.ShowChildBalance(newBalance);
                });

                // ✅ Reward listener
                _rewardService.ListenToReward(child.Uid, reward =>
                {
                    if (reward == null)
                    {
                        Debug.Log("📭 Reward was removed.");
                        _currentExtraReward = null;
                        _view.ShowExtraRewardTitle("NO EXTRA REWARD YET");
                        _view.ShowExtraRewardProgress(0, 0, _type);
                        CheckExtraRewardEligibility();
                        return;
                    }

                    Debug.Log($"📡 Reward changed for child {child.DisplayName}");
                    _currentExtraReward = reward;
                    ShowExtraRewardProgressUI();
                });

                // ✅ Update AsNeeded states if needed
                new DailyContractStateUpdater().Run(childUID, isAdmin: false);
                
                TokenOwner.Set(child.Uid);
            });
        }
        
        private void OnContractsChanged(List<SmartContractModel> allContracts)
        {
            _allContracts = allContracts ?? new List<SmartContractModel>();
            _view.UpdateCalendarColors(_allContracts, _currentChild?.Uid ?? string.Empty);
            FilterAndShowContracts();
            ShowExtraRewardProgressUI();
            CheckExtraRewardEligibility();
        }

        public override void OnDaySelected(DateTime day)
        {
            base.OnDaySelected(day);
            FilterAndShowContracts();
        }

        public void ConfirmContractByRole(string contractId) => 
            ChildConfirmContract(contractId);
        public void UndoConfirmContractByRole(string contractId) => 
            ChildUndoConfirmContract(contractId);
        
        private void ChildConfirmContract(string contractId)
        {
            if (_selectedDay == default)
            {
                Debug.LogWarning("❌ No selected day set for confirmation.");
                return;
            }

            // ✅ Normalize visual ID
            if (!ContractIdHelper.TryNormalizeVisualContractId(contractId, out var realId, out _))
            {
                Debug.LogWarning($"❌ Invalid contract ID format: {contractId}");
                return;
            }
            
            //string realId = contractId;

            _contractService.GetContractById(realId, contract =>
            {
                if (contract == null || contract.IsCopy)
                {
                    Debug.LogWarning($"❌ Contract not found or is a copy: {realId}");
                    return;
                }

                SmartContractState targetState = contract.RequireParentalApproval
                    ? SmartContractState.ReadyToConfirm
                    : SmartContractState.Completed;

                bool shouldGiveReward = targetState == SmartContractState.Completed;

                if (contract.RepeatMode == RepeatType.AsNeeded)
                {
                    _contractService.GetAsNeededCopyByParentId(contract.Id, copy =>
                    {
                        SmartContractModel copyToUse;
                        int queueIndex;

                        if (copy != null)
                        {
                            copy.LoadStateHistory();
                            queueIndex = GetNextAsNeededQueueIndex(copy, _selectedDay);
                            copy.SetStateOnDateWithQueue(_selectedDay, targetState, queueIndex);
                            copyToUse = copy;

                            TemporarilyHideParentContract(contract.Id, _asNeededReSetDelay);
                            FilterAndShowContracts();

                            Debug.Log($"➕ Added queue #{queueIndex} to AsNeeded copy: {copy.Id}");
                        }
                        else
                        {
                            copyToUse = new SmartContractModel
                            {
                                Id = Guid.NewGuid().ToString(),
                                ParentId = contract.Id,
                                Title = contract.Title,
                                IconPath = contract.IconPath ?? "DefaultIcon",
                                RewardAmount = contract.RewardAmount,
                                RequirePhotoProof = contract.RequirePhotoProof,
                                RequireParentalApproval = contract.RequireParentalApproval,
                                RequireNotificationOnThisDevice = contract.RequireNotificationOnThisDevice,
                                DueTime = string.IsNullOrWhiteSpace(contract.DueTime) ? "00:00" : contract.DueTime,
                                AssignedToUid = contract.AssignedToUid,
                                AdminUID = contract.AdminUID,
                                IsCopy = true,
                                RepeatMode = RepeatType.AsNeeded,
                                RepeatDays = new List<DayOfWeek>()
                            };

                            copyToUse.SetStartDate(_selectedDay);
                            queueIndex = 0;
                            copyToUse.SetStateOnDateWithQueue(_selectedDay, targetState, queueIndex);

                            TemporarilyHideParentContract(contract.Id, _asNeededReSetDelay);
                            FilterAndShowContracts();

                            Debug.Log($"🆕 Created new AsNeeded copy: {copyToUse.Id}");
                        }

                        contract.LoadStateHistory(); // state stays untouched

                        _contractService.SaveContract(copyToUse, _ =>
                        {
                            if (shouldGiveReward)
                            {
                                Debug.Log($"✅ Child confirmed AsNeeded contract: {contract.Title} | Queue #{queueIndex} | +{contract.RewardAmount}");
                                _distributorService.DistributeIncome(_currentChild.Uid, contract.RewardAmount, $"Contract '{contract.Title}' confirmed");
                                
                                //UpdateChildBalance(parent.RewardAmount, $"Contract '{parent.Title}' confirmed");
                                
                                var actorUid  = _currentChild.Uid;     // who did the action
                                var actorRole = "child";
                                var targetUid = contract.AdminUID;
                                
                                Debug.Log($"[Notify] target:{targetUid} actor:{actorUid}/{actorRole} child:{_currentChild?.Uid} admin:{_currentChild?.AdminUID}");

                                _notificationService.Notify(
                                    targetUid,
                                    NotificationEventType.ContractSubmittedByChild,
                                    contract,
                                    actorUid,
                                    actorRole
                                );

                            }
                            else
                            {
                                Debug.Log($"🕓 Child submitted AsNeeded contract for approval: {contract.Title}");
                                
                                var actorUid  = _currentChild.Uid;     // who did the action
                                var actorRole = "child";
                                var targetUid = contract.AdminUID;
                                
                                Debug.Log($"[Notify] target:{targetUid} actor:{actorUid}/{actorRole} child:{_currentChild?.Uid} admin:{_currentChild?.AdminUID}");

                                _notificationService.Notify(
                                    targetUid,
                                    NotificationEventType.ContractSubmittedByChild,
                                    contract,
                                    actorUid,
                                    actorRole
                                );
                            }
                        });
                    });

                    return;
                }

                // 🔁 Flat (non-copy) contract logic
                _contractService.SetContractStateOnDate(contract.Id, _selectedDay, targetState, success =>
                {
                    if (success)
                    {
                        if (shouldGiveReward)
                        {
                            Debug.Log($"✅ Child confirmed flat contract: {contract.Title} | +{contract.RewardAmount}");
                            _distributorService.DistributeIncome(_currentChild.Uid, contract.RewardAmount, $"Contract '{contract.Title}' confirmed");

                            //UpdateChildBalance(parent.RewardAmount, $"Contract '{parent.Title}' confirmed");
                            
                            var actorUid  = _currentChild.Uid;     // who did the action
                            var actorRole = "child";
                            var targetUid = contract.AdminUID;
                            
                            Debug.Log($"[Notify] target:{targetUid} actor:{actorUid}/{actorRole} child:{_currentChild?.Uid} admin:{_currentChild?.AdminUID}");

                            _notificationService.Notify(
                                targetUid,
                                NotificationEventType.ContractSubmittedByChild,
                                contract,
                                actorUid,
                                actorRole
                            );
                        }
                        else
                        {
                            Debug.Log($"🕓 Child submitted flat contract for approval: {contract.Title}");
                            
                            var actorUid  = _currentChild.Uid;     // who did the action
                            var actorRole = "child";
                            var targetUid = contract.AdminUID;
                            
                            Debug.Log($"[Notify] target:{targetUid} actor:{actorUid}/{actorRole} child:{_currentChild?.Uid} admin:{_currentChild?.AdminUID}");

                            _notificationService.Notify(
                                targetUid,
                                NotificationEventType.ContractSubmittedByChild,
                                contract,
                                actorUid,
                                actorRole
                            );
                        }
                    }
                });
            });
        }
        
       private void ChildUndoConfirmContract(string contractId)
        {
            if (_selectedDay == default)
            {
                Debug.LogWarning("❌ Selected day is not set for undo.");
                return;
            }
            
            string realId = contractId;
            string queueKeyFromVisualId = null;
            if (contractId.Contains("#"))
                ContractIdHelper.TryNormalizeVisualContractId(contractId, out realId, out queueKeyFromVisualId);

            _contractService.GetContractById(realId, contract =>
            {
                if (contract == null)
                {
                    Debug.LogWarning($"❌ Undo failed: Contract not found: {realId}");
                    return;
                }

                // ✅ AsNeeded COPY handling
                if (contract.IsCopy && contract.RepeatMode == RepeatType.AsNeeded)
                {
                    contract.LoadStateHistory();

                    string queueKey = queueKeyFromVisualId;

                    if (string.IsNullOrEmpty(queueKey))
                    {
                        string prefix = _selectedDay.ToString("yyyy-MM-dd") + "#";
                        queueKey = contract.StateHistory.Keys
                            .Where(k => k.StartsWith(prefix))
                            .OrderByDescending(ExtractQueueIndex)
                            .FirstOrDefault();
                    }

                    if (string.IsNullOrEmpty(queueKey) || !contract.StateHistory.TryGetValue(queueKey, out var stateList))
                    {
                        Debug.LogWarning($"⚠️ No queued state to undo for copy: {contract.Id}");
                        return;
                    }

                    bool wasCompleted = stateList.Any(r => r.State == SmartContractState.Completed);
                    bool wasWaitingApproval = stateList.Any(r => r.State == SmartContractState.ReadyToConfirm);

                    contract.StateHistory.Remove(queueKey);
                    contract.SyncStateHistory();

                    string parentId = contract.ParentId;

                    _contractService.GetContractById(parentId, parent =>
                    {
                        if (parent != null)
                        {
                            parent.LoadStateHistory();
                            parent.StateHistory.Remove(queueKey);
                            parent.SyncStateHistory();
                            _contractService.SaveContract(parent, _ => { });
                        }

                        void FinalizeUndo(bool success)
                        {
                            if (!success) return;

                            if (wasCompleted)
                            {
                                Debug.Log($"↩️ Child undid confirmation (completed) for '{contract.Title}' | -{contract.RewardAmount}");
                                
                                _distributorService.UndoDistribution(
                                    _currentChild.Uid,
                                    contract.RewardAmount,
                                    $"Undo confirmation for contract '{contract.Title}'"
                                );
                                
                                var actorUid  = _currentChild.Uid;     // who did the action
                                var actorRole = "child";
                                var targetUid = contract.AdminUID;
                                
                                Debug.Log($"[Notify] target:{targetUid} actor:{actorUid}/{actorRole} child:{_currentChild?.Uid} admin:{_currentChild?.AdminUID}");

                                _notificationService.Notify(
                                    targetUid,
                                    NotificationEventType.ContractUndoByChild,
                                    contract,
                                    actorUid,
                                    actorRole
                                );
                            }
                            else if (wasWaitingApproval)
                            {
                                Debug.Log($"↩️ Child canceled approval request for '{contract.Title}'");
                            }
                        }

                        if (contract.StateHistory.Count == 0)
                        {
                            _contractService.DeleteContract(contract.Id, FinalizeUndo);
                        }
                        else
                        {
                            _contractService.SaveContract(contract, FinalizeUndo);
                        }
                    });

                    return;
                }

                // ✅ Flat contract fallback
                SmartContractState previousState = contract.GetStateOnDate(_selectedDay);

                if (previousState == SmartContractState.Completed || previousState == SmartContractState.ReadyToConfirm)
                {
                    contract.SetStateOnDate(_selectedDay, SmartContractState.ReadyToSell);
                    _contractService.SaveContract(contract, success =>
                    {
                        if (success)
                        {
                            if (previousState == SmartContractState.Completed)
                            {
                                _distributorService.UndoDistribution(
                                    _currentChild.Uid,
                                    contract.RewardAmount,
                                    $"Undo confirmation for contract '{contract.Title}'"
                                );
                            }

                            Debug.Log($"↩️ Undo confirmed: {contract.Title} | State: {previousState} → ReadyToSell | Day: {_selectedDay:yyyy-MM-dd}");
                            
                            var actorUid  = _currentChild.Uid;     // who did the action
                            var actorRole = "child";
                            var targetUid = contract.AdminUID;
                                
                            Debug.Log($"[Notify] target:{targetUid} actor:{actorUid}/{actorRole} child:{_currentChild?.Uid} admin:{_currentChild?.AdminUID}");

                            _notificationService.Notify(
                                targetUid,
                                NotificationEventType.ContractUndoByChild,
                                contract,
                                actorUid,
                                actorRole
                            );
                        }
                    });
                }
                else
                {
                    Debug.Log($"ℹ️ No undoable state found for contract: {contract.Title} on {_selectedDay:yyyy-MM-dd}");
                }
            });
        }
       
        private int ExtractQueueIndex(string key)
        {
            var parts = key.Split('#');
            if (parts.Length != 2) return -1;

            var queuePart = parts[1].Split(':')[0];
            return int.TryParse(queuePart, out int result) ? result : -1;
        }
        
        public void ChildBuyAdminSellContract(string contractId, DateTime selectedDay) => 
            BuyContract(contractId, selectedDay);
        
        public void UndoPurchaseContract(string contractId, DateTime selectedDay)
        {
            if (selectedDay == default)
            {
                Debug.LogWarning("❌ Selected day is not set.");
                return;
            }

            _contractService.GetContractById(contractId, contract =>
            {
                if (contract == null)
                {
                    Debug.LogWarning($"❌ Contract not found: {contractId}");
                    return;
                }

                // 🚫 Copies can’t be purchased
                if (contract.IsCopy)
                {
                    Debug.LogWarning($"🚫 Cannot undo purchase for copy: {contract.Title}");
                    return;
                }

                contract.LoadStateHistory();

                string key = selectedDay.ToString("yyyy-MM-dd");

                if (!contract.StateHistory.TryGetValue(key, out var records) ||
                    !records.Any(r => r.State == SmartContractState.Purchased))
                {
                    Debug.LogWarning($"⚠️ No Purchased state to undo for contract: {contract.Title}");
                    return;
                }

                // ✅ Replace with ReadyToBuy instead of removing entirely
                contract.StateHistory[key] = new List<SmartContractModel.StateRecord>
                {
                    new SmartContractModel.StateRecord
                    {
                        State = SmartContractState.ReadyToBuy
                    }
                };

                contract.SyncStateHistory();

                _contractService.SaveContract(contract, success =>
                {
                    if (success)
                    {
                        Debug.Log($"↩️ Purchase undone: {contract.Title} reverted to ReadyToBuy (+{contract.RewardAmount})");

                        _distributorService.UndoPurchaseContract(
                            _currentChild.Uid,
                            contract.RewardAmount,
                            $"Undo purchase of contract '{contract.Title}'"
                        );
                        
                        var actorUid  = _currentChild.Uid;     // who did the action
                        var actorRole = "child";
                        var targetUid = contract.AdminUID;
                        
                        Debug.Log($"[Notify] target:{targetUid} actor:{actorUid}/{actorRole} child:{_currentChild?.Uid} admin:{_currentChild?.AdminUID}");

                        _notificationService.Notify(
                            targetUid,
                            NotificationEventType.ContractUndoPurchasedByChild,
                            contract,
                            actorUid,
                            actorRole
                        );
                    }
                });
            });
        }

        public void SaveContract(SmartContractModel contract)
        {
            //contract.AdminUID = _adminUID;
            contract.AdminUID = _currentChild?.AdminUID;

            _contractService.SaveContract(contract, success =>
            {
                if (!success)
                {
                    Debug.LogWarning("❌ Failed to save contract");
                    return;
                }
                if (contract.IsSurprise)
                {
                    var actorUid  = _currentChild.Uid;     // who did the action
                    var actorRole = "child";
                    var targetUid = contract.AdminUID;
                    
                    Debug.Log($"[Notify] target:{targetUid} actor:{actorUid}/{actorRole} child:{_currentChild?.Uid} admin:{_currentChild?.AdminUID}");

                    _notificationService.Notify(
                        targetUid,
                        NotificationEventType.SurpriseContractCreated,
                        contract,
                        actorUid,
                        actorRole
                    );
                }
            });
        }

        public void EditContract(string contractId)
        {
            // ✅ Normalize visual ID first
            if (!ContractIdHelper.TryNormalizeVisualContractId(contractId, out var realId, out _))
                realId = contractId;

            _contractService.GetContractById(realId, contract =>
            {
                if (contract == null)
                {
                    Debug.LogWarning($"❌ Failed to load contract for editing: {realId}");
                    return;
                }
                
                Debug.Log($"🎯 Editing contract: {contract.Title} | ID: {contract.Id}");

                SmartContractDraft.LoadFromModel(contract);
                _view.OnChildSurpriseContractEdit(contract); // this triggers InitializeUI(contract)
                
                if (contract.IsSurprise)
                {
                    var actorUid  = _currentChild.Uid;     // who did the action
                    var actorRole = "child";
                    var targetUid = contract.AdminUID;
                    
                    Debug.Log($"[Notify] target:{targetUid} actor:{actorUid}/{actorRole} child:{_currentChild?.Uid} admin:{_currentChild?.AdminUID}");

                    _notificationService.Notify(
                        targetUid,
                        NotificationEventType.SurpriseContractUpdated,
                        contract,
                        actorUid,
                        actorRole
                    );
                }
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

        public void OnChildSurpriseButtonPressed() => _view.OnChildSurpriseContractCreate();

        public void OnRewardButtonPressed() => _view.OpenRewardPanel(false);
        public void OpenExtraRewardCreator()
        {
            if (_currentChild == null)
            {
                Debug.LogWarning("❌ No child selected.");
                return;
            }

            // Retrieve the existing reward for this child if it exists
            _rewardService.LoadReward(_currentChild.Uid, existingReward =>
            {
                _view.ShowExtraRewardCreator(_currentChild.Uid, () =>
                {
                    Debug.Log("🎉 Extra reward created or cancelled.");
                    CheckExtraRewardEligibility();
                    ShowExtraRewardProgressUI();
                }, existingReward);
            });
        }

        public void ClaimExtraReward()
        {
            if (_currentChild == null) return;

            _rewardService.LoadReward(_currentChild.Uid, reward =>
            {
                if (reward == null || reward.IsClaimed)
                {
                    Debug.LogWarning("Reward already claimed or not found.");
                    return;
                }

                int fulfilled = 0;

                foreach (var dayOfWeek in reward.SelectedDays)
                {
                    var date = GetClosestPastOrTodayDate(dayOfWeek);
                    
                    var contracts = _allContracts
                        .Where(c => c.AssignedToUid == _currentChild.Uid)
                        .Where(c => !c.IsCopy)
                        .Where(c => c.RepeatMode == RepeatType.EveryDay || c.RepeatMode == RepeatType.SpecificDays)
                        .Where(c => c.IsVisibleOn(date))
                        .ToList();

                    bool allComplete = contracts.Count > 0 && contracts.All(c =>
                        c.HasStateOnDate(date, SmartContractState.Completed) ||
                        c.HasStateOnDate(date, SmartContractState.Purchased));

                    if (allComplete)
                        fulfilled++;
                }

                if (fulfilled != reward.SelectedDays.Count)
                {
                    Debug.LogWarning("❌ Cannot claim reward: not all days fulfilled.");
                    return;
                }

                reward.IsClaimed = true;

                _rewardService.DeleteReward(_currentChild.Uid, deleted =>
                {
                    if (deleted)
                    {
                        Debug.Log($"🎉 Reward claimed and deleted by child: +{reward.RewardAmount}");
                        
                        _distributorService.DistributeIncome(
                            _currentChild.Uid,
                            reward.RewardAmount,
                            $"Child Claimed Extra Reward: {reward.RewardTitle}"
                        );

                        _view.ShowRewardPayout(reward);
                        CheckExtraRewardEligibility();
                    }
                    else
                    {
                        Debug.LogError("❌ Failed to delete reward after claiming (child).");
                    }
                });
            });
        }
        
        private DateTime GetClosestPastOrTodayDate(DayOfWeek targetDay)
        {
            var today = DateTime.Today;
            int daysBack = ((int)today.DayOfWeek - (int)targetDay + 7) % 7;
            return today.AddDays(-daysBack);
        }
        
        public void CleanupContractListenerService()
            => _contractListenerService.StopListening();

        public void CleanupChildListenerService()
            => _childService.StopListening();
        
        public void Cleanup() => _contractListenerService.StopListening();
    }
}