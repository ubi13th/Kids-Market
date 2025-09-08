// Assets/_App/Notifications/NotificationChannels.cs
using System;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using Unity.Notifications.Android;
using UnityEngine.Android;
#endif

public static class NotificationChannels
{
#if UNITY_ANDROID && !UNITY_EDITOR
    // New id so you don't inherit old importance from "general"
    public const string AlertsChannelId = "alerts_v2";

    public static void EnsureAlertsChannel()
    {
        // Nuke/recreate so we’re 100% at HIGH
        try { AndroidNotificationCenter.DeleteNotificationChannel(AlertsChannelId); } catch { }

        var ch = new AndroidNotificationChannel
        {
            Id          = AlertsChannelId,
            Name        = "Alerts",
            Description = "Heads-up banners and pop-ups",
            Importance  = Importance.High,              // <-- heads-up banners
            EnableVibration = true,
            EnableLights    = true,
            CanShowBadge    = true,
            LockScreenVisibility = LockScreenVisibility.Public
        };
        AndroidNotificationCenter.RegisterNotificationChannel(ch);
        Debug.Log("[Notifications] Registered channel '" + AlertsChannelId + "' as HIGH");
    }

    // Optional: open system UI directly to this channel so user can enable banners
    public static void OpenAlertsChannelSettings()
    {
        try
        {
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity    = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            using var intent      = new AndroidJavaObject("android.content.Intent",
                "android.settings.CHANNEL_NOTIFICATION_SETTINGS");

            intent.Call<AndroidJavaObject>("putExtra", "android.provider.extra.APP_PACKAGE",
                activity.Call<string>("getPackageName"));
            intent.Call<AndroidJavaObject>("putExtra", "android.provider.extra.CHANNEL_ID",
                AlertsChannelId);

            activity.Call("startActivity", intent);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Notifications] Failed to open channel settings: " + e.Message);
        }
    }
#endif
}
