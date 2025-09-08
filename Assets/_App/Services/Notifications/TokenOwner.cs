using System;
using _App.Bootstrap;

public static class TokenOwner
{
    /// <summary>Profile UID under which this device should save its FCM token
    /// (child's AssignedToUid, or admin profile UID). Set this once after login.</summary>
    public static string PreferredUid { get; private set; }

    public static event Action<string> OnChanged;

    public static void Set(string profileUid)
    {
        if (PreferredUid == profileUid) return;
        PreferredUid = profileUid;
        OnChanged?.Invoke(PreferredUid);
    }

    /// <summary>Fallback to Auth UID if PreferredUid not set yet.</summary>
    public static string Resolve()
        => !string.IsNullOrEmpty(PreferredUid)
           ? PreferredUid
           : FirebaseInit.Auth?.CurrentUser?.UserId;
}
