using _App.Services.BalanceService;
using UnityEngine;

namespace _App.Balance
{
    public class HistoryPresenter : MonoBehaviour
    {
        [SerializeField] private UnifiedHistoryView _historyView;

        private readonly FirebaseHistoryService _historyService = new();
        private string _childUid;
        private ChildModel _currentChild;

        public void Initialize(ChildModel currentChild, string childUid)
        {
            _currentChild = currentChild;
            _childUid = childUid;
            LoadAndShow();
        }

        private void LoadAndShow()
        {
            _historyService.LoadCombinedHistory(_childUid, entries =>
            {
                _historyView.Show(_currentChild, entries);
            });
        }
    }
}