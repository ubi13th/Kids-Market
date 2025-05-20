using System;
using _App.Models;

namespace _App.Services
{
    public interface IRewardService
    {
        void CheckExtraRewardEligibility(string childUID, DateTime weekStart, Action<bool> callback);
        void PayoutReward(string childUID, Action<RewardModel> onRewardIssued);
        void SaveRewardPreset(RewardModel reward, Action<bool> onComplete);
        void LoadRewardPreset(string childUID, Action<RewardModel> callback);
    }
}