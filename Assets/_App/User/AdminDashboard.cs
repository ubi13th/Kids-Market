using System;
using System.Collections.Generic;
using System.Linq;
using _App.Bootstrap;
using UnityEngine;
using TMPro;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;
using UnityEngine.UI;

public class AdminDashboard : MonoBehaviour
{
    [Header("Child Info Display")]
    [SerializeField] private Button currentChildAvatarButton;
    [SerializeField] private Image currentChildAvatarIcon;
    [SerializeField] private TextMeshProUGUI currentChildNameText;
    [SerializeField] private TextMeshProUGUI currentChildPointsText;
    [SerializeField] private Button addNewUserButton;

    [Header("Child Selection")]
    [SerializeField] private GameObject newUserCreationPanel;
    [SerializeField] private GameObject childSelectionPanel;
    [SerializeField] private Transform childSelectionGrid;
    [SerializeField] private GameObject childSelectorItemPrefab;
    [SerializeField] private Button exitChildSelectorPanelButton;

    [Header("Contractss Display")]
    [SerializeField] private Transform contractListContainer;
    [SerializeField] private GameObject contractEntryPrefab;
    [SerializeField] private GameObject contractCreatorUI;
    [SerializeField] private Button addNewContractCreatorButton;
    
    [Header("UI Feedback")]
    [SerializeField] private TextMeshProUGUI childrenListText;

    private Query _childrenQuery;
    private string _adminUID;
    private readonly Dictionary<string, ChildModel> _childData = new();
    private ChildModel _currentChild;

    private async void Start()
    {
        await FirebaseInit.WaitUntilReady();

        SetupListenerForChildren();

        currentChildAvatarButton.onClick.AddListener(ShowChildSelectorPanel);
        exitChildSelectorPanelButton.onClick.AddListener(HideChildSelectorPanel);
        addNewContractCreatorButton.onClick.AddListener(OpenContractCreator);
        addNewUserButton.onClick.AddListener(OpenUserCreatorPanel);
    }

    private void OpenUserCreatorPanel()
    {
        newUserCreationPanel.SetActive(true);
    }
    
    private void OpenContractCreator()
    {
        if (_currentChild != null)
        {
            SmartContractDraft.Reset(); // Always start fresh
            SmartContractDraft.AssignedToUid = _currentChild.Uid; // 👈 assign the selected child UID
            contractCreatorUI.SetActive(true);
        }
        else
        {
            Debug.LogError("_currentChild = null");
        }
    }
    
    private void SetupListenerForChildren()
    {
        FirebaseUser user = FirebaseInit.Auth.CurrentUser;
        if (user == null)
        {
            SetChildrenListText("Not signed in.");
            Debug.LogError("Admin not signed in.");
            return;
        }

        _adminUID = user.UserId;

        if (string.IsNullOrEmpty(_adminUID))
        {
            SetChildrenListText("Invalid admin UID.");
            Debug.LogError("Invalid admin UID.");
            return;
        }

        _childrenQuery = FirebaseInit.DbRef
            .Child(AppConstants.Children)
            .OrderByChild(AppConstants.AdminUID)
            .EqualTo(_adminUID);

        _childrenQuery.ValueChanged += OnChildrenDataChanged;
    }

    private void OnChildrenDataChanged(object sender, ValueChangedEventArgs args)
    {
        if (args.DatabaseError != null)
        {
            SetChildrenListText("Failed to fetch children.");
            Debug.LogError("Firebase error: " + args.DatabaseError.Message);
            return;
        }

        ClearChildSelectorPanel();
        _childData.Clear();

        if (!args.Snapshot.Exists || args.Snapshot.ChildrenCount == 0)
        {
            SetChildrenListText("No children linked to this account.");
            return;
        }

        foreach (var childSnapshot in args.Snapshot.Children)
        {
            string childId = childSnapshot.Key;
            string childName = childSnapshot.Child(AppConstants.DisplayName).Value?.ToString() ?? "Unnamed";
            string avatarPath = childSnapshot.Child(AppConstants.AvatarPath).Value?.ToString() ?? AppConstants.DefaultAvatar;
            int.TryParse(childSnapshot.Child(AppConstants.Balance).Value?.ToString(), out int balance);
            Enum.TryParse(childSnapshot.Child(AppConstants.RewardPreference).Value?.ToString(), out RewardType rewardType);
            
            ChildModel child = new()
            {
                Uid = childId,
                DisplayName = childName,
                AvatarPath = avatarPath,
                AdminUID = _adminUID,
                Balance = balance,
                RewardPreference = rewardType
            };

            _childData[childId] = child;

            GameObject selectorItem = Instantiate(childSelectorItemPrefab, childSelectionGrid);
            selectorItem.GetComponentInChildren<TextMeshProUGUI>().text = childName;
            selectorItem.transform.Find("Avatar").GetComponent<Image>().sprite = AvatarLoader.LoadAvatar(avatarPath);

            string selectedId = childId;
            selectorItem.GetComponent<Button>().onClick.AddListener(() =>
            {
                SetCurrentChild(_childData[selectedId]);
                HideChildSelectorPanel();
            });
        }

        if (_currentChild == null && _childData.Count > 0) 
            SetCurrentChild(_childData.Values.First());

        SetChildrenListText($"{_childData.Count} children linked.");
    }

    private void SetCurrentChild(ChildModel child)
    {
        _currentChild = child;
        currentChildNameText.text = child.DisplayName;
        currentChildAvatarIcon.sprite = AvatarLoader.LoadAvatar(child.AvatarPath);
        currentChildPointsText.text = $"{child.Balance}";

        LoadAdminContracts(child);
    }

    private void LoadAdminContracts(ChildModel child)
    {
        ContractLoader.LoadContractsForChild(child.Uid, contracts =>
        {
            foreach (Transform child in contractListContainer)
                Destroy(child.gameObject);
            
            if (contracts == null || contracts.Count == 0)
            {
                // handle empty state
                return;
            }
            
            SmartContractUIRenderer.RenderContractsToUI(
                contracts,
                contractListContainer,
                contractEntryPrefab,
                _currentChild
            );
        });
    }
    
    private void ShowChildSelectorPanel() => 
        childSelectionPanel.SetActive(true);

    private void HideChildSelectorPanel() => 
        childSelectionPanel.SetActive(false);

    private void ClearChildSelectorPanel()
    {
        foreach (Transform child in childSelectionGrid)
            Destroy(child.gameObject);
    }

    private void SetChildrenListText(string text)
    {
        if (childrenListText != null)
            childrenListText.text = text;
    }

    private void OnDestroy()
    {
        if (_childrenQuery != null)
            _childrenQuery.ValueChanged -= OnChildrenDataChanged;
    }
}