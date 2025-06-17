using System;
using System.Collections.Generic;
using _App.Bootstrap;
using Firebase.Database;
using Firebase.Extensions;
using Newtonsoft.Json;
using UnityEngine;

namespace _App.Services.BalanceService
{
   public class FirebaseBalanceService : IBalanceService
    {
        private bool _isReady => FirebaseInit.DbRef != null;

        private DatabaseReference ChildrenRef
        {
            get
            {
                if (!_isReady)
                {
                    Debug.LogWarning("⚠️ FirebaseBalanceService is not ready. DbRef is null.");
                    return null;
                }

                return FirebaseInit.DbRef.Child(AppConstants.Children);
            }
        }

        public void AdjustBalance(string childUid, float delta, string reason, bool recordHistory = true, Action<bool> onComplete = null)
        {
            var balanceRef = ChildrenRef?.Child(childUid)?.Child(AppConstants.Balance);
            if (balanceRef == null)
            {
                onComplete?.Invoke(false);
                return;
            }

            balanceRef.GetValueAsync().ContinueWithOnMainThread(task =>
            {
                if (!task.IsCompletedSuccessfully || !task.Result.Exists)
                {
                    Debug.LogError("❌ Failed to read current balance.");
                    onComplete?.Invoke(false);
                    return;
                }

                float current = float.Parse(task.Result.Value.ToString());
                float updated = (float)Math.Round(current + delta, 2);

                balanceRef.SetValueAsync(updated).ContinueWithOnMainThread(setTask =>
                {
                    if (setTask.IsCompletedSuccessfully)
                    {
                        Debug.Log($"💰 Balance updated: {current} → {updated} | Reason: {reason}");

                        if (recordHistory)
                            AddBalanceHistory(childUid, delta, reason);

                        onComplete?.Invoke(true);
                    }
                    else
                    {
                        Debug.LogError($"❌ Failed to update balance: {setTask.Exception}");
                        onComplete?.Invoke(false);
                    }
                });
            });
        }

        public void GetBalance(string childUid, Action<float> onResult)
        {
            var balanceRef = ChildrenRef?.Child(childUid)?.Child(AppConstants.Balance);
            if (balanceRef == null)
            {
                onResult?.Invoke(0f);
                return;
            }

            balanceRef.GetValueAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsCompletedSuccessfully && task.Result.Exists)
                {
                    float balance = float.Parse(task.Result.Value.ToString());
                    onResult(balance);
                }
                else
                {
                    Debug.LogWarning("⚠️ Balance not found. Returning 0.");
                    onResult(0f);
                }
            });
        }

        public void ListenToBalance(string childUid, Action<float> onChanged)
        {
            var balanceRef = ChildrenRef?.Child(childUid)?.Child(AppConstants.Balance);
            if (balanceRef == null)
                return;

            balanceRef.ValueChanged += (sender, args) =>
            {
                if (args.DatabaseError != null)
                {
                    Debug.LogError($"❌ Balance listener error: {args.DatabaseError.Message}");
                    return;
                }

                if (args.Snapshot.Exists)
                {
                    float balance = float.Parse(args.Snapshot.Value.ToString());
                    onChanged(balance);
                }
            };
        }
        
        public void AddBalanceHistory(string childUid, float delta, string reason)
        {
            var historyRef = ChildrenRef.Child(childUid).Child(AppConstants.BalanceHistory);

            // Step 1: Add new entry
            var entry = new BalanceHistoryEntry
            {
                Amount = delta,
                Reason = reason,
                Timestamp = DateTime.UtcNow.ToString("s")
            };
            
            var json = JsonConvert.SerializeObject(entry);
            var newEntryRef = historyRef.Push();
            newEntryRef.SetRawJsonValueAsync(json);

            //var newEntryRef = historyRef.Push();
            //newEntryRef.SetRawJsonValueAsync(JsonUtility.ToJson(entry));

            // Step 2: Clean up if there are more than 100 entries
            historyRef.GetValueAsync().ContinueWithOnMainThread(task =>
            {
                if (!task.IsCompletedSuccessfully || !task.Result.Exists) return;

                var allEntries = new List<DataSnapshot>();

                foreach (var child in task.Result.Children)
                    allEntries.Add(child);

                // Sort by key creation time (Firebase Push keys are time-ordered)
                allEntries.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));

                int excess = allEntries.Count - 100;
                if (excess <= 0) return;

                // Delete oldest entries
                for (int i = 0; i < excess; i++)
                    historyRef.Child(allEntries[i].Key).RemoveValueAsync();
            });
        }
    }
}