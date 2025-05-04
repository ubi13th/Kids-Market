using System;
using _App.Bootstrap;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Firebase.Extensions;

public class ChildProfileSetupUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private Image avatarPreview;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button avatarButton;
    [SerializeField] private AvatarPickerUI avatarPickerUI;
    [SerializeField] private TextMeshProUGUI statusText;

    private RewardType _rewardType;
    private string _adminUID;
    private string _childUID;
    private string _avatarPath;

    private void Awake()
    {
        avatarButton.onClick.AddListener(OpenAvatarPicker);
        saveButton.onClick.AddListener(SaveProfile);
    }

    private void Start()
    {
        _childUID = PlayerPrefs.GetString(AppConstants.ChildUID, "");
        _adminUID = PlayerPrefs.GetString(AppConstants.AdminUID, "");
        
        if (string.IsNullOrEmpty(_childUID))
        {
            Debug.LogError("❌ No child UID found.");
            statusText.text = "User not signed in.";
            return;
        }

        LoadChildProfile();
    }

    private void LoadChildProfile()
    {
        FirebaseInit.DbRef
            .Child(AppConstants.Children)
            .Child(_childUID)
            .GetValueAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsCompletedSuccessfully && task.Result.Exists)
                {
                    var snapshot = task.Result;

                    string name = snapshot.Child(AppConstants.DisplayName).Value?.ToString() ?? "";
                    _avatarPath = snapshot.Child(AppConstants.AvatarPath).Value?.ToString() ?? AppConstants.DefaultAvatar;
                    
                    // Parse RewardPreference
                    var rewardRaw = snapshot.Child(AppConstants.RewardPreference).Value?.ToString();
                    _rewardType = Enum.TryParse<RewardType>(rewardRaw, out var parsed) ? parsed : RewardType.None;

                    nameInput.text = name;
                    SetAvatarPreview(_avatarPath);
                }
                else
                {
                    Debug.LogError("⚠️ Failed to load child profile: " + task.Exception);
                    statusText.text = "Couldn't load your profile.";
                }
            });
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
        avatarPreview.sprite = sprite;
    }

    private void SetRewardTypeMoney()
    {
        _rewardType = RewardType.Money;
    }
    
    private void SetRewardTypePoints()
    {
        _rewardType = RewardType.Points;
    }
    
    private void SetRewardTypeNone()
    {
        _rewardType = RewardType.None;
    }

    private void SaveProfile()
    {
        string newName = nameInput.text.Trim();

        if (string.IsNullOrEmpty(newName))
        {
            statusText.text = "Name cannot be empty.";
            return;
        }

        ChildModel updated = new ChildModel
        {
            Uid = _childUID,
            DisplayName = newName,
            AvatarPath = _avatarPath ?? AppConstants.DefaultAvatar,
            JoinCode = "",
            AdminUID = _adminUID,          // Optional: you can load this if needed
            Balance = 0,     // Optional: update if needed
            RewardPreference = _rewardType
        };

        string json = JsonUtility.ToJson(updated);

        FirebaseInit.DbRef
            .Child(AppConstants.Children)
            .Child(_childUID)
            .SetRawJsonValueAsync(json)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsCompletedSuccessfully)
                {
                    Debug.Log("✅ Child profile updated.");
                    statusText.text = "Profile saved!";
                    SceneLoader.LoadHomeScene(); // Go to dashboard/home
                }
                else
                {
                    Debug.LogError("❌ Failed to save child profile: " + task.Exception);
                    statusText.text = "Failed to save profile.";
                }
            });
    }
}
