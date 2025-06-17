using System;
using System.Collections.Generic;
using System.Linq;
using _App.Bootstrap;
using _App.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _App.ExtraReward
{
    public class ExtraRewardCreatorView : MonoBehaviour
    {
        [Header("General UI")]
        [SerializeField] private Image icon;
        [SerializeField] private Button openIconPickerButton;
        [SerializeField] private TMP_InputField titleInput;
        [SerializeField] private Button moneyButton;
        [SerializeField] private TextMeshProUGUI moneyButtonText;
        [SerializeField] private Button eventButton;
        [SerializeField] private TextMeshProUGUI eventButtonText;
        [SerializeField] private GameObject enterNameTextGo;
        [SerializeField] private GameObject enterRewardTextGo;
        [SerializeField] private GameObject rewardCreatorPanel;
        [SerializeField] private GameObject infoPanel;
        
        [Header("Reward Amount UI")]
        [SerializeField] private TextMeshProUGUI rewardText;
        [SerializeField] private TMP_InputField rewardInputField;
        [SerializeField] private Button rewardPlusButton;
        [SerializeField] private Button rewardMinusButton;

        [Header("Day Selection UI")]
        [SerializeField] private DaySelectorUI daySelectorUI;
        [SerializeField] private TextMeshProUGUI repeatLabelText;
        
        [Header("Money Reward")]
        [SerializeField] private GameObject moneyPanel;
        [SerializeField] private TMP_InputField moneyAmountInput;

        [Header("Event Reward")]
        [SerializeField] private GameObject eventPanel;
        [SerializeField] private TMP_InputField eventDescriptionInput;

        [Header("Icon Picker")]
        [SerializeField] private ContractIconPickerUI contractIconPickerUI;

        [Header("Control Buttons")]
        [SerializeField] private Button saveButton;
        [SerializeField] private Button cancelButton;
        
        [SerializeField] private Color greenColor, lightGreyColor, greyColor, darkGreyColor;
        
        private string _selectedIconPath = "ContractIconDefault";
        
        private ExtraRewardModel _editingReward;

        private IRewardService _rewardService;
        private string _childUid;
        private Action _onCloseCallback;
        
        private RewardType _currentRewardType = RewardType.Money;
        private float _rewardAmount = 0f;
        
        private bool _isMoney;
        private bool _isAdmin;

        public void Initialize(bool isAdmin, string childUid, IRewardService rewardService, Action onClose, ExtraRewardModel editingReward = null)
        {
            _isAdmin = isAdmin;
            _childUid = childUid;
            _rewardService = rewardService;
            _onCloseCallback = onClose;

            _editingReward = editingReward;
            
            Debug.Log($"editingReward = {editingReward}   _isAdmin = {_isAdmin}");

            PopulateExistingRewardFields(isAdmin, editingReward);
            
            InitializeUI();
        }

        private void OnDisable()
        {
            daySelectorUI.OnSelectionChanged -= UpdateRepeatLabelFromSelectedDays;
            
            saveButton.onClick.RemoveAllListeners();
            cancelButton.onClick.RemoveAllListeners();

            moneyButton.onClick.RemoveAllListeners();
            eventButton.onClick.RemoveAllListeners();
            
            openIconPickerButton.onClick.RemoveAllListeners();
            
            rewardPlusButton.onClick.RemoveAllListeners();
            rewardMinusButton.onClick.RemoveAllListeners();
            rewardInputField.onValueChanged.RemoveAllListeners();
            rewardInputField.onEndEdit.RemoveAllListeners();

            moneyButton.onClick.RemoveAllListeners();
            eventButton.onClick.RemoveAllListeners();
        }

        private void InitializeUI()
        {
            cancelButton.onClick.AddListener(OnCancelClicked);
            daySelectorUI.OnSelectionChanged += UpdateRepeatLabelFromSelectedDays;
            
            if (_isAdmin)
            {
                _isMoney = true;
                
                infoPanel.SetActive(false);
                
                enterNameTextGo.SetActive(true);
                enterRewardTextGo.SetActive(true);
            
                saveButton.gameObject.SetActive(true);
                saveButton.onClick.AddListener(OnSaveClicked);
                
                openIconPickerButton.interactable = true;
                openIconPickerButton.onClick.AddListener(OpenContractIconPicker);
            
                titleInput.interactable = true;
                titleInput.onSelect.AddListener(_ => ActivateCaret(titleInput));
            
                rewardPlusButton.gameObject.SetActive(true);
                rewardPlusButton.onClick.AddListener(() => AdjustReward(+1));
                
                rewardMinusButton.gameObject.SetActive(true);
                rewardMinusButton.onClick.AddListener(() => AdjustReward(-1));
                
                rewardInputField.interactable = true;
                rewardInputField.onValueChanged.AddListener(OnRewardInputChanged);
                rewardInputField.onEndEdit.AddListener(OnRewardInputEndEdit);

                moneyButton.interactable = true;
                moneyButton.onClick.AddListener(() => { RewardMode(true); });
                
                eventButton.interactable = true;
                eventButton.onClick.AddListener(() => { RewardMode(false); });
            }
            else
            {
                enterNameTextGo.SetActive(false);
                enterRewardTextGo.SetActive(false);
                saveButton.gameObject.SetActive(false);
                openIconPickerButton.interactable = false;
                titleInput.interactable = false;
                rewardPlusButton.gameObject.SetActive(false);
                rewardMinusButton.gameObject.SetActive(false);
                rewardInputField.interactable = false;
                moneyButton.interactable = false;
                eventButton.interactable = false;
                
                infoPanel.SetActive(_editingReward == null);
            }
        }

        private void ActivateCaret(TMP_InputField input)
        {
            input.ActivateInputField();
            input.caretPosition = input.text.Length;
        }
        
        private void PopulateExistingRewardFields(bool isAdmin, ExtraRewardModel editingReward)
        {
            if (editingReward != null)
            {
                _selectedIconPath = editingReward?.IconPath ?? "ContractIconDefault";
                icon.sprite = ContractIconLoader.Load(_selectedIconPath);

                titleInput.text = editingReward?.RewardTitle ?? "";
                RewardMode(editingReward?.Type == RewardType.Money);

                if (editingReward?.Type == RewardType.Money)
                    _rewardAmount = editingReward.RewardAmount;
                else if (editingReward?.Type == RewardType.Event)
                    eventDescriptionInput.text = editingReward.EventDescription;
            }
            else
            {
                titleInput.text = "";
                RewardMode(true);
                moneyAmountInput.text = "";
                eventDescriptionInput.text = "";
            }
            
            daySelectorUI.Initialize(isAdmin, editingReward != null ? new HashSet<DayOfWeek>(editingReward.SelectedDays) : null);
            UpdateRewardDisplay();
        }
        
        private void OpenContractIconPicker()
        {
            contractIconPickerUI.OnIconSelected = (iconPath) =>
            {
                _selectedIconPath = iconPath;
                icon.sprite = ContractIconLoader.Load(iconPath);
            };

            contractIconPickerUI.gameObject.SetActive(true);
        }
        
        private void RewardMode(bool isMoney)
        {
            _isMoney = isMoney;

            bool isEvent = !isMoney;

            moneyButton.GetComponent<Image>().color = isMoney ? darkGreyColor : greyColor;
            moneyButtonText.color = isMoney ? greenColor : lightGreyColor;

            eventButton.GetComponent<Image>().color = isEvent ? darkGreyColor : greyColor;
            eventButtonText.color = isEvent ? greenColor : lightGreyColor;
            
            _currentRewardType = isMoney ? RewardType.Money : RewardType.Event;

            UpdateRewardDisplay();
        }

        private void UpdateRepeatLabelFromSelectedDays(HashSet<DayOfWeek> selectedDays)
        {
            if (selectedDays == null || selectedDays.Count == 0)
            {
                repeatLabelText.text = "Select days";
                return;
            }

            var sorted = selectedDays.OrderBy(d => d == DayOfWeek.Sunday ? 7 : (int)d).ToList();
            string label = string.Join(", ", sorted.Select(d => d.ToString().Substring(0, 3)));
            repeatLabelText.text = $"{label}";
        }
        
        private void AdjustReward(int direction)
        {
            float step = _currentRewardType == RewardType.Money ? 0.25f : 1f;
            _rewardAmount = Mathf.Max(0, _rewardAmount + direction * step);
            UpdateRewardDisplay();
        }

        private void OnRewardInputChanged(string input)
        {
            if (float.TryParse(input, out float parsedValue))
                _rewardAmount = Mathf.Max(0, parsedValue);
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
            eventPanel.SetActive(_currentRewardType == RewardType.Event);
            moneyPanel.SetActive(_currentRewardType == RewardType.Money);
            
            string rewardString = _currentRewardType == RewardType.Money
                ? $"{_rewardAmount:F2}"
                : $"{_rewardAmount}";

            rewardText.text = rewardString;

            if (rewardInputField.text != rewardString)
                rewardInputField.SetTextWithoutNotify(rewardString);
        }

        private void OnCancelClicked()
        {
            rewardCreatorPanel.SetActive(false);
            _onCloseCallback?.Invoke();
        }

        private void OnSaveClicked()
        {
            var isEditing = _editingReward != null;

            var reward = isEditing ? _editingReward : new ExtraRewardModel
            {
                Id = Guid.NewGuid().ToString(),
                ChildUid = _childUid,
                AdminUid = FirebaseInit.Auth.CurrentUser?.UserId,
                IsClaimed = false
            };

            reward.RewardTitle = titleInput.text.Trim();
            reward.Type = _currentRewardType;
            reward.RewardAmount = _currentRewardType == RewardType.Money ? _rewardAmount : 0;
            reward.EventDescription = _currentRewardType == RewardType.Event ? eventDescriptionInput.text.Trim() : "";
            reward.SelectedDays = daySelectorUI.SelectedDays.ToList();
            reward.IconPath = _selectedIconPath;

            _rewardService.SaveReward(reward, success =>
            {
                if (success)
                    Debug.Log($"{(isEditing ? "✏️ Edited" : "✅ Created")} Extra Reward: {reward.RewardTitle}");
                else
                    Debug.LogError("❌ Failed to save Extra Reward.");

                rewardCreatorPanel.SetActive(false);
                _onCloseCallback?.Invoke();
            });
        }
    }
}
