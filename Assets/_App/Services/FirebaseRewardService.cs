using System;
using System.Collections.Generic;
using _App.Bootstrap;
using Firebase.Database;
using Firebase.Extensions;
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

        // ✅ Check if child completed all visible contracts this week
        public void CheckExtraRewardEligibility(string childUID, DateTime weekStart, Action<bool> callback)
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
                    DateTime weekEnd = weekStart.AddDays(7);

                    foreach (var snapshot in task.Result.Children)
                    {
                        var json = snapshot.GetRawJsonValue();
                        var contract = JsonUtility.FromJson<SmartContractModel>(json);

                        // Check if contract is visible on any day this week
                        for (int i = 0; i < 7; i++)
                        {
                            var day = weekStart.AddDays(i);
                            if (contract.IsVisibleOn(day))// &&
                                //contract.State != SmartContractState.Completed &&
                                //contract.State != SmartContractState.Purchased)
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

        // ✅ Reward payout (supports float balances for money rewards)
        public void PayoutReward(string childUID, Action<RewardModel> onRewardIssued)
        {
            if (!_isReady)
            {
                Debug.LogWarning("🟡 RewardService not ready.");
                return;
            }

            LoadRewardPreset(childUID, preset =>
            {
                if (preset == null)
                {
                    Debug.LogError("❌ Reward preset is null.");
                    onRewardIssued?.Invoke(null);
                    return;
                }

                if (preset.Type == RewardType.Points || preset.Type == RewardType.Money)
                {
                    var balanceRef = ChildrenRef.Child(childUID).Child(AppConstants.Balance);

                    balanceRef.GetValueAsync().ContinueWithOnMainThread(balanceTask =>
                    {
                        if (!balanceTask.IsCompletedSuccessfully)
                        {
                            Debug.LogError("❌ Failed to fetch balance.");
                            onRewardIssued?.Invoke(null);
                            return;
                        }

                        float currentBalance = 0f;
                        float.TryParse(balanceTask.Result?.Value?.ToString(), out currentBalance);

                        float newBalance = currentBalance + preset.Amount;

                        balanceRef.SetValueAsync(newBalance).ContinueWithOnMainThread(setTask =>
                        {
                            if (setTask.IsCompletedSuccessfully)
                            {
                                Debug.Log($"✅ Reward issued. New balance: {newBalance}");
                                onRewardIssued?.Invoke(preset);
                            }
                            else
                            {
                                Debug.LogError("❌ Failed to update balance.");
                                onRewardIssued?.Invoke(null);
                            }
                        });
                    });
                }
                else
                {
                    // 🎉 Event reward — no balance modification
                    onRewardIssued?.Invoke(preset);
                }
            });
        }

        public void SaveRewardPreset(RewardModel reward, Action<bool> onComplete)
        {
            if (!_isReady)
            {
                Debug.LogWarning("🟡 RewardService not ready.");
                onComplete?.Invoke(false);
                return;
            }

            if (string.IsNullOrEmpty(reward.ChildUid))
            {
                Debug.LogError("❌ Reward missing ChildUid.");
                onComplete?.Invoke(false);
                return;
            }

            string json = JsonUtility.ToJson(reward);
            RewardsRef.Child(reward.ChildUid).SetRawJsonValueAsync(json)
                .ContinueWithOnMainThread(task => onComplete?.Invoke(task.IsCompletedSuccessfully));
        }

        public void LoadRewardPreset(string childUID, Action<RewardModel> callback)
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

                var model = JsonUtility.FromJson<RewardModel>(task.Result.GetRawJsonValue());
                model.ChildUid = childUID;
                callback?.Invoke(model);
            });
        }
    }
}
