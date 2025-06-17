using System;
using System.Collections.Generic;
using Newtonsoft.Json;

[Serializable]
public class ExtraRewardModel
{
    [JsonProperty] public string Id;
    [JsonProperty] public string AdminUid;
    [JsonProperty] public string ChildUid;
    [JsonProperty] public string RewardTitle;
    [JsonProperty] public string IconPath;
    [JsonProperty] public string EventDescription; // only if type == Event
    [JsonProperty] public float RewardAmount; // only if type == Money
    [JsonProperty] public RewardType Type;
    [JsonProperty] public List<DayOfWeek> SelectedDays;
    [JsonProperty] public bool IsClaimed;
}