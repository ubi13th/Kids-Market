using System;
using System.Collections;
using System.Collections.Generic;
using _App.AdminDashboard;
using _App.Dashboard;
using _App.Services.BalanceService;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _App.Balance
{
    public class BalanceDashboardView : MonoBehaviour, IDashboardView
    {
        [SerializeField] private JarManagerView jarManagerView;
        [SerializeField] private JarCreatEditController  jarCreatEditController;
        [Header("Reset To Zero")]
        [SerializeField] private Button resetToZeroButton;
        [SerializeField] private GameObject resetConfirmationDialog;
        [SerializeField] private Button yesResetButton;
        [SerializeField] private Button noResetButton;
        
        [Header("Home Panel")]
        [SerializeField] private Button plusButton;
        [SerializeField] private Button minusButton;
        [SerializeField] private Button backButton;
        [SerializeField] private GameObject balanceDashboardPanel;
        [SerializeField] private Button createNewJarButton;
        [SerializeField] private Button addNewJarButton;

        [Header("Adjust Panel")]
        [SerializeField] private GameObject adjustPanel;
        [SerializeField] private Button exitAdjustPanelButton;
        [SerializeField] private TMP_InputField amountInputField;
        [SerializeField] private Button plusAdjustButton;
        [SerializeField] private Button minusAdjustButton;
        [SerializeField] private TextMeshProUGUI debitCreditText;
        [SerializeField] private TextMeshProUGUI beforeAmountText;
        [SerializeField] private TextMeshProUGUI afterAmountText;
        [SerializeField] private TMP_InputField noteInputText;
        [SerializeField] private Button saveButton;

        [Header("Jar Fill")] 
        [SerializeField] private GameObject jarIcon;
        [SerializeField] private Image fillImage;
        [SerializeField] private bool animateFill = true;
        [SerializeField] private float fillSpeed = 1f;
        private Coroutine _fillCoroutine;
        private float _goalAmount;
        private float _currentSavedAmount;

        private string _currentChildUId;
        private float _currentBalance;
        private float _childBalance;
        private float _debitCreditAmount;
        private bool _isDebit;
        private RewardType _currentRewardType;
        
        private IDashboardPresenter  _presenter;
        private IAdminDashboardPresenter  _adminPresenter;
        
        public event Action OnChildInitialized;

        private FirebaseJarService _jarService;

        public void Initialize(IDashboardPresenter presenter)
        {
            // Only set if the presenter supports admin settings
            if (presenter is IAdminDashboardPresenter admin)
                _adminPresenter = admin;
        }

        private void Start()
        {
            backButton.onClick.AddListener(CloseBalancePanel);
            
            resetToZeroButton.onClick.AddListener(OpenResetConfirmationDialog);
            yesResetButton.onClick.AddListener(ConfirmResetBalance);
            noResetButton.onClick.AddListener(() => resetConfirmationDialog.SetActive(false));
            
            plusButton.onClick.AddListener(AdjustPlus);
            minusButton.onClick.AddListener(AdjustMinus);
            exitAdjustPanelButton.onClick.AddListener(CloseAdjustPanel);
            plusAdjustButton.onClick.AddListener(() => AdjustBalance(+1));
            minusAdjustButton.onClick.AddListener(() => AdjustBalance(-1));
            saveButton.onClick.AddListener(SaveBalance);

            createNewJarButton.onClick.AddListener(OnNewJarButtonClicked);
            addNewJarButton.onClick.AddListener(OnNewJarButtonClicked);
            
            amountInputField.onValueChanged.AddListener(OnBalanceAdjustInputChanged);
            amountInputField.onSelect.AddListener(_ => ActivateCaret(amountInputField));
            noteInputText.onSelect.AddListener(_ => ActivateCaret(noteInputText));
            amountInputField.Select();
            amountInputField.ActivateInputField();
            
            _jarService = new FirebaseJarService();
        }
        
        private void ActivateCaret(TMP_InputField input)
        {
            input.ActivateInputField();
            input.caretPosition = input.text.Length;
        }
        
        private void GetCurrentJar()
        {
            _currentChildUId = _presenter?.CurrentChild?.Uid;

            if (string.IsNullOrEmpty(_currentChildUId))
                return;

            _jarService.HasAnyJar(_currentChildUId, exists =>
            {
                jarIcon.SetActive(exists);
            });

            _jarService.GetJars(_currentChildUId, jars =>
            {
                if (jars == null || jars.Count == 0)
                    return;

                _goalAmount = jars[0].GoalAmount;
                _currentSavedAmount = jars[0].SavedAmount;

                UpdateJarFill();
            });
        }


        /*private void GetCurrentJar()
        {
            _currentChildUId = _adminPresenter?.CurrentChild?.Uid ?? null;
            if(_currentChildUId == null)
                return;

            _jarService.HasAnyJar(_currentChildUId, exists =>
            {
                jarIcon.SetActive(exists);
            });
            
            _jarService.GetJars(_currentChildUId, jars =>
            {
                _goalAmount = jars[0].GoalAmount;
                _currentSavedAmount = jars[0].SavedAmount;
                
                UpdateJarFill();
            });
        }*/

        private IEnumerator AnimateJarFill(float targetFill)
        {
            while (!Mathf.Approximately(fillImage.fillAmount, targetFill))
            {
                fillImage.fillAmount = Mathf.MoveTowards(fillImage.fillAmount, targetFill, Time.deltaTime * fillSpeed);
                yield return null;
            }
        }

        private void UpdateJarFill()
        {
            float fill = Mathf.Clamp01(_currentSavedAmount / _goalAmount);
            
            if (animateFill && !Mathf.Approximately(fillImage.fillAmount, fill))
            {
                if (_fillCoroutine != null)
                    StopCoroutine(_fillCoroutine);
                _fillCoroutine = StartCoroutine(AnimateJarFill(fill));
            }
            else
            {
                fillImage.fillAmount = fill;
            }
        }

        public void ReloadJarsAfterSaving()
        {
            jarCreatEditController.OnJarSavedSuccessfully = () =>
            {
                jarManagerView.Initialize(_currentChildUId); // reload jars
                createNewJarButton.gameObject.SetActive(false);
                addNewJarButton.gameObject.SetActive(true);
            };
        }

        public void OnChildSet(ChildModel child)
        {
            if (child == null)
            {
                Debug.LogWarning("❌ OnChildSet called with null child.");
                return;
            }
            
            _currentRewardType = child.RewardPreference;
            plusButton.gameObject.SetActive(_currentRewardType != RewardType.None);
            minusButton.gameObject.SetActive(_currentRewardType != RewardType.None);
            
            //jarManagerView.Initialize(child.Uid, child); // reload jars
            //GetCurrentJar();
            
            _jarService.ListenToJars(child.Uid, updatedJars =>
            {
                //jarManagerView.Render(updatedJars); // or re-Initialize if needed
                jarManagerView.Initialize(child.Uid, child); // reload jars
                _goalAmount = updatedJars[0].GoalAmount;
                _currentSavedAmount = updatedJars[0].SavedAmount;
                UpdateJarFill();
            });

        }

        private void OnNewJarButtonClicked()
        {
            jarCreatEditController.Initialize(_adminPresenter.CurrentChild);
            jarCreatEditController.ShowStep1();
        }
        
        private void AdjustMinus()
        {
            OpenAdjustPanel(-1);
            _isDebit = true;
        }

        private void AdjustPlus()
        {
            OpenAdjustPanel(+1);
            _isDebit = false;
        }

        private void OpenAdjustPanel(int direction)
        {
            _currentBalance = _adminPresenter?.CurrentChild?.Balance ?? 0f;
            _currentChildUId = _adminPresenter?.CurrentChild?.Uid ?? null;

            beforeAmountText.text = CurrentBalanceString();
            afterAmountText.text = AfterBalanceString();
            
            _debitCreditAmount = 0f;
            noteInputText.text = "";
            amountInputField.SetTextWithoutNotify("0");
    
            UpdateBalanceDisplay();

            adjustPanel.SetActive(true);
            debitCreditText.text = direction == -1 ? "DEBIT" : "CREDIT";
        }
        
        private void CloseBalancePanel()
        {
            adjustPanel.SetActive(false);
            balanceDashboardPanel.SetActive(false);
        }
        
        private void CloseAdjustPanel()
        {
            adjustPanel.SetActive(false);
        }
        
        private void AdjustBalance(int direction)
        {
            var step = _currentRewardType == RewardType.Money ? 0.25f : 1f;
            _debitCreditAmount = Mathf.Max(0, _debitCreditAmount + direction * step);
            
            if (_debitCreditAmount <= 0)
                _debitCreditAmount = 0;
            
            UpdateBalanceDisplay();
        }
    
        private void OnBalanceAdjustInputChanged(string input)
        {
            if (!float.TryParse(input, out float parsedValue)) return;

            float step = _currentRewardType == RewardType.Money ? 0.25f : 1f;
            parsedValue = Mathf.Round(parsedValue / step) * step;
            parsedValue = (float)Math.Round(parsedValue, 2); // ✅ Force two decimal places

            _debitCreditAmount = Mathf.Max(0, parsedValue);
            UpdateBalanceDisplay();
        }

        private void UpdateBalanceDisplay()
        {
            beforeAmountText.text = CurrentBalanceString();
            afterAmountText.text = AfterBalanceString();
            if (amountInputField.text != NewBalanceString())
                amountInputField.SetTextWithoutNotify(NewBalanceString());
            
            saveButton.interactable = _debitCreditAmount > 0;
        }

        private string CurrentBalanceString()
        {
            string currentBalanceString = _currentRewardType == RewardType.Money
                ? ($"{_currentBalance:F2}")
                : ($"{_currentBalance}");
            return currentBalanceString;
        }

        private string AfterBalanceString()
        {
            string afterBalanceString = "";

            if (_isDebit)
            {
                afterBalanceString = _currentRewardType == RewardType.Money
                    ? ($"{_currentBalance - _debitCreditAmount:F2}")
                    : ($"{_currentBalance - _debitCreditAmount}");
            }
            else
            {
                afterBalanceString = _currentRewardType == RewardType.Money
                    ? ($"{_currentBalance + _debitCreditAmount:F2}")
                    : ($"{_currentBalance + _debitCreditAmount}");
            }

            return afterBalanceString;
        }

        private string NewBalanceString()
        {
            string newBalanceString = _currentRewardType == RewardType.Money
                ? ($"{_debitCreditAmount:F2}")
                : ($"{_debitCreditAmount}");
            return newBalanceString;
        }
        
        private void OpenResetConfirmationDialog()
        {
            float currentBalance = _adminPresenter?.CurrentChild?.Balance ?? 0f;

            if (Mathf.Approximately(currentBalance, 0f))
            {
                Debug.Log("ℹ️ Balance is already zero.");
                return;
            }

            resetConfirmationDialog.SetActive(true);
        }

        private void ConfirmResetBalance()
        {
            resetConfirmationDialog.SetActive(false);

            if (_adminPresenter == null || _adminPresenter.CurrentChild == null)
            {
                Debug.LogWarning("❌ Cannot reset balance: No admin or current child.");
                return;
            }

            float currentBalance = _adminPresenter.CurrentChild.Balance;
            _adminPresenter.UpdateChildBalance(-currentBalance, "Manual reset to zero", recordHistory: true);

            Debug.Log($"🔄 Balance reset to zero. Deducted {currentBalance}");
        }

        private void SaveBalance()
        {
            if (_adminPresenter == null || _adminPresenter.CurrentChild == null)
            {
                Debug.LogWarning("❌ Cannot save balance: No admin or current child.");
                return;
            }

            float delta = _debitCreditAmount;
            if (debitCreditText.text.ToUpper().Contains("DEBIT"))
                delta = -delta;

            string reason = string.IsNullOrWhiteSpace(noteInputText.text)
                ? (delta >= 0 ? "No notes for Credit" : "No notes for Debit")
                : noteInputText.text;

            _adminPresenter.UpdateChildBalance(delta, reason, recordHistory: true);
            Debug.Log($"💾 Balance saved: {delta} | Reason: {reason}");

            // Reset UI
            _debitCreditAmount = 0f;
            amountInputField.SetTextWithoutNotify("0");
            noteInputText.text = "";
            UpdateBalanceDisplay();
            CloseAdjustPanel();
        }
        
        public void ShowNewProfileCreatorPanelWhenNoUserYet()
        {
            throw new NotImplementedException();
        }

        public void UpdateUIWhenNoContracts(List<SmartContractModel> allContracts)
        {
            throw new NotImplementedException();
        }

        public void ShowChildren(List<ChildModel> children)
        {
            throw new NotImplementedException();
        }

        public void ShowCurrentChild(ChildModel child)
        {
            throw new NotImplementedException();
        }

        public void ShowChildBalance(float balance)
        {
            throw new NotImplementedException();
        }

        public void HighlightDayInCalendar(DateTime selectedDay)
        {
            throw new NotImplementedException();
        }

        public void ShowExtraRewardStatus(string message)
        {
            throw new NotImplementedException();
        }

        public void OpenContractCreator()
        {
            throw new NotImplementedException();
        }

        public void OpenProfileSelector()
        {
            throw new NotImplementedException();
        }

        public void CloseProfileSelector()
        {
            throw new NotImplementedException();
        }

        public void OpenRewardPanel(bool isAdmin)
        {
            throw new NotImplementedException();
        }

        public void OpenAdjustBalancePanel()
        {
            throw new NotImplementedException();
        }

        public void ShowExtraRewardCreator(string childUid, Action onClose, ExtraRewardModel existingReward = null)
        {
            throw new NotImplementedException();
        }

        public void ShowExtraRewardTitle(string rewardTitle)
        {
            throw new NotImplementedException();
        }

        public void ShowExtraRewardProgress(int completed, int total, RewardType type)
        {
            throw new NotImplementedException();
        }

        public void ShowExtraRewardEligible(bool eligible)
        {
            throw new NotImplementedException();
        }

        public void ShowRewardPayout(ExtraRewardModel extraReward)
        {
            throw new NotImplementedException();
        }

        public void UpdateCalendarColors(List<SmartContractModel> allContracts, string childId)
        {
            throw new NotImplementedException();
        }

        public void ShowSelectedDay(DateTime selectedDay)
        {
            throw new NotImplementedException();
        }

        public void OpenEditContractPanel()
        {
            throw new NotImplementedException();
        }

        public void SelectToday()
        {
            throw new NotImplementedException();
        }

        public void ShowGroupedContracts(Dictionary<RepeatType, List<SmartContractModel>> grouped)
        {
            throw new NotImplementedException();
        }

        public void SetupCalendarButtons()
        {
            throw new NotImplementedException();
        }

        public void OnChildSurpriseContractCreate() //SmartContractModel contract = null
        {
            throw new NotImplementedException();
        }

        public void OnChildSurpriseContractEdit(SmartContractModel contract)
        {
            throw new NotImplementedException();
        }

        public void UpdateReports(ChildModel child, List<SmartContractModel> allContracts)
        {
            throw new NotImplementedException();
        }
    }
}