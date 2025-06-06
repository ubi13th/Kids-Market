using System;
using System.Collections.Generic;
using System.Linq;
using _App.ChildDashboard;
using _App.Dashboard;
using _App.Services;
using _App.Services.BalanceService;
using UnityEngine;

namespace _App.AdminDashboard
{
    public class AdminDashboardPresenter : BaseDashboardPresenter, IAdminDashboardPresenter
    {
        private readonly IAdminContractListenerService _contractListenerService;
        private readonly IBalanceListenerService _balanceListenerService;
        private readonly IncomeDistributorService _distributorService = new();
        
        private string _pendingNewChildUid = null;
        
        public AdminDashboardPresenter(
            IDashboardView view,
            IChildService childService,
            IContractService contractService,
            IRewardService rewardService,
            IAppSettingsService appSettingsService,
            IDateService dateService,
            IAdminContractListenerService contractListenerService,
            IBalanceService balanceService,
            IBalanceListenerService balanceListenerService
        ) : base(view, childService, contractService, rewardService, appSettingsService, dateService, balanceService)
        {
            _contractListenerService = contractListenerService;
            _balanceListenerService = balanceListenerService;
        }

        public void Initialize(string adminUID)
        {
            _adminUID = adminUID;
            _selectedDay = _dateService.GetCurrentDay();
            _view.HighlightDayInCalendar(_selectedDay);

            _appSettingsService.LoadWeekStartsOn(adminUID, loadedDay =>
            {
                DateService.SaveWeekStartDay(loadedDay);
                RefreshCalendarUI();
            });

            _childService.ListenToChildren(adminUID, OnChildrenUpdated);
            _contractListenerService.ListenToAdminContracts(adminUID, OnContractsChanged);

            new DailyContractStateUpdater().Run(adminUID, isAdmin: true);
        }
        
        public void SetPendingNewChild(string childUid) => 
            _pendingNewChildUid = childUid;

        public new void UpdateChildBalance(float delta, string reason, bool recordHistory = false)
        {
            base.UpdateChildBalance(delta, reason, recordHistory);
        }


        private void RefreshCalendarUI()
        {
            _view.SetupCalendarButtons();
            _view.UpdateCalendarColors(_allContracts, _currentChild?.Uid ?? string.Empty);
        }

        public void SaveWeekStartsOnData(DayOfWeek newStartDay)
        {
            _appSettingsService.SaveWeekStartsOn(newStartDay, _adminUID);
            DateService.SaveWeekStartDay(newStartDay);
            RefreshCalendarUI();
        }
        
        public void RefreshChildren()
        {
            _childService.StopListening();
            _childService.ListenToChildren(_adminUID, OnChildrenUpdated);
        }
        
        private void OnChildrenUpdated(List<ChildModel> children)
        {
            if (children == null || children.Count == 0)
            {
                _view.ShowExtraRewardStatus("No children linked.");
                _currentChild = null;
                return;
            }

            _children = children;
            _view.ShowChildren(children);

            if (!string.IsNullOrEmpty(_pendingNewChildUid))
            {
                var newChild = children.FirstOrDefault(c => c.Uid == _pendingNewChildUid);
                if (newChild != null)
                {
                    SetCurrentChild(newChild);
                    _view.SelectToday();
                    Debug.Log($"🆕 Selected new child: {_currentChild.DisplayName}");
                    _pendingNewChildUid = null; // reset
                    return;
                }
            }

            if (_currentChild == null)
            {
                SetCurrentChild(children.First());
                _view.SelectToday();
                Debug.Log($"SetCurrentChild _currentChild 1 = {_currentChild?.Uid}");
            }
            else
            {
                var existing = children.FirstOrDefault(c => c.Uid == _currentChild.Uid);
                if (existing != null)
                {
                    SetCurrentChild(existing);
                    _view.SelectToday();
                    Debug.Log($"SetCurrentChild _currentChild 3 = {_currentChild}");
                }
                else
                {
                    SetCurrentChild(children.First());
                    _view.SelectToday();
                    Debug.Log($"🆕 Current child not found. Switched to: {_currentChild.DisplayName}");
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

        public override void OnDaySelected(DateTime day)
        {
            base.OnDaySelected(day);
            FilterAndShowContracts();
        }

        public override void SetCurrentChild(ChildModel child)
        {
            base.SetCurrentChild(child);
            
            _balanceListenerService.StopListening(_currentChild.Uid); // cleanup previous
            _balanceListenerService.ListenToBalance(_currentChild.Uid, newBalance =>
            {
                _currentChild.Balance = newBalance;
                _view.ShowChildBalance(newBalance);
            });
            
            _view.UpdateCalendarColors(_allContracts, _currentChild.Uid);
            FilterAndShowContracts();
            CheckExtraRewardEligibility();
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

        public void ConfirmContractByRole(string contractId) => 
            AdminConfirmContract(contractId);

        public void UndoConfirmContractByRole(string contractId) => 
            AdminUndoConfirmContract(contractId);
        
        private void AdminConfirmContract(string contractId)
        {
            if (_selectedDay == default)
            {
                Debug.LogWarning("❌ Selected day is not set.");
                return;
            }
            
            if (!ContractIdHelper.TryNormalizeVisualContractId(contractId, out var realId, out _))
            {
                Debug.LogWarning($"❌ Invalid contract ID format: {contractId}");
                return;
            }

            _contractService.GetContractById(realId, contract =>
            {
                if (contract == null)
                {
                    Debug.LogWarning($"❌ Contract not found: {contractId}");
                    return;
                }

                // ✅ CASE 1: Confirming an AsNeeded COPY that's in ReadyToConfirm
                if (contract.IsCopy && contract.RepeatMode == RepeatType.AsNeeded)
                {
                    contract.LoadStateHistory();
                    string keyPrefix = _selectedDay.ToString("yyyy-MM-dd") + "#";

                    string matchingKey = contract.StateHistory.Keys
                        .Where(k => k.StartsWith(keyPrefix))
                        .OrderByDescending(ExtractQueueIndex)
                        .FirstOrDefault(k => contract.StateHistory[k]
                            .Any(r => r.State == SmartContractState.ReadyToConfirm));

                    if (string.IsNullOrEmpty(matchingKey))
                    {
                        Debug.LogWarning($"⚠️ No ReadyToConfirm state found to confirm for copy: {contract.Id}");
                        return;
                    }

                    // ✅ Overwrite ReadyToConfirm with Completed in the copy only
                    contract.StateHistory[matchingKey] = new List<SmartContractModel.StateRecord>
                    {
                        new SmartContractModel.StateRecord
                        {
                            State = SmartContractState.Completed,
                            QueueId = matchingKey
                        }
                    };
                    contract.SyncStateHistory();

                    _contractService.SaveContract(contract, success =>
                    {
                        if (success)
                        {
                            Debug.Log($"✅ Admin approved ReadyToConfirm copy: {contract.Title} | Queue {matchingKey} | +{contract.RewardAmount}");
                            //UpdateChildBalance(contract.RewardAmount);
                            _distributorService.DistributeIncome(_currentChild.Uid, contract.RewardAmount, $"Contract '{contract.Title}' confirmed");

                            //UpdateChildBalance(contract.RewardAmount, $"Contract '{contract.Title}' confirmed");
                        }
                    });

                    return;
                }

                // ✅ CASE 2: Confirming a parent AsNeeded contract (creates or updates a copy)
                if (!contract.IsCopy && contract.RepeatMode == RepeatType.AsNeeded)
                {
                    var parent = contract;

                    _contractService.GetAsNeededCopyByParentId(parent.Id, copy =>
                    {
                        SmartContractModel copyToUse;
                        int queueIndex;

                        if (copy != null)
                        {
                            copy.LoadStateHistory();
                            queueIndex = GetNextAsNeededQueueIndex(copy, _selectedDay);
                            copy.SetStateOnDateWithQueue(_selectedDay, SmartContractState.Completed, queueIndex);
                            copyToUse = copy;

                            Debug.Log($"➕ Added queue #{queueIndex} to existing copy: {copy.Id}");
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
                            copyToUse.SetStateOnDateWithQueue(_selectedDay, SmartContractState.Completed, queueIndex);

                            Debug.Log($"🆕 Created new AsNeeded copy: {copyToUse.Id}");
                        }

                        _contractService.SaveContract(copyToUse, copySaveSuccess =>
                        {
                            if (!copySaveSuccess)
                            {
                                Debug.LogError("❌ Failed to save AsNeeded copy!");
                                return;
                            }

                            Debug.Log($"✅ Admin confirmed new AsNeeded copy: {parent.Title} | Queue #{queueIndex} | +{parent.RewardAmount}");
                            //UpdateChildBalance(parent.RewardAmount);
                            _distributorService.DistributeIncome(_currentChild.Uid, contract.RewardAmount, $"Contract '{contract.Title}' confirmed");

                            //UpdateChildBalance(parent.RewardAmount, $"Contract '{parent.Title}' confirmed");
                        });
                    });

                    return;
                }

                // ✅ CASE 3: Confirming a flat contract (Once, EveryDay, SpecificDays)
                if (!contract.IsCopy && contract.RepeatMode != RepeatType.AsNeeded)
                {
                    _contractService.SetContractStateOnDate(contract.Id, _selectedDay, SmartContractState.Completed, success =>
                    {
                        if (success)
                        {
                            Debug.Log($"✅ Admin confirmed flat contract: {contract.Title} | +{contract.RewardAmount}");
                            //UpdateChildBalance(contract.RewardAmount);
                            _distributorService.DistributeIncome(_currentChild.Uid, contract.RewardAmount, $"Contract '{contract.Title}' confirmed");

                            //UpdateChildBalance(contract.RewardAmount, $"Contract '{contract.Title}' confirmed");
                        }
                    });
                }
            });
        }
        
        private void AdminUndoConfirmContract(string contractId)
        {
            if (_selectedDay == default)
            {
                Debug.LogWarning("❌ Selected day is not set.");
                return;
            }

            // ✅ Normalize pseudo-copy ID
            if (!ContractIdHelper.TryNormalizeVisualContractId(contractId, out var realId, out var queueKeyFromVisualId))
            {
                Debug.LogWarning($"❌ Invalid contract ID format: {contractId}");
                return;
            }

            _contractService.GetContractById(realId, contract =>
            {
                if (contract == null)
                {
                    Debug.LogWarning($"❌ Contract not found: {realId}");
                    return;
                }

                // ✅ AsNeeded COPY undo
                if (contract.IsCopy && contract.RepeatMode == RepeatType.AsNeeded)
                {
                    contract.LoadStateHistory();

                    string queueKey = queueKeyFromVisualId;
                    if (string.IsNullOrEmpty(queueKey))
                    {
                        string keyPrefix = _selectedDay.ToString("yyyy-MM-dd") + "#";
                        queueKey = contract.StateHistory.Keys
                            .Where(k => k.StartsWith(keyPrefix))
                            .OrderByDescending(ExtractQueueIndex)
                            .FirstOrDefault();
                    }

                    if (string.IsNullOrEmpty(queueKey) || !contract.StateHistory.TryGetValue(queueKey, out var value))
                    {
                        Debug.LogWarning($"⚠️ No matching state to undo for AsNeeded copy: {contractId}");
                        return;
                    }

                    bool wasCompleted = value.Any(r => r.State == SmartContractState.Completed);

                    contract.StateHistory.Remove(queueKey);
                    contract.SyncStateHistory();

                    // Update parent
                    string parentId = contract.ParentId;
                    _contractService.GetContractById(parentId, parent =>
                    {
                        if (parent == null)
                        {
                            Debug.LogWarning($"❌ Parent not found: {parentId}");
                            return;
                        }

                        parent.LoadStateHistory();
                        parent.StateHistory.Remove(queueKey);
                        parent.SyncStateHistory();
                        _contractService.SaveContract(parent, _ => { });

                        void FinalizeUndo(bool success)
                        {
                            if (success && wasCompleted)
                            {
                                Debug.Log($"↩️ Undo confirmed for AsNeeded contract: {contract.Title} | Queue {queueKey} (-{contract.RewardAmount})");

                                _distributorService.UndoDistribution(
                                    _currentChild.Uid,
                                    contract.RewardAmount,
                                    $"Undo confirmation for contract '{contract.Title}'"
                                );
                            }
                        }

                        if (contract.StateHistory.Count == 0)
                            _contractService.DeleteContract(contract.Id, FinalizeUndo);
                        else
                            _contractService.SaveContract(contract, FinalizeUndo);
                    });

                    return;
                }

                // 🔁 Flat contract (Once, EveryDay, SpecificDays)
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

                            Debug.Log($"↩️ Undo confirmed: {contract.Title} | {previousState} → ReadyToSell");
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
            if (parts.Length == 2 && int.TryParse(parts[1], out int index))
                return index;

            return -1;
        }

        public void AdminDeclineContract(string contractId)
        {
            if (!ContractIdHelper.TryNormalizeVisualContractId(contractId, out var realId, out var queueKeyFromVisualId))
            {
                Debug.LogWarning($"❌ Invalid contract ID: {contractId}");
                return;
            }

            _contractService.GetContractById(realId, contract =>
            {
                if (contract == null)
                {
                    Debug.LogWarning($"❌ Contract not found: {realId}");
                    return;
                }

                // ✅ If it's an AsNeeded copy with ReadyToConfirm
                if (contract.IsCopy && contract.RepeatMode == RepeatType.AsNeeded)
                {
                    contract.LoadStateHistory();

                    string keyPrefix = _selectedDay.ToString("yyyy-MM-dd") + "#";
                    string matchingKey = queueKeyFromVisualId;

                    // Fallback: find the latest ReadyToConfirm if queueKey is not known
                    if (string.IsNullOrEmpty(matchingKey))
                    {
                        matchingKey = contract.StateHistory.Keys
                            .Where(k => k.StartsWith(keyPrefix))
                            .OrderByDescending(ExtractQueueIndex)
                            .FirstOrDefault(k =>
                                contract.StateHistory[k].Any(r => r.State == SmartContractState.ReadyToConfirm));
                    }

                    if (string.IsNullOrEmpty(matchingKey))
                    {
                        Debug.LogWarning($"⚠️ No ReadyToConfirm state to decline for AsNeeded copy: {contractId}");
                        return;
                    }

                    // Remove from copy
                    contract.StateHistory.Remove(matchingKey);
                    contract.SyncStateHistory();

                    string parentId = contract.ParentId;

                    _contractService.GetContractById(parentId, parent =>
                    {
                        if (parent == null)
                        {
                            Debug.LogWarning($"❌ Parent not found: {parentId}");
                            return;
                        }

                        parent.LoadStateHistory();
                        parent.StateHistory.Remove(matchingKey);
                        parent.SyncStateHistory();
                        _contractService.SaveContract(parent, _ => { });

                        void FinalizeDecline(bool success)
                        {
                            if (success)
                                Debug.Log($"🛑 Admin declined AsNeeded confirmation request: {contract.Title} (queue {matchingKey})");
                        }

                        if (contract.StateHistory.Count == 0)
                            _contractService.DeleteContract(contract.Id, FinalizeDecline);
                        else
                            _contractService.SaveContract(contract, FinalizeDecline);
                    });

                    return;
                }

                // ✅ For non-copy contracts with ReadyToConfirm state
                if (contract.GetStateOnDate(_selectedDay, isAdmin: true) == SmartContractState.ReadyToConfirm)
                {
                    Debug.Log($"🛑 Contract trying to be declined: {contract.Title}");
                    
                    _contractService.SetContractStateOnDate(contract.Id, _selectedDay, SmartContractState.ReadyToSell, success =>
                    {
                        if (success)
                        {
                            Debug.Log($"🛑 Contract declined and reverted to ReadyToSell: {contract.Title}");
                        }
                    });
                }
            });
        }
        
        public void ChildBuyAdminSellContract(string contractId) => 
            BuyContract(contractId);

        public void UndoPurchaseContract(string contractId)
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

                string key = _selectedDay.ToString("yyyy-MM-dd");

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
                        
                        _distributorService.UndoDistribution(
                            _currentChild.Uid,
                            contract.RewardAmount,
                            $"Undo purchase of contract '{contract.Title}'"
                        );
                    }
                });
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

                SmartContractDraft.LoadFromModel(contract);
                _view.OpenEditContractPanel();
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

        public override void PayoutExtraReward()
        {
            base.PayoutExtraReward();
        }
        
        public void OnAdjustBalanceButtonPressed() => _view.OpenAdjustBalancePanel();
        public void OnRewardButtonPressed() => _view.OpenRewardPanel();
        public void OnAdminSurpriseButtonPressed() => _view.OnAdminSurpriseButtonClick();
        public void OnChildSurpriseButtonPressed() => _view.OnChildSurpriseButtonClick();

        public override void CheckExtraRewardEligibility()
        {
            if (_currentChild == null) return;

            var weekStart = _dateService.GetWeekStart(_selectedDay);
            _rewardService.CheckExtraRewardEligibility(_currentChild.Uid, weekStart, eligible =>
            {
                _view.ShowExtraRewardEligible(eligible);
            });
        }
        
        public void BuildFamilyModelAsync(Action<FamilyModel> callback)
        {
            _childService.GetAdminProfile(_adminUID, adminUser =>
            {
                var family = new FamilyModel
                {
                    AdminUid = _adminUID,
                    Adults = new List<UserModel> { adminUser },
                    Kids = _children
                };
        
                callback?.Invoke(family);
            });
        }

        public void CleanupContractListenerService() => _contractListenerService.StopListening();
        public void CleanupChildListenerService() => _childService.StopListening();
    }
}