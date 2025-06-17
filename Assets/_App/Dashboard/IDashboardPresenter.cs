using System;
using System.Collections.Generic;
using _App.Models;

namespace _App.Dashboard
{
    public interface IDashboardPresenter
    {
        DateTime SelectedDay { get; }

        void Initialize(string userUid);
        void OnDaySelected(DateTime day);
        void SetCurrentChild(ChildModel child);
        ChildModel CurrentChild { get; }


        void ConfirmContractByRole(string contractId);
        void UndoConfirmContractByRole(string contractId);
        
        List<SmartContractModel> GetAllContracts();

        void ChildBuyAdminSellContract(string contractId, DateTime selectedDay);
        void UndoPurchaseContract(string contractId, DateTime selectedDay);

        void OnChildSurpriseButtonPressed();
        
        void SaveContract(SmartContractModel contract);
        void EditContract(string contractId);
        void DeleteContract(string contractId);
        event Action OnChildInitialized;
        
        void PayoutExtraReward();

        void CleanupContractListenerService();
        void CleanupChildListenerService();
        int GetLastQueueIndexForDay(SmartContractModel contract, DateTime selectedDay);
        
        void OnRewardButtonPressed();
        void OpenExtraRewardCreator();
        void ClaimExtraReward();
    }
}