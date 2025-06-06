using System.Collections.Generic;
using _App.Models;
using _App.Services.BalanceService;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _App.Balance
{
    public class JarManagerView : MonoBehaviour
    {
        [SerializeField] private JarAdjustPanel jarAdjustPanel;
        [SerializeField] private JarCreatEditController jarCreatEditController;
            
        [Header("UI References")]
        [SerializeField] private Transform jarContainer;
        [SerializeField] private GameObject jarEntryPrefab;
        [SerializeField] private GameObject createNewJarButtonGo;
        [SerializeField] private GameObject addNewJarButtonGo;
        [SerializeField] private GameObject jarMenuPanel;
        
        [Header("Jar Menu Panel")]
        [SerializeField] private TextMeshProUGUI nameJarOptionText;
        [SerializeField] private Button jarMenuPanelExitButton;
        [SerializeField] private Button editJarButton;
        [SerializeField] private Button adjustJarButton;
        [SerializeField] private Button deleteJarButton;
        [SerializeField] private Button deleteConfirmButton;
        [SerializeField] private Button cancelDeleteButton;
        [SerializeField] private GameObject confirmDeletePanel;
        
        private FirebaseJarService _jarService;
        private string _childUid;
        private List<SavingJarModel> _jars = new();
        
        private Dictionary<string, SavingJarView> _viewByJarId = new();
        
        private RewardType _currentRewardType = RewardType.Money; // configurable fallback

        private RewardType GetRewardType() => 
            _currentRewardType;

        public void Initialize(string childUid, ChildModel child = null)
        {
            _childUid = childUid;
            _jarService = new FirebaseJarService();
            
            _currentRewardType = child?.RewardPreference ?? RewardType.Money;
            
            if(gameObject.activeInHierarchy)
                LoadAndDisplayJars();
        }

        private void Start()
        {
            jarMenuPanelExitButton.onClick.AddListener(CloseJarMenuPanel);
        }

        private void OnEnable()
        {
            if (!string.IsNullOrEmpty(_childUid))
                LoadAndDisplayJars();
        }

        private void LoadAndDisplayJars()
        {
            _jarService.GetJars(_childUid, jars =>
            {
                if (jars == null)
                {
                    Debug.LogWarning($"❌ Failed to load jars for {_childUid}");
                    return;
                }
                
                _jars = jars;
                RedrawJarUI();
                CloseJarMenuPanel();
            });
        }

        private void RedrawJarUI()
        {
            createNewJarButtonGo.gameObject.SetActive(_jars.Count == 0);
            addNewJarButtonGo.gameObject.SetActive(_jars.Count > 0);
            
            // Clear existing entries
            foreach (Transform child in jarContainer)
                Destroy(child.gameObject);

            foreach (var jar in _jars)
                InstantiateJarEntry(jar);
        }

        private void InstantiateJarEntry(SavingJarModel jar)
        {
            foreach (Transform child in jarContainer)
                Destroy(child.gameObject);
            
            GameObject entry = Instantiate(jarEntryPrefab, jarContainer);

            var view = entry.GetComponent<SavingJarView>();
            if (view == null)
            {
                Debug.LogError($"❌ Missing SavingJarView on prefab!");
                return;
            }

            // Track the view for updates
            _viewByJarId[jar.Id] = view;

            // Pass RewardType — you need to cache it somewhere (inject or assign it from parent)
            RewardType currentRewardType = GetRewardType();

            //RewardType currentRewardType = RewardType.Money; // or get it from a field
            view.SetJarUI(
                currentRewardType,
                jar.Name,
                jar.SavedAmount,
                jar.GoalAmount,
                Mathf.RoundToInt(jar.IncomePercentage * 100)
            );

            // Optional: Hook up the button
            var rootButton = entry.transform.GetComponent<Button>();
            if (rootButton)
            {
                rootButton.onClick.AddListener(() =>
                {
                    Debug.Log($"🔍 Selected jar: {jar.Name}");
                    // Open edit panel
                    OpenJarMenuPanel(jar, _childUid);
                });
            }
        }
        
        public void UpdateJarVisual(SavingJarModel jar)
        {
            if (_viewByJarId.TryGetValue(jar.Id, out var view))
            {
                RewardType currentRewardType = RewardType.Money; // reuse or inject
                view.SetJarUI(
                    currentRewardType,
                    jar.Name,
                    jar.SavedAmount,
                    jar.GoalAmount,
                    Mathf.RoundToInt(jar.IncomePercentage * 100)
                );
            }
        }
        
        private void OpenJarMenuPanel(SavingJarModel jar, string childUid)
        {
            jarMenuPanel.SetActive(true);
            nameJarOptionText.text = $"{jar.Name} Jar Option";

            AssignListener(editJarButton, () =>
                jarCreatEditController.OpenEditJar(jar, childUid, OnJarUpdated));
    
            AssignListener(adjustJarButton, () =>
                jarAdjustPanel.OpenAdjustJar(jar, childUid, OnJarAdjusted));

            AssignListener(deleteJarButton, OnClickDelete);
            AssignListener(deleteConfirmButton, () => OnClickConfirmDelete(jar));
            AssignListener(cancelDeleteButton, OnClickCancelDelete);
        }

        private void AssignListener(Button button, UnityEngine.Events.UnityAction action)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }
        
        private void CloseJarMenuPanel()
        {
            jarMenuPanel.SetActive(false);
        }
        
        private void OnJarUpdated(SavingJarModel updatedJar)
        {
            LoadAndDisplayJars();
        }

        private void OnJarAdjusted(SavingJarModel adjustedJar)
        {
            LoadAndDisplayJars();
        }

        private void OnClickDelete()
        {
            confirmDeletePanel.SetActive(true);
        }

        private void OnClickCancelDelete()
        {
            confirmDeletePanel.SetActive(false);
        }

        private void OnClickConfirmDelete(SavingJarModel jar)
        {
            _jarService.DeleteJar(_childUid, jar.Id, success =>
            {
                if (success) 
                    LoadAndDisplayJars();
                confirmDeletePanel.SetActive(false);
            });
        }
    }
}
