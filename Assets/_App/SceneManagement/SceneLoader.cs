using UnityEngine;

public static class SceneLoader
{
    public static void LoadAppropriateScene()
    {
        if (string.IsNullOrEmpty(UserSession.CurrentUserId))
        {
            Debug.Log("➡️ Loading Login Scene");
            LoadLogInScene();
        }
        else
        {
            LoadHomeScene();
        }
    }

    public static void LoadHomeScene()
    {
        LoadDashboardScene();
        
        // if (!SubscriptionManager.IsPremium)
        //     LoadFreeDashboardScene();
        // else
        //     LoadPremiumDashboardScene();
    }

    public static void LoadLogInScene()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(AppConstants.LoginScene);
    }
    
    public static void LoadChildDashboardScene()
    {
        Debug.Log("➡️ Loading Child Dashboard Scene");
        UnityEngine.SceneManagement.SceneManager.LoadScene(AppConstants.ChildDashboardScene);
    }

    private static void LoadDashboardScene()
    {
        Debug.Log("➡️ Loading Home Dashboard Scene");
        UnityEngine.SceneManagement.SceneManager.LoadScene(AppConstants.DashboardScene);
    }

    private static void LoadPremiumDashboardScene()
    {
        Debug.Log("➡️ Loading Premium Dashboard Scene");
        UnityEngine.SceneManagement.SceneManager.LoadScene(AppConstants.PremiumDashboardScene);
    }

    public static void LoadSubscriptionScene()
    {
        if (SubscriptionManager.IsPremium) return;
        Debug.Log("➡️ Loading Subscription Scene");
        UnityEngine.SceneManagement.SceneManager.LoadScene(AppConstants.SubscriptionScene);
    }
    
    public static void LoadSettingsScene()
    {
        Debug.Log("➡️ Loading Settings Scene");
        UnityEngine.SceneManagement.SceneManager.LoadScene(AppConstants.SettingsScene);
    }
    
    public static void LoadReportsScene()
    {
        Debug.Log("➡️ Loading Reports Scene");
        UnityEngine.SceneManagement.SceneManager.LoadScene(AppConstants.ReportsScene);
    }
}