using System;
using _App.Bootstrap;
using _App.Dashboard;
using _App.Services;
using Firebase.Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _App.SmartContracts
{
    public class SurpriseContractCreation : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject panel;
        [SerializeField] private TextMeshProUGUI createEditScText;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_InputField titleInput;
        [SerializeField] private TMP_InputField rewardInput;
        [SerializeField] private Button rewardPlusButton;
        [SerializeField] private Button rewardMinusButton;
        [SerializeField] private Button iconPickerButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button deleteButton;

        [SerializeField] private ContractIconPickerUI iconPickerUI;

        private IContractService _contractService;
        private IDashboardPresenter _presenter;
        //private IChildService _childService; // ✅ Add this

        private string _childUid;
        private float _rewardAmount = 0f;
        private bool _isEditing = false;
        
        private RewardType _currentRewardType;

        public void Initialize(IDashboardPresenter presenter, IContractService contractService)
        {
            _contractService = contractService;
            _childUid = presenter.CurrentChild?.Uid;
            _presenter = presenter;
            //_childService = childService;
            
            if (string.IsNullOrEmpty(_childUid))
                Debug.LogWarning("⚠️ SurpriseContractCreation initialized before CurrentChild was set.");
        }
        
        private void Start()
        {
            iconPickerButton.onClick.AddListener(OpenIconPicker);
            saveButton.onClick.AddListener(SaveContract);
            cancelButton.onClick.AddListener(Hide);
            deleteButton.onClick.AddListener(DeleteContract);
            rewardPlusButton.onClick.AddListener(() => AdjustReward(+1));
            rewardMinusButton.onClick.AddListener(() => AdjustReward(-1));
            
            titleInput.onValueChanged.AddListener(OnTitleChanged);
            titleInput.onSelect.AddListener(_ => ActivateCaret(titleInput));

            // Ensure initial state
            OnTitleChanged(titleInput.text);

            void OnTitleChanged(string text)
            {
                saveButton.interactable = !string.IsNullOrWhiteSpace(text);
            }

            void ActivateCaret(TMP_InputField field)
            {
                field.ActivateInputField();
                field.caretPosition = field.text.Length;
            }
            
            rewardInput.onSelect.AddListener(OnSelectRewardInput);
            rewardInput.onValueChanged.AddListener(OnRewardInputChanged);
            rewardInput.onEndEdit.AddListener(OnRewardInputEndEdit);
            
            SmartContractDraft.StartDate = DateTime.Today;
        }

        public void InitializeUI(SmartContractModel existing = null)
        {
            panel.SetActive(true);
            _isEditing = existing != null;
            
            Debug.Log(_isEditing);
            
            createEditScText.text = _isEditing ? "Create Surprise Contract" : "Edit Surprise Contract";
            
            SmartContractDraft.Reset(_childUid);
            LoadChildRewardConfig(SmartContractDraft.AssignedToUid);
            
            SmartContractDraft.IsSurprise = true;
            SmartContractDraft.RequiresParentalApproval = true;
            SmartContractDraft.RepeatMode = RepeatType.Once;
            SmartContractDraft.SetStartDate(DateTime.Today);

            if (!_isEditing) 
                return;
            SmartContractDraft.LoadFromModel(existing);
            UpdateUIFromDraft();
        }
        
        private void OnSelectRewardInput(string _)
        {
            rewardInput.ActivateInputField();
            rewardInput.caretPosition = rewardInput.text.Length;
        }

        private void Hide()
        {
            SmartContractDraft.Reset();
            panel.SetActive(false);
        }

        private void UpdateUIFromDraft()
        {
            titleInput.text = SmartContractDraft.Title;
            icon.sprite = ContractIconLoader.Load(SmartContractDraft.IconPath);
            _rewardAmount = SmartContractDraft.RewardAmount;
            rewardInput.text = _rewardAmount.ToString("0.##");
        }

        private void OpenIconPicker()
        {
            iconPickerUI.OnIconSelected = path =>
            {
                SmartContractDraft.IconPath = path;
                icon.sprite = ContractIconLoader.Load(path);
            };

            iconPickerUI.gameObject.SetActive(true);
        }
        
        // ----------------- Reward ----------------------
        private void LoadChildRewardConfig(string childUid)
        {
            if (string.IsNullOrEmpty(childUid))
            {
                Debug.LogError("❌ LoadChildRewardConfig: childUid is null or empty");
                return;
            }

            if (FirebaseInit.DbRef == null)
            {
                Debug.LogError("❌ Firebase DbRef is null.");
                return;
            }
            
            FirebaseInit.DbRef.Child(AppConstants.Children).Child(childUid).GetValueAsync()
                .ContinueWithOnMainThread(task =>
                {
                    if (!task.IsCompletedSuccessfully || !task.Result.Exists)
                        return;

                    var snapshot = task.Result;
                    Enum.TryParse(snapshot.Child(AppConstants.RewardPreference).Value?.ToString(), out _currentRewardType);
                    UpdateRewardDisplay();
                });
        }

        private void AdjustReward(int direction)
        {
            float step = _currentRewardType == RewardType.Money ? 0.25f : 1f;
            _rewardAmount = Mathf.Max(0, _rewardAmount + direction * step);
            UpdateRewardDisplay();
        }
        
        // Called every time user types — don't format here
        private void OnRewardInputChanged(string input)
        {
            if (float.TryParse(input, out float parsedValue))
            {
                _rewardAmount = Mathf.Max(0, parsedValue);
                SmartContractDraft.RewardAmount = _rewardAmount;
            }
        }

        private void OnRewardInputEndEdit(string input)
        {
            float step = _currentRewardType == RewardType.Money ? 0.25f : 1f;
            float parsedValue = Mathf.Round(_rewardAmount / step) * step;
            parsedValue = (float)Math.Round(parsedValue, 2);

            _rewardAmount = parsedValue;
            UpdateRewardDisplay();
        }
        
        private void UpdateRewardDisplay()
        {
            string rewardString = _currentRewardType == RewardType.Money
                ? $"{_rewardAmount:F2}"
                : $"{_rewardAmount}";

            rewardInput.SetTextWithoutNotify(rewardString);
            SmartContractDraft.RewardAmount = _rewardAmount;
        }

        private void SaveContract()
        {
            if (string.IsNullOrWhiteSpace(titleInput.text))
            {
                Debug.LogWarning("❌ Title is empty.");
                return;
            }

            if (!float.TryParse(rewardInput.text, out _rewardAmount) || _rewardAmount <= 0)
            {
                Debug.LogWarning("❌ Reward must be a positive number.");
                return;
            }

            SmartContractDraft.Title = titleInput.text;
            SmartContractDraft.RewardAmount = _rewardAmount;

            var contract = SmartContractDraft.ToContractModelFor(_childUid);

            // ✅ Preserve ID when editing
            if (_isEditing && !string.IsNullOrEmpty(SmartContractDraft.Id))
                contract.Id = SmartContractDraft.Id;

            contract.AdminUID = _presenter.CurrentChild?.AdminUID;
            contract.IsSurprise = true;
            contract.RequireParentalApproval = true;
            contract.RepeatMode = RepeatType.Once;
            contract.SetStateOnDate(DateTime.Today, SmartContractState.ReadyToSell);

            _contractService.SaveContract(contract, success =>
            {
                if (success)
                {
                    Debug.Log($"Surprise contract saved with contract.Id = {contract.Id} | AdminUID = {contract.AdminUID}");
                    Hide();
                }
                else
                {
                    Debug.LogError("❌ Failed to save surprise contract.");
                }
            });
        }
        
        private void DeleteContract()
        {
            if (!_isEditing || SmartContractDraft.OriginalModel == null || string.IsNullOrEmpty(SmartContractDraft.OriginalModel.Id))
            {
                Debug.LogWarning("❌ No contract to delete.");
                return;
            }

            _contractService.DeleteContract(SmartContractDraft.OriginalModel.Id, success =>
            {
                if (success)
                {
                    Debug.Log("Surprise contract deleted.");
                    Hide();
                }
                else
                {
                    Debug.LogError("❌ Failed to delete surprise contract.");
                }
            });
        }
    }
}