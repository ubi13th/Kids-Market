// ✅ Updated SmartContractDraft to use only RepeatDaysPerChild

using System;
using System.Collections.Generic;

// public enum AssignMode
// {
//     Everyone,
//     Anyone,
//     Rotate
// }

public static class SmartContractDraft
{
    public static string Title { get; set; }
    public static string IconPath { get; set; }
    public static float RewardAmount { get; set; }

    public static DateTime StartDate { get; set; }  // From this date the contract starts appearing
    public static TimeSpan DueTime { get; set; }    // Optional

    public static string AssignedToUid { get; set; }
    public static string Id { get; set; }
    public static bool RequiresPhotoProof { get; set; }
    public static bool RequiresParentalApproval { get; set; }
    public static bool RequireNotificationOnThisDevice { get; set; }
    public static bool SaveAsPreset { get; set; }
    public static RepeatType RepeatMode { get; set; } = RepeatType.EveryDay;
    public static Dictionary<string, List<DayOfWeek>> RepeatDaysPerChild { get; set; } = new();

    //public static AssignMode AssignmentMode = AssignMode.Everyone;

    public static void Reset(string assignedToUid = "")
    {
        Title = string.Empty;
        IconPath = string.Empty;
        RewardAmount = 0f;

        StartDate = DateTime.Today;
        DueTime = TimeSpan.Zero;

        AssignedToUid = assignedToUid;
        Id = null;
        RequiresPhotoProof = false;
        RequiresParentalApproval = false;
        RequireNotificationOnThisDevice = false;

        RepeatMode = RepeatType.EveryDay;
        RepeatDaysPerChild.Clear();
    }

    public static SmartContractModel ToContractModelFor(string childUid)
    {
        // var mode = AssignmentMode == AssignMode.Rotate
        //     ? RepeatType.SpecificDays
        //     : RepeatMode;

        var repeatDays = RepeatDaysPerChild.TryGetValue(childUid, out var value)
            ? new List<DayOfWeek>(value)
            : new List<DayOfWeek>();

        return new SmartContractModel
        {
            Title = Title,
            IconPath = IconPath,
            RewardAmount = RewardAmount,
            RequirePhotoProof = RequiresPhotoProof,
            RequireParentalApproval = RequiresParentalApproval,
            RequireNotificationOnThisDevice = RequireNotificationOnThisDevice,
            //RepeatMode = mode,
            RepeatDays = repeatDays,
            StartDate = StartDate.ToString("yyyy-MM-dd"),
            DueTime = DueTime.ToString(@"hh\:mm"),
            AssignedToUid = childUid,
            AdminUID = UserSession.CurrentUserId,
            Id = null
        };
    }

    public static void LoadFromModel(SmartContractModel model)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));

        Title = model.Title;
        IconPath = model.IconPath;
        RewardAmount = model.RewardAmount;

        AssignedToUid = model.AssignedToUid;
        Id = model.Id;
        RequiresPhotoProof = model.RequirePhotoProof;
        RequiresParentalApproval = model.RequireParentalApproval;
        RequireNotificationOnThisDevice = model.RequireNotificationOnThisDevice;

        RepeatMode = model.RepeatMode;
        StartDate = model.GetStartDate() != DateTime.MinValue ? model.GetStartDate() : DateTime.Today;
        DueTime = model.GetDueTime();
        
        // ✳️ Sync days
        RepeatDaysPerChild.Clear();
        RepeatDaysPerChild = new Dictionary<string, List<DayOfWeek>>();
        if (!string.IsNullOrEmpty(model.AssignedToUid))
            RepeatDaysPerChild[model.AssignedToUid] = new List<DayOfWeek>(model.RepeatDays);
    }

    public static void SetStartDate(DateTime date) =>
        StartDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified);

    public static void SetDueTime(TimeSpan time) => DueTime = time;
}
