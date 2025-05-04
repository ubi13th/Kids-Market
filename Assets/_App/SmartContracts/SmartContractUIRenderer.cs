using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class SmartContractUIRenderer
{
    public static void RenderContractsToUI(
        List<SmartContractModel> contracts,
        Transform container,
        GameObject contractEntryPrefab,
        ChildModel childContext)
    {
        // Clear old entries
        foreach (Transform child in container)
            UnityEngine.Object.Destroy(child.gameObject);

        if (contracts == null || contracts.Count == 0)
            return;

        // Sort by state and due date
        var sortedContracts = contracts
            .OrderBy(c => c.State)
            .ThenBy(c => DateTime.TryParse(c.DueDate, out var date) ? date : DateTime.MaxValue)
            .ToList();

        foreach (var contract in sortedContracts)
        {
            var entry = UnityEngine.Object.Instantiate(contractEntryPrefab, container);

            // Title
            var titleText = entry.transform.Find(AppConstants.Title)?.GetComponent<TextMeshProUGUI>();
            if (titleText != null)
                titleText.text = contract.Title;

            // Description
            var descriptionText = entry.transform.Find(AppConstants.Description)?.GetComponent<TextMeshProUGUI>();
            if (descriptionText != null)
                descriptionText.text = contract.Description;

            // Reward
            var rewardText = entry.transform.Find(AppConstants.Reward)?.GetComponent<TextMeshProUGUI>();
            if (rewardText != null)
            {
                string rewardDisplay = childContext.RewardPreference switch
                {
                    RewardType.Money => $"{contract.RewardAmount:F2}",
                    RewardType.Points => $"{contract.RewardAmount}",
                    _ => ""
                };

                rewardText.text = rewardDisplay;
                rewardText.gameObject.SetActive(childContext.RewardPreference != RewardType.None);
            }

            // Due date
            var dueDateText = entry.transform.Find(AppConstants.DueDate)?.GetComponent<TextMeshProUGUI>();
            if (dueDateText != null)
                dueDateText.text = ParseDateForDisplay(contract.DueDate);

            // Icon
            var iconImage = entry.transform.Find(AppConstants.Icon)?.GetComponent<Image>();
            if (iconImage != null)
                iconImage.sprite = ContractIconLoader.Load(contract.IconPath);
        }
    }

    private static string ParseDateForDisplay(string iso)
    {
        return DateTime.TryParse(iso, out var date) ? date.ToString("MMM dd, yyyy") : "";
    }
}
