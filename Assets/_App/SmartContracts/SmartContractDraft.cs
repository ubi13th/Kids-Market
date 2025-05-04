using System;

public static class SmartContractDraft
{
    public static string Title { get; set; }
    public static string Description { get; set; }
    public static string IconPath { get; set; }
    public static float RewardAmount { get; set; }
    public static DateTime DueDate { get; set; }
    public static string AssignedToUid { get; set; }
    
    public static bool RequiresPhotoProof { get; set; }
    public static bool RequiresParentalApproval { get; set; }
    public static bool ReminderEnabled { get; set; }
    public static TimeSpan DueTime { get; set; } // Only time of day

    public static SmartContractState State { get; set; } = SmartContractState.ReadyToSell;

    public static void Reset()
    {
        Title = "";
        Description = "";
        IconPath = "";
        RewardAmount = 0.00f;
        DueDate = DateTime.UtcNow.AddDays(1);
        AssignedToUid = "";
        State = SmartContractState.ReadyToSell;
    }
}