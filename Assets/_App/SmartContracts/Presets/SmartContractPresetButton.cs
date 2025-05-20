using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class SmartContractPresetButton : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI rewardText;
    [SerializeField] private Image contractIcon;
    [SerializeField] private Button editButton;
    [SerializeField] private Button deleteButton;

    private SmartContractCustomPreset _jsonCustomPresetData;
    private SmartContractPreset _presetData;
    private SmartContractCreationStep1 _creationStepPanel;

    // Called for custom (user-created) presets
    public void InitFromJson(SmartContractCustomPreset customPreset, SmartContractCreationStep1 creationStep)
    {
        _jsonCustomPresetData = customPreset;
        _presetData = null;
        _creationStepPanel = creationStep;

        titleText.text = customPreset.title;
        rewardText.text = $"{customPreset.defaultReward}";
        contractIcon.sprite = ContractIconLoader.Load(customPreset.iconPath);

        // 🔧 Clear old listeners to avoid duplicates
        deleteButton.onClick.RemoveAllListeners();
        editButton.onClick.RemoveAllListeners();

        deleteButton.onClick.AddListener(OnDeleteCustomPreset);
        editButton.onClick.AddListener(OnSelectPreset);

        deleteButton.gameObject.SetActive(true);
    }

    // Called for default (ScriptableObject) presets
    public void InitFromScriptable(SmartContractPreset so, SmartContractCreationStep1 creationStep)
    {
        _presetData = so;
        _jsonCustomPresetData = null;
        _creationStepPanel = creationStep;

        titleText.text = so.title;
        rewardText.text = $"{so.defaultReward}";
        contractIcon.sprite = ContractIconLoader.Load(so.iconPath);

        deleteButton.onClick.RemoveAllListeners();
        editButton.onClick.RemoveAllListeners();
        
        editButton.onClick.AddListener(OnSelectPreset);

        deleteButton.gameObject.SetActive(false);
    }

    private void OnSelectPreset()
    {
        if (!_creationStepPanel) return;

        if (_jsonCustomPresetData != null)
        {
            _creationStepPanel.OnJsonPresetSelected(_jsonCustomPresetData);
        }
        else if (_presetData != null)
        {
            _creationStepPanel.OnScriptablePresetSelected(_presetData);
        }
    }

    private void OnDeleteCustomPreset()
    {
        if (_jsonCustomPresetData == null) return;

        Debug.Log($"🗑️ Deleting preset: {_jsonCustomPresetData.title}");
        PresetStorage.DeletePreset(_jsonCustomPresetData.title);
        Destroy(gameObject);

        _creationStepPanel?.RefreshPresetList();
    }
}