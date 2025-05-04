using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using _App.Bootstrap;
using UnityEngine;
using TMPro;
using Firebase.Auth;

public class DeleteAccountHandler : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI messageText;
    public GameObject deleteConfirmationPanel;

    private async void Start() => 
        await FirebaseInit.WaitUntilReady();

    public void OnClickDeleteAccount() => 
        deleteConfirmationPanel.SetActive(true);

    public void CancelDelete() => 
        deleteConfirmationPanel.SetActive(false);

    public void OnClickConfirmDeleteAccount() => 
        _ = HandleAccountDeletionAsync(); // Fire-and-forget wrapper

    private async Task HandleAccountDeletionAsync()
    {
        ShowMessage("Processing account deletion...");

        FirebaseUser user = FirebaseInit.Auth.CurrentUser;

        if (user == null)
        {
            ShowMessage("No user is currently logged in.");
            return;
        }

        string email = PlayerPrefs.GetString(AppConstants.AdminEmail);
        string password = PlayerPrefs.GetString(AppConstants.AdminPassword);

        try
        {
            var credential = EmailAuthProvider.GetCredential(email, password);
            await user.ReauthenticateAsync(credential);

            await DeleteUserDataAsync(user.UserId);
            await user.DeleteAsync();

            PlayerPrefs.DeleteKey(AppConstants.AdminEmail);
            PlayerPrefs.DeleteKey(AppConstants.AdminPassword);
            PlayerPrefs.Save();

            HideConfirmationPanel();

            ShowMessage("✅ Account successfully deleted.");
            
            SceneLoader.LoadLogInScene();
        }
        catch (Exception ex)
        {
            Debug.LogError("❌ Deletion failed: " + ex);
            ShowMessage("Error during deletion. Make sure credentials are correct.");
        }
    }

    private async Task DeleteUserDataAsync(string userId)
    {
        List<Task> deletionTasks = new List<Task>();

        // Delete children assigned to this admin
        var childrenSnapshot = await FirebaseInit.DbRef.Child(AppConstants.Children).GetValueAsync();
        if (childrenSnapshot.Exists)
        {
            foreach (var child in childrenSnapshot.Children)
            {
                string childId = child.Key;
                string adminUID = child.Child(AppConstants.AdminUID).Value?.ToString();

                if (adminUID == userId)
                {
                    var deleteTask = FirebaseInit.DbRef.Child(AppConstants.Children).Child(childId).RemoveValueAsync();
                    deletionTasks.Add(deleteTask);

                    try
                    {
                        await deleteTask;
                        Debug.Log($"🧹 Deleted child: {childId}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"⚠️ Failed to delete child {childId}: {ex}");
                    }
                }
            }
        }

        // Delete admin node
        var adminDeleteTask = FirebaseInit.DbRef.Child(AppConstants.Admins).Child(userId).RemoveValueAsync();
        deletionTasks.Add(adminDeleteTask);

        try
        {
            await adminDeleteTask;
            Debug.Log("✅ Deleted admin node.");
        }
        catch (Exception ex)
        {
            Debug.LogError("❌ Failed to delete admin node: " + ex);
        }

        await Task.WhenAll(deletionTasks);
    }

    private void ShowMessage(string message)
    {
        if (messageText != null)
        {
            messageText.text = message;
        }

        Debug.Log(message);
    }
    
    private void HideConfirmationPanel() =>
        deleteConfirmationPanel.SetActive(false);
}
