using UnityEngine;
using TMPro;
using _App.Bootstrap;
using Firebase.Extensions;
using UnityEngine.UI;

public class ChildJoinHandler : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Button joinButton;
    //[SerializeField] private GameObject childNameAndAvatarSetUpPanel;

    private void Start()
    {
        FirebaseInit.WaitUntilReady().ContinueWithOnMainThread(_ =>
        {
            SignInAnonymously();
            joinButton.onClick.AddListener(OnJoinButtonPressed);
            joinCodeInput.onSelect.AddListener(_ => ActivateCaret(joinCodeInput));
        });
    }
    
    private void ActivateCaret(TMP_InputField field)
    {
        field.ActivateInputField();
        field.caretPosition = field.text.Length;
    }

    private void SignInAnonymously()
    {
        FirebaseInit.Auth.SignInAnonymouslyAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCompletedSuccessfully)
            {
                SafeLog("✅ Child signed in anonymously.");
                statusText.text = "Signed in. Enter your name and join code.";
                TryAutoLogin();
            }
            else
            {
                SafeLog("❌ Anonymous sign-in failed: " + task.Exception);
                statusText.text = "Failed to sign in.";
            }
        });
    }
    
    private void TryAutoLogin()
    {
        string savedChildUID = PlayerPrefs.GetString(AppConstants.ChildUID, "");

        SafeLog($"✅ TryAutoLogin Saved Child UID = {savedChildUID}");

        if (string.IsNullOrEmpty(savedChildUID))
            return;

        FirebaseInit.DbRef.Child(AppConstants.Children).Child(savedChildUID).GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCompletedSuccessfully && task.Result.Exists)
            {
                SafeLog("✅ Auto-login successful.");
                SceneLoader.LoadHomeScene();
                //SceneLoader.LoadChildDashboardScene();
            }
            else
            {
                SafeLog("⚠️ No saved child found.");
                PlayerPrefs.DeleteKey(AppConstants.ChildUID);
            }
        });
    }

    private void OnJoinButtonPressed()
    {
        string joinCode = joinCodeInput.text.Trim().ToUpper();

        if (string.IsNullOrEmpty(joinCode))
        {
            statusText.text = "Please enter your join code.";
            return;
        }

        FindChildByJoinCode(joinCode);
    }

    private void FindChildByJoinCode(string joinCode)
    {
        FirebaseInit.DbRef.Child(AppConstants.Children).GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCompletedSuccessfully && task.Result.Exists)
            {
                foreach (var childSnapshot in task.Result.Children)
                {
                    string code = childSnapshot.Child(AppConstants.JoinCode).Value?.ToString();
                    if (code == joinCode)
                    {
                        string childUID = childSnapshot.Key;
                        string adminUID = childSnapshot.Child(AppConstants.AdminUID).Value?.ToString();
                        AssignDeviceToChild(childUID, adminUID);
                        return;
                    }
                }

                statusText.text = "Invalid join code.";
            }
            else
            {
                SafeLog("❌ Error fetching children: " + task.Exception);
                statusText.text = "Could not validate join code.";
            }
        });
    }

    private void AssignDeviceToChild(string childUID, string adminUID)
    {
        if (string.IsNullOrEmpty(childUID))
        {
            statusText.text = "Invalid child UID.";
            return;
        }

        PlayerPrefs.SetString(AppConstants.ChildUID, childUID);
        PlayerPrefs.SetString(AppConstants.AdminUID, adminUID);
        PlayerPrefs.Save();

        SafeLog("✅ Device assigned to child UID: " + childUID);
        SceneLoader.LoadHomeScene();
        //SceneLoader.LoadChildDashboardScene();
    }
    
    private void SafeLog(string message)
    {
        if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            Debug.Log(message);
        else
            Debug.Log("🕓 Delayed log (graphics not ready): " + message);
    }


    /*private void TryAutoLogin()
    {
        string savedChildUID = PlayerPrefs.GetString(AppConstants.ChildUID, "");
        
        Debug.Log($"✅ TryAutoLogin Saved Child UID = {savedChildUID}");

        if (string.IsNullOrEmpty(savedChildUID))
            return;

        FirebaseInit.DbRef.Child(AppConstants.Children).Child(savedChildUID).GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCompletedSuccessfully && task.Result.Exists)
            {
                Debug.Log("✅ Auto-login successful.");
                SceneLoader.LoadChildDashboardScene(); // based on login/session
            }
            else
            {
                Debug.Log("⚠️ No saved child found.");
                PlayerPrefs.DeleteKey(AppConstants.ChildUID);
            }
        });
    }

    private void OnJoinButtonPressed()
    {
        string joinCode = joinCodeInput.text.Trim().ToUpper();

        if (string.IsNullOrEmpty(joinCode))
        {
            statusText.text = "Please enter your join code.";
            return;
        }

        FindAdminByJoinCode(joinCode);
    }

    private void FindAdminByJoinCode(string joinCode)
    {
        FirebaseInit.DbRef.Child(AppConstants.Admins).GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCompletedSuccessfully && task.Result.Exists)
            {
                foreach (var adminSnapshot in task.Result.Children)
                {
                    string code = adminSnapshot.Child(AppConstants.JoinCode).Value?.ToString();
                    if (code == joinCode)
                    {
                        string adminUID = adminSnapshot.Key;
                        SaveChildDataUnderAdmin(adminUID);
                        return;
                    }
                }

                statusText.text = "Invalid join code.";
            }
            else
            {
                Debug.LogError("❌ Error fetching admins: " + task.Exception);
                statusText.text = "Could not validate join code.";
            }
        });
    }

    private void SaveChildDataUnderAdmin(string adminUID)
    {
        string childUID = FirebaseInit.Auth.CurrentUser?.UserId;

        if (string.IsNullOrEmpty(childUID))
        {
            statusText.text = "Not signed in.";
            return;
        }

        var newChild = new ChildModel
        {
            Uid = childUID,
            DisplayName = name,
            AvatarPath = "", // Will be added in profile setup
            JoinCode = "",   // Not needed for child
            AdminUID = adminUID,
            Balance = 0
        };
        
        string json = JsonUtility.ToJson(newChild);

        FirebaseInit.DbRef.Child(AppConstants.Children)
            .Child(childUID)
            .SetRawJsonValueAsync(json)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsCompletedSuccessfully)
                {
                    PlayerPrefs.SetString(AppConstants.ChildUID, childUID);
                    PlayerPrefs.SetString(AppConstants.AdminUID, adminUID);
                    PlayerPrefs.Save();
                    
                    statusText.text = "Joined successfully!";
                    Debug.Log("✅ Child data saved.");

                    // 👉 Move to profile setup UI
                    ShowNameAvatarSetUpPanel();
                }
                else
                {
                    Debug.LogError("❌ Failed to save child: " + task.Exception);
                    statusText.text = "Failed to join.";
                }
            });
    }

    private void ShowNameAvatarSetUpPanel()
    {
        childNameAndAvatarSetUpPanel.SetActive(true);
    }*/
}
