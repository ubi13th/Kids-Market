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
    }

    public static void LoadLogInScene()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(AppConstants.LoginScene);
    }

    private static void LoadDashboardScene()
    {
        Debug.Log("➡️ Loading Home Dashboard Scene");
        UnityEngine.SceneManagement.SceneManager.LoadScene(AppConstants.DashboardScene);
    }

    public static void LoadSubscriptionScene()
    {
        if (SubscriptionManager.IsPremium) return;
        Debug.Log("➡️ Loading Subscription Scene");
        UnityEngine.SceneManagement.SceneManager.LoadScene(AppConstants.SubscriptionScene);
    }
}