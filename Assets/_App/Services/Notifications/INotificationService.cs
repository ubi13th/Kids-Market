using System.Collections.Generic;
using _App.Models;

namespace _App.Services.Notifications
{
    public enum NotificationEventType
    {
        ContractCreatedByAdmin,
        ContractSubmittedByChild,
        ContractUndoByChild,
        ContractUndoPurchasedByChild,
        ContractPurchasedByChild,
        ContractApprovedByAdmin,
        ContractDeclinedByAdmin,
        ContractPurchasedByAdmin,
        ContractUndoPurchasedByAdmin,
        ContractUndoByAdmin,
        SurpriseContractCreated,
        SurpriseContractUpdated
    }

    public interface INotificationService
    {
        /// <summary>
        /// Send a push notification to a single target user.
        /// </summary>
        void Notify(string targetUid, NotificationEventType type, SmartContractModel contract, string actorUid, string actorRole);

        /// <summary>
        /// Send the same push to multiple target users.
        /// </summary>
        void NotifyMany(IEnumerable<string> targetUids, NotificationEventType type, SmartContractModel contract, string actorUid, string actorRole);
    }
}