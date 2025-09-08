using System;
using _App.Bootstrap;
using _App.Services;
using Firebase.Database;
using Firebase.Extensions;
using UnityEngine;

public class FirebaseSettingsService : IAppSettingsService
{
    private readonly DatabaseReference _dbRef = FirebaseInit.DbRef;

    public void SaveWeekStartsOn(DayOfWeek day, string adminUID)
    {
        int value = (int)day;
        _dbRef.Child(AppConstants.Admins).Child(adminUID).Child(AppConstants.Settings).Child(AppConstants.WeekStartsOn)
            .SetValueAsync(value)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsCompletedSuccessfully)
                    Debug.Log($"✅ WeekStartsOn saved as {day} ({value})");
                else
                    Debug.LogError("❌ Failed to save WeekStartsOn to Firebase");
            });
    }

    public void LoadWeekStartsOn(string adminUID, Action<DayOfWeek> onLoaded)
    {
        _dbRef.Child(AppConstants.Admins).Child(adminUID).Child(AppConstants.Settings).Child(AppConstants.WeekStartsOn)
            .GetValueAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsCompletedSuccessfully && task.Result.Exists)
                {
                    int value = Convert.ToInt32(task.Result.Value);
                    DayOfWeek loadedDay = (DayOfWeek)value;
                    Debug.Log($"📥 Loaded WeekStartsOn: {loadedDay}");
                    onLoaded?.Invoke(loadedDay);
                }
                else
                {
                    Debug.LogWarning("⚠️ WeekStartsOn not found — using default (Monday)");
                    onLoaded?.Invoke(DayOfWeek.Monday); // fallback
                }
            });
    }
    
    public void LoadAdminWeekStartsOn(string childUid, Action<DayOfWeek> onLoaded)
    {
        var dbRef = FirebaseInit.DbRef;

        // Step 1: Get admin UID for this child
        dbRef.Child(AppConstants.Children).Child(childUid).Child(AppConstants.AdminUID)
            .GetValueAsync()
            .ContinueWithOnMainThread(adminTask =>
            {
                if (!adminTask.IsCompletedSuccessfully || !adminTask.Result.Exists)
                {
                    Debug.LogWarning("⚠️ AdminUID not found for this child.");
                    onLoaded?.Invoke(DayOfWeek.Monday); // fallback
                    return;
                }

                string adminUID = adminTask.Result.Value.ToString();

                // Step 2: Get WeekStartsOn from admin settings
                dbRef.Child(AppConstants.Admins).Child(adminUID).Child(AppConstants.Settings).Child(AppConstants.WeekStartsOn)
                    .GetValueAsync()
                    .ContinueWithOnMainThread(weekTask =>
                    {
                        if (!weekTask.IsCompletedSuccessfully || !weekTask.Result.Exists)
                        {
                            Debug.LogWarning("⚠️ WeekStartsOn not found under admin settings.");
                            onLoaded?.Invoke(DayOfWeek.Monday);
                            return;
                        }

                        int value = Convert.ToInt32(weekTask.Result.Value);
                        DayOfWeek startDay = (DayOfWeek)value;
                        Debug.Log($"📥 Loaded WeekStartsOn from admin: {startDay}");

                        onLoaded?.Invoke(startDay);
                    });
            });
    }
}
