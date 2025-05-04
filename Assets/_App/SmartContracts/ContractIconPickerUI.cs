using UnityEngine;
using UnityEngine.UI;

public class ContractIconPickerUI : MonoBehaviour
{
    [SerializeField] private Transform iconGrid;
    [SerializeField] private GameObject iconButtonPrefab;
    [SerializeField] private Button cancelButton;

    public System.Action<string> OnIconSelected;

    private void Start()
    {
        LoadIcons();
        cancelButton.onClick.AddListener(() => gameObject.SetActive(false));
    }

    private void LoadIcons()
    {
        foreach (Transform child in iconGrid)
            Destroy(child.gameObject); // Clean old icons if any

        var icons = ContractIconLoader.LoadAllIcons();
        
        if (icons == null || icons.Length == 0)
        {
            Debug.LogWarning("No contract icons found in Resources.");
            return;
        }

        foreach (var icon in icons)
        {
            GameObject btn = Instantiate(iconButtonPrefab, iconGrid);
            btn.GetComponent<Image>().sprite = icon;

            string iconName = icon.name; // Save the name

            btn.GetComponent<Button>().onClick.AddListener(() =>
            {
                OnIconSelected?.Invoke(iconName);
                //gameObject.SetActive(false);
            });
        }
    }
}