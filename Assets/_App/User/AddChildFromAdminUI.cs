using System;
using System.Linq;
using _App.AdminDashboard;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using _App.Bootstrap;
using _App.ChildDashboard;
using _App.Dashboard;

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
    
    [SerializeField] private Color greenColor, lightGreyColor, greyColor;
    
    private string _lastSavedChildUid = null;

    private string _avatarPath = AppConstants.DefaultAvatar;
    private RewardType _selectedRewardType = RewardType.None;
    
    private IChildService _childService;
    
    private IDashboardPresenter  _presenter;
    private IAdminDashboardPresenter  _adminPresenter;
        
    public void Initialize(IDashboardPresenter presenter)
    {
        _presenter = presenter;

        if (presenter is IAdminDashboardPresenter admin)
            _adminPresenter = admin;
    }
    
    private void Awake()
    {
        _childService = new FirebaseChildService();
        
        avatarPickButton.onClick.AddListener(OpenAvatarPicker);
        
        saveButton?.gameObject.SetActive(true);
        saveButton?.onClick.AddListener(SaveNewChild);
        
        backButton.onClick.AddListener(CloseUserCreatorPanel);

        moneyButton.onClick.AddListener(() => SetRewardType(RewardType.Money));
        pointsButton.onClick.AddListener(() => SetRewardType(RewardType.Points));
        noneButton.onClick.AddListener(() => SetRewardType(RewardType.None));
        
        nameInput.onSelect.AddListener(_ => ActivateCaret(nameInput));

        SetRewardType(RewardType.Money); // Default selection
    }
    
    private void ActivateCaret(TMP_InputField field)
    {
        field.ActivateInputField();
        field.caretPosition = field.text.Length;
    }

    private void CloseUserCreatorPanel()
    {
        _lastSavedChildUid = null;
        gameObject.SetActive(false);
    }

    private void OpenAvatarPicker()
    {
        saveButton?.gameObject.SetActive(true);
        
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
        
        saveButton?.gameObject.SetActive(true);

        UpdateButtonStyle(moneyButton, type == RewardType.Money);
        UpdateButtonStyle(pointsButton, type == RewardType.Points);
        UpdateButtonStyle(noneButton, type == RewardType.None);
    }

    private void UpdateButtonStyle(Button button, bool isSelected)
    {
        var background = button.GetComponent<Image>();
        var label = button.GetComponentInChildren<TextMeshProUGUI>();

        if (isSelected)
        {
            background.color = lightGreyColor;
            label.color = greenColor;
        }
        else
        {
            background.color = greyColor;
            label.color = lightGreyColor;
        }

        button.interactable = !isSelected;
    }
    
    private void SaveNewChild()
    {
        string childName = nameInput.text.Trim();
        if (string.IsNullOrEmpty(childName))
        {
            statusText.text = "❌ Name is required.";
            return;
        }
    
        string adminUID = FirebaseInit.Auth.CurrentUser?.UserId;
        if (string.IsNullOrEmpty(adminUID))
        {
            statusText.text = "❌ Admin not signed in.";
            return;
        }
    
        // ✅ If we're editing an existing child (based on internal UID tracking)
        if (!string.IsNullOrEmpty(_lastSavedChildUid))
        {
            var existingChild = _adminPresenter?.GetAllChildren()
                .FirstOrDefault(c => c.Uid == _lastSavedChildUid);
    
            if (existingChild != null)
            {
                existingChild.DisplayName = childName;
                existingChild.AvatarPath = _avatarPath;
                existingChild.RewardPreference = _selectedRewardType;
    
                _childService.SaveChildProfile(existingChild, success =>
                {
                    if (success)
                    {
                        Saving(existingChild.DisplayName, existingChild.JoinCode);
                    }
                    else
                    {
                        Debug.LogError("❌ Failed to update child.");
                        statusText.text = "❌ Failed to update user.";
                    }
                });
    
                return;
            }
        }
    
        // ✅ Create a new child
        string newChildUID = Guid.NewGuid().ToString();
        string joinCode = GenerateJoinCode();
    
        ChildModel newChild = new ChildModel
        {
            Uid = newChildUID,
            DisplayName = childName,
            AvatarPath = _avatarPath,
            AdminUID = adminUID,
            RewardPreference = _selectedRewardType,
            JoinCode = joinCode,
            Balance = 0
        };
    
        _adminPresenter?.SetPendingNewChild(newChildUID);
        _childService.AddNewChild(newChild, success =>
        {
            if (success)
            {
                _lastSavedChildUid = newChildUID; // ✅ Track last created UID
                Saving(childName, joinCode);
            }
            else
            {
                Debug.LogError("❌ Failed to save child.");
                statusText.text = "❌ Failed to create user.";
            }
        });
    }


    /*private void SaveNewChild()
    {
        string childName = nameInput.text.Trim();
        if (string.IsNullOrEmpty(childName))
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
            DisplayName = childName,
            AvatarPath = _avatarPath,
            AdminUID = adminUID,
            RewardPreference = _selectedRewardType,
            JoinCode = joinCode,
            Balance = 0
        };
        
        _adminPresenter?.SetPendingNewChild(newChildUID); // ✅ Tell presenter which child to select
        
        _childService.AddNewChild(newChild, success =>
        {
            if (success)
            {
                Saving(childName, joinCode);
            }
            else
            {
                Debug.LogError("❌ Failed to save child.");
                statusText.text = "❌ Failed to create user.";
            }
        });
    }*/

    private void Saving(string childName, string joinCode)
    {
        joinCodeText.text = $"{joinCode}";
        statusText.text = "Child created! Join Code:";
        joinCodeText.gameObject.SetActive(true);
        joinCodeText.text = $"{joinCode}";
        saveButton?.gameObject.SetActive(false);
    }

    private string GenerateJoinCode(int length = 6)
    {
        const string chars = "0123456789"; // ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789
        System.Random rng = new();
        return new string(new char[length]
            .Select(_ => chars[rng.Next(chars.Length)]).ToArray());
    }
}
