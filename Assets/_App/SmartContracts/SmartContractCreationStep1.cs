using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SmartContractCreationStep1 : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField contractNameInput;
    //[SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Image iconPreview;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Button openIconPickerButton;
    
    [SerializeField] private Button presetsButton;
    [SerializeField] private GameObject iconNamePanel;
    [SerializeField] private GameObject presetsPanel;
    [SerializeField] private GameObject otherSettingsPanel;

    [SerializeField] private ContractIconPickerUI contractIconPickerUI;

    private string _selectedIconPath;
    private string _selectedDescription;
    private float _defaultReward;

    private void Awake()
    {
        backButton.onClick.AddListener(ExitCreateSmartContract);
        nextButton.onClick.AddListener(ProceedToStep2);
        openIconPickerButton.onClick.AddListener(OpenContractIconPicker);
        presetsButton.onClick.AddListener(OpenPresetsPanel);
    }

    private void ExitCreateSmartContract()
    {
        gameObject.SetActive(false);
    }

    private void OpenPresetsPanel()
    {
        iconNamePanel.SetActive(false);
        presetsPanel.SetActive(true);
    }
    
    private void ClosePresetsPanel()
    {
        iconNamePanel.SetActive(true);
        presetsPanel.SetActive(false);
    }

    private void OpenOtherSettingsPanel()
    {
        iconNamePanel.SetActive(false);
        presetsPanel.SetActive(false);
        otherSettingsPanel.SetActive(true);
    }

    public void OnPresetSelected(SmartContractPreset preset)
    {
        if (preset == null)
        {
            Debug.LogError("❌ No preset provided.");
            return;
        }

        contractNameInput.text = preset.title;
        _selectedIconPath = preset.iconPath;
        _selectedDescription = preset.description;
        _defaultReward = preset.defaultReward;

        //descriptionText.text = _selectedDescription ?? "";
        iconPreview.sprite = ContractIconLoader.Load(_selectedIconPath);

        ClosePresetsPanel();
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

        SmartContractDraft.Title = contractNameInput.text;
        SmartContractDraft.IconPath = _selectedIconPath;
        SmartContractDraft.Description = _selectedDescription;
        SmartContractDraft.RewardAmount = _defaultReward;

        // TODO: Tell SceneManager or UI flow to show Step 2
        Debug.Log("✅ Proceeded to Step 2 setup");

        OpenOtherSettingsPanel();
    }
}
