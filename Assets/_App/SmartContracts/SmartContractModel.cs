using System;
using UnityEngine.Serialization;

[Serializable]
public class SmartContractModel
{
    public string Id;                    // Unique contract ID
    public string Title;                 // Task title
    public string Description;           // Task description (optional)
    public string IconPath;            // Task icon (gallery or resource name)
    public string AssignedToUid;         // Child UID this contract is assigned to
    public float RewardAmount;             // Points or coins rewarded on completion
    public string DueDate;               // ISO date string (for JSON safety)
    public bool RequirePhotoProof;
    public bool RequireParentalApproval;
    public SmartContractState State;

    public DateTime GetDueDate() =>
        DateTime.TryParse(DueDate, out var date) ? date : DateTime.MinValue;

    public void SetDueDate(DateTime date) =>
        DueDate = date.ToString("o"); // ISO 8601 format for Firebase-safe string
}

public enum SmartContractState
{
    ReadyToSell = 0,
    PendingConfirmation = 1,
    Completed = 2
}