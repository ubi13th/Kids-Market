using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _App.Balance
{
    public class UnifiedHistoryView : MonoBehaviour
    {
        [SerializeField] private Transform entryContainer;
        [SerializeField] private GameObject entryPrefab;
        [SerializeField] private GameObject headerPrefab;
        [SerializeField] private Sprite coinSprite;
        [SerializeField] private Sprite jarSprite;
        [SerializeField] private Sprite adjustInSprite;
        [SerializeField] private Sprite adjustOutSprite;

        private RewardType _currentRewardType;

        public void Show(ChildModel currentChild, List<UnifiedHistoryEntry> entries)
        {
            foreach (Transform child in entryContainer)
                Destroy(child.gameObject);

            _currentRewardType = currentChild.RewardPreference;

            var grouped = GroupHistoryByDay(entries);

            foreach (var group in grouped)
            {
                // ───── Date Header ─────
                GameObject headerGO = Instantiate(headerPrefab, entryContainer);
                var headerText = headerGO.GetComponentInChildren<TextMeshProUGUI>();
                headerText.text = FormatDateLabel(group.Key);

                // ───── Entries ─────
                foreach (var entry in group.Value)
                {
                    GameObject go = Instantiate(entryPrefab, entryContainer);

                    var icon = go.transform.Find("Icon").GetComponent<Image>();
                    var reason = go.transform.Find("Reason").GetComponent<TextMeshProUGUI>();
                    var amount = go.transform.Find("Amount").GetComponent<TextMeshProUGUI>();
                    var balanceText = go.transform.Find("Title")?.GetComponent<TextMeshProUGUI>();
                    var rightIcon = go.transform.Find("RightIcon")?.GetComponent<Image>();

                    icon.sprite = entry.IsCredit ? adjustInSprite : adjustOutSprite;
                    reason.text = entry.Reason;
                    amount.text = (entry.IsCredit ? "+" : "") + SavedAmountString(entry.Amount);
                    //amount.color = entry.IsCredit ? Color.green : Color.red;

                    if (entry.Type == UnifiedHistoryEntry.EntryType.Balance)
                    {
                        if (balanceText) balanceText.text = currentChild.DisplayName;
                        if (rightIcon) rightIcon.sprite = coinSprite;
                    }
                    else
                    {
                        if (balanceText) balanceText.text = entry.JarName;
                        if (rightIcon) rightIcon.sprite = jarSprite;
                    }
                }
            }
        }
        
        private string SavedAmountString(float savedAmount)
        {
            return _currentRewardType == RewardType.Money ? $"{savedAmount:F2}" : $"{savedAmount}";
        }

        private Dictionary<string, List<UnifiedHistoryEntry>> GroupHistoryByDay(List<UnifiedHistoryEntry> entries)
        {
            return entries
                .GroupBy(e => e.Timestamp.ToString("yyyy-MM-dd"))
                .OrderByDescending(g => g.Key)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.Timestamp).ToList());
        }

        private string FormatDateLabel(string yyyyMMdd)
        {
            var date = DateTime.Parse(yyyyMMdd);
            var today = DateTime.Today;

            if (date == today) return "Today";
            if (date == today.AddDays(-1)) return "Yesterday";
            return date.ToString("dd MMMM");
        }
    }
}
