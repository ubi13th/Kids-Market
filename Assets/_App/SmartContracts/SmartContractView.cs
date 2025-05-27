using System;
using System.Linq;
using _App.AdminDashboard;
using _App.Dashboard;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SmartContractView : MonoBehaviour //, IPointerDownHandler, IDragHandler, IEndDragHandler
{
    [Header("UI References")]
    //[SerializeField] private RectTransform swipeContainer;
    //[SerializeField] private float swipeThreshold = 100f;

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
    
    [SerializeField] private Button settingButton;
    [SerializeField] private Button editButton;
    [SerializeField] private Button sellButton;
    [SerializeField] private Button declineButton;
    [SerializeField] private Button deleteButton;

    //private AdminDashboardPresenter _presenter;
    //private Vector2 _startPosition;
    //private bool _isSwipedOpen = false;

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

    public void Initialize(SmartContractModel contract)
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

        if (!string.IsNullOrEmpty(contract.DueTime) && contract.DueTime != "00:00")
        {
            dueTimeText?.SetText(contract.DueTime);
            dueTimeText?.gameObject.SetActive(true);
        }
        else
        {
            dueTimeText?.gameObject.SetActive(false);
        }

        var today = DateTime.Today;
        var selectedDay = _presenter.SelectedDay.Date;
        var isFuture = selectedDay > today;
        
        //int latestQueue = _presenter.GetLastQueueIndexForDay(contract, selectedDay);

        // if (latestQueue >= 1)
        // {
        //     copyLastQueueGo.SetActive(true);
        //     copyLastQueueText.text = $"{latestQueue + 1}";
        // }
        // else
        //     copyLastQueueGo.SetActive(false);
        
        SetupButtons(contract, isFuture, selectedDay, today);
        CloseSetUpButtons();
        //ResetSwipePosition();
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
            _adminPresenter?.EditContract(contract.Id);
        });
        deleteButton?.onClick.AddListener(() => _adminPresenter?.DeleteContract(contract.Id));
        
        declineButtonContainer?.gameObject.SetActive(false);
        clock.SetActive(false);

        if (settingButton != null)
        {
            settingButton.interactable = _isAdmin;
            
            settingButton?.onClick.AddListener(() =>
            {
                if (_isSettingOn)
                    CloseSetUpButtons();
                else
                    OpenSetUpButtons();
            });
        }
        
        var state = contract.GetStateOnDate(selectedDay, _isAdmin);
        bool isTodayOrPast = selectedDay <= today;
        bool canShowSell = isTodayOrPast && !isFuture;

        // === Edit icon ===
        editIcon.SetActive(isFuture);
        //rewardText.gameObject.SetActive(!isFuture);

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
                    sellButton.onClick.AddListener(() => _presenter.UndoPurchaseContract(contract.Id));
                    break;
                case SmartContractState.ReadyToSell:
                    sellButtonText.gameObject.SetActive(true);
                    sellButton.GetComponent<Image>().color = cyanColor;
                    sellButtonText.text = _isAdmin ? "Buy" : "Sell";
                    sellButton.onClick.AddListener(() => _presenter.ConfirmContractByRole(contract.Id));
                    break;
                case SmartContractState.ReadyToConfirm:
                    sellButton.GetComponent<Image>().color = _isAdmin ? greenColor : lightGreyColor;
                    if (_isAdmin)
                    {
                        sellButton.onClick.AddListener(() => _presenter.ConfirmContractByRole(contract.Id));
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
                    sellButton.onClick.AddListener(() => _presenter.ChildBuyAdminSellContract(contract.Id));
                    sellButtonText.gameObject.SetActive(true);
                    sellButtonText.text = _isAdmin ? "Sell" : "Buy";
                    if(!_isAdmin)
                        rewardText?.SetText($"-{contract.RewardAmount:F2}");
                    break;
                default:
                    sellButton.GetComponent<Image>().color = !_isAdmin ? greenColor : lightGreyColor;
                    //sellButton.onClick.AddListener(() => _presenter.ConfirmContractByRole(contract.Id));
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


    /*private void SetupButtons(SmartContractModel contract, bool isFuture,  DateTime selectedDay, DateTime today)
    {
        sellButton?.onClick.RemoveAllListeners();
        deleteButton?.onClick.RemoveAllListeners();
        rootButton?.onClick.RemoveAllListeners();

        deleteButton?.onClick.AddListener(() => _presenter?.DeleteContract(contract.Id));
        rootButton?.onClick.AddListener(() =>
        {
            Debug.Log("Click on prefab");
            if (_isSwipedOpen)
                CloseDeleteSwipe();
            else
                _presenter?.EditContract(contract.Id);
        });

        Color buttonColor = contract.State switch
        {
            SmartContractState.ReadyToSell or SmartContractState.ReadyToBuy or SmartContractState.ReadyToConfirm => Color.green,
            SmartContractState.Completed or SmartContractState.Purchased => Color.grey,
            _ => Color.gray
        };
        
        if (editIcon != null)
            editIcon.SetActive(isFuture && selectedDay != today);

        if (sellButton != null)
        {
            sellButton.gameObject.SetActive(!isFuture && selectedDay <= today);
            
            sellButton.GetComponent<Image>().color = buttonColor;

            if (!isFuture  && selectedDay <= today && _presenter != null)
            {
                if(contract.State is SmartContractState.Completed or SmartContractState.Purchased)
                    sellButton.onClick.AddListener(() => _presenter.UndoConfirmContract(contract.Id));
                else
                    sellButton.onClick.AddListener(() => _presenter.ConfirmContract(contract.Id));
            }
            
            var label = sellButton.transform.GetChild(0).gameObject;
            var check = sellButton.transform.GetChild(1).gameObject;
            var coin = sellButton.transform.GetChild(2).gameObject;

            label.SetActive(contract.State is SmartContractState.ReadyToBuy or SmartContractState.ReadyToSell or SmartContractState.ReadyToConfirm);
            check.SetActive(contract.State is SmartContractState.Completed or SmartContractState.Purchased);
            coin.SetActive(contract.State == SmartContractState.Purchased);
        }

        gameObject.transform.GetChild(0).GetChild(2).GetComponent<CanvasGroup>().alpha = contract.State is SmartContractState.Completed or SmartContractState.Purchased ? 0.5f : 1f;
    }*/

    // === Swipe & Scroll Logic ===
    
    /*public void OnPointerDown(PointerEventData eventData) => 
        _startPosition = eventData.pressPosition;

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 delta = eventData.position - _startPosition;
        swipeContainer.anchoredPosition = new Vector2(Mathf.Clamp(delta.x, -swipeThreshold, 0), 0);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (Mathf.Abs(swipeContainer.anchoredPosition.x) > swipeThreshold / 2f)
            OpenDeleteSwipe();
        else
            CloseDeleteSwipe();
    } 
    private void OpenDeleteSwipe()
    {
       StopAllCoroutines();
       StartCoroutine(SmoothSwipeTo(new Vector2(-swipeThreshold * 2.5f, 0)));
       _isSwipedOpen = true;
    }
   
   private void CloseDeleteSwipe()
   {
       StopAllCoroutines();
       StartCoroutine(SmoothSwipeTo(Vector2.zero));
       _isSwipedOpen = false;
   }
   
   private System.Collections.IEnumerator SmoothSwipeTo(Vector2 target)
   {
       float duration = 0.2f;
       float elapsed = 0f;
       Vector2 start = swipeContainer.anchoredPosition;
   
       while (elapsed < duration)
       {
           elapsed += Time.deltaTime;
           swipeContainer.anchoredPosition = Vector2.Lerp(start, target, elapsed / duration);
           yield return null;
       }
   
       swipeContainer.anchoredPosition = target;
   }

    private void ResetSwipePosition()
    {
        swipeContainer.anchoredPosition = _isSwipedOpen
            ? new Vector2(-swipeThreshold * 2.5f, 0)
            : Vector2.zero;
    }*/
}
