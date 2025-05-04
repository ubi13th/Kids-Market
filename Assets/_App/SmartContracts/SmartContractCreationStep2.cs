using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using _App.Bootstrap;
using Firebase.Extensions;

public class SmartContractCreationStep2 : MonoBehaviour
{
    [Header("UI References")] 
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text contractTitleText;
    [SerializeField] private TMP_Text rewardText;
    [SerializeField] private TMP_InputField rewardInputField;
    [SerializeField] private Button rewardPlusButton;
    [SerializeField] private Button rewardMinusButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private TMP_Dropdown repeatDropdown;
    [SerializeField] private TMP_Text dueTimeText;
    [SerializeField] private Toggle dueTimeToggle;
    [SerializeField] private Toggle photoProofToggle;
    [SerializeField] private Toggle parentalApprovalToggle;
    [SerializeField] private Button deleteButton;
    
    [SerializeField] private GameObject iconNamePanel;
    [SerializeField] private GameObject rewardBlock;
    [SerializeField] private Button backButton;

    private float _childBalance;
    private float _rewardAmount;
    private RewardType _currentRewardType;
    
    private DateTime _dueDateTime;

    private void Awake()
    {
        rewardPlusButton.onClick.AddListener(() => AdjustReward(+1));
        rewardMinusButton.onClick.AddListener(() => AdjustReward(-1));
        saveButton.onClick.AddListener(SaveContract);
        deleteButton.onClick.AddListener(DeleteDraft);
        dueTimeToggle.onValueChanged.AddListener(OnDueTimeToggleChanged);
    }

    private void OnEnable()
    {
        InitializeUI();
    }

    private void InitializeUI()
    {
        backButton.onClick.AddListener(CloseOtherSettingsPanel);
        rewardInputField.onValidateInput += ValidateRewardInput;
        rewardInputField.onValueChanged.AddListener(OnRewardInputChanged);
        
        LoadChildRewardConfig(SmartContractDraft.AssignedToUid);

        icon.sprite = ContractIconLoader.Load(SmartContractDraft.IconPath);
        contractTitleText.text = SmartContractDraft.Title;
        _rewardAmount = SmartContractDraft.RewardAmount;
        UpdateRewardDisplay();

        _dueDateTime = SmartContractDraft.DueDate == default
            ? DateTime.UtcNow.AddDays(1)
            : SmartContractDraft.DueDate;

        dueTimeText.text = _dueDateTime.ToLocalTime().ToString("HH:mm");
        dueTimeToggle.isOn = false; // By default due time reminder off
    }
    
    private void LoadChildRewardConfig(string childUid)
    {
        FirebaseInit.DbRef.Child(AppConstants.Children).Child(childUid).GetValueAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (!task.IsCompletedSuccessfully || !task.Result.Exists)
                {
                    Debug.LogError("❌ Failed to load child reward settings.");
                    return;
                }

                var snapshot = task.Result;
                
                // Parse RewardPreference
                var rewardRaw = snapshot.Child(AppConstants.RewardPreference).Value?.ToString();
                _currentRewardType = Enum.TryParse<RewardType>(rewardRaw, out var parsed) ? parsed : RewardType.None;
                
                // Parse PointsBalance
                var balance = snapshot.Child(AppConstants.Balance).Value?.ToString();
                _childBalance = float.TryParse(balance, out var p) ? p : 0;
                
                _rewardAmount = 0f;
                UpdateRewardDisplay();
            });
    }
    
    private void CloseOtherSettingsPanel()
    {
        gameObject.SetActive(false);
        iconNamePanel.SetActive(true);
    }
    
    private void AdjustReward(int direction)
    {
        float step = _currentRewardType switch
        {
            RewardType.Money => 0.25f,
            RewardType.Points => 1f,
            _ => 0f
        };

        _rewardAmount = Mathf.Max(0, _rewardAmount + direction * step);
        UpdateRewardDisplay();
    }
    
    private void OnRewardInputChanged(string input)
    {
        if (float.TryParse(input, out float parsedValue))
        {
            float step = _currentRewardType == RewardType.Money ? 0.25f : 1f;

            // Snap to valid step increments
            parsedValue = Mathf.Round(parsedValue / step) * step;
            _rewardAmount = Mathf.Max(0, parsedValue);

            UpdateRewardDisplay();
        }
    }
    
    private char ValidateRewardInput(string text, int charIndex, char addedChar)
    {
        // Allow only digits or a dot (.) for money type
        if (char.IsDigit(addedChar))
            return addedChar;

        if (_currentRewardType == RewardType.Money && addedChar == '.' && !text.Contains("."))
            return addedChar;

        return '\0'; // reject character
    }


    private void UpdateRewardDisplay()
    {
        switch (_currentRewardType)
        {
            case RewardType.Money:
                rewardBlock.SetActive(true);
                
                rewardText.text = $"{_childBalance + _rewardAmount:F2}";
                
                if (rewardInputField.text != $"{_childBalance + _rewardAmount:F2}")
                    rewardInputField.SetTextWithoutNotify($"{_childBalance + _rewardAmount:F2}");
                break;
            case RewardType.Points:
                rewardBlock.SetActive(true);
                
                rewardText.text = $"{_childBalance + _rewardAmount}";
                
                if (rewardInputField.text != $"{_childBalance + _rewardAmount}")
                    rewardInputField.SetTextWithoutNotify($"{_childBalance + _rewardAmount}");
                break;
            case RewardType.None:
                rewardBlock.SetActive(false);
                rewardText.text = "";
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        SmartContractDraft.RewardAmount = _rewardAmount;
    }

    private void OnDueTimeToggleChanged(bool isOn)
    {
        dueTimeText.gameObject.SetActive(isOn);
    }

    private void SaveContract()
    {
        var contract = new SmartContractModel
        {
            Id = Guid.NewGuid().ToString(),
            Title = SmartContractDraft.Title,
            Description = SmartContractDraft.Description,
            IconPath = SmartContractDraft.IconPath,
            AssignedToUid = SmartContractDraft.AssignedToUid,
            RewardAmount = _rewardAmount,
            DueDate = dueTimeToggle.isOn ? _dueDateTime.ToUniversalTime().ToString("o") : "", // Save ISO 8601 format
            State = SmartContractState.ReadyToSell,
            RequirePhotoProof = photoProofToggle.isOn,
            RequireParentalApproval = parentalApprovalToggle.isOn
        };

        string json = JsonUtility.ToJson(contract);

        FirebaseInit.DbRef
            .Child(AppConstants.SmartContracts)
            .Child(contract.Id)
            .SetRawJsonValueAsync(json)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsCompletedSuccessfully)
                {
                    Debug.Log($"✅ Contract '{contract.Title}' saved successfully!");
                    SmartContractDraft.Reset();
                    SceneLoader.LoadHomeScene(); // or your home scene
                }
                else
                {
                    Debug.LogError($"❌ Failed to save contract: {task.Exception}");
                }
            });
    }

    private void DeleteDraft()
    {
        SmartContractDraft.Reset();
        SceneLoader.LoadHomeScene(); // or back to previous
    }
}
