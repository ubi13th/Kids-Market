using System;
using System.Collections.Generic;

[Serializable]
public class SmartContractCustomPreset
{
    public string title;
    public string iconPath;
    public float defaultReward;

    public string startDate;       // Store as string for JSON compatibility
    public string dueTime;

    public RepeatType repeatMode;
    public List<DayOfWeek> repeatDays = new();

    public bool requiresPhotoProof;
    public bool requiresParentalApproval;
    public bool requireNotificationOnThisDevice;
}