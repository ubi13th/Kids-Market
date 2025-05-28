using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _App.Settings
{
    public class SettingsProfileButton : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Image avatarImage;
        [SerializeField] private GameObject ownerText;

        private string _userId;
        private Action<string> _onClicked;

        public void Initialize(string userId, string displayName, string avatarPath, Action<string> onClick, bool isAdmin)
        {
            _userId = userId;
            nameText.text = displayName;
            avatarImage.sprite = AvatarLoader.LoadAvatar(avatarPath); // ✅ Load sprite
            _onClicked = onClick;
            
            ownerText.SetActive(isAdmin);
        }

        public void OnClick() => _onClicked?.Invoke(_userId);
    }
}