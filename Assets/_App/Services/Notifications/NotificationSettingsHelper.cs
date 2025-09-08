// Assets/_App/Services/Notifications/NotificationSettingsHelper.cs
using System;
using UnityEngine;

namespace _App.Services.Notifications
{
    public static class NotificationSettingsHelper
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// Opens the system screen for this app's specific notification channel (API 26+).
        /// Falls back to the app's notification settings if channel UI is not available.
        /// </summary>
        public static void OpenAndroidChannelSettings(string channelId)
        {
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity    = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var intent      = new AndroidJavaObject(
                    "android.content.Intent",
                    "android.settings.CHANNEL_NOTIFICATION_SETTINGS" // Settings.ACTION_CHANNEL_NOTIFICATION_SETTINGS
                );

                string pkg = activity.Call<string>("getPackageName");

                // extras: Settings.EXTRA_APP_PACKAGE / Settings.EXTRA_CHANNEL_ID
                intent.Call<AndroidJavaObject>("putExtra", "android.provider.extra.APP_PACKAGE", pkg);
                intent.Call<AndroidJavaObject>("putExtra", "android.provider.extra.CHANNEL_ID",   channelId);

                activity.Call("startActivity", intent);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NotificationSettingsHelper] Channel settings open failed: {e.Message}. " +
                                 "Opening app notification settings instead.");
                OpenAndroidAppNotificationSettings();
            }
        }

        /// <summary>
        /// Opens this app's notification settings (works on API 21+).
        /// </summary>
        public static void OpenAndroidAppNotificationSettings()
        {
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity    = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var intent      = new AndroidJavaObject(
                    "android.content.Intent",
                    "android.settings.APP_NOTIFICATION_SETTINGS" // Settings.ACTION_APP_NOTIFICATION_SETTINGS
                );

                string pkg = activity.Call<string>("getPackageName");
                int uid    = activity.Call<AndroidJavaObject>("getApplicationInfo").Get<int>("uid");

                // Newer extras
                intent.Call<AndroidJavaObject>("putExtra", "android.provider.extra.APP_PACKAGE", pkg);
                // Older (Oreo) extras for compatibility
                intent.Call<AndroidJavaObject>("putExtra", "app_package", pkg);
                intent.Call<AndroidJavaObject>("putExtra", "app_uid",     uid);

                activity.Call("startActivity", intent);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NotificationSettingsHelper] App notification settings open failed: {e.Message}");
            }
        }
#else
        // Stubs for non-Android / Editor so calls compile safely.
        public static void OpenAndroidChannelSettings(string channelId) =>
            Debug.Log("[NotificationSettingsHelper] Channel settings not available on this platform.");

        public static void OpenAndroidAppNotificationSettings() =>
            Debug.Log("[NotificationSettingsHelper] App notification settings not available on this platform.");
#endif
    }
}
