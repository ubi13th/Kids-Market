using UnityEngine;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;
using TMPro;
using System;
using Firebase;

public class ChildJoinHandler : MonoBehaviour
{
    private FirebaseAuth auth;
    private DatabaseReference dbRef;

    [Header("UI References")]
    public TMP_InputField joinCodeInput;
    public TMP_InputField nameInput;
    public TextMeshProUGUI statusText;
    public GameObject mainPagePanel;
    
    private async void Start()
    {
        await FirebaseInit.WaitUntilReady();

        auth = FirebaseInit.Auth;
        dbRef = FirebaseInit.DbRef;
        
        // Use Firebase safely
        
        SignInAnonymously();
    }
    
    private void SignInAnonymously()
    {
        auth.SignInAnonymouslyAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCompleted && !task.IsFaulted)
            {
                Debug.Log("Child signed in anonymously.");
                statusText.text = "Signed in. Enter your name and join code.";

                // 🔄 Try auto login if data exists
                AutoLoginIfPossible();
            }
            else
            {
                Debug.LogError("Anonymous sign-in failed.");
                statusText.text = "Failed to sign in.";
            }
        });
    }
    
    private void AutoLoginIfPossible()
    {
        string savedChildUID = PlayerPrefs.GetString("ChildUserId", "");

        if (string.IsNullOrEmpty(savedChildUID))
            return; // No previous login

        dbRef.Child("children").Child(savedChildUID).GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCompleted && task.Result.Exists)
            {
                Debug.Log("✅ Auto-login successful. Child already joined.");
                statusText.text = "Welcome back!";
                ShowMainPage();
            }
            else
            {
                Debug.Log("No matching child data found for stored UID.");
                // Optional: clear saved UID if data is gone
                PlayerPrefs.DeleteKey("ChildUserId");
            }
        });
    }



    public void OnJoinButtonPressed()
    {
        string childName = nameInput.text.Trim();
        string joinCode = joinCodeInput.text.Trim().ToUpper();

        FindAdminByJoinCode(joinCode, childName);
    }

    private void FindAdminByJoinCode(string joinCode, string childName)
    {
        Debug.Log("Trying to find admin by join code...");
        
        dbRef.Child("admins").GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCompleted && task.Result.Exists)
            {
                foreach (var adminSnapshot in task.Result.Children)
                {
                    string storedJoinCode = adminSnapshot.Child("joinCode").Value?.ToString();
                    if (storedJoinCode == joinCode)
                    {
                        string adminUID = adminSnapshot.Key;
                        SaveChildData(childName, adminUID);
                        return;
                    }
                }

                statusText.text = "Invalid join code.";
            }
            else
            {
                statusText.text = "Could not find admins.";
                Debug.LogError("Error fetching admins: " + task.Exception);
            }
        });
    }

    private void SaveChildData(string name, string adminUID)
    {
        string childUID = auth.CurrentUser.UserId;

        ChildData data = new ChildData
        {
            name = name,
            adminUID = adminUID,
            //createdAt = DateTime.UtcNow.ToString("o") // Optional timestamp
        };

        string json = JsonUtility.ToJson(data);

        dbRef.Child("children").Child(childUID).SetRawJsonValueAsync(json).ContinueWithOnMainThread(task =>
        {
            if (task.IsCompleted)
            {
                statusText.text = "Joined successfully!";
                Debug.Log("Child data saved.");
                
                PlayerPrefs.SetString("ChildUserId", auth.CurrentUser.UserId);
                PlayerPrefs.Save();

                ShowMainPage();
            }
            else
            {
                statusText.text = "Failed to join.";
                Debug.LogError("Error saving child: " + task.Exception);
            }
        });
    }

    private void ShowMainPage()
    {
        mainPagePanel.SetActive(true);
    }

    [Serializable]
    public class ChildData
    {
        public string name;
        public string adminUID;
        //public string createdAt;
    }
}
