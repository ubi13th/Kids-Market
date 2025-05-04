using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class SmartContractPresetButton : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI rewardText;
    [SerializeField] private Image contractIcon;
    
    private SmartContractPreset _presetData;
    private SmartContractCreationStep1 _creationStepPanel;

    public void Init(SmartContractPreset preset, SmartContractCreationStep1 creationStep)
    {
        if (preset != null)
        {
            if (titleText != null)
                titleText.text = preset.title;
            if (descriptionText != null)
                descriptionText.text = preset.description;
            if (rewardText != null)
                rewardText.text = preset.defaultReward.ToString();
            if (contractIcon != null)
                contractIcon.sprite = ContractIconLoader.Load(preset.iconPath);
        }
        
        _presetData = preset;
        _creationStepPanel = creationStep;
        titleText.text = preset.title;
    }

    public void OnButtonClicked()
    {
        if (_creationStepPanel != null && _presetData != null)
        {
            _creationStepPanel.OnPresetSelected(_presetData);
        }
    }
}