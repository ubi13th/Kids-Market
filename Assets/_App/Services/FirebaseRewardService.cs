using System;
using System.Collections.Generic;
using System.Linq;
using _App.Bootstrap;
using Firebase.Database;
using Firebase.Extensions;
using Newtonsoft.Json;
using UnityEngine;

namespace _App.Services
{
    public class FirebaseRewardService : IRewardService
    {
        private bool _isReady = false;

        public FirebaseRewardService() => Init();

        private async void Init()
        {
            await FirebaseInit.WaitUntilReady();
            _isReady = true;
        }

        private DatabaseReference ContractsRef =>
            FirebaseInit.DbRef?.Child(AppConstants.SmartContracts);

        private DatabaseReference RewardsRef =>
            FirebaseInit.DbRef?.Child(AppConstants.ExtraRewards);

        private DatabaseReference ChildrenRef =>
            FirebaseInit.DbRef?.Child(AppConstants.Children);

        private DatabaseReference GetRewardRef(string childUid, string rewardId) =>
            RewardsRef.Child(childUid).Child(rewardId);
        
        private DatabaseReference _activeRewardRef;
        private EventHandler<ValueChangedEventArgs> _activeRewardValueListener;
        private EventHandler<ChildChangedEventArgs> _childAddedListener;
        private EventHandler<ChildChangedEventArgs> _childChangedListener;
        private EventHandler<ChildChangedEventArgs> _childRemovedListener;


        // ✅ Save Extra Reward
        public void SaveReward(ExtraRewardModel extraReward, Action<bool> onComplete)
        {
            if (!_isReady || string.IsNullOrEmpty(extraReward.ChildUid))
            {
                Debug.LogWarning("🟡 RewardService not ready or missing child.");
                onComplete?.Invoke(false);
                return;
            }

            if (string.IsNullOrEmpty(extraReward.Id))
                extraReward.Id = Guid.NewGuid().ToString();

            string json = JsonConvert.SerializeObject(extraReward);
            GetRewardRef(extraReward.ChildUid, extraReward.Id)
                .SetRawJsonValueAsync(json)
                .ContinueWithOnMainThread(task => onComplete?.Invoke(task.IsCompletedSuccessfully));
        }

        // ✅ Load current reward
        public void LoadReward(string childUID, Action<ExtraRewardModel> callback)
        {
            if (!_isReady)
            {
                Debug.LogWarning("🟡 RewardService not ready.");
                callback?.Invoke(null);
                return;
            }

            RewardsRef.Child(childUID).GetValueAsync().ContinueWithOnMainThread(task =>
            {
                if (!task.IsCompletedSuccessfully || !task.Result.Exists)
                {
                    callback?.Invoke(null);
                    return;
                }

                foreach (var rewardSnap in task.Result.Children)
                {
                    var json = rewardSnap.GetRawJsonValue();
                    var model = JsonConvert.DeserializeObject<ExtraRewardModel>(json);
                    model.ChildUid = childUID;
                    callback?.Invoke(model); // return first one only for now
                    return;
                }

                callback?.Invoke(null);
            });
        }

        // ✅ Check if reward can be claimed
        public void CheckExtraRewardEligibility(string childUID, ExtraRewardModel reward, Action<bool> callback)
        {
            if (!_isReady)
            {
                Debug.LogWarning("🟡 RewardService not ready.");
                callback?.Invoke(false);
                return;
            }

            ContractsRef
                .OrderByChild(AppConstants.AssignedToUid)
                .EqualTo(childUID)
                .GetValueAsync().ContinueWithOnMainThread(task =>
                {
                    if (!task.IsCompletedSuccessfully)
                    {
                        Debug.LogError("❌ Failed to check reward eligibility.");
                        callback?.Invoke(false);
                        return;
                    }

                    bool eligible = true;

                    foreach (var snapshot in task.Result.Children)
                    {
                        var json = snapshot.GetRawJsonValue();
                        var contract = JsonConvert.DeserializeObject<SmartContractModel>(json);

                        foreach (var dayOfWeek in reward.SelectedDays)
                        {
                            DateTime date = GetClosestDateForWeekday(dayOfWeek);

                            if (contract.IsVisibleOn(date) &&
                                !contract.HasStateOnDate(date, SmartContractState.Completed) &&
                                !contract.HasStateOnDate(date, SmartContractState.Purchased)) // "Buy Back"
                            {
                                eligible = false;
                                break;
                            }
                        }

                        if (!eligible)
                            break;
                    }

                    callback?.Invoke(eligible);
                });
        }

        // ✅ Payout and mark as claimed
        public void PayoutReward(string childUID, ExtraRewardModel reward, Action<bool> onComplete)
        {
            if (!_isReady)
            {
                Debug.LogWarning("🟡 RewardService not ready.");
                onComplete?.Invoke(false);
                return;
            }

            if (reward.Type == RewardType.Money || reward.Type == RewardType.Points)
            {
                var balanceRef = ChildrenRef.Child(childUID).Child(AppConstants.Balance);

                balanceRef.GetValueAsync().ContinueWithOnMainThread(balanceTask =>
                {
                    if (!balanceTask.IsCompletedSuccessfully)
                    {
                        Debug.LogError("❌ Failed to fetch balance.");
                        onComplete?.Invoke(false);
                        return;
                    }

                    float.TryParse(balanceTask.Result?.Value?.ToString(), out var currentBalance);
                    float newBalance = currentBalance + reward.RewardAmount;

                    balanceRef.SetValueAsync(newBalance).ContinueWithOnMainThread(setTask =>
                    {
                        if (!setTask.IsCompletedSuccessfully)
                        {
                            Debug.LogError("❌ Failed to update balance.");
                            onComplete?.Invoke(false);
                            return;
                        }

                        reward.IsClaimed = true;
                        SaveReward(reward, saveOk => onComplete?.Invoke(saveOk));
                    });
                });
            }
            else
            {
                reward.IsClaimed = true;
                SaveReward(reward, saveOk => onComplete?.Invoke(saveOk));
            }
        }
        
        public void ListenToReward(string childUid, Action<ExtraRewardModel> onChanged)
        {
            StopListeningToReward(); // cleanup first

            _activeRewardRef = RewardsRef.Child(childUid);

            _childAddedListener = (sender, args) =>
            {
                if (!args.Snapshot.Exists) return;

                var json = args.Snapshot.GetRawJsonValue();
                var reward = JsonConvert.DeserializeObject<ExtraRewardModel>(json);
                reward.Id = args.Snapshot.Key;
                reward.ChildUid = childUid;

                onChanged?.Invoke(reward);
            };

            _childChangedListener = (sender, args) =>
            {
                if (!args.Snapshot.Exists) return;

                var json = args.Snapshot.GetRawJsonValue();
                var reward = JsonConvert.DeserializeObject<ExtraRewardModel>(json);
                reward.Id = args.Snapshot.Key;
                reward.ChildUid = childUid;

                onChanged?.Invoke(reward);
            };

            _childRemovedListener = (sender, args) =>
            {
                Debug.Log($"🗑️ Reward removed for child: {childUid}");
                onChanged?.Invoke(null); // notify UI to reset
            };

            _activeRewardRef.ChildAdded += _childAddedListener;
            _activeRewardRef.ChildChanged += _childChangedListener;
            _activeRewardRef.ChildRemoved += _childRemovedListener;
        }

        
        /*public void ListenToReward(string childUid, Action<ExtraRewardModel> onChanged)
        {
            // Cleanup previous listener
            if (_activeRewardRef != null && _activeRewardListener != null)
                _activeRewardRef.ValueChanged -= _activeRewardListener;

            // Set new reference
            _activeRewardRef = RewardsRef.Child(childUid);
            _activeRewardListener = (_, args) =>
            {
                if (!args.Snapshot.Exists || !args.Snapshot.HasChildren)
                {
                    onChanged?.Invoke(null);
                    return;
                }

                foreach (var rewardSnap in args.Snapshot.Children)
                {
                    var json = rewardSnap.GetRawJsonValue();
                    var model = JsonConvert.DeserializeObject<ExtraRewardModel>(json);
                    model.Id = rewardSnap.Key;
                    model.ChildUid = childUid;

                    onChanged?.Invoke(model); // only first
                    return;
                }

                onChanged?.Invoke(null);
            };

            // Attach listener
            _activeRewardRef.ValueChanged += _activeRewardListener;
        }*/

        
        public void StopListeningToReward()
        {
            if (_activeRewardRef != null)
            {
                if (_activeRewardValueListener != null)
                {
                    _activeRewardRef.ValueChanged -= _activeRewardValueListener;
                    _activeRewardValueListener = null;
                }

                if (_childAddedListener != null)
                {
                    _activeRewardRef.ChildAdded -= _childAddedListener;
                    _childAddedListener = null;
                }

                if (_childChangedListener != null)
                {
                    _activeRewardRef.ChildChanged -= _childChangedListener;
                    _childChangedListener = null;
                }

                if (_childRemovedListener != null)
                {
                    _activeRewardRef.ChildRemoved -= _childRemovedListener;
                    _childRemovedListener = null;
                }

                _activeRewardRef = null;
                
                Debug.Log("🧹 Stopped reward listeners for child.");
            }
        }
        
        public void DeleteReward(string childUID, Action<bool> onComplete)
        {
            var rewardRef = FirebaseInit.DbRef
                .Child(AppConstants.ExtraRewards)
                .Child(childUID);

            rewardRef.RemoveValueAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsCompletedSuccessfully)
                {
                    Debug.Log($"🗑️ Deleted reward for child {childUID}");
                    onComplete?.Invoke(true);
                }
                else
                {
                    Debug.LogError($"❌ Failed to delete reward for child {childUID}");
                    onComplete?.Invoke(false);
                }
            });
        }


        // 🔧 Helper
        private DateTime GetClosestDateForWeekday(DayOfWeek day)
        {
            var today = DateTime.Today;
            int daysUntilTarget = ((int)day - (int)today.DayOfWeek + 7) % 7;
            return today.AddDays(daysUntilTarget);
        }
    }
}