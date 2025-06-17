using System;
using System.Collections.Generic;
using _App.Bootstrap;
using Firebase.Database;
using Firebase.Extensions;
using Newtonsoft.Json;
using UnityEngine;

namespace _App.Services
{
    public class DailyContractStateUpdater
    {
        private bool _hasRunToday = false;
        private DateTime _lastRunDate;

        private const int MaxDaysToKeep = 31;

        public void Run(string userUid, bool isAdmin = true)
        {
            if (_hasRunToday && _lastRunDate == DateTime.Today)
                return;

            Debug.Log("🔁 Running daily SmartContract state scan...");

            var refToContracts = FirebaseInit.DbRef.Child(AppConstants.Contracts);
            Query query = isAdmin
                ? refToContracts.OrderByChild(AppConstants.AdminUID).EqualTo(userUid)
                : refToContracts.OrderByChild(AppConstants.AssignedToUid).EqualTo(userUid);

            query.GetValueAsync().ContinueWithOnMainThread(task =>
            {
                if (!task.IsCompletedSuccessfully || !task.Result.Exists)
                {
                    Debug.LogWarning("⚠️ No contracts found or failed to fetch.");
                    return;
                }

                DateTime today = DateTime.Today;
                DateTime cutoff = today.AddDays(-MaxDaysToKeep);
                int updatedCount = 0;

                foreach (var snapshot in task.Result.Children)
                {
                    try
                    {
                        // var json = snapshot.GetRawJsonValue();
                        // var contract = JsonUtility.FromJson<SmartContractModel>(json);
                        var json = snapshot.GetRawJsonValue();
                        var contract = JsonConvert.DeserializeObject<SmartContractModel>(json);
                        
                        contract.Id = snapshot.Key;

                        if (string.IsNullOrEmpty(contract.StartDate) || !DateTime.TryParse(contract.StartDate, out _))
                            continue;

                        // ───────────────────── Once ─────────────────────
                        if (contract.RepeatMode == RepeatType.Once)
                        {
                            if (contract.GetStartDate().Date == today)
                            {
                                contract.LoadStateHistory();
                                if (!contract.HasStateOnDate(today))
                                {
                                    contract.SetStateOnDate(today, SmartContractState.ReadyToSell);
                                    contract.SyncStateHistory();
                                    UpdateStateHistoryInFirebase(refToContracts, contract);
                                    Debug.Log($"🟢 Once contract set to ReadyToSell: {contract.Title}");
                                }
                            }

                            continue;
                        }

                        // ───────────────────── AsNeeded ─────────────────────
                        if (contract.RepeatMode == RepeatType.AsNeeded && !contract.IsCopy)
                        {
                            contract.LoadStateHistory();
                            if (!contract.HasStateOnDate(today))
                            {
                                contract.SetStateOnDate(today, SmartContractState.ReadyToSell);
                                contract.SyncStateHistory();
                                UpdateStateHistoryInFirebase(refToContracts, contract);
                                Debug.Log($"🟢 AsNeeded contract activated: {contract.Title}");
                            }

                            continue;
                        }

                        // ─────── Skip all others except EveryDay / SpecificDays ───────
                        if (contract.RepeatMode != RepeatType.EveryDay &&
                            contract.RepeatMode != RepeatType.SpecificDays)
                            continue;

                        contract.LoadStateHistory();
                        bool shouldSave = false;

                        // ─── Initialize today's state ───
                        if (!contract.HasStateOnDate(today) && contract.IsVisibleOn(today))
                        {
                            contract.SetStateOnDate(today, SmartContractState.ReadyToSell);
                            shouldSave = true;
                        }

                        // ─── Remove stale entries ───
                        List<string> staleKeys = new();
                        foreach (var entry in contract.StateHistory)
                        {
                            if (DateTime.TryParse(entry.Key, out DateTime entryDate) && entryDate < cutoff)
                                staleKeys.Add(entry.Key);
                        }
                        foreach (var key in staleKeys)
                            contract.StateHistory.Remove(key);

                        // ─── Backfill past days if still visible ───
                        for (int i = 1; i < MaxDaysToKeep; i++)
                        {
                            DateTime pastDate = today.AddDays(-i);
                            if (pastDate < cutoff || !contract.IsVisibleOn(pastDate))
                                continue;

                            var pastState = contract.GetStateOnDate(pastDate, isAdmin);
                            if (pastState == SmartContractState.ReadyToSell || pastState == SmartContractState.ReadyToConfirm)
                            {
                                contract.SetStateOnDate(pastDate, SmartContractState.ReadyToBuy);
                                shouldSave = true;
                            }
                        }

                        if (shouldSave)
                        {
                            contract.SyncStateHistory();
                            updatedCount++;
                            UpdateStateHistoryInFirebase(refToContracts, contract);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"❌ Failed to process contract: {ex.Message}");
                    }
                }

                _hasRunToday = true;
                _lastRunDate = today;

                Debug.Log($"✅ Daily scan complete. {updatedCount} contract(s) auto-updated.");
            });
        }

        private void UpdateStateHistoryInFirebase(DatabaseReference refToContracts, SmartContractModel contract)
        {
            var update = new Dictionary<string, object>
            {
                ["stateHistoryRaw"] = contract.stateHistoryRaw
            };

            refToContracts.Child(contract.Id).UpdateChildrenAsync(update);
        }
    }
}
