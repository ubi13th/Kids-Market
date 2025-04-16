using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using TMPro;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;

public class DeleteAccountHandler : MonoBehaviour
{
    public TextMeshProUGUI messageText;
    public GameObject deleteConfirmationPanel;
    public GameObject adminDashboardPanel;

    private FirebaseAuth auth;
    private DatabaseReference dbRef;
    
    private async void Start()
    {
        await FirebaseInit.WaitUntilReady();

        auth = FirebaseInit.Auth;
        dbRef = FirebaseInit.DbRef;

        // Use Firebase safely
    }
    
    public void OnClickDeleteAccount()
    {
        ShowDeleteConfirmation();
    }

    public void OnClickConfirmDeleteAccount()
    {
        var email = PlayerPrefs.GetString("AdminEmail");
        var password = PlayerPrefs.GetString("AdminPassword");

        FirebaseUser user = auth.CurrentUser;

        if (user == null)
        {
            ShowMessage("No user is currently logged in.");
            return;
        }

        var credential = EmailAuthProvider.GetCredential(email, password);

        user.ReauthenticateAsync(credential).ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled || task.IsFaulted)
            {
                ShowMessage("Reauthentication failed. Check your email/password.");
                Debug.LogError(task.Exception);
                return;
            }

            // ✅ Delete from Realtime Database first
            DeleteUserData(user.UserId, () =>
            {
                // ✅ THEN delete Firebase user
                user.DeleteAsync().ContinueWithOnMainThread(deleteTask =>
                {
                    if (deleteTask.IsCompleted)
                    {
                        HideDeleteConfirmation();
                        ShowMessage("Account successfully deleted.");
                        Debug.Log("User deleted.");
                        
                        HideAdminDashboard();
                    }
                    else
                    {
                        ShowMessage("Failed to delete user.");
                        Debug.LogError(deleteTask.Exception);
                    }
                });
            });
        });
    }

    private void DeleteUserData(string userId, Action onComplete)
    {
        List<Task> deletionTasks = new List<Task>();

        // Delete all children linked to this admin
        var childDeletionTask = dbRef.Child("children").GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCompleted && task.Result.Exists)
            {
                DataSnapshot snapshot = task.Result;

                foreach (var child in snapshot.Children)
                {
                    string childId = child.Key;
                    string adminUID = child.Child("adminUID").Value?.ToString();

                    if (adminUID == userId)
                    {
                        var childDeleteTask = dbRef.Child("children").Child(childId).RemoveValueAsync();
                        deletionTasks.Add(childDeleteTask);

                        childDeleteTask.ContinueWithOnMainThread(ct =>
                        {
                            if (ct.IsCompleted)
                                Debug.Log($"🧹 Deleted child: {childId}");
                            else
                                Debug.LogWarning($"⚠️ Failed to delete child {childId}: {ct.Exception}");
                        });
                    }
                }
            }
        });

        deletionTasks.Add(childDeletionTask);

        // Delete admin node
        var adminDeleteTask = dbRef.Child("admins").Child(userId).RemoveValueAsync();
        deletionTasks.Add(adminDeleteTask);

        adminDeleteTask.ContinueWithOnMainThread(task =>
        {
            if (task.IsCompleted)
                Debug.Log("✅ Deleted from 'admins'");
            else
                Debug.LogError("❌ Failed to delete from 'admins': " + task.Exception);
        });

        // Wait for all deletions to complete before continuing
        Task.WhenAll(deletionTasks).ContinueWithOnMainThread(_ =>
        {
            onComplete?.Invoke();
        });
    }
    
    private void ShowMessage(string message)
    {
        if (messageText != null)
        {
            messageText.text = message;
        }

        Debug.Log(message);
    }
    
    private void ShowDeleteConfirmation()
    {
        deleteConfirmationPanel.SetActive(true);
    }

    private void HideDeleteConfirmation()
    {
        deleteConfirmationPanel.SetActive(false);
    }

    private void HideAdminDashboard()
    {
        adminDashboardPanel.SetActive(false);
    }

    public void CancelDelete()
    {
        deleteConfirmationPanel.SetActive(false);
    }
}