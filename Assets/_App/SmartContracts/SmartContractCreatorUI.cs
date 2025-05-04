using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Firebase.Extensions;
using _App.Bootstrap;

public class SmartContractCreatorUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_InputField titleInput;
    [SerializeField] private TMP_InputField descriptionInput;
    [SerializeField] private TMP_InputField rewardInput;
    [SerializeField] private Button createButton;
    [SerializeField] private Image iconPreview;
    [SerializeField] private AvatarPickerUI avatarPickerUI;
    [SerializeField] private Button pickIconButton;

    private string _selectedIconPath = AppConstants.DefaultAvatar;
    private ChildModel _targetChild;

    private void Awake()
    {
        createButton.onClick.AddListener(CreateContract);
        pickIconButton.onClick.AddListener(() => avatarPickerUI.gameObject.SetActive(true));
        avatarPickerUI.OnAvatarSelected = path =>
        {
            _selectedIconPath = path;
            iconPreview.sprite = AvatarLoader.LoadAvatar(path);
        };
    }

    public void Init(ChildModel targetChild)
    {
        _targetChild = targetChild;
        _selectedIconPath = AppConstants.DefaultAvatar;
        iconPreview.sprite = AvatarLoader.LoadAvatar(_selectedIconPath);
        ClearFields();
        gameObject.SetActive(true);
    }

    private void ClearFields()
    {
        titleInput.text = "";
        descriptionInput.text = "";
        rewardInput.text = "";
    }

    private void CreateContract()
    {
        if (_targetChild == null)
        {
            Debug.LogError("❌ No child selected.");
            return;
        }

        if (string.IsNullOrWhiteSpace(titleInput.text) || !int.TryParse(rewardInput.text, out int reward))
        {
            Debug.LogWarning("❗ Title and reward are required.");
            return;
        }

        var contract = new SmartContractModel
        {
            Id = Guid.NewGuid().ToString(),
            Title = titleInput.text,
            Description = descriptionInput.text,
            AssignedToUid = _targetChild.Uid,
            RewardAmount = reward,
            IconPath = _selectedIconPath,
            DueDate = DateTime.UtcNow.AddDays(2).ToString("o"),
            State = SmartContractState.ReadyToSell
        };

        string json = JsonUtility.ToJson(contract);

        FirebaseInit.DbRef.Child(AppConstants.SmartContracts)
            .Child(contract.Id)
            .SetRawJsonValueAsync(json)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsCompletedSuccessfully)
                {
                    Debug.Log("✅ Contract created and assigned to " + _targetChild.DisplayName);
                    gameObject.SetActive(false);
                }
                else
                {
                    Debug.LogError("❌ Failed to create contract: " + task.Exception);
                }
            });
    }
}
