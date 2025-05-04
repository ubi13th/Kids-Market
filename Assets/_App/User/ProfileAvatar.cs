using System.Threading.Tasks;
using _App.Bootstrap;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ProfileAvatar : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image avatarImage;
    [SerializeField] private AvatarPickerUI avatarPickerUI;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Fallback")]
    [SerializeField] private string defaultAvatarIcon = AppConstants.DefaultAvatar;

    private string avatarPath;

    private async void Start()
    {
        await FirebaseInit.WaitUntilReady();
        await LoadAvatarFromDatabase();
        avatarImage.GetComponent<Button>().onClick.AddListener(OpenAvatarPicker);
    }

    private async Task LoadAvatarFromDatabase()
    {
        string userType = UserSession.IsAdmin ? AppConstants.Admins : AppConstants.Children;

        var snapshot = await FirebaseInit.DbRef
            .Child(userType)
            .Child(UserSession.CurrentUserId)
            .Child(AppConstants.AvatarPath)
            .GetValueAsync();

        if (snapshot.Exists && snapshot.Value is string path)
        {
            avatarPath = path;
            LoadAvatarToUI(avatarPath);
        }
        else
        {
            Debug.Log("👤 No avatar found, using default.");
            LoadAvatarToUI(null); // trigger fallback
        }
    }

    private void OpenAvatarPicker()
    {
        avatarPickerUI.OnAvatarSelected = async (string selectedPath) =>
        {
            if (string.IsNullOrEmpty(selectedPath)) return;

            avatarPath = selectedPath;

            string userType = UserSession.IsAdmin ? AppConstants.Admins : AppConstants.Children;

            await FirebaseInit.DbRef
                .Child(userType)
                .Child(UserSession.CurrentUserId)
                .Child(AppConstants.AvatarPath)
                .SetValueAsync(selectedPath);

            Debug.Log($"✅ Avatar updated in DB: {selectedPath}");

            LoadAvatarToUI(selectedPath);

            if (statusText != null)
                statusText.text = "Avatar updated!";
        };

        ShowAvatarPickerUI();
    }

    private void ShowAvatarPickerUI()
    {
        avatarPickerUI.gameObject.SetActive(true);
    }

    private void LoadAvatarToUI(string path)
    {
        Sprite sprite = !string.IsNullOrEmpty(path)
            ? AvatarLoader.LoadAvatar(path)
            : null;

        if (sprite == null)
        {
            sprite = Resources.Load<Sprite>($"Icons/UserAvatars/{defaultAvatarIcon}");
            Debug.LogWarning("⚠️ Avatar not found, fallback used.");
        }

        avatarImage.sprite = sprite;
    }
}