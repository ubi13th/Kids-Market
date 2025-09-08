using System;
using System.Collections.Generic;
using _App.Services.Notifications;
using Firebase.Messaging;
using UnityEngine;

#if UNITY_ANDROID
using Unity.Notifications.Android;
#endif

#if UNITY_IOS
using Unity.Notifications.iOS;
#endif

[DisallowMultipleComponent]
public class FcmReceiver : MonoBehaviour
{
    // MUST MATCH the server’s ANDROID_CHANNEL_ID
    public const string AndroidChannelId = "kids_market_default";

    private void Awake()
    {
        // Firebase hooks
        FirebaseMessaging.TokenReceived += OnTokenReceived;
        FirebaseMessaging.MessageReceived += OnMessageReceived;

#if UNITY_ANDROID && !UNITY_EDITOR
        // Ask for notif permission on Android 13+
        NotifPermission.Ensure();

        // Register channel with HIGH importance for banners/pop-ups
        var channel = new AndroidNotificationChannel
        {
            Id = AndroidChannelId,
            Name = "Kids Market Alerts",
            Description = "Task updates and approvals",
            Importance = Importance.High, // banners enabled
            EnableVibration = true,
            CanShowBadge = true,
            LockScreenVisibility = LockScreenVisibility.Public,
        };
        AndroidNotificationCenter.RegisterNotificationChannel(channel);
#elif UNITY_IOS
        // Ask for alert/badge/sound on first run
        iOSNotificationCenter.RequestAuthorization(
            AuthorizationOption.Alert | AuthorizationOption.Badge | AuthorizationOption.Sound, true);
#endif
    }

    private void OnDestroy()
    {
        FirebaseMessaging.TokenReceived -= OnTokenReceived;
        FirebaseMessaging.MessageReceived -= OnMessageReceived;
    }

    private void OnTokenReceived(object _, TokenReceivedEventArgs e)
    {
        Debug.Log($"[FcmReceiver] Token: {e.Token}");
        // Your DeviceTokenRegistrar should persist & claim this.
    }

    private void OnMessageReceived(object _, MessageReceivedEventArgs e)
    {
        var msg = e.Message;
        var title = msg.Notification?.Title;
        var body  = msg.Notification?.Body;

        // Fallback to data payload keys if title/body not present
        if (string.IsNullOrEmpty(title)) msg.Data?.TryGetValue("title", out title);
        if (string.IsNullOrEmpty(body))  msg.Data?.TryGetValue("body",  out body);

        Debug.Log($"[FcmReceiver] Message received\n" +
                  $"  Title: {title}\n  Body: {body}\n  Data: {DumpDict(msg.Data)}");

        // When app is foreground, Android won't show a system banner for
        // FCM notification messages — post a LOCAL notification instead.
#if UNITY_ANDROID && !UNITY_EDITOR
        var notif = new AndroidNotification
        {
            Title = string.IsNullOrEmpty(title) ? Application.productName : title,
            Text  = string.IsNullOrEmpty(body)  ? "(no text)" : body,
            SmallIcon = "default",
            LargeIcon = "default",
            FireTime = DateTime.Now
        };
        AndroidNotificationCenter.SendNotification(notif, AndroidChannelId);
#elif UNITY_IOS
        var iosNotif = new iOSNotification
        {
            Identifier = Guid.NewGuid().ToString(),
            Title = string.IsNullOrEmpty(title) ? Application.productName : title,
            Body  = string.IsNullOrEmpty(body)  ? "(no text)" : body,
            ShowInForeground = true,
            ForegroundPresentationOption = (PresentationOption.Alert | PresentationOption.Sound),
            Trigger = new iOSNotificationTimeIntervalTrigger { TimeInterval = new TimeSpan(0), Repeats = false }
        };
        iOSNotificationCenter.ScheduleNotification(iosNotif);
#endif
    }

    private static string DumpDict(IDictionary<string, string> d)
    {
        if (d == null || d.Count == 0) return "{}";
        var parts = new List<string>();
        foreach (var kv in d) parts.Add($"{kv.Key}={kv.Value}");
        return "{ " + string.Join(", ", parts) + " }";
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [ContextMenu("Open Android Channel Settings")]
    private void OpenChannelSettings() =>
        NotificationSettingsHelper.OpenAndroidChannelSettings(AndroidChannelId);
#endif
}








/*
// Assets/_App/Notifications/FcmReceiver.cs
using System;
using System.Collections.Generic;
using Firebase.Messaging;
using UnityEngine;

#if UNITY_ANDROID
using Unity.Notifications.Android;
using _App.Services.Notifications; // <-- helper namespace
#endif

#if UNITY_IOS
using Unity.Notifications.iOS;
#endif

[DisallowMultipleComponent]
public class FcmReceiver : MonoBehaviour
{
    const string AndroidChannelId = "general";

    void Awake()
    {
        // Firebase Messaging hooks
        FirebaseMessaging.TokenReceived += OnTokenReceived;
        FirebaseMessaging.MessageReceived += OnMessageReceived;

#if UNITY_ANDROID && !UNITY_EDITOR
        NotifPermission.Ensure();

        // Ensure a notification channel exists.
        var channel = new AndroidNotificationChannel
        {
            Id = AndroidChannelId,
            Name = "General",
            Importance = Importance.High,
            Description = "General notifications"
        };
        AndroidNotificationCenter.RegisterNotificationChannel(channel);
#elif UNITY_IOS
        // Ask for alert/badge/sound on first run
        iOSNotificationCenter.RequestAuthorization(
            AuthorizationOption.Alert | AuthorizationOption.Badge | AuthorizationOption.Sound, true);
#endif
        
#if UNITY_ANDROID && !UNITY_EDITOR
            
#endif
    }

    void OnDestroy()
    {
        FirebaseMessaging.TokenReceived -= OnTokenReceived;
        FirebaseMessaging.MessageReceived -= OnMessageReceived;
    }

    private void OnTokenReceived(object sender, TokenReceivedEventArgs e)
    {
        Debug.Log($"[FcmReceiver] Token: {e.Token}");
        // DeviceTokenRegistrar will persist it.
    }

    private void OnMessageReceived(object sender, MessageReceivedEventArgs e)
    {
        var msg = e.Message;
        var title = msg.Notification?.Title;
        var body  = msg.Notification?.Body;

        // Fallback to data payload keys if title/body not present
        if (string.IsNullOrEmpty(title)) msg.Data?.TryGetValue("title", out title);
        if (string.IsNullOrEmpty(body))  msg.Data?.TryGetValue("body",  out body);

        Debug.Log($"[FcmReceiver] Message received\n" +
                  $"  Title: {title}\n  Body: {body}\n  Data: {DumpDict(msg.Data)}");

#if UNITY_ANDROID && !UNITY_EDITOR
        var notif = new AndroidNotification
        {
            Title = string.IsNullOrEmpty(title) ? Application.productName : title,
            Text  = string.IsNullOrEmpty(body)  ? "(no text)" : body,
            SmallIcon = "default",
            LargeIcon = "default",
            FireTime = DateTime.Now
        };
        AndroidNotificationCenter.SendNotification(notif, AndroidChannelId);
#elif UNITY_IOS
        var iosNotif = new iOSNotification
        {
            Identifier = Guid.NewGuid().ToString(),
            Title = string.IsNullOrEmpty(title) ? Application.productName : title,
            Body  = string.IsNullOrEmpty(body)  ? "(no text)" : body,
            ShowInForeground = true,
            ForegroundPresentationOption = (PresentationOption.Alert | PresentationOption.Sound),
            Trigger = new iOSNotificationTimeIntervalTrigger { TimeInterval = new TimeSpan(0), Repeats = false }
        };
        iOSNotificationCenter.ScheduleNotification(iosNotif);
#endif
    }

    private static string DumpDict(IDictionary<string, string> d)
    {
        if (d == null || d.Count == 0) return "{}";
        var parts = new List<string>();
        foreach (var kv in d) parts.Add($"{kv.Key}={kv.Value}");
        return "{ " + string.Join(", ", parts) + " }";
    }
}
*/
