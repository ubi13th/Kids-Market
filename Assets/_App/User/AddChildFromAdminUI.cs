using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Firebase.Extensions;
using _App.Bootstrap;

public class AddChildFromAdminUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private Image avatarImage;
    [SerializeField] private Button avatarPickButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private AvatarPickerUI avatarPickerUI;

    [SerializeField] private Button backButton;
    
    [SerializeField] private Button moneyButton;
    [SerializeField] private Button pointsButton;
    [SerializeField] private Button noneButton;

    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI joinCodeText;

    private string _avatarPath = AppConstants.DefaultAvatar;
    private RewardType _selectedRewardType = RewardType.None;

    private void Awake()
    {
        avatarPickButton.onClick.AddListener(OpenAvatarPicker);
        
        saveButton.gameObject.SetActive(true);
        saveButton.onClick.AddListener(SaveNewChild);
        
        backButton.onClick.AddListener(CloseUserCreatorPanel);

        moneyButton.onClick.AddListener(() => SetRewardType(RewardType.Money));
        pointsButton.onClick.AddListener(() => SetRewardType(RewardType.Points));
        noneButton.onClick.AddListener(() => SetRewardType(RewardType.None));

        SetRewardType(RewardType.Money); // Default selection
    }

    private void CloseUserCreatorPanel()
    {
        gameObject.SetActive(false);
    }

    private void OpenAvatarPicker()
    {
        avatarPickerUI.OnAvatarSelected = (avatarPath) =>
        {
            _avatarPath = avatarPath;
            avatarImage.sprite = AvatarLoader.LoadAvatar(avatarPath);
        };
        avatarPickerUI.gameObject.SetActive(true);
    }

    private void SetRewardType(RewardType type)
    {
        _selectedRewardType = type;

        // UI highlight logic (example: change color or interactable state)
        moneyButton.interactable = type != RewardType.Money;
        pointsButton.interactable = type != RewardType.Points;
        noneButton.interactable = type != RewardType.None;
    }

    private void SaveNewChild()
    {
        string name = nameInput.text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            statusText.text = "❌ Name is required.";
            return;
        }

        string newChildUID = Guid.NewGuid().ToString();
        string adminUID = FirebaseInit.Auth.CurrentUser?.UserId;

        if (string.IsNullOrEmpty(adminUID))
        {
            statusText.text = "❌ Admin not signed in.";
            return;
        }

        string joinCode = GenerateJoinCode();

        ChildModel newChild = new ChildModel
        {
            Uid = newChildUID,
            DisplayName = name,
            AvatarPath = _avatarPath,
            AdminUID = adminUID,
            RewardPreference = _selectedRewardType,
            JoinCode = joinCode,
            Balance = 0
        };

        string json = JsonUtility.ToJson(newChild);

        FirebaseInit.DbRef.Child(AppConstants.Children).Child(newChildUID)
            .SetRawJsonValueAsync(json)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsCompletedSuccessfully)
                {
                    Debug.Log($"✅ Child '{name}' saved with JoinCode: {joinCode}");
                    statusText.text = "Child created! Join Code:";
                    joinCodeText.gameObject.SetActive(true);
                    joinCodeText.text = $"{joinCode}";
                    
                    saveButton.gameObject.SetActive(false);
                }
                else
                {
                    Debug.LogError("❌ Failed to save child: " + task.Exception);
                    statusText.text = "❌ Failed to create user.";
                }
            });
    }

    private string GenerateJoinCode(int length = 6)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        System.Random rng = new();
        return new string(new char[length]
            .Select(_ => chars[rng.Next(chars.Length)]).ToArray());
    }
}
