using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AvatarPickerUI : MonoBehaviour
{
    [SerializeField] private Transform iconGrid;
    [SerializeField] private GameObject avatarButtonPrefab;
    [SerializeField] private Button galleryButton;
    [SerializeField] private Button cancelButton;

    public System.Action<string> OnAvatarSelected;

    private void Start()
    {
        LoadIcons();
        galleryButton.onClick.AddListener(PickFromGallery);
        cancelButton.onClick.AddListener(() => gameObject.SetActive(false));
    }

    private void LoadIcons()
    {
        foreach (Transform child in iconGrid)
            Destroy(child.gameObject); // Clean up old

        Sprite[] icons = Resources.LoadAll<Sprite>("Icons/UserAvatars");

        foreach (Sprite icon in icons)
        {
            GameObject btn = Instantiate(avatarButtonPrefab, iconGrid);
            btn.GetComponent<Image>().sprite = icon;
            string iconName = icon.name;

            btn.GetComponent<Button>().onClick.AddListener(() =>
            {
                OnAvatarSelected?.Invoke(iconName);
                gameObject.SetActive(false);
            });
        }
    }

    private void PickFromGallery()
    {
        GalleryPicker.PickImage((path) =>
        {
            if (!string.IsNullOrEmpty(path))
            {
                OnAvatarSelected?.Invoke(path);
                gameObject.SetActive(false);
            }
        });
    }
}