using System;
using System.Collections.Generic;
using _App.Dashboard;

namespace _App.AdminDashboard
{
    public interface IAdminDashboardPresenter : IDashboardPresenter
    {
        string AdminUID { get; }
        
        List<ChildModel> GetAllChildren();

        void SaveContract(SmartContractModel contract);
        void EditContract(string contractId);
        void DeleteContract(string contractId);

        void OnAddContractButtonPressed();
        void PrepareNewContractDraft();
        
        void OnRewardButtonPressed();
        void OnAdjustBalanceButtonPressed();
        void OnAdminSurpriseButtonPressed();
        void OnSelectProfileButtonPressed();
        void OnExitSelectProfileButtonPressed();

        void SaveWeekStartsOnData(DayOfWeek newStartDay);
        void AdminDeclineContract(string contractId);
        
        void BuildFamilyModelAsync(Action<FamilyModel> callback);
        void RefreshChildren();

        void SetPendingNewChild(string childUid);
    }
}