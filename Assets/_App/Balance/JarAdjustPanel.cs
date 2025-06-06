using System;
using System.Collections;
using _App.Models;
using _App.Services.BalanceService;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _App.Balance
{
    public class JarAdjustPanel : MonoBehaviour
    {
        [SerializeField] private Image fillImage;
        [SerializeField] private bool animateFill = true;
        [SerializeField] private float fillSpeed = 1f;
        [SerializeField] private TextMeshProUGUI nameDisplayText;
        [SerializeField] private TextMeshProUGUI savedDisplayText;
        [SerializeField] private TextMeshProUGUI goalAmountDisplayText;
        [SerializeField] private TextMeshProUGUI percentDisplayText;
        
        [SerializeField] private GameObject jarMenuPanel;
        
        [SerializeField] private TMP_InputField fillAmountInput;
        [SerializeField] private TextMeshProUGUI beforeAmountText;
        [SerializeField] private TextMeshProUGUI afterAmountText;
        [SerializeField] private TMP_InputField noteInputText;
        [SerializeField] private TextMeshProUGUI debitCreditText;
        //[SerializeField] private GameObject adjustPanel;
        [SerializeField] private Button exitAdjustPanelButton;
        [SerializeField] private Button creditButton;
        [SerializeField] private Button debitButton;
        [SerializeField] private TextMeshProUGUI creditButtonText;
        [SerializeField] private TextMeshProUGUI debitButtonText;
        [SerializeField] private Button saveAfterAdjustButton;
        
        [SerializeField] private Button adjustPlusButton;
        [SerializeField] private Button adjustMinusButton;
        
        [SerializeField] private Color greenColor, lightGreyColor, greyColor, darkGreyColor;

        private SavingJarModel _currentJar;
        private string _childUid;
        private float _goalAmount;
        private float _debitCreditAmount;
        private float _currentSavedAmount;
        private bool _isDebit;
        
        private Coroutine _fillCoroutine;

        private Action<SavingJarModel> _onAdjusted;

        private FirebaseJarService _jarService;
        private RewardType _currentRewardType;

        private void Start()
        {
            exitAdjustPanelButton.onClick.AddListener(CloseAdjustPanel);
            
            creditButton.onClick.AddListener(AdjustPlus);
            debitButton.onClick.AddListener(AdjustMinus);

            fillAmountInput.onValidateInput += ValidateBalanceAmountInput;
            fillAmountInput.onValueChanged.AddListener(OnBalanceAdjustInputChanged);

            adjustPlusButton.onClick.AddListener(()=> AdjustBalance(+1));
            adjustMinusButton.onClick.AddListener(()=> AdjustBalance(-1));
        }
        
        private void ResetAdjustInputs()
        {
            _debitCreditAmount = 0f;
            noteInputText.text = "";
            fillAmountInput.SetTextWithoutNotify("0");
        }


        public void OpenAdjustJar(SavingJarModel jar, string childUid, Action<SavingJarModel> onAdjusted)
        {
            _currentJar = jar;
            _childUid = childUid;
            _onAdjusted = onAdjusted;

            _currentSavedAmount = jar.SavedAmount;
            _goalAmount = jar.GoalAmount;

            nameDisplayText.text = jar.Name;
            savedDisplayText.text = $"Saved: {jar.SavedAmount}";
            goalAmountDisplayText.text = $"Goal: {jar.GoalAmount}";
            percentDisplayText.text = $"{jar.IncomePercentage * 100}%";
            
            _jarService = new FirebaseJarService();

            OpenAdjustJarPanel();
        }

        private void OpenAdjustJarPanel()
        {
            jarMenuPanel.SetActive(false);
            gameObject.SetActive(true);

            _isDebit = true;

            ResetAdjustInputs();
            
            UpdateJarFill();
            
            beforeAmountText.text = FormatAmount(_currentSavedAmount);
            afterAmountText.text = FormatAmount(_isDebit ? _currentSavedAmount - _debitCreditAmount : _currentSavedAmount + _debitCreditAmount);
            fillAmountInput.SetTextWithoutNotify(FormatAmount(_debitCreditAmount));
    
            UpdateBalanceDisplay();
            
            saveAfterAdjustButton.onClick.RemoveAllListeners();
            saveAfterAdjustButton.onClick.AddListener(()=> SaveAdjustedAmount(_isDebit));;
        }
        
        private void UpdateJarFill()
        {
            float fill = Mathf.Clamp01(_currentSavedAmount / _goalAmount);
            if (animateFill && !Mathf.Approximately(fillImage.fillAmount, fill))
            {
                if (_fillCoroutine != null)
                    StopCoroutine(_fillCoroutine);
                _fillCoroutine = StartCoroutine(AnimateFill(fill));
            }
            else
            {
                fillImage.fillAmount = fill;
            }
        }

        private void CloseAdjustPanel()
        {
            gameObject.SetActive(false);
        }

        private IEnumerator AnimateFill(float targetFill)
        {
            while (!Mathf.Approximately(fillImage.fillAmount, targetFill))
            {
                fillImage.fillAmount = Mathf.MoveTowards(fillImage.fillAmount, targetFill, Time.deltaTime * fillSpeed);
                yield return null;
            }
        }
        
        private void SetAdjustMode(bool isDebit)
        {
            _isDebit = isDebit;

            bool credit = !isDebit;

            debitButton.GetComponent<Image>().color = isDebit ? darkGreyColor : greyColor;
            debitButtonText.color = isDebit ? greenColor : lightGreyColor;

            creditButton.GetComponent<Image>().color = credit ? darkGreyColor : greyColor;
            creditButtonText.color = credit ? greenColor : lightGreyColor;

            debitCreditText.text = isDebit ? "-" : "+";
        }

        private void AdjustMinus() => SetAdjustMode(true);
        private void AdjustPlus() => SetAdjustMode(false);
        
        private string FormatAmount(float amount)
        {
            return _currentRewardType == RewardType.Money
                ? $"{amount:F2}"
                : $"{amount}";
        }

        //--------------------------- adjust jar saved amount ---------------------------

        private void AdjustBalance(int direction)
        {
            var step = _currentRewardType == RewardType.Money ? 0.5f : 1f;
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
    
        private char ValidateBalanceAmountInput(string text, int charIndex, char addedChar)
        {
            return char.IsDigit(addedChar) || (_currentRewardType == RewardType.Money && addedChar == '.' && !text.Contains(".")) ? addedChar : '\0';
        }

        private void UpdateBalanceDisplay()
        {
            beforeAmountText.text = FormatAmount(_currentSavedAmount);
            afterAmountText.text = FormatAmount(_isDebit ? _currentSavedAmount - _debitCreditAmount : _currentSavedAmount + _debitCreditAmount);
            if (fillAmountInput.text != FormatAmount(_debitCreditAmount))
                fillAmountInput.SetTextWithoutNotify(FormatAmount(_debitCreditAmount));
            
            saveAfterAdjustButton.interactable = _debitCreditAmount > 0;
        }

        private void SaveAdjustedAmount(bool isDebit)
        {
            if(isDebit)
                OnClickDebitJarSavedAmount();
            else
                OnClickCreditJarSavedAmount();
        }
        
        private bool TryGetPositiveInputAmount(out float amount)
        {
            return float.TryParse(fillAmountInput.text, out amount) && amount > 0f;
        }
        
        private void OnClickCreditJarSavedAmount()
        {
            if (TryGetPositiveInputAmount(out var amount))
            {
                string reason = noteInputText.text;

                if (noteInputText.text == "")
                    reason = "No Notes";

                _jarService.CreditJar(_childUid, _currentJar.Id, amount, reason, recordHistory: true, success =>
                {
                    if (success)
                    {
                        _currentJar.SavedAmount = (float)Math.Round(_currentJar.SavedAmount + amount, 2);
                        _currentSavedAmount = _currentJar.SavedAmount;
                        _onAdjusted?.Invoke(_currentJar);
                        CloseAdjustPanel();
                    }
                });
            }
        }
        
        private void OnClickDebitJarSavedAmount()
        {
            if (TryGetPositiveInputAmount(out var amount))
            {
                string reason = noteInputText.text;
                
                if (noteInputText.text == "")
                    reason = "No Notes";

                _jarService.DebitJar(_childUid, _currentJar.Id, amount, reason, recordHistory: true, success =>
                {
                    if (success)
                    {
                        _currentJar.SavedAmount = (float)Math.Round(_currentJar.SavedAmount - amount, 2);
                        _currentSavedAmount = _currentJar.SavedAmount;
                        _onAdjusted?.Invoke(_currentJar);
                        CloseAdjustPanel();
                    }
                });
            }
        }
    }
}