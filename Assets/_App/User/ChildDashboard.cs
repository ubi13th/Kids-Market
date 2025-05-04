using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Firebase.Database;
using Firebase.Extensions;
using _App.Bootstrap;

public class ChildDashboard : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image avatarImage;
    [SerializeField] private TextMeshProUGUI childNameText;
    [SerializeField] private TextMeshProUGUI balanceText;
    [SerializeField] private Transform contractListContainer;
    [SerializeField] private GameObject contractEntryPrefab;
    [SerializeField] private TextMeshProUGUI statusText;

    private ChildModel _currentChild;
    private string _childUID;
    private DatabaseReference _childRef;
    private DatabaseReference _contractsRef;
    
    private Query _contractsQuery;

    private async void Start()
    {
        await FirebaseInit.WaitUntilReady();

        _childUID = PlayerPrefs.GetString(AppConstants.ChildUID, "");
        if (string.IsNullOrEmpty(_childUID))
        {
            Debug.LogError("❌ No child UID found.");
            statusText.text = "User not signed in.";
            return;
        }

        _childRef = FirebaseInit.DbRef.Child(AppConstants.Children).Child(_childUID);
        _contractsRef = FirebaseInit.DbRef.Child(AppConstants.SmartContracts);

        // Initial Load
        PullToRefresh();
    }

    private void PullToRefresh()
    {
        LoadChildHeader();
        SetupContractListener();
    }

    private void LoadChildHeader()
    {
        _childRef.GetValueAsync().ContinueWithOnMainThread((System.Threading.Tasks.Task<DataSnapshot> task) =>
        {
            if (task.IsCompletedSuccessfully && task.Result.Exists)
            {
                var snapshot = task.Result;
                string name = snapshot.Child(AppConstants.DisplayName).Value?.ToString() ?? "Unnamed";
                string avatarPath = snapshot.Child(AppConstants.AvatarPath).Value?.ToString() ?? AppConstants.DefaultAvatar;
                int balance = int.TryParse(snapshot.Child(AppConstants.Balance).Value?.ToString(), out var p) ? p : 0;
                Enum.TryParse(snapshot.Child(AppConstants.RewardPreference).Value?.ToString(), out RewardType rewardPref);

                childNameText.text = name;
                avatarImage.sprite = AvatarLoader.LoadAvatar(avatarPath);
                balanceText.text = $"{balance}";
                
                _currentChild = new ChildModel
                {
                    Uid = _childUID,
                    DisplayName = name,
                    AvatarPath = avatarPath,
                    Balance = balance,
                    RewardPreference = rewardPref
                };

                childNameText.text = name;
                avatarImage.sprite = AvatarLoader.LoadAvatar(avatarPath);
                balanceText.text = $"{balance}";
                
                LoadContracts(); // 🔁 Now safe to load contracts
            }
            else
            {
                Debug.LogError("⚠️ Failed to load child profile: " + task.Exception);
                statusText.text = "Failed to load profile.";
            }
        });
    }
    
    private void SetupContractListener()
    {
        _contractsQuery = FirebaseInit.DbRef
            .Child(AppConstants.SmartContracts)
            .OrderByChild(AppConstants.AssignedToUid)
            .EqualTo(_childUID);

        _contractsQuery.ValueChanged += OnContractsChanged;
    }
    
    private void LoadContracts()
    {
        if (_currentChild == null)
        {
            Debug.LogWarning("❌ Cannot load contracts before child profile is ready.");
            return;
        }

        ContractLoader.LoadContractsForChild(_currentChild.Uid, contracts =>
        {
            if (contracts == null || contracts.Count == 0)
            {
                statusText.text = "No smart contracts available.";
                return;
            }

            statusText.text = string.Empty;
            
            SmartContractUIRenderer.RenderContractsToUI(
                contracts,
                contractListContainer,
                contractEntryPrefab,
                _currentChild
            );
        });
    }

    private void OnContractsChanged(object sender, ValueChangedEventArgs args)
    {
        foreach (Transform child in contractListContainer)
            Destroy(child.gameObject);

        if (args.DatabaseError != null)
        {
            statusText.text = "Failed to load contracts.";
            Debug.LogError("Firebase error: " + args.DatabaseError.Message);
            return;
        }

        if (!args.Snapshot.Exists || args.Snapshot.ChildrenCount == 0)
        {
            statusText.text = "No smart contracts found.";
            return;
        }

        statusText.text = string.Empty;
        
        var contracts = new List<SmartContractModel>();
        foreach (var contractSnapshot in args.Snapshot.Children)
        {
            try
            {
                var json = contractSnapshot.GetRawJsonValue();
                var contract = JsonUtility.FromJson<SmartContractModel>(json);
                contracts.Add(contract);
            }
            catch (Exception ex)
            {
                Debug.LogError("❌ Error parsing contract: " + ex.Message);
            }
        }

        SmartContractUIRenderer.RenderContractsToUI(
            contracts,
            contractListContainer,
            contractEntryPrefab,
            _currentChild
        );

        /*foreach (var contractSnapshot in args.Snapshot.Children)
        {
            try
            {
                var json = contractSnapshot.GetRawJsonValue();
                var contract = JsonUtility.FromJson<SmartContractModel>(json);

                GameObject entry = Instantiate(contractEntryPrefab, contractListContainer);
                
                entry.GetComponentInChildren<TextMeshProUGUI>().text = contract.Title;

                var icon = entry.transform.Find(AppConstants.Icon)?.GetComponent<Image>();
                if (icon != null)
                {
                    icon.sprite = AvatarLoader.LoadAvatar(contract.IconPath ?? AppConstants.DefaultContractIcon);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("❌ Error parsing contract: " + ex.Message);
            }
        }*/
    }
}
