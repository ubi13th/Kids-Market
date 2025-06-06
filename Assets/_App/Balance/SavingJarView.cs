using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class SavingJarView : MonoBehaviour
{
    [SerializeField] private Image fillImage;
    [SerializeField] private TextMeshProUGUI jarNameText;
    [SerializeField] private TextMeshProUGUI savedAmountText;
    [SerializeField] private TextMeshProUGUI goalAmountText;
    [SerializeField] private TextMeshProUGUI percentsText;

    [Header("Optional Settings")]
    [SerializeField] private bool animateFill = true;
    [SerializeField] private float fillSpeed = 1f;
    //[SerializeField] private Gradient fillColorByPercent;

    private RewardType _currentRewardType;
    private Coroutine _fillCoroutine;

    public void SetJarUI(RewardType currentRewardType, string jarName, float savedAmount, float goalAmount, int percents)
    {
        _currentRewardType = currentRewardType;
        float fill = Mathf.Clamp01(savedAmount / goalAmount);

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

        //if (fillColorByPercent != null)
            //fillImage.color = fillColorByPercent.Evaluate(fill);

        if (jarNameText != null)
            jarNameText.text = jarName;

        if (savedAmountText != null)
            savedAmountText.text = "Saved: " + NewSavedAmountString(savedAmount);

        if (goalAmountText != null)
            goalAmountText.text = "Goal: " + NewGoalAmountString(goalAmount);

        if (percentsText != null)
            percentsText.text = $"{percents}%";
    }

    private string NewGoalAmountString(float goalAmount)
    {
        goalAmount = Mathf.Max(goalAmount, 1f); // prevent division by zero
        return _currentRewardType == RewardType.Money ? $"{goalAmount:F2}" : $"{goalAmount}";
    }

    private string NewSavedAmountString(float savedAmount)
    {
        return _currentRewardType == RewardType.Money ? $"{savedAmount:F2}" : $"{savedAmount}";
    }

    private IEnumerator AnimateFill(float targetFill)
    {
        while (!Mathf.Approximately(fillImage.fillAmount, targetFill))
        {
            fillImage.fillAmount = Mathf.MoveTowards(fillImage.fillAmount, targetFill, Time.deltaTime * fillSpeed);
            yield return null;
        }
    }
}