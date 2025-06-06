using System;
using System.Collections.Generic;
using System.Linq;
using _App.Balance;
using _App.Bootstrap;
using _App.Models;
using Firebase.Database;
using Firebase.Extensions;
using Newtonsoft.Json;
using UnityEngine;

namespace _App.Services.BalanceService
{
    public class FirebaseHistoryService
    {
        private bool IsReady => FirebaseInit.DbRef != null;

        private DatabaseReference GetChildrenRef() =>
            IsReady ? FirebaseInit.DbRef.Child(AppConstants.Children) : null;

        private DatabaseReference GetBalanceHistoryRef(string childUid) =>
            GetChildrenRef()?.Child(childUid)?.Child(AppConstants.BalanceHistory);

        private DatabaseReference GetJarsRef(string childUid) =>
            GetChildrenRef()?.Child(childUid)?.Child(AppConstants.SavingJars);

        public void LoadCombinedHistory(string childUid, Action<List<UnifiedHistoryEntry>> onComplete)
        {
            if (!IsReady)
            {
                Debug.LogWarning("⚠️ Firebase is not ready.");
                onComplete?.Invoke(new List<UnifiedHistoryEntry>());
                return;
            }

            var balanceHistoryRef = GetBalanceHistoryRef(childUid);
            var jarsRef = GetJarsRef(childUid);

            List<UnifiedHistoryEntry> result = new();

            // Step 1: Load Balance History
            balanceHistoryRef.GetValueAsync().ContinueWithOnMainThread(balanceTask =>
            {
                if (balanceTask.IsCompletedSuccessfully && balanceTask.Result.Exists)
                {
                    foreach (var entrySnap in balanceTask.Result.Children)
                    {
                        var json = entrySnap.GetRawJsonValue();
                        var entry = JsonConvert.DeserializeObject<BalanceHistoryEntry>(json);

                        result.Add(new UnifiedHistoryEntry
                        {
                            Type = UnifiedHistoryEntry.EntryType.Balance,
                            Reason = entry.Reason,
                            Amount = entry.Amount,
                            Timestamp = DateTime.Parse(entry.Timestamp),
                            BalanceAfter = 0,     // Optional: you can calculate if needed
                            JarName = null
                        });
                    }
                    
                    var balanceEntries = result
                        .Where(e => e.Type == UnifiedHistoryEntry.EntryType.Balance)
                        .OrderBy(e => e.Timestamp)
                        .ToList();

                    float runningTotal = 0f;
                    foreach (var entry in balanceEntries)
                    {
                        runningTotal += entry.Amount;
                        entry.BalanceAfter = runningTotal;
                    }
                }

                // Step 2: Load Jar History
                jarsRef.GetValueAsync().ContinueWithOnMainThread(jarsTask =>
                {
                    if (jarsTask.IsCompletedSuccessfully && jarsTask.Result.Exists)
                    {
                        foreach (var jarSnap in jarsTask.Result.Children)
                        {
                            string jarName = jarSnap.Child("Name").Value?.ToString();
                            var historySnap = jarSnap.Child("History");

                            if (!historySnap.Exists) continue;

                            foreach (var historyEntrySnap in historySnap.Children)
                            {
                                var json = historyEntrySnap.GetRawJsonValue();
                                if (string.IsNullOrEmpty(json)) continue;

                                JarHistoryEntry jarEntry;
                                try
                                {
                                    jarEntry = JsonUtility.FromJson<JarHistoryEntry>(json);
                                }
                                catch (Exception ex)
                                {
                                    Debug.LogWarning($"⚠️ Failed to parse JarHistoryEntry: {ex.Message}");
                                    continue;
                                }

                                if (!DateTime.TryParse(jarEntry.Timestamp, out var timestamp)) continue;

                                result.Add(new UnifiedHistoryEntry
                                {
                                    Type = UnifiedHistoryEntry.EntryType.Jar,
                                    Reason = jarEntry.Reason,
                                    Amount = jarEntry.Amount,
                                    Timestamp = timestamp,
                                    BalanceAfter = 0,
                                    JarName = jarName
                                });
                            }
                        }


                        
                        
                        
                        
                        /*foreach (var jarSnap in jarsTask.Result.Children)
                        {
                            string jarName = jarSnap.Child("Name").Value?.ToString();

                            foreach (var child in jarSnap.Children)
                            {
                                // Skip metadata keys (e.g., Id, Name, SavedAmount, etc.)
                                if (!int.TryParse(child.Key, out _)) continue;

                                var reason = child.Child("Reason").Value?.ToString();
                                var timestampStr = child.Child("Timestamp").Value?.ToString();
    
                                if (!DateTime.TryParse(timestampStr, out var timestamp)) continue;

                                // For now, assume amount isn't stored — you can extend if needed
                                float amount = 0f;

                                result.Add(new UnifiedHistoryEntry
                                {
                                    Type = UnifiedHistoryEntry.EntryType.Jar,
                                    Reason = reason,
                                    Amount = amount,
                                    Timestamp = timestamp,
                                    BalanceAfter = 0,
                                    JarName = jarName
                                });
                            }
                        }*/
                    }

                    // Sort all by timestamp (latest first)
                    var sorted = result.OrderByDescending(e => e.Timestamp).ToList();
                    onComplete?.Invoke(sorted);
                });
            });
        }
    }
}