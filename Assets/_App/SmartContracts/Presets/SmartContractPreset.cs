using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SmartContractPreset", menuName = "KidsMarket/SmartContractPreset")]
public class SmartContractPreset : ScriptableObject
{
    public string title;
    public string iconPath;
    public float defaultReward;

    public DateTime startDate;
    public TimeSpan dueTime;

    public RepeatType repeatMode;
    public List<DayOfWeek> repeatDays = new();

    public bool requiresPhotoProof;
    public bool requiresParentalApproval;
    public bool requireNotificationOnThisDevice;
}