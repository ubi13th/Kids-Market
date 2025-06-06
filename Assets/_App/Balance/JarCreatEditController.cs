using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using _App.Models;
using _App.Services.BalanceService;

namespace _App.Balance
{
    public class JarCreatEditController : MonoBehaviour
    {
        [SerializeField] private BalanceDashboardView balanceDashboardView;
        [SerializeField] private JarManagerView jarManagerView;
        [Header("Step 1")]
        [SerializeField] private Button backStep1Button;
        [SerializeField] private GameObject step1Panel;
        [SerializeField] private TMP_InputField step1NameInput;
        [SerializeField] private Button step1NextButton;

        [Header("Step 2")]
        [SerializeField] private TextMeshProUGUI createEditDisplayText;
        [SerializeField] private GameObject jarMenuPanel;
        [SerializeField] private GameObject step2Panel;
        [SerializeField] private TMP_InputField step2NameInput;
        [SerializeField] private TextMeshProUGUI savedDisplayText;
        [SerializeField] private TMP_InputField goalAmountInput;
        [SerializeField] private Button plusGoalAmountButton;
        [SerializeField] private Button minusGoalAmountButton;
        [SerializeField] private TextMeshProUGUI goalAmountDisplayText;
        [SerializeField] private TextMeshProUGUI percentDisplayText;
        [SerializeField] private Slider incomePercentSlider;
        [SerializeField] private TMP_Text percentLabel;
        [SerializeField] private Button saveJarButton;
        [SerializeField] private Button backStep2Button;
        
        [Header("Step 2 Fill Jar Image")]
        [SerializeField] private Image fillImage;
        [SerializeField] private bool animateFill = true;
        [SerializeField] private float fillSpeed = 1f;

        private string _childUid;
        private float _goalAmount;
        private float _currentSavedAmount;
        private FirebaseJarService _jarService;
        private SavingJarModel _currentJar;

        private Coroutine _fillCoroutine;

        private Action<SavingJarModel> _onUpdated;
        public Action OnJarSavedSuccessfully; // optional callback (e.g., to refresh UI)
        
        private RewardType _currentRewardType;

        public void Initialize(ChildModel child)
        {
            _childUid = child.Uid;
            _jarService = new FirebaseJarService();
            _currentRewardType = child.RewardPreference;
        }

        private void Start()
        {
            backStep1Button.onClick.AddListener(HideAllSteps);
            
            step1NextButton.onClick.AddListener(ProceedToStep2);
            saveJarButton.onClick.AddListener(SaveJar);

            incomePercentSlider.onValueChanged.AddListener(UpdateSliderLabel);
            
            plusGoalAmountButton.onClick.AddListener(() => AdjustGoalAmount(+1));
            minusGoalAmountButton.onClick.AddListener(() => AdjustGoalAmount(-1));
            
            goalAmountInput.onValidateInput += ValidateGoalAmountInput;
            goalAmountInput.onValueChanged.AddListener(OnGoalAmountInputChanged);
            
            _jarService = new FirebaseJarService();
        }
        
        public void OpenEditJar(SavingJarModel jar, string childUid, Action<SavingJarModel> onUpdated)
        {
            _childUid = childUid;
            _onUpdated = onUpdated;
            _currentJar = jar;

            _currentSavedAmount = jar.SavedAmount;
            _goalAmount = jar.GoalAmount;
            
            step2NameInput.text = jar.Name;
            savedDisplayText.text = SavedAmountString(jar.SavedAmount);
            goalAmountDisplayText.text = GoalAmountString(jar.GoalAmount);
            goalAmountInput.text = $"{jar.GoalAmount}";
            incomePercentSlider.value = jar.IncomePercentage * 100;
            percentLabel.text = $"{jar.IncomePercentage * 100}%";
            percentDisplayText.text = $"{jar.IncomePercentage * 100}%";

            backStep2Button.onClick.RemoveAllListeners();
            backStep2Button.onClick.AddListener(HideAllSteps);

            _jarService = new FirebaseJarService();

            OpenEditJarPanel();
        }
        
        private string GoalAmountString(float goalAmount)
        {
            goalAmount = Mathf.Max(goalAmount, 1f); // prevent division by zero
            return _currentRewardType == RewardType.Money ? $"Goal: {goalAmount:F2}" : $"Goal: {goalAmount}";
        }
        
        private string SavedAmountString(float savedAmount)
        {
            return _currentRewardType == RewardType.Money ? $"{savedAmount:F2}" : $"{savedAmount}";
        }

        private void ResetForm()
        {
            step1NameInput.text = "";
            step2NameInput.text = "";
            goalAmountInput.text = "0";
            incomePercentSlider.value = 0;
            _currentJar = null;
            _onUpdated = null;
        }

        public void ShowStep1()
        {
            ResetForm();
                
            step1Panel.SetActive(true);
            step2Panel.SetActive(false);
            
            createEditDisplayText.text = "CREATE JAR";
            
            backStep2Button.onClick.RemoveAllListeners();
            backStep2Button.onClick.AddListener(ShowStep1);
        }
        
        private void HideAllSteps()
        {
            ResetForm();
            step1Panel.SetActive(false);
            step2Panel.SetActive(false);
            balanceDashboardView.ReloadJarsAfterSaving();
        }
        
        private void OpenEditJarPanel()
        {
            jarMenuPanel.SetActive(false);
            step2Panel.SetActive(true);
            
            createEditDisplayText.text = "EDIT JAR";
            
            SetFillAmount();
        }

        private void SetFillAmount()
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
        
        private IEnumerator AnimateFill(float targetFill)
        {
            while (!Mathf.Approximately(fillImage.fillAmount, targetFill))
            {
                fillImage.fillAmount = Mathf.MoveTowards(fillImage.fillAmount, targetFill, Time.deltaTime * fillSpeed);
                yield return null;
            }
        }

        private void OpenCreateJarPanel2()
        {
            step2Panel.SetActive(true);
            step1Panel.SetActive(false);

            // Carry over name from step 1
            step2NameInput.text = step1NameInput.text;
        }

        private void ProceedToStep2()
        {
            if (string.IsNullOrWhiteSpace(step1NameInput.text))
            {
                Debug.LogWarning("⚠️ Name is required.");
                return;
            }

            OpenCreateJarPanel2();
        }

        private void UpdateSliderLabel(float value)
        {
            percentLabel.text = $"{value}%";
            percentDisplayText.text = $"{value}%";
        }
        
        private void SetGoalAmount(float value)
        {
            _goalAmount = Mathf.Max(0, value);
            UpdateGoalAmountDisplay();
        }
        
        private void AdjustGoalAmount(int direction)
        {
            var step = _currentRewardType == RewardType.Money ? 0.5f : 1f;
            SetGoalAmount(_goalAmount + direction * step);
        }

        private void OnGoalAmountInputChanged(string input)
        {
            if (!float.TryParse(input, out float parsedValue)) return;

            float step = _currentRewardType == RewardType.Money ? 0.5f : 1f;
            parsedValue = Mathf.Round(parsedValue / step) * step;
            parsedValue = (float)Math.Round(parsedValue, 2);

            SetGoalAmount(parsedValue);
        }
    
        private char ValidateGoalAmountInput(string text, int charIndex, char addedChar)
        {
            return char.IsDigit(addedChar) || (_currentRewardType == RewardType.Money && addedChar == '.' && !text.Contains(".")) ? addedChar : '\0';
        }

        private void UpdateGoalAmountDisplay()
        {
            if (goalAmountInput.text != $"{_goalAmount}")
                goalAmountInput.SetTextWithoutNotify($"{_goalAmount}");
            
            if (goalAmountDisplayText.text != GoalAmountString(_goalAmount))
                goalAmountDisplayText.text = GoalAmountString(_goalAmount);
            
            saveJarButton.interactable = _goalAmount > 0;
        }
        
        private SavingJarModel BuildJarModelFromUI()
        {
            float.TryParse(goalAmountInput.text, out var goalAmount);
            float percent = incomePercentSlider.value / 100f;

            return new SavingJarModel
            {
                Id = _currentJar?.Id,
                Name = step2NameInput.text.Trim(),
                GoalAmount = goalAmount,
                IncomePercentage = percent,
                SavedAmount = _currentJar?.SavedAmount ?? 0f,
                History = _currentJar?.History ?? new List<JarHistoryEntry>()
            };
        }
        
        private bool IsValidJarInput(string name, float goalAmount)
        {
            if (string.IsNullOrEmpty(name))
            {
                Debug.LogWarning("⚠️ Name is required.");
                return false;
            }

            if (goalAmount <= 0)
            {
                Debug.LogWarning("⚠️ Goal amount must be greater than 0.");
                return false;
            }

            return true;
        }
        
        private void SaveJar()
        {
            string name = step2NameInput.text.Trim();
            float.TryParse(goalAmountInput.text, out var goalAmount);

            if (!IsValidJarInput(name, goalAmount)) 
                return;
            
            var newJar = BuildJarModelFromUI();

            _jarService.SaveOrUpdateJar(_childUid, newJar, success =>
            {
                if (success)
                {
                    Debug.Log($"✅ Jar '{name}' saved.");
                    OnJarSavedSuccessfully?.Invoke();
                    _onUpdated?.Invoke(newJar);
                    HideAllSteps();
                    jarManagerView.UpdateJarVisual(newJar);
                }
                else
                {
                    Debug.LogError("❌ Failed to save jar.");
                }
            });
        }
    }
}
