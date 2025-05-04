using System.Threading.Tasks;
using _App.Bootstrap;
using UnityEngine;

public static class UserSession
{
    public static string CurrentUserId { get; private set; }
    public static bool IsAdmin { get; private set; }

    public static async Task LoadCurrentUser()
    {
        string email = PlayerPrefs.GetString(AppConstants.AdminEmail, null);
        string password = PlayerPrefs.GetString(AppConstants.AdminPassword, null);

        if (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(password))
        {
            try
            {
                var result = await FirebaseInit.Auth.SignInWithEmailAndPasswordAsync(email, password);
                var user = result.User;

                if (user != null)
                {
                    CurrentUserId = user.UserId;
                    IsAdmin = true; // You can enhance this with a Firestore flag later
                    Debug.Log($"👤 Admin signed in: {user.DisplayName ?? "Unnamed"}");
                }
                else
                {
                    Debug.LogWarning("⚠️ Auth succeeded but Admin is null.");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"❌ Admin failed to sign in: {ex.Message}");
            }
        }
        else
        {
            Debug.Log("🔓 No saved credentials, Admin not signed in.");
        }
    }

    public static void ClearSession()
    {
        CurrentUserId = null;
        IsAdmin = false;
        PlayerPrefs.DeleteKey(AppConstants.AdminEmail);
        PlayerPrefs.DeleteKey(AppConstants.AdminPassword);
    }
}