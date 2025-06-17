using System;
using System.Collections.Generic;
using _App.Models;

namespace _App.Services
{
    public interface IRewardService
    {
        /// <summary>
        /// Save or update a reward under a specific child.
        /// </summary>
        void SaveReward(ExtraRewardModel extraReward, Action<bool> onComplete);

        /// <summary>
        /// Load the most recent or default reward for a child.
        /// </summary>
        void LoadReward(string childUID, Action<ExtraRewardModel> callback);
        
        /// <summary>
        /// Check if the child has fulfilled all required days to claim this reward.
        /// </summary>
        void CheckExtraRewardEligibility(string childUID, ExtraRewardModel reward, Action<bool> callback);

        /// <summary>
        /// Payout the reward (update balance if needed and mark as claimed).
        /// </summary>
        void PayoutReward(string childUID, ExtraRewardModel reward, Action<bool> onComplete);
        
        /// <summary>
        /// Delete the reward (delete reward after it's been claimed).
        /// </summary>
        void DeleteReward(string childUID, Action<bool> onComplete);

        void ListenToReward(string childUid, Action<ExtraRewardModel> onChanged);

        void StopListeningToReward();
    }
}