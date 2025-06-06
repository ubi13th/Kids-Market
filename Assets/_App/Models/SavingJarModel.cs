using System;
using System.Collections.Generic;

namespace _App.Models
{
    [Serializable]
    public class SavingJarModel
    {
        public string Id;                   // Unique ID for the jar (GUID or Firebase key)
        public string Name;                 // e.g. "Save", "Donate"
        public float SavedAmount = 0f;      // How much is currently saved
        public float GoalAmount = 0f;       // Target goal (optional)
        public float IncomePercentage = 0f; // Optional: % of new income to go to this jar

        // Computed: returns 0–1 range
        public float FillRatio => GoalAmount > 0f ? SavedAmount / GoalAmount : 0f;

        // Returns rounded percent string
        public string PercentDisplay => GoalAmount > 0f ? $"{Math.Round(FillRatio * 100f)}%" : "0%";
        
        public List<JarHistoryEntry> History = new();
    }
}