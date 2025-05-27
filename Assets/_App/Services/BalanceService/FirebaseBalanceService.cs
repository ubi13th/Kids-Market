using System;
using System.Collections.Generic;
using _App.Bootstrap;
using Firebase.Database;
using Firebase.Extensions;
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

        public void AdjustBalance(string childUid, float delta, string reason, Action<bool> onComplete = null)
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
            var historyRef = ChildrenRef?.Child(childUid)?.Child("BalanceHistory");
            if (historyRef == null)
                return;

            var entry = new Dictionary<string, object>
            {
                { "Delta", delta },
                { "Reason", reason },
                { "Timestamp", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mmZ")
                }
            };

            historyRef.Push().SetValueAsync(entry);
        }
    }
}