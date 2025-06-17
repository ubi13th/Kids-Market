using System;
using _App.AdminDashboard;
using _App.Dashboard;
using _App.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SmartContractView : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI rewardText;
    [SerializeField] private TextMeshProUGUI reward2Text;
    [SerializeField] private TextMeshProUGUI dueTimeText;
    [SerializeField] private TextMeshProUGUI copyLastQueueText;
    [SerializeField] private TextMeshProUGUI sellButtonText;
    [SerializeField] private Image iconImage;
    [SerializeField] private GameObject editIcon;
    [SerializeField] private CanvasGroup canvasGroup;
    
    [SerializeField] private GameObject copyLastQueueGo;
    [SerializeField] private GameObject sellBtnContainer;
    [SerializeField] private GameObject declineButtonContainer;
    [SerializeField] private GameObject check;
    [SerializeField] private GameObject coin;
    [SerializeField] private GameObject clock;
    [SerializeField] private GameObject surpriseContractFrame;
    
    [SerializeField] private Button settingButton;
    [SerializeField] private Button editButton;
    [SerializeField] private Button sellButton;
    [SerializeField] private Button declineButton;
    [SerializeField] private Button deleteButton;

    private bool _isSettingOn = false;

    public string ContractId { get; private set; }
    public SmartContractModel ContractData { get; private set; }
    
    [SerializeField] private Color redColor, greenColor, greyColor, lightGreyColor, cyanColor;
    
    private IDashboardPresenter _presenter;
    private IAdminDashboardPresenter _adminPresenter;

    private bool _isAdmin = false;

    public void Setup(IDashboardPresenter presenter)
    {
        _presenter = presenter;
        _adminPresenter = presenter as IAdminDashboardPresenter;
    }
    
    public void Initialize(SmartContractModel contract, DateTime selectedDay)
    {
        _isAdmin = UserSession.IsAdmin;
        _isSettingOn = false;
        ContractData = contract;
        ContractId = contract.Id;

        titleText?.SetText(contract.Title);
        rewardText?.SetText($"{contract.RewardAmount:F2}");
        reward2Text?.SetText($"{contract.RewardAmount:F2}");

        if (iconImage != null)
            iconImage.sprite = ContractIconLoader.Load(contract.IconPath);
        
        surpriseContractFrame.SetActive(contract.IsSurprise);

        if (!string.IsNullOrEmpty(contract.DueTime) && contract.DueTime != "00:00")
        {
            dueTimeText?.SetText(contract.DueTime);
            dueTimeText?.gameObject.SetActive(true);
        }
        else
        {
            dueTimeText?.gameObject.SetActive(false);

            if (contract.IsSurprise)
            {
                dueTimeText?.gameObject.SetActive(true);
                dueTimeText?.SetText("Surprise Contract");
            }
        }

        var today = DateTime.Today;
        selectedDay = selectedDay.Date;
        var isFuture = selectedDay > today;

        SetupButtons(contract, isFuture, selectedDay, today);
        CloseSetUpButtons();
    }
    
    private void SetupButtons(SmartContractModel contract, bool isFuture, DateTime selectedDay, DateTime today)
    {
        sellButton?.onClick.RemoveAllListeners();
        declineButton?.onClick.RemoveAllListeners();
        deleteButton?.onClick.RemoveAllListeners();
        editButton?.onClick.RemoveAllListeners();

        declineButton?.onClick.AddListener(() => { _adminPresenter?.AdminDeclineContract(contract.Id); });
        
        editButton?.onClick.AddListener(() =>
        {
            CloseSetUpButtons();

            if (contract.IsSurprise)
            {
                // 🛠 Normalize to get real ID
                if (ContractIdHelper.TryNormalizeVisualContractId(contract.Id, out var realId, out _))
                    _presenter?.EditContract(realId);
                else
                    _presenter?.EditContract(contract.Id); // fallback
            }
            else
            {
                _adminPresenter?.EditContract(contract.Id);
            }
        });
        
        declineButtonContainer?.gameObject.SetActive(false);
        clock.SetActive(false);

        settingButton.interactable = false;
            
        if (_isAdmin)
        {
            settingButton.interactable = true;
            deleteButton?.onClick.AddListener(() => { _adminPresenter?.DeleteContract(contract.Id); });
        }
        else
        {
            if (contract.IsSurprise)
            {
                settingButton.interactable = true;
                deleteButton?.onClick.AddListener(() => _presenter?.DeleteContract(contract.Id));
            }
        }
            
        settingButton?.onClick.AddListener(() =>
        {
            if (_isSettingOn)
                CloseSetUpButtons();
            else
                OpenSetUpButtons();
        });
        
        var state = contract.GetStateOnDate(selectedDay, _isAdmin);
        bool isTodayOrPast = selectedDay <= today;
        bool canShowSell = isTodayOrPast && !isFuture;

        // === Edit icon ===
        editIcon.SetActive(isFuture);

        // === SELL BUTTON ===
        sellBtnContainer?.gameObject.SetActive(canShowSell);

        if (canShowSell && _presenter != null)
        {
            switch (state)
            {
                case SmartContractState.Completed:
                    sellButton.GetComponent<Image>().color = greyColor;
                    sellButton.onClick.AddListener(() => _presenter.UndoConfirmContractByRole(contract.Id));
                    break;
                case SmartContractState.Purchased:
                    sellButton.GetComponent<Image>().color = greyColor;
                    sellButton.onClick.AddListener(() => _presenter.UndoPurchaseContract(contract.Id, selectedDay));
                    break;
                case SmartContractState.ReadyToSell:
                    sellButtonText.gameObject.SetActive(true);
                    sellButton.GetComponent<Image>().color = cyanColor;
                    sellButtonText.text = _isAdmin ? "Buy" : "Sell";
                    sellButton.onClick.AddListener(() =>
                    {
                        //Debug.Log($"🟢 SELL pressed for contract ID: {contract.Id}");
                        _presenter.ConfirmContractByRole(contract.Id);
                    });
                    break;
                case SmartContractState.ReadyToConfirm:
                    sellButton.GetComponent<Image>().color = _isAdmin ? greenColor : lightGreyColor;
                    if (_isAdmin)
                    {
                        sellButton.onClick.AddListener(() =>
                        {
                            //Debug.Log($"🟢 SELL pressed for contract ID: {contract.Id}");
                            _presenter.ConfirmContractByRole(contract.Id);
                        });
                        sellButtonText.gameObject.SetActive(true);
                        sellButtonText.text = "Confirm";
                        clock.SetActive(false);
                        declineButtonContainer?.gameObject.SetActive(true);
                    }
                    else
                    {
                        sellButton.onClick.AddListener(() => _presenter.UndoConfirmContractByRole(contract.Id));
                        sellButtonText.gameObject.SetActive(false);
                        clock.SetActive(true);
                    }
                    break;
                case SmartContractState.ReadyToBuy:
                    sellButton.GetComponent<Image>().color = redColor;
                    sellButton.onClick.AddListener(() => _presenter.ChildBuyAdminSellContract(contract.Id, selectedDay));
                    sellButtonText.gameObject.SetActive(true);
                    sellButtonText.text = _isAdmin ? "Sell" : "Buy";
                    if(!_isAdmin)
                        rewardText?.SetText($"-{contract.RewardAmount:F2}");
                    break;
                default:
                    sellButton.GetComponent<Image>().color = !_isAdmin ? greenColor : lightGreyColor;
                    break;
            }
        }

        // === Button icons and text ==
        check.SetActive(state is SmartContractState.Completed or SmartContractState.Purchased);
        coin.SetActive(state is SmartContractState.Purchased);

        // === Dim if done ===
        if (canvasGroup != null)
            canvasGroup.alpha = state is SmartContractState.Completed or SmartContractState.Purchased ? 0.5f : 1f;
    }

    private void OpenSetUpButtons()
    {
        _isSettingOn = true;
        
        editButton.gameObject.SetActive(true);
        deleteButton.gameObject.SetActive(true);
    }
    
    private void CloseSetUpButtons()
    {
        _isSettingOn = false;
        
        editButton.gameObject.SetActive(false);
        deleteButton.gameObject.SetActive(false);
    }
}
