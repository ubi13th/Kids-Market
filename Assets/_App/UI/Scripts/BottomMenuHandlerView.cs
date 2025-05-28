using _App.AdminDashboard;
using _App.Dashboard;
using UnityEngine;
using UnityEngine.UI;

namespace _App.UI.Scripts
{
    public class BottomMenuHandlerView : MonoBehaviour
    {
        [Header("Buttons Set Up")]
        [SerializeField] private Button homeButton;
        [SerializeField] private GameObject homeActivatedIcon;
        [SerializeField] private Button reportsButton;
        [SerializeField] private GameObject reportsActivatedIcon;
        [SerializeField] private Button settingsButton;
        [SerializeField] private GameObject settingsActivatedIcon;
        
        [Header("Panels Set Up")]
        [SerializeField] private GameObject reportsPanel;
        [SerializeField] private GameObject settingsPanel;

        private void Start()
        {
            homeButton.onClick.AddListener(OpenHomePanel);
            reportsButton.onClick.AddListener(OpenReportsPanel);
            settingsButton.onClick.AddListener(OpenSettingsPanel);
        }

        private void OpenHomePanel()
        {
            homeActivatedIcon.SetActive(true);
            reportsActivatedIcon.SetActive(false);
            settingsActivatedIcon.SetActive(false);
            reportsPanel.SetActive(false);
            settingsPanel.SetActive(false);
        }
        
        private void OpenReportsPanel()
        {
            reportsActivatedIcon.SetActive(true);
            homeActivatedIcon.SetActive(false);
            settingsActivatedIcon.SetActive(false);
            settingsPanel.SetActive(false);
            reportsPanel.SetActive(true);
        }
        
        private void OpenSettingsPanel()
        {
            reportsPanel.SetActive(false);
            reportsActivatedIcon.SetActive(false);
            homeActivatedIcon.SetActive(false);
            settingsActivatedIcon.SetActive(true);
            settingsPanel.SetActive(true);
        }
    }
}