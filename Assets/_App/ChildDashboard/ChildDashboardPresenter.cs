using System;
using System.Collections.Generic;
using System.Linq;
using _App.Dashboard;
using _App.Services;
using _App.Services.BalanceService;
using UnityEngine;

namespace _App.ChildDashboard
{
    public class ChildDashboardPresenter : BaseDashboardPresenter, IDashboardPresenter
    {
        private readonly IChildContractListenerService _contractListenerService;
        private readonly IBalanceListenerService _balanceListenerService;
        private readonly IncomeDistributorService _distributorService = new();
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

                // ⛑️ AppSettingsService must call ContinueWithOnMainThread internally
                _appSettingsService.LoadWeekStartsOn(child.AdminUID, loadedDay =>
                {
                    DateService.SaveWeekStartDay(loadedDay);
                    _view.SetupCalendarButtons();
                    _view.UpdateCalendarColors(_allContracts, child.Uid);
                });

                // ⛑️ ContractService listener must also run on main thread
                _contractListenerService.ListenToChildContracts(childUID, OnContractsChanged);
                
                _balanceListenerService.StopListening(_currentChild.Uid); // cleanup previous
                _balanceListenerService.ListenToBalance(_currentChild.Uid, newBalance =>
                {
                    _currentChild.Balance = newBalance;
                    _view.ShowChildBalance(newBalance);
                });

                // ✅ Daily updater only manipulates data, no UI — safe
                new DailyContractStateUpdater().Run(childUID, isAdmin: false);
            });
        }
        
        private void OnContractsChanged(List<SmartContractModel> allContracts)
        {
            _allContracts = allContracts ?? new List<SmartContractModel>();
            _view.UpdateCalendarColors(_allContracts, _currentChild.Uid);
            FilterAndShowContracts();
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

            _contractService.GetContractById(realId, parent =>
            {
                if (parent == null || parent.IsCopy)
                {
                    Debug.LogWarning($"❌ Contract not found or is a copy: {realId}");
                    return;
                }

                SmartContractState targetState = parent.RequireParentalApproval
                    ? SmartContractState.ReadyToConfirm
                    : SmartContractState.Completed;

                bool shouldGiveReward = targetState == SmartContractState.Completed;

                if (parent.RepeatMode == RepeatType.AsNeeded)
                {
                    _contractService.GetAsNeededCopyByParentId(parent.Id, copy =>
                    {
                        SmartContractModel copyToUse;
                        int queueIndex;

                        if (copy != null)
                        {
                            copy.LoadStateHistory();
                            queueIndex = GetNextAsNeededQueueIndex(copy, _selectedDay);
                            copy.SetStateOnDateWithQueue(_selectedDay, targetState, queueIndex);
                            copyToUse = copy;

                            TemporarilyHideParentContract(parent.Id, _asNeededReSetDelay);
                            FilterAndShowContracts();

                            Debug.Log($"➕ Added queue #{queueIndex} to AsNeeded copy: {copy.Id}");
                        }
                        else
                        {
                            copyToUse = new SmartContractModel
                            {
                                Id = Guid.NewGuid().ToString(),
                                ParentId = parent.Id,
                                Title = parent.Title,
                                IconPath = parent.IconPath ?? "DefaultIcon",
                                RewardAmount = parent.RewardAmount,
                                RequirePhotoProof = parent.RequirePhotoProof,
                                RequireParentalApproval = parent.RequireParentalApproval,
                                RequireNotificationOnThisDevice = parent.RequireNotificationOnThisDevice,
                                DueTime = string.IsNullOrWhiteSpace(parent.DueTime) ? "00:00" : parent.DueTime,
                                AssignedToUid = parent.AssignedToUid,
                                AdminUID = parent.AdminUID,
                                IsCopy = true,
                                RepeatMode = RepeatType.AsNeeded,
                                RepeatDays = new List<DayOfWeek>()
                            };

                            copyToUse.SetStartDate(_selectedDay);
                            queueIndex = 0;
                            copyToUse.SetStateOnDateWithQueue(_selectedDay, targetState, queueIndex);

                            TemporarilyHideParentContract(parent.Id, _asNeededReSetDelay);
                            FilterAndShowContracts();

                            Debug.Log($"🆕 Created new AsNeeded copy: {copyToUse.Id}");
                        }

                        parent.LoadStateHistory(); // state stays untouched

                        _contractService.SaveContract(copyToUse, _ =>
                        {
                            if (shouldGiveReward)
                            {
                                Debug.Log($"✅ Child confirmed AsNeeded contract: {parent.Title} | Queue #{queueIndex} | +{parent.RewardAmount}");
                                _distributorService.DistributeIncome(_currentChild.Uid, parent.RewardAmount, $"Contract '{parent.Title}' confirmed");
                                
                                //UpdateChildBalance(parent.RewardAmount, $"Contract '{parent.Title}' confirmed");
                            }
                            else
                            {
                                Debug.Log($"🕓 Child submitted AsNeeded contract for approval: {parent.Title}");
                            }
                        });
                    });

                    return;
                }

                // 🔁 Flat (non-copy) contract logic
                _contractService.SetContractStateOnDate(parent.Id, _selectedDay, targetState, success =>
                {
                    if (success)
                    {
                        if (shouldGiveReward)
                        {
                            Debug.Log($"✅ Child confirmed flat contract: {parent.Title} | +{parent.RewardAmount}");
                            _distributorService.DistributeIncome(_currentChild.Uid, parent.RewardAmount, $"Contract '{parent.Title}' confirmed");

                            //UpdateChildBalance(parent.RewardAmount, $"Contract '{parent.Title}' confirmed");
                        }
                        else
                        {
                            Debug.Log($"🕓 Child submitted flat contract for approval: {parent.Title}");
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

            // ✅ Normalize visual contract ID
            if (!ContractIdHelper.TryNormalizeVisualContractId(contractId, out var realId, out var queueKeyFromVisualId))
            {
                Debug.LogWarning($"❌ Invalid contract ID format: {contractId}");
                return;
            }

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
        
        public new void UndoPurchaseContract(string contractId, DateTime selectedDay)
        {
            if (_selectedDay == default)
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
                    }
                });
            });
        }
        
        public void OnChildSurpriseButtonPressed()
        {
            Debug.Log("🎁 Child surprise button pressed");
        }

        public void CleanupContractListenerService()
        {
            throw new NotImplementedException();
        }

        public void CleanupChildListenerService()
        {
            throw new NotImplementedException();
        }

        public void Cleanup() => _contractListenerService.StopListening();
    }
}