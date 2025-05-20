using System;
using _App.AdminDashboard;
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
    [SerializeField] private TextMeshProUGUI dueTimeText;
    [SerializeField] private Image iconImage;
    [SerializeField] private GameObject editIcon;
    [SerializeField] private CanvasGroup canvasGroup;
    
    [SerializeField] private Button settingButton;
    [SerializeField] private Button editButton;
    [SerializeField] private Button sellButton;
    [SerializeField] private Button deleteButton;

    private AdminDashboardPresenter _presenter;
    //private Vector2 _startPosition;
    //private bool _isSwipedOpen = false;

    private bool _isSettingOn = false;

    public string ContractId { get; private set; }
    public SmartContractModel ContractData { get; private set; }
    
    public void Setup(AdminDashboardPresenter presenter)
    {
        _presenter = presenter;
    }

    public void Initialize(SmartContractModel contract)
    {
        _isSettingOn = false;
        
        ContractData = contract;
        ContractId = contract.Id;

        titleText?.SetText(contract.Title);
        rewardText?.SetText($"{contract.RewardAmount:F2}");

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

        SetupButtons(contract, isFuture, selectedDay, today);
        CloseSetUpButtons();
        //ResetSwipePosition();
    }
    
    private void SetupButtons(SmartContractModel contract, bool isFuture, DateTime selectedDay, DateTime today)
    {
        sellButton?.onClick.RemoveAllListeners();
        deleteButton?.onClick.RemoveAllListeners();
        editButton?.onClick.RemoveAllListeners();

        editButton?.onClick.AddListener(() => { _presenter?.EditContract(contract.Id); });
        deleteButton?.onClick.AddListener(() => _presenter?.DeleteContract(contract.Id));
        
        settingButton?.onClick.AddListener(() =>
        {
            if (_isSettingOn)
                CloseSetUpButtons();
            else
                OpenSetUpButtons();
        });
        
        // editButton?.onClick.AddListener(() =>
        // {
        //     _presenter?.EditContract(contract.Id);
        //     //if (_isSwipedOpen)
        //         //CloseDeleteSwipe();
        //     //else
        //         //_presenter?.EditContract(contract.Id);
        // });

        bool isAdmin = UserSession.IsAdmin;
        var state = contract.GetStateOnDate(selectedDay, isAdmin);
        bool isTodayOrPast = selectedDay <= today;
        bool canShowSell = isTodayOrPast && !isFuture;

        // === Set colors ===
        Color buttonColor = state switch
        {
            SmartContractState.ReadyToSell or SmartContractState.ReadyToConfirm => Color.green,
            SmartContractState.ReadyToBuy => Color.cyan,
            SmartContractState.Completed or SmartContractState.Purchased => Color.grey,
            _ => Color.gray
        };

        // === Edit icon ===
        if (editIcon != null)
            editIcon.SetActive(isFuture && selectedDay != today);

        // === SELL BUTTON ===
        if (sellButton != null)
        {
            sellButton.gameObject.SetActive(canShowSell);
            sellButton.GetComponent<Image>().color = buttonColor;

            if (canShowSell && _presenter != null)
            {
                if (state is SmartContractState.Completed or SmartContractState.Purchased)
                    sellButton.onClick.AddListener(() => _presenter.UndoConfirmContract(contract.Id));
                else
                    sellButton.onClick.AddListener(() => _presenter.ConfirmContract(contract.Id));
            }

            // === Button icons and text ===
            var label = sellButton.transform.GetChild(0).gameObject;
            var check = sellButton.transform.GetChild(1).gameObject;
            var coin = sellButton.transform.GetChild(2).gameObject;

            label.SetActive(state is SmartContractState.ReadyToSell or SmartContractState.ReadyToBuy or SmartContractState.ReadyToConfirm);
            check.SetActive(state is SmartContractState.Completed or SmartContractState.Purchased);
            coin.SetActive(state == SmartContractState.Purchased);
        }

        // === Dim if done ===
        //var canvasGroup = gameObject.transform.GetChild(0).GetChild(2).GetComponent<CanvasGroup>();
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
