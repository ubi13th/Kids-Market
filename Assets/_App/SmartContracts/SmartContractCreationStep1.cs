using System;
using System.Collections.Generic;
using _App.AdminDashboard;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SmartContractCreationStep1 : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField contractNameInput;
    [SerializeField] private Image iconPreview;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Button openIconPickerButton;
    
    [SerializeField] private GameObject iconNamePanel;
    [SerializeField] private SmartContractCreationStep2 otherSettingsPanel;
    
    [Header("UI Presets")]
    [SerializeField] private Transform defaultPresetsGrid;
    [SerializeField] private Transform customPresetsGrid;
    [SerializeField] private GameObject presetButtonPrefab;

    public bool isCreatingNewContract = false;

    private List<SmartContractPreset> _presetList;
    
    private IAdminDashboardPresenter _presenter;

    [SerializeField] private ContractIconPickerUI contractIconPickerUI;
    
    public static Action OnPresetSaved;

    private string _selectedIconPath;
    private string _selectedDescription;
    private float _defaultReward;

    private void OnEnable()
    {
        isCreatingNewContract = false;
        
        backButton.onClick.AddListener(ExitCreateSmartContract);
        nextButton.onClick.AddListener(ProceedToStep2);
        openIconPickerButton.onClick.AddListener(OpenContractIconPicker);
        
        OnPresetSaved += RefreshPresetList;

        RefreshPresetList();
    }

    private void OnDisable()
    {
        backButton.onClick.RemoveAllListeners();
        nextButton.onClick.RemoveAllListeners();
        openIconPickerButton.onClick.RemoveAllListeners();
        
        OnPresetSaved -= RefreshPresetList;
    }

    public void Initialize(IAdminDashboardPresenter presenter)
    {
        _presenter = presenter;
        otherSettingsPanel.Initialize(presenter);
    }

    private void ExitCreateSmartContract()
    {
        gameObject.SetActive(false);
    }
    
    private void LoadPresets()
    {
        // Load default presets from Resources
        var defaultPresets = Resources.LoadAll<SmartContractPreset>(AppConstants.Presets);

        foreach (var so in defaultPresets)
        {
            GameObject go = Instantiate(presetButtonPrefab, defaultPresetsGrid);
            SmartContractPresetButton btn = go.GetComponent<SmartContractPresetButton>();
            btn.InitFromScriptable(so, this);
        }

        // Load user presets from disk
        var customPresets = PresetStorage.LoadAllPresets();

        foreach (var jsonPreset in customPresets)
        {
            GameObject go = Instantiate(presetButtonPrefab, customPresetsGrid);
            SmartContractPresetButton btn = go.GetComponent<SmartContractPresetButton>();
            btn.InitFromJson(jsonPreset, this);
        }
    }
    
    public void RefreshPresetList()
    {
        LoadPresets();
        PopulateDefaultPresetButtons();
        PopulateCustomPresetButtons();
    }
    
    private void PopulateDefaultPresetButtons()
    {
        if (defaultPresetsGrid == null || presetButtonPrefab == null)
        {
            Debug.LogError("❌ Default presets UI references are missing.");
            return;
        }

        foreach (Transform child in defaultPresetsGrid)
            Destroy(child.gameObject);

        var defaultPresets = Resources.LoadAll<SmartContractPreset>(AppConstants.Presets);
        if (defaultPresets == null)
        {
            Debug.LogWarning("❌ No default presets found in Resources/Presets.");
            return;
        }

        foreach (var so in defaultPresets)
        {
            if (so == null) continue;

            GameObject go = Instantiate(presetButtonPrefab, defaultPresetsGrid);
            var button = go.GetComponent<SmartContractPresetButton>();
            if (button != null)
                button.InitFromScriptable(so, this);
        }
    }
    
    private void PopulateCustomPresetButtons()
    {
        if (customPresetsGrid == null || presetButtonPrefab == null)
        {
            Debug.LogError("❌ Custom presets UI references are missing.");
            return;
        }

        // ✅ Clear previous buttons
        foreach (Transform child in customPresetsGrid)
            Destroy(child.gameObject);

        var customPresets = PresetStorage.LoadAllPresets();
        if (customPresets == null)
        {
            Debug.LogWarning("⚠️ No custom presets found.");
            return;
        }

        foreach (var preset in customPresets)
        {
            if (preset == null) continue;

            GameObject go = Instantiate(presetButtonPrefab, customPresetsGrid);
            var button = go.GetComponent<SmartContractPresetButton>();
            if (button != null)
                button.InitFromJson(preset, this);
        }
    }
    
    private void ApplyCustomPresetValues(
        string title,
        string iconPath,
        float reward,
        DateTime startDate,
        TimeSpan dueTime,
        RepeatType repeatMode,
        List<DayOfWeek> repeatDays,
        bool requiresPhotoProof,
        bool requiresParentalApproval,
        bool notifyMeOnThisDeviceEnabled)
    {
        contractNameInput.text = title;
        _selectedIconPath = iconPath;
        _defaultReward = reward;
        iconPreview.sprite = ContractIconLoader.Load(iconPath);

        // ✅ Load into draft
        SmartContractDraft.Title = title;
        SmartContractDraft.IconPath = iconPath;
        SmartContractDraft.RewardAmount = reward;
        SmartContractDraft.SetStartDate(startDate);
        SmartContractDraft.SetDueTime(dueTime);
        SmartContractDraft.RepeatMode = repeatMode;
        SmartContractDraft.RequiresPhotoProof = requiresPhotoProof;
        SmartContractDraft.RequiresParentalApproval = requiresParentalApproval;
        SmartContractDraft.RequireNotificationOnThisDevice = notifyMeOnThisDeviceEnabled;

        string assignedUid = SmartContractDraft.AssignedToUid;
        if (!string.IsNullOrEmpty(assignedUid)) 
            SmartContractDraft.RepeatDaysPerChild[assignedUid] = new List<DayOfWeek>(repeatDays);
    }
    
    private void ApplyDefaultPresetValues(
        string title,
        string iconPath,
        float reward)
    {
        contractNameInput.text = title;
        _selectedIconPath = iconPath;
        _defaultReward = reward;
        iconPreview.sprite = ContractIconLoader.Load(iconPath);

        // ✅ Load into draft for Step2
        SmartContractDraft.Title = title;
        SmartContractDraft.IconPath = iconPath;
        SmartContractDraft.RewardAmount = reward;
    }
    
    public void OnJsonPresetSelected(SmartContractCustomPreset customPreset)
    {
        ApplyCustomPresetValues(customPreset.title, customPreset.iconPath, customPreset.defaultReward,
            DateTime.Parse(customPreset.startDate), TimeSpan.Parse(customPreset.dueTime), customPreset.repeatMode,
            customPreset.repeatDays, customPreset.requiresPhotoProof, customPreset.requiresParentalApproval, customPreset.requireNotificationOnThisDevice);
        
        ProceedToStep2();
    }

    public void OnScriptablePresetSelected(SmartContractPreset so)
    {
        ApplyDefaultPresetValues(so.title, so.iconPath, so.defaultReward);
        
        ProceedToStep2();
    }

    private void OpenOtherSettingsPanel()
    {
        iconNamePanel.SetActive(false);
        otherSettingsPanel.gameObject.SetActive(true);
    }

    private void OpenContractIconPicker()
    {
        contractIconPickerUI.OnIconSelected = (string iconName) =>
        {
            _selectedIconPath = iconName;
            iconPreview.sprite = ContractIconLoader.Load(iconName);
        };

        contractIconPickerUI.gameObject.SetActive(true);
    }

    private void ProceedToStep2()
    {
        if (string.IsNullOrEmpty(contractNameInput.text))
        {
            Debug.LogWarning("Contract name is empty.");
            return;
        }

        isCreatingNewContract = true;

        SmartContractDraft.Title = contractNameInput.text;
        SmartContractDraft.IconPath = _selectedIconPath;
        SmartContractDraft.RewardAmount = _defaultReward;

        OpenOtherSettingsPanel();
    }
}
