using System;
using System.Collections.Generic;

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

        void ChildBuyAdminSellContract(string contractId);
        void UndoPurchaseContract(string contractId);

        void OnChildSurpriseButtonPressed();
        
        void PayoutExtraReward();

        void CleanupContractListenerService();
        void CleanupChildListenerService();
        int GetLastQueueIndexForDay(SmartContractModel contract, DateTime selectedDay);
    }
}