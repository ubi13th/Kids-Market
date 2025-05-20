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
    public string ParentId;
    public string AdminUID;
    public string AssignedToUid;
    
    //public AssignMode AssignmentMode = AssignMode.Everyone;

    [SerializeField] private bool isCopy;
    public bool IsCopy
    {
        get => isCopy;
        set => isCopy = value;
    }

    // --- New State History ---
    
    [NonSerialized]
    public Dictionary<string, SmartContractState> StateHistory = new();

    [SerializeField]
    public string stateHistoryRaw;

    public void SyncStateHistory()
    {
        StateHistory ??= new();
        stateHistoryRaw = string.Join(";", StateHistory.Select(kv => $"{kv.Key}:{(int)kv.Value}"));
    }

    public void LoadStateHistory()
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
    }

    public void SetStartDate(DateTime date) =>
        StartDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified)
            .ToString("yyyy-MM-dd");

    public DateTime GetStartDate() =>
        DateTime.TryParseExact(StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var result) ? result : DateTime.MinValue;

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

    public bool IsVisibleOn(DateTime day)
    {
        //if (IsHiddenOnDate(day))
            //return false;
        
        var start = GetStartDate().Date;
        var target = day.Date;

        if (target < start)
            return false;

        //if (IsCopy && RepeatMode == RepeatType.EveryDay)
            //return target == start; // ✅ One-day snapshot copy

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

    public bool ShouldAppearInEveryDayGroup(DateTime selectedDay)
    {
        var target = selectedDay.Date;
        var start = GetStartDate().Date;
        
        //if (HiddenDates.Contains(target.ToString("yyyy-MM-dd")))
            //return false;

        // ✅ 1. Copies appear in EveryDay only on the day they were created
        if (IsCopy && start == target)
            return true;

        // ✅ 2. Completed ONCE contracts appear in EveryDay only on completion day
        if (!IsCopy && RepeatMode == RepeatType.Once)
        {
            LoadStateHistory();
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
    }
    
    /*[NonSerialized]
    public HashSet<string> HiddenDates = new();

    [SerializeField]
    public string hiddenDatesRaw;

    public void SyncHiddenDates()
    {
        HiddenDates ??= new();
        hiddenDatesRaw = string.Join(";", HiddenDates);
    }

    public void LoadHiddenDates()
    {
        HiddenDates = new HashSet<string>();

        if (string.IsNullOrEmpty(hiddenDatesRaw))
            return;

        var entries = hiddenDatesRaw.Split(';');
        foreach (var entry in entries)
        {
            if (DateTime.TryParseExact(entry, "yyyy-MM-dd", null, DateTimeStyles.None, out _))
            {
                HiddenDates.Add(entry);
            }
        }
    }

    public void HideOnDate(DateTime date)
    {
        LoadHiddenDates();
        HiddenDates.Add(date.ToString("yyyy-MM-dd"));
        SyncHiddenDates();
    }

    public void UnhideOnDate(DateTime date)
    {
        LoadHiddenDates();
        HiddenDates.Remove(date.ToString("yyyy-MM-dd"));
        SyncHiddenDates();
    }

    public bool IsHiddenOnDate(DateTime date)
    {
        LoadHiddenDates();
        return HiddenDates.Contains(date.ToString("yyyy-MM-dd"));
    }*/
}
