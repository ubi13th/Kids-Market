using UnityEngine;

public class AdminChildDashboardPanelController : MonoBehaviour
{
    [SerializeField] private GameObject adminHomePanel;
    [SerializeField] private GameObject childHomePanel;

    private void Start()
    {
        CheckWhoLoggedIn();
    }

    private void CheckWhoLoggedIn()
    {
        var savedChildUid = PlayerPrefs.GetString(AppConstants.ChildUID, "");

        if (string.IsNullOrEmpty(savedChildUid))
        {
            Debug.Log("✅ Auto-login successful. ADMIN");
            adminHomePanel.SetActive(true);
            childHomePanel.SetActive(false);
        }
        else
        {
            Debug.Log("✅ Auto-login successful. CHILD");
            adminHomePanel.SetActive(false);
            childHomePanel.SetActive(true);
        }
    }
}