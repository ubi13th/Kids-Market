using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Firebase.Extensions;
using _App.Bootstrap;

public class AdminProfileSetupUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private Image avatarImage;
    [SerializeField] private Button avatarButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private AvatarPickerUI avatarPickerUI;
    [SerializeField] private TextMeshProUGUI statusText;

    private string _uid;
    private string _avatarPath;

    private void Awake()
    {
        avatarButton.onClick.AddListener(OpenAvatarPicker);
        continueButton.onClick.AddListener(SaveProfile);
    }

    public void Init(string userId)
    {
        _uid = userId;
        _avatarPath = AppConstants.DefaultAvatar;
        gameObject.SetActive(true);

        SetAvatarPreview(_avatarPath);
    }

    private void OpenAvatarPicker()
    {
        avatarPickerUI.OnAvatarSelected = avatarPath =>
        {
            _avatarPath = string.IsNullOrEmpty(avatarPath) ? AppConstants.DefaultAvatar : avatarPath;
            SetAvatarPreview(_avatarPath);
        };

        avatarPickerUI.gameObject.SetActive(true);
    }

    private void SetAvatarPreview(string avatarPath)
    {
        var sprite = AvatarLoader.LoadAvatar(avatarPath) ?? AvatarLoader.LoadAvatar(AppConstants.DefaultAvatar);
        avatarImage.sprite = sprite;
    }

    private void SaveProfile()
    {
        string name = nameInputField.text.Trim();

        if (string.IsNullOrEmpty(name))
        {
            statusText.text = "❌ Name cannot be empty.";
            return;
        }

        var admin = new AdminModel
        {
            Uid = _uid,
            Email = FirebaseInit.Auth.CurrentUser.Email,
            DisplayName = name,
            AvatarPath = _avatarPath ?? AppConstants.DefaultAvatar,
            JoinCode = GenerateJoinCode(),
            Mode = AppMode.Free
        };

        string json = JsonUtility.ToJson(admin);

        FirebaseInit.DbRef
            .Child(AppConstants.Admins)
            .Child(_uid)
            .SetRawJsonValueAsync(json)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsCompletedSuccessfully)
                {
                    Debug.Log("✅ Admin profile saved.");
                    SceneLoader.LoadHomeScene();
                }
                else
                {
                    Debug.LogError("❌ Failed to save admin profile: " + task.Exception);
                    statusText.text = "Failed to save profile.";
                }
            });
    }

    private string GenerateJoinCode(int length = 6)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var rng = new System.Random();
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[rng.Next(s.Length)]).ToArray());
    }
}