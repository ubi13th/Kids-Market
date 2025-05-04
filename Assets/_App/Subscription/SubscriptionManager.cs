using UnityEngine;

public static class SubscriptionManager
{
    public static bool IsPremium
    {
        get => PlayerPrefs.GetInt(AppConstants.IsPremium, 0) == 1;
        set => PlayerPrefs.SetInt(AppConstants.IsPremium, value ? 1 : 0);
    }
    
    public static void ActivatePremiumManually()
    {
        IsPremium = true;
        Debug.Log("✨ Premium Activated (Dev)");
    }

    public static void DeactivatePremium()
    {
        IsPremium = false;
        Debug.Log("🔓 Premium Disabled");
    }
}