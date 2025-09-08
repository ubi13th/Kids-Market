// NotifPermission.cs
#if UNITY_ANDROID && !UNITY_EDITOR
using Unity.Notifications.Android;
using UnityEngine;
#endif
using System;
using System.Reflection;

public static class NotifPermission
{
    public static void Ensure()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // POST_NOTIFICATIONS exists only on Android 13+ (API 33)
        if (GetSdkInt() < 33) return;

        try
        {
            // If the new Mobile Notifications API is present, use it
            MethodInfo req = typeof(AndroidNotificationCenter).GetMethod(
                "RequestPermission", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);

            if (req != null)
            {
                var status = AndroidNotificationCenter.UserPermissionToPost;
                if (status == PermissionStatus.Denied || status == PermissionStatus.NotRequested)
                    req.Invoke(null, null);
                return;
            }

            // Fallback for older packages: request Android permission directly
            const string POST = "android.permission.POST_NOTIFICATIONS";
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(POST))
                UnityEngine.Android.Permission.RequestUserPermission(POST);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[NotifPermission] Request failed: " + e.Message);
        }
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static int GetSdkInt()
    {
        try
        {
            using var version = new AndroidJavaClass("android.os.Build$VERSION");
            return version.GetStatic<int>("SDK_INT");
        }
        catch { return 0; }
    }
#endif
}