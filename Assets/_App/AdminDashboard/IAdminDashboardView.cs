using System;
using System.Collections.Generic;

namespace _App.AdminDashboard
{
    public interface IAdminDashboardView
    {
        void ShowChildren(List<ChildModel> children);
        void ShowCurrentChild(ChildModel child);
        void ShowChildBalance(float balance);
        void ShowDaySelection(DateTime selectedDay);
        void ShowExtraRewardStatus(string message);
    
        void OpenContractCreator();
        void OpenProfileSelector();
        void CloseProfileSelector();
        void OpenRewardPanel();
        void OpenAdjustBalancePanel();

        void ShowExtraRewardEligible(bool eligible);
        void ShowRewardPayout(RewardModel reward);
        void UpdateCalendarColors(List<SmartContractModel> allContracts, string childId);
        void ShowSelectedDay(DateTime selectedDay);
        void OpenEditContractPanel();
        void SelectToday();
        void ShowGroupedContracts(Dictionary<RepeatType, List<SmartContractModel>> grouped);
    }
}