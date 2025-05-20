using System;
using System.Collections.Generic;
using _App.Bootstrap;
using Firebase.Database;
using Firebase.Extensions;
using UnityEngine;

namespace _App.Services
{
    public class DailyContractStateUpdater
    {
        private bool _hasRunToday = false;
        private DateTime _lastRunDate;

        private const int MaxDaysToKeep = 31; // 🔧 Keep only last N days of state history

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
                DateTime yesterday = today.AddDays(-1);
                int updatedCount = 0;

                foreach (var snapshot in task.Result.Children)
                {
                    try
                    {
                        var json = snapshot.GetRawJsonValue();
                        var contract = JsonUtility.FromJson<SmartContractModel>(json);
                        contract.Id = snapshot.Key;

                        if (!DateTime.TryParse(contract.StartDate, out _))
                            continue;

                        // ✅ Only handle EveryDay or SpecificDays
                        if (contract.RepeatMode != RepeatType.EveryDay &&
                            contract.RepeatMode != RepeatType.SpecificDays)
                            continue;

                        if (!contract.IsVisibleOn(yesterday))
                            continue;

                        contract.LoadStateHistory();
                        bool shouldSave = false;

                        // 🧹 Remove entries older than MaxDaysToKeep
                        DateTime cutoff = DateTime.Today.AddDays(-MaxDaysToKeep);
                        List<string> keysToRemove = new();
                        foreach (var entry in contract.StateHistory)
                        {
                            if (DateTime.TryParse(entry.Key, out DateTime entryDate) && entryDate < cutoff)
                                keysToRemove.Add(entry.Key);
                        }
                        foreach (var key in keysToRemove)
                            contract.StateHistory.Remove(key);

                        // 🧠 Evaluate yesterday's state
                        var yesterdayState = contract.GetStateOnDate(yesterday, isAdmin);

                        // ✅ ReadyToConfirm ➜ ReadyToBuy
                        if (yesterdayState == SmartContractState.ReadyToConfirm)
                        {
                            contract.SetStateOnDate(yesterday, SmartContractState.ReadyToBuy);
                            shouldSave = true;
                        }
                        // ✅ ReadyToSell ➜ ReadyToBuy
                        else if (yesterdayState == SmartContractState.ReadyToSell)
                        {
                            contract.SetStateOnDate(yesterday, SmartContractState.ReadyToBuy);
                            shouldSave = true;
                        }

                        if (shouldSave)
                        {
                            contract.SyncStateHistory();
                            updatedCount++;

                            var update = new Dictionary<string, object>
                            {
                                ["stateHistoryRaw"] = contract.stateHistoryRaw
                            };

                            refToContracts.Child(contract.Id).UpdateChildrenAsync(update);
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
    }
}