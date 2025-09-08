using System;
using System.Threading.Tasks;
using _App.Bootstrap;
using UnityEngine;
using TMPro;
using Firebase.Auth;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class AdminAuthHandler : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI signUpStatusText;

    [Header("Panels")]
    [SerializeField] private GameObject adminSignUpPanel;
    [SerializeField] private GameObject childJoinPanel;
    [SerializeField] private GameObject adminProfileSetupPanel;
    [SerializeField] private GameObject adminOptionsPanel;
    [SerializeField] private GameObject joinHouseholdPanel;
    [SerializeField] private Button joinHouseholdButton;
    [SerializeField] private Button newHouseholdButton;
    [SerializeField] private Button backJoinHouseholdButton;
    [SerializeField] private Button backAdminSignUpPanelButton;
    [SerializeField] private Button backAdminNameAndSignUpPanelButton;
    [SerializeField] private Button backAdminSignUpOptionsPanelButton;

    private string _newlySignedUpUserId;

    private void Start()
    {
        joinHouseholdButton.onClick.RemoveAllListeners();
        joinHouseholdButton.onClick.AddListener(ShowJoinHouseholdPanel);
        
        newHouseholdButton.onClick.RemoveAllListeners();
        newHouseholdButton.onClick.AddListener(ShowAdminSignUpPanel);
        
        backJoinHouseholdButton.onClick.RemoveAllListeners();
        backJoinHouseholdButton.onClick.AddListener(HideJoinHouseholdPanel);
        
        backAdminSignUpPanelButton.onClick.RemoveAllListeners();
        backAdminSignUpPanelButton.onClick.AddListener(HideAdminSignUpPanel);
        
        backAdminNameAndSignUpPanelButton.onClick.RemoveAllListeners();
        backAdminNameAndSignUpPanelButton.onClick.AddListener(HideAdminProfileSetupPanel);
        
        backAdminSignUpOptionsPanelButton.onClick.RemoveAllListeners();
        backAdminSignUpOptionsPanelButton.onClick.AddListener(HideAdminSignUpOptionsPanel);
    }

    public async void OnClickAdminLogIn()
    {
        await FirebaseInit.WaitUntilReady();
        CheckIfRegistered();
    }

    public async void OnClickUserLogIn()
    {
        await FirebaseInit.WaitUntilReady();
        childJoinPanel.SetActive(true);
    }

    private async void SignInAuto()
    {
        await FirebaseInit.WaitUntilReady();

        string email = PlayerPrefs.GetString(AppConstants.AdminEmail);
        string password = PlayerPrefs.GetString(AppConstants.AdminPassword);
            
        await SignIn(email, password);
    }

    private async Task SignIn(string email, string password)
    {
        try
        {
            var result = await FirebaseInit.Auth.SignInWithEmailAndPasswordAsync(email, password);
            FirebaseUser user = result.User;

            if (user != null)
            {
                Debug.Log($"Signed in as {user.Email} (UID: {user.UserId})");
                SceneLoader.LoadAppropriateScene(); // Go to dashboard or profile
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Sign in failed: {e.Message}");
        }
    }

    private bool IsPasswordValid(string password, out string reason)
    {
        reason = "";

        if (string.IsNullOrWhiteSpace(password))
        {
            reason = "Password cannot be empty.";
            return false;
        }

        if (password.Length < 6)
        {
            reason = "Password must be at least 6 characters long.";
            return false;
        }

        return true;
    }

    public async void SignUp(string email, string password)
    {
        if (!IsPasswordValid(password, out var passwordReason))
        {
            signUpStatusText.text = $"❌ {passwordReason}";
            return;
        }

        try
        {
            var result = await FirebaseInit.Auth.CreateUserWithEmailAndPasswordAsync(email, password);
            FirebaseUser newUser = result.User;

            if (newUser != null)
            {
                PlayerPrefs.SetString(AppConstants.AdminEmail, email);
                PlayerPrefs.SetString(AppConstants.AdminPassword, password);
                PlayerPrefs.Save();

                // Store UID and show profile setup panel
                _newlySignedUpUserId = newUser.UserId;
                ShowAdminProfileSetupPanel();
                adminProfileSetupPanel.GetComponent<AdminProfileSetupUI>().Init(_newlySignedUpUserId);
            }
        }
        catch (Exception e)
        {
            signUpStatusText.text = $"SignUp failed: {e.Message}";
            Debug.LogError($"SignUp failed: {e.Message}");
            if (e is AggregateException aggEx)
            {
                foreach (var inner in aggEx.InnerExceptions)
                {
                    signUpStatusText.text = $"Inner Exception: {inner.Message}";
                    Debug.LogError($"Inner Exception: {inner.Message}");
                }
            }
        }
    }

    private void CheckIfRegistered()
    {
        FirebaseUser user = FirebaseInit.Auth.CurrentUser;
        
        //PlayerPrefs.SetString(AppConstants.AdminEmail, "markiyan76@gmail.com");
        //PlayerPrefs.SetString(AppConstants.AdminPassword, "123456");
        //PlayerPrefs.Save();

        if (user != null && PlayerPrefs.HasKey(AppConstants.AdminEmail))
        {
            Debug.Log($"Signed in as {user.Email} (UID: {user.UserId})");
            SignInAuto();
        }
        else
        {
            ShowAdminSignUpOptionsPanel();
        }
    }
    
    private void ShowAdminSignUpOptionsPanel() =>
        adminOptionsPanel.SetActive(true);
    private void HideAdminSignUpOptionsPanel() =>
        adminOptionsPanel.SetActive(false);
    
    private void ShowJoinHouseholdPanel() =>
        joinHouseholdPanel.SetActive(true);
    private void HideJoinHouseholdPanel() =>
        joinHouseholdPanel.SetActive(false);
    
    private void ShowAdminSignUpPanel()
    {
        adminSignUpPanel.SetActive(true);
        signUpStatusText.text = "Please enter your email and create password";
    }
    private void HideAdminSignUpPanel() => 
        adminSignUpPanel.SetActive(false);

    private void ShowAdminProfileSetupPanel() => 
        adminProfileSetupPanel.SetActive(true);
    private void HideAdminProfileSetupPanel() => 
        adminProfileSetupPanel.SetActive(false);

    public void QuitApp() =>
        Application.Quit();
}
