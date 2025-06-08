using System;
using System.Linq;
using UnityEngine;

namespace _App.Services.BalanceService
{
    public class IncomeDistributorService
    {
        private readonly FirebaseJarService _jarService;
        private readonly IBalanceService _balanceService;

        public IncomeDistributorService()
        {
            _jarService = new FirebaseJarService();
            _balanceService = new FirebaseBalanceService(); // or inject
        }
        
        public void DistributeIncome(string childUid, float totalAmount, string reason)
        {
            _jarService.GetJars(childUid, jars =>
            {
                if (jars == null || jars.Count == 0)
                {
                    // No jars → 100% to balance
                    _balanceService.AdjustBalance(childUid, totalAmount, reason, recordHistory: false);
                    return;
                }

                float totalJarPercent = 0f;
                foreach (var jar in jars)
                    totalJarPercent += jar.IncomePercentage;

                totalJarPercent = Mathf.Clamp01(totalJarPercent);

                float jarShare = (float)Math.Round(totalAmount * totalJarPercent, 2);
                float balanceShare = (float)Math.Round(totalAmount - jarShare, 2);

                // ✅ Step 1: Add to balance (without recording history)
                _balanceService.AdjustBalance(childUid, balanceShare, reason + " (after jar split)", recordHistory: false);

                // ✅ Step 2: Distribute to jars (with history)
                foreach (var jar in jars)
                {
                    float jarAmount = (float)Math.Round(totalAmount * jar.IncomePercentage, 2);
                    if (jarAmount <= 0f) continue;

                    _jarService.CreditJar(childUid, jar.Id, jarAmount, reason, recordHistory: false, success =>
                    {
                        if (success)
                            Debug.Log($"💰 Credited {jarAmount} to jar '{jar.Name}'");
                        else
                            Debug.LogWarning($"❌ Failed to credit jar '{jar.Name}'");
                    });
                }

                Debug.Log($"💸 Distributed {totalAmount} → {balanceShare} to balance, {jarShare} to jars");
            });
        }
        
        public void UndoDistribution(string childUid, float totalReward, string reason)
        {
            if (string.IsNullOrEmpty(childUid) || totalReward <= 0)
            {
                Debug.LogWarning("❌ Invalid undo distribution input.");
                return;
            }

            _jarService.GetJars(childUid, jars =>
            {
                float totalJarPercentage = 0f;
                foreach (var jar in jars)
                    totalJarPercentage += jar.IncomePercentage;

                float clampedPercentage = Mathf.Clamp01(totalJarPercentage);
                float childPortion = (float)Math.Round(totalReward * (1f - clampedPercentage), 2);

                Debug.Log($"↩️ Calculated child undo portion: {childPortion} (from {totalReward}, jar percent sum: {clampedPercentage * 100f}%)");

                // Step 1: Withdraw only the child's portion (without history)
                _balanceService.AdjustBalance(childUid, -childPortion, $"↩️ Undo: {reason}", recordHistory: false, success =>
                {
                    if (!success)
                    {
                        Debug.LogWarning($"❌ Failed to deduct from child balance during undo: {reason}");
                        return;
                    }

                    Debug.Log($"↩️ Deducted {childPortion} from child balance for undo: {reason}");

                    // Step 2: Withdraw from each jar (with history)
                    foreach (var jar in jars)
                    {
                        float jarAmount = (float)Math.Round(totalReward * jar.IncomePercentage, 2);
                        if (jarAmount <= 0f) continue;

                        _jarService.DebitJar(childUid, jar.Id, jarAmount, $"↩️ Undo: {reason}", recordHistory: false, jarSuccess =>
                        {
                            if (jarSuccess)
                                Debug.Log($"↩️ Deducted {jarAmount} from jar '{jar.Name}' due to undo.");
                            else
                                Debug.LogWarning($"❌ Failed to deduct {jarAmount} from jar '{jar.Name}' during undo.");
                        });
                    }
                });
            });
        }
        
        public void UndoPurchaseContract(string childUid, float amount, string reason)
        {
            if (amount <= 0f) return;
            _balanceService.AdjustBalance(childUid, amount, reason, recordHistory: false);
        }
    }
}