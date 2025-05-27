using System;
using System.Collections.Generic;
using System.Linq;
using _App.Bootstrap;
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
                _view.SelectToday();
            }
            else
            {
                var existing = children.FirstOrDefault(c => c.Uid == _currentChild.Uid);
                if (existing != null)
                {
                    SetCurrentChild(existing);
                    _view.SelectToday();
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

        public void OnAdjustBalanceButtonPressed() => _view.OpenAdjustBalancePanel();
        public void OnRewardButtonPressed() => _view.OpenRewardPanel();
        public void OnAdminSurpriseButtonPressed() => _view.OnAdminSurpriseButtonClick();
        public void OnChildSurpriseButtonPressed() => _view.OnChildSurpriseButtonClick();

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
                            UpdateChildBalance(contract.RewardAmount, $"Contract '{contract.Title}' confirmed");
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
                            UpdateChildBalance(parent.RewardAmount, $"Contract '{parent.Title}' confirmed");
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
                            UpdateChildBalance(contract.RewardAmount, $"Contract '{contract.Title}' confirmed");
                        }
                    });
                }
            });
        }


        
        /*private void AdminConfirmContract(string contractId)
        {
            if (_selectedDay == default)
            {
                Debug.LogWarning("❌ Selected day is not set.");
                return;
            }

            _contractService.GetContractById(contractId, parent =>
            {
                if (parent == null || parent.IsCopy)
                {
                    if (parent == null)
                        Debug.LogWarning($"❌ Contract not found: {contractId}");
                    if (parent.IsCopy && parent.RepeatMode == RepeatType.AsNeeded)
                    {
                        parent.LoadStateHistory();
                        string keyPrefix = _selectedDay.ToString("yyyy-MM-dd") + "#";

                        string matchingKey = parent.StateHistory.Keys
                            .Where(k => k.StartsWith(keyPrefix))
                            .OrderByDescending(ExtractQueueIndex)
                            .FirstOrDefault(k => parent.StateHistory[k]
                                .Any(r => r.State == SmartContractState.ReadyToConfirm));

                        if (string.IsNullOrEmpty(matchingKey))
                        {
                            Debug.LogWarning($"⚠️ No ReadyToConfirm state found to confirm for copy: {parent.Id}");
                            return;
                        }

                        // 🔄 Overwrite the state to Completed
                        parent.StateHistory[matchingKey] = new List<SmartContractModel.StateRecord>
                        {
                            new SmartContractModel.StateRecord
                            {
                                State = SmartContractState.Completed,
                                QueueId = matchingKey
                            }
                        };
                        parent.SyncStateHistory();

                        string parentId = parent.ParentId;

                        _contractService.GetContractById(parentId, parentContract =>
                        {
                            if (parentContract != null)
                            {
                                parentContract.LoadStateHistory();
                                parentContract.StateHistory[matchingKey] = new List<SmartContractModel.StateRecord>
                                {
                                    new SmartContractModel.StateRecord
                                    {
                                        State = SmartContractState.Completed,
                                        QueueId = matchingKey
                                    }
                                };
                                parentContract.SyncStateHistory();
                                _contractService.SaveContract(parentContract, _ => { });
                            }

                            _contractService.SaveContract(parent, success =>
                            {
                                if (success)
                                {
                                    Debug.Log($"✅ Admin approved copy: {parent.Title} | Queue {matchingKey} | +{parent.RewardAmount}");
                                    UpdateChildBalance(parent.RewardAmount);
                                }
                            });
                        });

                        return;
                    }


                    //else if (parent.IsCopy)
                        //Debug.LogWarning($"🚫 Tried to confirm a copy (not allowed): {parent.Title} | {parent.Id}");

                    return;
                }
                
                Debug.Log($" Parent found: {parent.Id} and Parent is Copy = {parent.IsCopy}");

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

                        parent.LoadStateHistory();

                        // Save copy + reward
                        _contractService.SaveContract(copyToUse, copySaveSuccess =>
                        {
                            if (!copySaveSuccess)
                            {
                                Debug.LogError("❌ Failed to save AsNeeded copy!");
                                return;
                            }

                            _contractService.SaveContract(parent, parentSaveSuccess =>
                            {
                                if (!parentSaveSuccess)
                                {
                                    Debug.LogError("❌ Failed to save AsNeeded parent!");
                                    return;
                                }

                                UpdateChildBalance(parent.RewardAmount);
                                Debug.Log($"✅ AsNeeded contract confirmed: {parent.Title} | Queue #{queueIndex} | +{parent.RewardAmount}");
                            });
                        });
                    });

                    return;
                }
                
                // ✅ Non-AsNeeded (flat) contract logic
                _contractService.SetContractStateOnDate(parent.Id, _selectedDay, SmartContractState.Completed, success =>
                {
                    if (success)
                    {
                        UpdateChildBalance(parent.RewardAmount);
                        Debug.Log($"✅ Flat contract confirmed: {parent.Title} | +{parent.RewardAmount}");
                    }
                });
            });
        }*/
        
        /*private void AdminUndoConfirmContract(string contractId)
        {
            _contractService.GetContractById(contractId, contract =>
            {
                if (contract == null)
                {
                    Debug.LogWarning($"❌ Contract not found: {contractId}");
                    return;
                }

                if (contract.IsCopy && contract.RepeatMode == RepeatType.AsNeeded)
                {
                    contract.LoadStateHistory();

                    string keyPrefix = _selectedDay.ToString("yyyy-MM-dd") + "#";

                    // Find last queue key for today
                    string matchingKey = contract.StateHistory.Keys
                        .Where(k => k.StartsWith(keyPrefix))
                        .OrderByDescending(ExtractQueueIndex)
                        .FirstOrDefault();


                    if (string.IsNullOrEmpty(matchingKey) || !contract.StateHistory.TryGetValue(matchingKey, out var value))
                    {
                        Debug.LogWarning($"⚠️ No matching state to undo for AsNeeded copy: {contractId}");
                        return;
                    }

                    bool wasCompleted = value
                        .Any(r => r.State == SmartContractState.Completed);

                    // Remove from copy
                    contract.StateHistory.Remove(matchingKey);
                    contract.SyncStateHistory();

                    // Handle parent contract
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

                        void FinalizeUndo(bool success)
                        {
                            if (success && wasCompleted)
                            {
                                Debug.Log($"↩️ Undo confirmed for AsNeeded contract: {contract.Title} (-{contract.RewardAmount})");
                                //UpdateChildBalance(-contract.RewardAmount);
                                _balanceService.AdjustBalance(
                                    _currentChild.Uid,
                                    -contract.RewardAmount,
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

                // 🔁 Non-copy fallback (Once, EveryDay, SpecificDays)
                if (contract.HasStateOnDate(_selectedDay, SmartContractState.Completed))
                {
                    contract.RemoveStateRecord(_selectedDay, queueId: null); // flat format
                    _contractService.SaveContract(contract, success =>
                    {
                        if (success)
                        {
                            Debug.Log($"↩️ Undo confirmed for contract: {contract.Title} (-{contract.RewardAmount})");
                            //UpdateChildBalance(-contract.RewardAmount);
                            _balanceService.AdjustBalance(
                                _currentChild.Uid,
                                -contract.RewardAmount,
                                $"Undo confirmation for contract '{contract.Title}'"
                            );
                            
                        }
                    });
                }
                else
                {
                    Debug.Log($"ℹ️ No completed state to undo for contract: {contract.Title}");
                }
            });
        }*/
        
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

                                _balanceService.AdjustBalance(
                                    _currentChild.Uid,
                                    -contract.RewardAmount,
                                    $"Undo confirmation for contract '{contract.Title}' (queue {queueKey})"
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
                                _balanceService.AdjustBalance(
                                    _currentChild.Uid,
                                    -contract.RewardAmount,
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

                        _balanceService.AdjustBalance(
                            contract.AssignedToUid,
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

        public override void CheckExtraRewardEligibility()
        {
            if (_currentChild == null) return;

            var weekStart = _dateService.GetWeekStart(_selectedDay);
            _rewardService.CheckExtraRewardEligibility(_currentChild.Uid, weekStart, eligible =>
            {
                _view.ShowExtraRewardEligible(eligible);
            });
        }

        public void Cleanup() => _contractListenerService.StopListening();
    }
}











/*
using System;
using System.Collections.Generic;
using System.Linq;
using _App.Bootstrap;
using _App.ChildDashboard;
using _App.Dashboard;
using _App.Services;
using UnityEngine;

namespace _App.AdminDashboard
{
    public class AdminDashboardPresenter : BaseDashboardPresenter, IAdminDashboardPresenter
    {
        private readonly IDashboardView _view;
        // private readonly IChildService _childService;
        // private readonly IContractService _contractService;
        // private readonly IRewardService _rewardService;
        // private readonly IAppSettingsService _appSettingsService;
        // private readonly IDateService _dateService;
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
            IDashboardView view,
            IChildService childService,
            IContractService contractService,
            IRewardService rewardService,
            IAppSettingsService appSettingsService,
            IDateService dateService

        ) : base(view, childService, contractService, rewardService, appSettingsService, dateService) // 👈 Fix here
        {
            _view = view;
            _contractService = contractService;
            _childService = childService;
            _rewardService = rewardService;
            _appSettingsService = appSettingsService;
            _dateService = dateService;
        }

        public void Initialize(string adminUID)
        {
            _adminUID = adminUID;
            _selectedDay = _dateService.GetCurrentDay();
            _view.ShowDaySelection(_selectedDay);
            
            _appSettingsService.LoadWeekStartsOn(adminUID, loadedDay =>
            {
                DateService.SaveWeekStartDay(loadedDay); // update local value
                RefreshCalendarUI();          // build calendar
            });

            _childService.ListenToChildren(adminUID, OnChildrenUpdated);
            _contractListenerService.ListenToAdminContracts(adminUID, OnContractsChanged);

            new DailyContractStateUpdater().Run(adminUID, isAdmin: true);
        }

        private void RefreshCalendarUI()
        {
            _view.SetupCalendarButtons(); // Rebuild layout
            _view.UpdateCalendarColors(_allContracts, _currentChild?.Uid ?? string.Empty);
        }

        public void SaveWeekStartsOnData(DayOfWeek newStartDay)
        {
            _appSettingsService.SaveWeekStartsOn(newStartDay, _adminUID);
            DateService.SaveWeekStartDay(newStartDay); // local
            RefreshCalendarUI(); 
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
                if (selectedDay < today && !contract.IsCopy &&
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
        
        public void OnAdminSurpriseButtonPressed() => _view.OnAdminSurpriseButtonClick();
        
        public void OnChildSurpriseButtonPressed() => _view.OnChildSurpriseButtonClick();

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
        
        private void AdminConfirmContract(string contractId)
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

                    var matchingCopies = _allContracts.Where(c =>
                        c.IsCopy &&
                        c.ParentId == parentId).ToList();

                    Debug.Log($"🧪 Matching copies with parentId={parentId}: {matchingCopies.Count}");

                    // ✅ Check if a copy already exists for this parent and selected day
                    var existingCopy = _allContracts.FirstOrDefault(c =>
                        c.IsCopy &&
                        c.ParentId == parentId &&
                        c.AssignedToUid == contract.AssignedToUid);

                    if (existingCopy != null)
                    {
                        Debug.Log($"❌ existingCopy = {existingCopy}");
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

        private void AdminUndoConfirmContract(string contractId)
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
                    if (!contract.HasStateOnDate(_selectedDay, SmartContractState.Completed))
                    {
                        Debug.Log($"⚠️ Undo skipped: no completed state for {_selectedDay:yyyy-MM-dd} in copy {contract.Id}");
                        return;
                    }
                    
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
        
        public void AdminDeclineContract(string contractId)
        {
            _contractService.GetContractById(contractId, contract =>
            {
                if (contract == null)
                {
                    Debug.LogWarning($"❌ Contract not found: {contractId}");
                    return;
                }

                // Only decline if in ReadyToConfirm state
                if (contract.GetStateOnDate(_selectedDay, isAdmin: true) != SmartContractState.ReadyToConfirm)
                {
                    Debug.LogWarning("⚠️ Can't decline: contract is not in ReadyToConfirm state.");
                    return;
                }

                _contractService.SetContractStateOnDate(contract.Id, _selectedDay, SmartContractState.ReadyToSell, success =>
                {
                    if (!success)
                    {
                        Debug.LogWarning("❌ Failed to decline contract.");
                        return;
                    }

                    Debug.Log("🛑 Contract declined and reverted to ReadyToSell.");
                });
            });
        }

        private void ChildConfirmContract(string contractId)
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

                SmartContractState targetState = contract.RequireParentalApproval
                    ? SmartContractState.ReadyToConfirm
                    : SmartContractState.Completed;

                bool shouldGiveReward = !contract.RequireParentalApproval;

                // ✅ AsNeeded logic (child creates or updates a copy)
                if (contract.RepeatMode == RepeatType.AsNeeded)
                {
                    string parentId = contract.Id;

                    var existingCopy = _allContracts.FirstOrDefault(c =>
                        c.IsCopy &&
                        c.ParentId == parentId &&
                        c.AssignedToUid == contract.AssignedToUid);

                    if (existingCopy != null)
                    {
                        existingCopy.SetStateOnDate(_selectedDay, targetState);

                        _contractService.SaveContract(existingCopy, success =>
                        {
                            if (!success)
                            {
                                Debug.LogWarning("❌ Failed to update AsNeeded copy.");
                                return;
                            }

                            if (shouldGiveReward)
                                UpdateChildBalance(contract.RewardAmount);
                        });
                    }
                    else
                    {
                        var copy = CreateCopyCompletedToday(contract, _selectedDay);
                        copy.SetStateOnDate(_selectedDay, targetState);

                        _contractService.SaveContract(copy, success =>
                        {
                            if (!success)
                            {
                                Debug.LogWarning("❌ Failed to save AsNeeded copy.");
                                return;
                            }

                            if (shouldGiveReward)
                                UpdateChildBalance(contract.RewardAmount);
                        });
                    }

                    return;
                }

                // ✅ Default: update state on main contract
                _contractService.SetContractStateOnDate(contract.Id, _selectedDay, targetState, success =>
                {
                    if (!success)
                    {
                        Debug.LogWarning("❌ Failed to update contract state.");
                        return;
                    }

                    if (shouldGiveReward)
                        UpdateChildBalance(contract.RewardAmount);
                });
            });
        }
        
        private void ChildUndoConfirmContract(string contractId)
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
                    bool wasCompleted = contract.HasStateOnDate(_selectedDay, SmartContractState.Completed);
                    bool wasWaitingApproval = contract.HasStateOnDate(_selectedDay, SmartContractState.ReadyToConfirm);

                    if (!wasCompleted && !wasWaitingApproval)
                        return;

                    contract.RemoveStateOnDate(_selectedDay);
                    contract.LoadStateHistory();

                    if (contract.StateHistory.Count == 0)
                    {
                        _contractService.DeleteContract(contract.Id, success =>
                        {
                            if (success && wasCompleted)
                                UpdateChildBalance(-contract.RewardAmount);
                        });
                    }
                    else
                    {
                        _contractService.SaveContract(contract, success =>
                        {
                            if (success && wasCompleted)
                                UpdateChildBalance(-contract.RewardAmount);
                        });
                    }

                    return;
                }

                if (contract.HasStateOnDate(_selectedDay, SmartContractState.ReadyToConfirm))
                {
                    _contractService.SetContractStateOnDate(contract.Id, _selectedDay, SmartContractState.ReadyToSell, success =>
                    {
                        if (success)
                        {
                            // No reward yet because approval wasn’t complete
                            Debug.Log($"🕓 Approval request canceled by child for {_selectedDay:yyyy-MM-dd}");
                        }
                    });
                }
            });
        }
        
        public void ConfirmContractByRole(string contractId)
        {
            if (UserSession.IsAdmin)
                AdminConfirmContract(contractId);
            else
                ChildConfirmContract(contractId);
        }
        
        public void UndoConfirmContractByRole(string contractId)
        {
            if (UserSession.IsAdmin)
                AdminUndoConfirmContract(contractId);
            else
                ChildUndoConfirmContract(contractId);
        }


        
        public void ChildBuyAdminSellContract(string contractId)
        {
            _contractService.GetContractById(contractId, contract =>
            {
                if (contract == null)
                {
                    Debug.LogWarning($"❌ Contract not found: {contractId}");
                    return;
                }

                // ✅ Default path: mark directly
                if (contract.HasStateOnDate(_selectedDay, SmartContractState.ReadyToBuy))
                {
                    _contractService.SetContractStateOnDate(contract.Id, _selectedDay, SmartContractState.Purchased, success =>
                    {
                        if (!success)
                        {
                            Debug.LogWarning("❌ Failed to update contract state.");
                            return;
                        }
                        UpdateChildBalance(-contract.RewardAmount);
                        
                        Debug.Log($"✅ Contract {contract.Title} purchased on {_selectedDay:yyyy-MM-dd}");
                    });
                }
            });
        }
        
        public void UndoPurchaseContract(string contractId)
        {
            _contractService.GetContractById(contractId, contract =>
            {
                if (contract == null)
                {
                    Debug.LogWarning($"❌ Contract not found: {contractId}");
                    return;
                }

                // ✅ Default path: mark directly
                if (contract.HasStateOnDate(_selectedDay, SmartContractState.Purchased))
                {
                    _contractService.SetContractStateOnDate(contract.Id, _selectedDay, SmartContractState.ReadyToBuy, success =>
                    {
                        if (!success)
                        {
                            Debug.LogWarning("❌ Failed to update contract state.");
                            return;
                        }
                        UpdateChildBalance(contract.RewardAmount);
                    });
                }
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
*/
