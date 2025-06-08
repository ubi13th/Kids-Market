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
        void OpenRewardPanel();
        void OpenAdjustBalancePanel();

        void ShowExtraRewardEligible(bool eligible);
        void ShowRewardPayout(RewardModel reward);
        void UpdateCalendarColors(List<SmartContractModel> allContracts, string childId);
        void ShowSelectedDay(DateTime selectedDay);
        void OpenEditContractPanel();
        void SelectToday();
        void ShowGroupedContracts(Dictionary<RepeatType, List<SmartContractModel>> grouped);
        void SetupCalendarButtons();
        void OnAdminSurpriseButtonClick();
        void OnChildSurpriseButtonClick();
        
        void UpdateReports(ChildModel child, List<SmartContractModel> allContracts);
    }
}