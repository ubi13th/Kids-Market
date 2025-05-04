using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SmartContractPresetSelectionPanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Transform presetsGrid;
    [SerializeField] private GameObject presetButtonPrefab;
    [SerializeField] private SmartContractCreationStep1 creationStep1Panel;
    [SerializeField] private GameObject iconNamePanel;
    [SerializeField] private Button backButton;

    private List<SmartContractPreset> _presetList;
    
    private void Start()
    {
        LoadPresets();

        if (_presetList == null || _presetList.Count == 0)
        {
            Debug.LogWarning("❌ No smart contract presets found!");
            return;
        }

        PopulatePresetButtons();
    }

    private void ClosePresetPanel()
    {
        gameObject.SetActive(false);
        iconNamePanel.SetActive(true);
    }

    private void LoadPresets()
    {
        _presetList = new List<SmartContractPreset>(Resources.LoadAll<SmartContractPreset>(AppConstants.Presets));
    }

    private void PopulatePresetButtons()
    {
        backButton.onClick.AddListener(ClosePresetPanel);
        
        foreach (Transform child in presetsGrid)
            Destroy(child.gameObject);

        foreach (var preset in _presetList)
        {
            GameObject presetButton = Instantiate(presetButtonPrefab, presetsGrid);
            var buttonScript = presetButton.GetComponent<SmartContractPresetButton>();
            buttonScript.Init(preset, creationStep1Panel);
        }
    }
}