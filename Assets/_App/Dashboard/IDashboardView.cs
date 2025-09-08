using System;
using System.Collections.Generic;
using _App.Models;

namespace _App.Dashboard
{
    public interface IDashboardView
    {
        void ShowChildren(List<ChildModel> children);
        void ShowCurrentChild(ChildModel child);
        void ShowChildBalance(float balance);
        void HighlightDayInCalendar(DateTime selectedDay);
        void ShowExtraRewardStatus(string message);
    
        void OpenContractCreator();
        void OpenProfileSelector();
        void CloseProfileSelector();
        void OpenRewardPanel(bool isAdmin);
        void OpenAdjustBalancePanel();
        
        void ShowExtraRewardCreator(string childUid, Action onClose, ExtraRewardModel existingReward = null);
        void ShowExtraRewardEligible(bool eligible);
        void ShowRewardPayout(ExtraRewardModel extraReward);
        /// <summary>
        /// Show the current Extra Reward title (e.g. "Pizza Party").
        /// </summary>
        void ShowExtraRewardTitle(string rewardTitle);

        /// <summary>
        /// Show how many selected days are fully completed (or bought).
        /// </summary>
        void ShowExtraRewardProgress(int completedDays, int totalDays, RewardType type);

        
        
        void UpdateCalendarColors(List<SmartContractModel> allContracts, string childId);
        void ShowSelectedDay(DateTime selectedDay);
        void OpenEditContractPanel();
        void SelectToday();
        void ShowGroupedContracts(Dictionary<RepeatType, List<SmartContractModel>> grouped);
        void SetupCalendarButtons();
        void OnChildSurpriseContractCreate();
        void OnChildSurpriseContractEdit(SmartContractModel contract);

        void UpdateReports(ChildModel child, List<SmartContractModel> allContracts);

        //event Action OnChildInitialized;
        void ShowNewProfileCreatorPanelWhenNoUserYet();
        void UpdateUIWhenNoContracts(List<SmartContractModel> allContracts);
    }
}