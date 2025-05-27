using Firebase.Database;
using System;
using System.Collections.Generic;
using _App.Bootstrap;
using Firebase.Extensions;
using UnityEngine;

namespace _App.ChildDashboard
{
    public class FirebaseChildService : IChildService
    {
        private bool _isReady = false;

        public FirebaseChildService() => 
            Init();

        private async void Init()
        {
            await FirebaseInit.WaitUntilReady();
            _isReady = true;
        }

        private DatabaseReference ChildrenRef =>
            FirebaseInit.DbRef?.Child(AppConstants.Children);

        public void ListenToChildren(string adminUID, Action<List<ChildModel>> onChanged)
        {
            if (!_isReady)
            {
                Debug.LogWarning("🟡 FirebaseChildService not ready yet.");
                return;
            }
            
            ChildrenRef
                .OrderByChild(AppConstants.AdminUID)
                .EqualTo(adminUID)
                .ValueChanged += (sender, args) =>
            {
                if (args.DatabaseError != null)
                {
                    Debug.LogError("Failed to fetch children: " + args.DatabaseError.Message);
                    onChanged?.Invoke(null);
                    return;
                }

                var children = new List<ChildModel>();
                foreach (var snapshot in args.Snapshot.Children)
                {
                    try
                    {
                        var model = ParseChild(snapshot);
                        children.Add(model);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("Error parsing child: " + e.Message);
                    }
                }

                onChanged?.Invoke(children);
            };
        }

        public void AddNewChild(ChildModel child, Action<bool> onComplete)
        {
            if (!_isReady)
            {
                Debug.LogWarning("🟡 FirebaseChildService not ready yet.");
                return;
            }
            
            var newRef = ChildrenRef.Push();
            newRef.SetRawJsonValueAsync(JsonUtility.ToJson(child)).ContinueWith(task =>
            {
                onComplete?.Invoke(task.IsCompletedSuccessfully);
            });
        }

        public void GetChildById(string childId, Action<ChildModel> callback)
        {
            if (!_isReady)
            {
                Debug.LogWarning("🟡 FirebaseChildService not ready yet.");
                return;
            }

            ChildrenRef.Child(childId).GetValueAsync().ContinueWithOnMainThread(task =>
            {
                if (!task.IsCompletedSuccessfully || !task.Result.Exists)
                {
                    callback?.Invoke(null);
                    return;
                }

                var model = ParseChild(task.Result);
                callback?.Invoke(model);
            });
        }

        public void UpdateBalance(string childUid, float newBalance, Action<bool> callback)
        {
            if (!_isReady)
            {
                Debug.LogWarning("🟡 FirebaseChildService not ready.");
                callback?.Invoke(false);
                return;
            }

            FirebaseInit.DbRef
                .Child(AppConstants.Children)
                .Child(childUid)
                .Child(AppConstants.Balance)
                .SetValueAsync(newBalance)
                .ContinueWithOnMainThread(task =>
                {
                    if (task.IsCompletedSuccessfully)
                        Debug.Log($"✅ Balance updated to {newBalance} for child {childUid}");
                    else
                        Debug.LogError("❌ Failed to update balance: " + task.Exception?.Message);

                    callback?.Invoke(task.IsCompletedSuccessfully);
                });
        }
        
        private ChildModel ParseChild(DataSnapshot snapshot)
        {
            return new ChildModel
            {
                Uid = snapshot.Key,
                DisplayName = snapshot.Child(AppConstants.DisplayName).Value?.ToString(),
                AvatarPath = snapshot.Child(AppConstants.AvatarPath).Value?.ToString(),
                AdminUID = snapshot.Child(AppConstants.AdminUID).Value?.ToString(),
                RewardPreference = Enum.TryParse(snapshot.Child(AppConstants.RewardPreference).Value?.ToString(), out RewardType r) ? r : RewardType.Money,
                Balance = float.TryParse(snapshot.Child(AppConstants.Balance).Value?.ToString(), out float b) ? b : 0.00f
            };
        }
    }
}