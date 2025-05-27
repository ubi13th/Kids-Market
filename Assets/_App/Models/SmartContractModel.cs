using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

[Serializable]
public class SmartContractModel
{
    public string Title;
    public float RewardAmount;

    public string StartDate;   // "yyyy-MM-dd"
    public RepeatType RepeatMode = RepeatType.EveryDay;
    public List<DayOfWeek> RepeatDays = new();
    public string DueTime;     // "HH:mm"

    public bool RequirePhotoProof;
    public bool RequireParentalApproval;
    public bool RequireNotificationOnThisDevice;

    public string IconPath;

    public string Id;
    public string OriginalId;
    public string ParentId;
    public string AdminUID;
    public string AssignedToUid;
    
    [SerializeField] private bool isCopy;
    public bool IsCopy
    {
        get => isCopy;
        set => isCopy = value;
    }
    
    public void SetStartDate(DateTime date) =>
        StartDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified)
            .ToString("yyyy-MM-dd");
    
    public DateTime GetStartDate()
    {
        if (string.IsNullOrEmpty(StartDate)) return DateTime.MinValue;
        return DateTime.TryParseExact(StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var result) ? result : DateTime.MinValue;
    }
    // public DateTime GetStartDate() =>
    //     DateTime.TryParseExact(StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
    //         DateTimeStyles.None, out var result) ? result : DateTime.MinValue;

    public void SetDueTime(TimeSpan time)
    {
        try
        {
            DueTime = time.ToString(@"hh\:mm");
        }
        catch (FormatException e)
        {
            Debug.LogError($"❌ Invalid DueTime: {e.Message}");
            DueTime = "00:00";
        }
    }

    public TimeSpan GetDueTime() =>
        TimeSpan.TryParse(DueTime, out var time) ? time : TimeSpan.Zero;

    // --- New State History --------------------------------------
    
    [NonSerialized]
    public Dictionary<string, List<StateRecord>> StateHistory = new();
    //public Dictionary<string, SmartContractState> StateHistory = new();

    [SerializeField]
    public string stateHistoryRaw;
    
    [Serializable]
    public class StateRecord
    {
        public string QueueId;
        public SmartContractState State;
    }
    
    private string ExtractDatePart(string queueId) => queueId.Split('#')[0];
    
    public void SyncStateHistory()
    {
        if (IsCopy && RepeatMode == RepeatType.AsNeeded)
        {
            // ✅ AsNeeded copy format: "2025-05-24#0:3#1:3#2:3|..."
            var groupedByDay = StateHistory
                .SelectMany(kv => kv.Value)
                .GroupBy(record => ExtractDatePart(record.QueueId))
                .ToDictionary(g => g.Key, g => g.ToList());
            
            var serialized = new List<string>();

            foreach (var day in groupedByDay.Keys)
            {
                var records = groupedByDay[day]
                    .OrderBy(r => ExtractQueueIndex(r.QueueId))
                    .Select(r => $"#{ExtractQueueIndex(r.QueueId)}:{(int)r.State}");

                serialized.Add($"{day}{string.Join("", records)}");
            }

            stateHistoryRaw = string.Join("|", serialized);
        }
        else
        {
            // ✅ Normal flat format: "2025-05-24:3;2025-05-25:0"
            stateHistoryRaw = string.Join(";",
                StateHistory.Select(kv => $"{kv.Key}:{(int)kv.Value.Last().State}"));
        }
    }
    
    private static int ExtractQueueIndex(string queueId)
    {
        if (string.IsNullOrEmpty(queueId)) return 0;
        var parts = queueId.Split('#');
        return (parts.Length == 2 && int.TryParse(parts[1], out var idx)) ? idx : 0;
    }

    public void LoadStateHistory()
    {
        StateHistory = new Dictionary<string, List<StateRecord>>();

        if (string.IsNullOrEmpty(stateHistoryRaw))
            return;

        // ✅ FORMAT A: AsNeeded Copy → queue-based "2025-05-24#0:3#1:3|..."
        if (IsCopy && RepeatMode == RepeatType.AsNeeded)
        {
            var entries = stateHistoryRaw.Split('|');

            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry))
                    continue;

                var segments = entry.Split('#');
                if (segments.Length < 2) continue;

                string datePart = segments[0];

                for (int i = 1; i < segments.Length; i++)
                {
                    var sub = segments[i].Split(':');
                    if (sub.Length == 2 &&
                        int.TryParse(sub[0], out var queueIndex) &&
                        int.TryParse(sub[1], out var stateValue))
                    {
                        string key = $"{datePart}#{queueIndex}";

                        var record = new StateRecord
                        {
                            QueueId = key,
                            State = (SmartContractState)stateValue
                        };

                        if (!StateHistory.TryGetValue(key, out var list))
                            StateHistory[key] = list = new();
                        list.Add(record);
                    }
                }
            }

            return;
        }

        // ✅ FORMAT B: Normal Flat "2025-05-24:3;2025-05-25:0"
        var flatEntries = stateHistoryRaw.Split(';');

        foreach (var entry in flatEntries)
        {
            var parts = entry.Split(':');
            if (parts.Length == 2 &&
                DateTime.TryParseExact(parts[0], "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out _) &&
                int.TryParse(parts[1], out int stateValue))
            {
                string key = parts[0];

                var record = new StateRecord
                {
                    QueueId = null,
                    State = (SmartContractState)stateValue
                };

                if (!StateHistory.ContainsKey(key))
                    StateHistory[key] = new List<StateRecord>();

                StateHistory[key].Add(record);
            }
        }
    }

    
    public void SetStateOnDate(DateTime date, SmartContractState state)
    {
        if (IsCopy && RepeatMode == RepeatType.AsNeeded)
        {
            Debug.LogWarning("❌ Use SetStateOnDateWithQueue for AsNeeded copies.");
            return;
        }

        LoadStateHistory();
        var key = date.ToString("yyyy-MM-dd");

        if (!StateHistory.ContainsKey(key))
            StateHistory[key] = new List<StateRecord>();

        // Only store a basic record (no queue tracking needed)
        StateHistory[key].Add(new StateRecord
        {
            QueueId = null, // ✅ Explicitly null
            State = state
        });

        SyncStateHistory();
    }
    
    public SmartContractState GetStateOnDate(DateTime date, bool isAdmin)
    {
        LoadStateHistory();
        string prefix = date.ToString("yyyy-MM-dd");

        var records = StateHistory
            .Where(kv => kv.Key == prefix || kv.Key.StartsWith(prefix + "#"))
            .SelectMany(kv => kv.Value)
            .ToList();

        if (records.Any())
            return records.Last().State;

        return isAdmin ? SmartContractState.ReadyToConfirm : SmartContractState.ReadyToSell;
    }

    public SmartContractState GetStateOnDate(DateTime date) =>
        GetStateOnDate(date, UserSession.IsAdmin);
    
    public bool HasStateOnDate(DateTime date, SmartContractState state)
    {
        LoadStateHistory();
        string prefix = date.ToString("yyyy-MM-dd");

        return StateHistory
            .Where(kv => kv.Key == prefix || kv.Key.StartsWith(prefix + "#"))
            .SelectMany(kv => kv.Value)
            .Any(r => r.State == state);
    }

    public bool HasStateOnDate(DateTime date)
    {
        LoadStateHistory();
        string prefix = date.ToString("yyyy-MM-dd");

        return StateHistory.Keys.Any(k => k == prefix || k.StartsWith(prefix + "#"));
    }
    
    public void RemoveStateRecord(DateTime date, string queueId)
    {
        LoadStateHistory();
        string targetKey  = TryGetMatchingKey(date);

        if (string.IsNullOrEmpty(targetKey ))
            return;

        var list = StateHistory[targetKey ];

        if (string.IsNullOrEmpty(queueId))
        {
            StateHistory.Remove(targetKey );
        }
        else
        {
            var target = list.FirstOrDefault(r => r.QueueId == queueId);
            if (target != null)
            {
                list.Remove(target);
                if (list.Count == 0)
                    StateHistory.Remove(targetKey );
            }
        }

        SyncStateHistory();
    }
    
    public void SetStateOnDateWithQueue(DateTime date, SmartContractState state, int queueIndex)
    {
        LoadStateHistory();

        if (!IsCopy || RepeatMode != RepeatType.AsNeeded)
        {
            Debug.LogWarning("❌ SetStateOnDateWithQueue should only be used for AsNeeded copies.");
            return;
        }

        string key = $"{date:yyyy-MM-dd}#{queueIndex}";
        string queueId = key;

        if (!StateHistory.ContainsKey(key))
            StateHistory[key] = new List<StateRecord>();

        StateHistory[key].Add(new StateRecord
        {
            QueueId = queueId,
            State = state
        });

        SyncStateHistory();
    }
    
    public SmartContractState GetLatestStateOnDate(DateTime date, bool isAdmin)
    {
        LoadStateHistory();
        string prefix = date.ToString("yyyy-MM-dd");

        // Gather all records with matching flat or queued keys
        var matching = StateHistory
            .Where(kv => kv.Key == prefix || kv.Key.StartsWith(prefix + "#"))
            .SelectMany(kv => kv.Value)
            .ToList();

        if (matching.Any())
            return matching.Last().State; // Latest added

        return isAdmin ? SmartContractState.ReadyToConfirm : SmartContractState.ReadyToSell;
    }
    
    public string TryGetMatchingKey(DateTime date)
    {
        string prefix = date.ToString("yyyy-MM-dd");

        return StateHistory.Keys
            .FirstOrDefault(k => k == prefix || k.StartsWith(prefix + "#"));
    }
    
    public bool ShouldAppearInEveryDayGroup(DateTime selectedDay)
    {
        LoadStateHistory();
        var key = selectedDay.ToString("yyyy-MM-dd");

        if (IsCopy && RepeatMode == RepeatType.AsNeeded)
        {
            // ✅ Check all queue-based entries for AsNeeded copy
            return StateHistory
                .Where(kv => kv.Key.StartsWith(key + "#"))
                .SelectMany(kv => kv.Value)
                .Any(r =>
                    r.State == SmartContractState.Completed ||
                    r.State == SmartContractState.ReadyToConfirm);
        }

        if (!IsCopy && RepeatMode == RepeatType.Once)
        {
            // ✅ Check flat key for Once parent
            return StateHistory.TryGetValue(key, out var records) &&
                   records.Any(r => r.State == SmartContractState.Completed);
        }

        if (!IsCopy && (RepeatMode == RepeatType.EveryDay || RepeatMode == RepeatType.SpecificDays))
        {
            return IsVisibleOn(selectedDay);
        }

        return false;
    }
    
    public bool IsVisibleOn(DateTime day)
    {
        var start = GetStartDate().Date;
        var target = day.Date;

        if (target < start)
            return false;

        return RepeatMode switch
        {
            RepeatType.EveryDay => true,
            RepeatType.Once => IsOnceVisibleOn(target),
            RepeatType.SpecificDays => RepeatDays.Contains(target.DayOfWeek),
            RepeatType.AsNeeded => !IsCopy,
            _ => false
        };
    }

    private bool IsOnceVisibleOn(DateTime target)
    {
        LoadStateHistory();
        if (StateHistory.TryGetValue(target.ToString("yyyy-MM-dd"), out var stateOnTarget) &&
            stateOnTarget.Any(r => r.State == SmartContractState.Completed))
            return false;

        foreach (var kv in StateHistory)
        {
            if (kv.Value.Any(r => r.State == SmartContractState.Completed) &&
                DateTime.TryParse(kv.Key, out var completedDay) &&
                completedDay < target)
            {
                return false;
            }
        }

        return true;
    }

    public RepeatType GetEffectiveRepeatMode() =>
        IsCopy ? RepeatType.EveryDay : RepeatMode;
    
    
    
    
    
    //----------------- old code ----------------------------
    
    // public void SyncStateHistory()
    // {
    //     StateHistory ??= new();
    //     stateHistoryRaw = string.Join(";", StateHistory.Select(kv => $"{kv.Key}:{(int)kv.Value}"));
    // }

    /*public void LoadStateHistory()
    {
        StateHistory = new Dictionary<string, SmartContractState>();

        if (string.IsNullOrEmpty(stateHistoryRaw))
            return;

        var entries = stateHistoryRaw.Split(';');
        foreach (var entry in entries)
        {
            var parts = entry.Split(':');
            if (parts.Length == 2 &&
                DateTime.TryParseExact(parts[0], "yyyy-MM-dd", null, DateTimeStyles.None, out var _) &&
                int.TryParse(parts[1], out int state))
            {
                StateHistory[parts[0]] = (SmartContractState)state;
            }
        }
    }*/

    /*public bool IsVisibleOn(DateTime day)
    {
        var start = GetStartDate().Date;
        var target = day.Date;

        if (target < start)
            return false;

        return RepeatMode switch
        {
            RepeatType.EveryDay => true,
            RepeatType.Once => IsOnceVisibleOn(target),
            RepeatType.SpecificDays => RepeatDays.Contains(target.DayOfWeek),
            RepeatType.AsNeeded => !IsCopy,

            _ => false
        };
    }

    private bool IsOnceVisibleOn(DateTime target)
    {
        LoadStateHistory();

        if (StateHistory.TryGetValue(target.ToString("yyyy-MM-dd"), out var stateOnTarget) &&
            stateOnTarget == SmartContractState.Completed)
            return false;

        foreach (var kv in StateHistory)
        {
            if (kv.Value == SmartContractState.Completed &&
                DateTime.TryParse(kv.Key, out var completedDay) &&
                completedDay < target)
            {
                return false;
            }
        }

        return true;
    }

    public void SetStateOnDate(DateTime date, SmartContractState state)
    {
        LoadStateHistory();
        var key = date.ToString("yyyy-MM-dd");
        StateHistory[key] = state;
        SyncStateHistory();
    }

    public SmartContractState GetStateOnDate(DateTime date, bool isAdmin)
    {
        LoadStateHistory();
        var key = date.ToString("yyyy-MM-dd");

        if (StateHistory.TryGetValue(key, out var state))
            return state;

        return isAdmin ? SmartContractState.ReadyToConfirm : SmartContractState.ReadyToSell;
    }

    public SmartContractState GetStateOnDate(DateTime date) =>
        GetStateOnDate(date, UserSession.IsAdmin);

    public void RemoveStateOnDate(DateTime date)
    {
        LoadStateHistory();
        var key = date.ToString("yyyy-MM-dd");
        if (StateHistory.Remove(key))
            SyncStateHistory();
    }

    public bool HasStateOnDate(DateTime date, SmartContractState state)
    {
        LoadStateHistory();
        var key = date.ToString("yyyy-MM-dd");
        return StateHistory.TryGetValue(key, out var storedState) && storedState == state;
    }
    
    public bool HasStateOnDate(DateTime date)
    {
        LoadStateHistory();
        return StateHistory.ContainsKey(date.ToString("yyyy-MM-dd"));
    }

    public bool ShouldAppearInEveryDayGroup(DateTime selectedDay)
    {
        var target = selectedDay.Date;
        LoadStateHistory();

        // ✅ 1. Copies appear if they have state for the day
        if (IsCopy && StateHistory.TryGetValue(target.ToString("yyyy-MM-dd"), out var copyState))
        {
            if (copyState == SmartContractState.Completed)
                return true;
        }

        // ✅ 2. Completed ONCE contracts appear in EveryDay only on completion day
        if (!IsCopy && RepeatMode == RepeatType.Once)
        {
            if (StateHistory.TryGetValue(target.ToString("yyyy-MM-dd"), out var state) &&
                state == SmartContractState.Completed)
                return true;
        }

        // ✅ 3. Regular EveryDay or SpecificDays contracts show if visible
        if (!IsCopy && (RepeatMode == RepeatType.EveryDay || RepeatMode == RepeatType.SpecificDays))
        {
            return IsVisibleOn(target);
        }

        return false;
    }

    public RepeatType GetEffectiveRepeatMode()
    {
        return IsCopy ? RepeatType.EveryDay : RepeatMode;
    }*/
}
