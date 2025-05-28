using _App.AdminDashboard;
using _App.Bootstrap;
using _App.ChildDashboard;
using _App.Dashboard;
using _App.UI.Scripts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _App.Settings
{
    public class EditSelectedUserView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private Image avatarImage;
        [SerializeField] private Button avatarPickButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private GameObject deleteConfirmationPanel;
        [SerializeField] private AvatarPickerUI avatarPickerUI;
        [SerializeField] private AppSettingsView appSettingsView;

        [SerializeField] private Button backButton;
        
        [SerializeField] private Button moneyButton;
        [SerializeField] private Button pointsButton;
        [SerializeField] private Button noneButton;

        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI joinCodeText;

        [SerializeField] private Button deleteButton;
        [SerializeField] private Button confirmDeleteButton;
        [SerializeField] private Button cancelButton;
        
        [SerializeField] private Color greenColor, lightGreyColor, greyColor;

        private IChildService _childService;

        private string _editingChildId;
        private string _avatarPath;
        private RewardType _selectedRewardType;
        
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
            
            statusText.text = "";

            avatarPickButton.onClick.AddListener(OpenAvatarPicker);
            saveButton.onClick.AddListener(SaveChanges);
            deleteButton.onClick.AddListener(OnClickDeleteAccount);
            confirmDeleteButton.onClick.AddListener(OnClickConfirmDeleteAccount);
            cancelButton.onClick.AddListener(OnClickCancelDeleteAccount);
            backButton.onClick.AddListener(CloseEditSelectedUserPanel);

            moneyButton.onClick.AddListener(() => SetRewardType(RewardType.Money));
            pointsButton.onClick.AddListener(() => SetRewardType(RewardType.Points));
            noneButton.onClick.AddListener(() => SetRewardType(RewardType.None));
        }

        public void LoadChildForEdit(ChildModel child)
        {
            _editingChildId = child.Uid;
            nameInput.text = child.DisplayName;
            _avatarPath = child.AvatarPath;
            avatarImage.sprite = AvatarLoader.LoadAvatar(_avatarPath);
            joinCodeText.text = child.JoinCode;
            joinCodeText.gameObject.SetActive(true);

            SetRewardType(child.RewardPreference);

            gameObject.SetActive(true);
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

        private void SaveChanges()
        {
            string name = nameInput.text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                statusText.text = "❌ Name is required.";
                return;
            }

            var updatedChild = new ChildModel
            {
                Uid = _editingChildId,
                DisplayName = name,
                AvatarPath = _avatarPath,
                RewardPreference = _selectedRewardType,
                AdminUID = FirebaseInit.Auth.CurrentUser?.UserId,
                JoinCode = joinCodeText.text
            };

            _childService.SaveChildProfile(updatedChild, success =>
            {
                if (success)
                {
                    statusText.text = "✅ Profile updated!";
                    Debug.Log($"✅ Updated child profile: {name}");
                    CloseEditSelectedUserPanel();
                }
                else
                {
                    statusText.text = "❌ Failed to update.";
                    Debug.LogError("❌ Failed to save child.");
                }
            });
        }
        
        private void OnClickDeleteAccount() => 
            deleteConfirmationPanel.SetActive(true);
        
        private void OnClickCancelDeleteAccount() => 
            deleteConfirmationPanel.SetActive(false);
        
        private void OnClickConfirmDeleteAccount() => 
            DeleteChild();
        
        private void DeleteChild()
        {
            _childService.DeleteChild(_editingChildId, success =>
            {
                if (success)
                {
                    statusText.text = "Deleted.";
                    Debug.Log($"Deleted child {_editingChildId}");

                    _adminPresenter.RefreshChildren(); // only if admin
                    CloseEditSelectedUserPanel();
                }
                else
                {
                    statusText.text = "❌ Failed to delete.";
                    Debug.LogError("❌ Failed to delete child.");
                }
            });
        }

        private void CloseEditSelectedUserPanel()
        {
            deleteConfirmationPanel.SetActive(false);
            gameObject.SetActive(false);
            appSettingsView.OpenFamilyProfilePanel();
        }
    }
}