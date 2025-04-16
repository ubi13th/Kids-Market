using System;
using System.Collections;
using System.Linq;
using Firebase;
using UnityEngine;
using TMPro;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;

public class FirebaseAuthHandler : MonoBehaviour
{
    private FirebaseAuth auth;
    private DatabaseReference dbRef;
    
    [SerializeField] private TMP_InputField emailInput;
    [SerializeField] private TMP_InputField passwordInput;

    [SerializeField] private TextMeshProUGUI joinCodeText;
    [SerializeField] private GameObject adminSignUpPanel;
    [SerializeField] private GameObject adminLogInPanel;
    [SerializeField] private GameObject adminDashboardPanel;
    [SerializeField] private GameObject childJoinPanel;
    
    private async void Start()
    {
        await FirebaseInit.WaitUntilReady();

        auth = FirebaseInit.Auth;
        dbRef = FirebaseInit.DbRef;

        // Use Firebase safely
    }

    private async void SignInAuto()
    {
        var email = PlayerPrefs.GetString("AdminEmail");
        var password = PlayerPrefs.GetString("AdminPassword");
        
        try
        {
            var result = await auth.SignInWithEmailAndPasswordAsync(email, password);
            FirebaseUser user = result.User;

            if (user != null)
            {
                Debug.Log("UID: " + user.UserId);
                Debug.Log($"Signed in as {user.Email}");
                Debug.Log("Is Anonymous: " + user.IsAnonymous);
                
                ShowAdminDashboardPanel();
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Sign in failed: " + e.Message);
        }
    }
    
    public async void SignInManually()
    {
        var email = emailInput.text;
        var password = passwordInput.text;
        PlayerPrefs.SetString("AdminEmail", email);
        PlayerPrefs.SetString("AdminPassword", password);
        
        try
        {
            var result = await auth.SignInWithEmailAndPasswordAsync(email, password);
            FirebaseUser user = result.User;

            if (user != null)
            {
                Debug.Log("UID: " + user.UserId);
                Debug.Log($"Signed in as {user.Email}");
                Debug.Log("Is Anonymous: " + user.IsAnonymous);
                
                ShowAdminDashboardPanel();
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Sign in failed: " + e.Message);
        }
    }

    public void OnClickAdminLogIn()
    {
        StartCoroutine(LogInOrSignUp());
    }
    
    public void OnClickUserLogIn()
    {
        childJoinPanel.SetActive(true);
    }

    private IEnumerator LogInOrSignUp()
    {
        yield return new WaitUntil(() => FirebaseAuth.DefaultInstance.CurrentUser != null || FirebaseApp.DefaultInstance != null);
    
        //auth = FirebaseAuth.DefaultInstance;
        
        //InitializeFirebase();
        
        CheckIfRegistered();
    }

    private void CheckIfRegistered()
    {
        FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
        
        if (user != null)
        {
            Debug.Log("Welcome back!");
            
            if (PlayerPrefs.HasKey("AdminPassword"))
            {
                SignInAuto();
            }
            else
            {
                ShowAdminSignInPanel();
            }
        }
        else
        {
            ShowAdminSignUpPanel();
            Debug.Log("Please log in or sign up.");
        }
    }

    private void ShowAdminSignUpPanel()
    {
        adminSignUpPanel.SetActive(true);
    }
    
    private void ShowAdminSignInPanel()
    {
        adminLogInPanel.SetActive(true);
    }
    
    private void ShowAdminDashboardPanel()
    {
        adminDashboardPanel.SetActive(true);
    }

    public async void SignUp(string email, string password, string displayName)
    {
        try
        {
            var result = await auth.CreateUserWithEmailAndPasswordAsync(email, password);
            FirebaseUser newUser = result.User;

            if (newUser != null)
            {
                // Set display name
                var profile = new UserProfile { DisplayName = displayName };
                await newUser.UpdateUserProfileAsync(profile);

                Debug.Log($"SignUp successful! Welcome, {newUser.DisplayName}");

                string joinCode = GenerateJoinCode();
                OnAdminSignedIn(newUser, joinCode);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"SignUp failed: {e.Message}");
            if (e is AggregateException aggEx)
            {
                foreach (var inner in aggEx.InnerExceptions)
                    Debug.LogError($"Inner Exception: {inner.Message}");
            }
        }
    }

    private void OnAdminSignedIn(FirebaseUser user, string joinCode)
    {
        DisplayJoinCode(joinCode);
        SaveAdmin(user.UserId, user.Email, user.DisplayName, joinCode);

        ShowAdminDashboardPanel();
    }

    private void DisplayJoinCode(string joinCode)
    {
        joinCodeText.text = $"Join Code: {joinCode}";
    }

    private void SaveAdmin(string userId, string email, string displayName, string joinCode)
    {
        AdminData admin = new AdminData
        {
            uid = userId,
            email = email,
            displayName = displayName,
            joinCode = joinCode
        };

        string json = JsonUtility.ToJson(admin);
        
        dbRef.Child("admins").Child(userId).SetRawJsonValueAsync(json).ContinueWithOnMainThread(task =>
        {
            if (task.IsCompletedSuccessfully)
                Debug.Log("Admin data saved.");
            else
                Debug.LogError("Failed to save admin: " + task.Exception);
        });
    }

    private string GenerateJoinCode(int length = 6)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        System.Random random = new System.Random();
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    [Serializable]
    public class AdminData
    {
        public string uid;
        public string email;
        public string displayName;
        public string joinCode;
    }
    
    public void QuitApp()
    {
        Application.Quit();
    }
}
