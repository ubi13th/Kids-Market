using Firebase.Database;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using _App.Bootstrap;
using Firebase.Extensions;
using UnityEngine;

namespace _App.ChildDashboard
{
    public class FirebaseChildService : IChildService
    {
        private bool _isReady = false;
        
        private EventHandler<ValueChangedEventArgs> _childrenListener;
        private Query _childrenQuery;

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
            
            // Detach previous listener if it exists
            StopListening();
            
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
        
        public void AddNewChild(ChildModel child, Action<bool> callback)
        {
            if (!_isReady)
            {
                Debug.LogWarning("🟡 FirebaseChildService not ready yet.");
                callback?.Invoke(false);
                return;
            }

            // Use provided UID as key instead of letting Firebase generate it
            var newRef = ChildrenRef.Child(child.Uid);

            newRef.SetRawJsonValueAsync(JsonUtility.ToJson(child)).ContinueWithOnMainThread(task =>
            {
                callback?.Invoke(task.IsCompletedSuccessfully);
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

        public void SaveChildProfile(ChildModel child, Action<bool> callback)
        {
            if (!_isReady)
            {
                Debug.LogWarning("🟡 FirebaseChildService not ready yet.");
                callback?.Invoke(false);
                return;
            }

            var updates = new Dictionary<string, object>
            {
                [AppConstants.DisplayName] = child.DisplayName,
                [AppConstants.AvatarPath] = child.AvatarPath,
                [AppConstants.RewardPreference] = child.RewardPreference.ToString(),
                [AppConstants.AdminUID] = child.AdminUID,
                [AppConstants.JoinCode] = child.JoinCode,
                [AppConstants.Balance] = child.Balance
            };

            ChildrenRef.Child(child.Uid).UpdateChildrenAsync(updates)
                .ContinueWithOnMainThread(task =>
                {
                    if (task.IsCompletedSuccessfully)
                    {
                        Debug.Log($"✅ Updated profile for child: {child.DisplayName}");
                        callback?.Invoke(true);
                    }
                    else
                    {
                        Debug.LogError($"❌ Failed to update child profile: {task.Exception?.Message}");
                        callback?.Invoke(false);
                    }
                });
        }

        public void DeleteChild(string childId, Action<bool> callback)
        {
            if (!_isReady)
            {
                Debug.LogWarning("🟡 FirebaseChildService not ready yet.");
                callback?.Invoke(false);
                return;
            }

            var contractsRef = FirebaseInit.DbRef.Child(AppConstants.Contracts);

            // Step 1: Get all contracts assigned to this child
            contractsRef.OrderByChild(AppConstants.AssignedToUid).EqualTo(childId)
                .GetValueAsync().ContinueWithOnMainThread(task =>
            {
                if (!task.IsCompletedSuccessfully)
                {
                    Debug.LogError($"❌ Failed to query contracts: {task.Exception?.Message}");
                    callback?.Invoke(false);
                    return;
                }

                var deletionTasks = new List<Task>();

                foreach (var snapshot in task.Result.Children)
                {
                    string contractId = snapshot.Key;
                    bool isCopy = snapshot.Child(AppConstants.IsCopy).Value?.ToString().ToLower() == "true";
                    string title = snapshot.Child(AppConstants.Title).Value?.ToString();

                    var deleteTask = contractsRef.Child(contractId).RemoveValueAsync();
                    deletionTasks.Add(deleteTask);

                    if (isCopy)
                        Debug.Log($"🧾 Deleted COPY contract '{title}' for child {childId} ({contractId})");
                    else
                        Debug.Log($"🧾 Deleted contract '{title}' for child {childId} ({contractId})");
                }

                // Step 2: Delete the child after all contracts are removed
                Task.WhenAll(deletionTasks).ContinueWithOnMainThread(contractDeleteTask =>
                {
                    if (!contractDeleteTask.IsCompletedSuccessfully)
                    {
                        Debug.LogError($"❌ Failed to delete one or more contracts for {childId}: {contractDeleteTask.Exception}");
                        callback?.Invoke(false);
                        return;
                    }

                    // Step 3: Delete the child profile
                    FirebaseInit.DbRef.Child(AppConstants.Children).Child(childId)
                        .RemoveValueAsync().ContinueWithOnMainThread(childDeleteTask =>
                    {
                        if (childDeleteTask.IsCompletedSuccessfully)
                        {
                            Debug.Log($"FirebaseChildService Deleted child: {childId}");
                            callback?.Invoke(true);
                        }
                        else
                        {
                            Debug.LogError($"❌ Failed to delete child: {childDeleteTask.Exception?.Message}");
                            callback?.Invoke(false);
                        }
                    });
                });
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
                Balance = float.TryParse(snapshot.Child(AppConstants.Balance).Value?.ToString(), out float b) ? b : 0.00f,
                JoinCode = snapshot.Child(AppConstants.JoinCode).Value?.ToString()
            };
        }
        
        public void GetAdminProfile(string adminUid, Action<UserModel> callback)
        {
            FirebaseInit.DbRef.Child(AppConstants.Admins).Child(adminUid)
                .GetValueAsync().ContinueWithOnMainThread(task =>
                {
                    if (!task.IsCompletedSuccessfully || !task.Result.Exists)
                    {
                        Debug.LogWarning($"❌ Admin data not found for UID: {adminUid}");
                        callback?.Invoke(null);
                        return;
                    }

                    var snap = task.Result;
                    var user = new UserModel
                    {
                        Uid = adminUid,
                        DisplayName = snap.Child(AppConstants.DisplayName).Value?.ToString() ?? "Admin",
                        AvatarPath = snap.Child(AppConstants.AvatarPath).Value?.ToString() ?? AppConstants.DefaultAvatar,
                        JoinCode = snap.Child(AppConstants.JoinCode).Value?.ToString() ?? ""
                    };

                    callback?.Invoke(user);
                });
        }

        public void StopListening()
        {
            if (_childrenQuery != null && _childrenListener != null)
            {
                _childrenQuery.ValueChanged -= _childrenListener;
                _childrenQuery = null;
                _childrenListener = null;
                Debug.Log("🛑 Stopped listening to children updates.");
            }
        }
    }
}