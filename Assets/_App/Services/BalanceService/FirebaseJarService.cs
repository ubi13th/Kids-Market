using System;
using System.Collections.Generic;
using _App.Bootstrap;
using _App.Models;
using Firebase.Database;
using Firebase.Extensions;
using UnityEngine;

namespace _App.Services.BalanceService
{
    public class FirebaseJarService
    {
        private bool _isReady => FirebaseInit.DbRef != null;

        private DatabaseReference ChildrenRef =>
            _isReady ? FirebaseInit.DbRef.Child(AppConstants.Children) : null;

        private DatabaseReference GetJarRef(string childUid) =>
            ChildrenRef?.Child(childUid)?.Child(AppConstants.SavingJars);

        // ────────────────────────────────────────
        
        public void HasAnyJar(string childUid, Action<bool> onResult)
        {
            var jarRef = GetJarRef(childUid);
            if (jarRef == null)
            {
                onResult?.Invoke(false);
                return;
            }

            jarRef.GetValueAsync().ContinueWithOnMainThread(task =>
            {
                if (!task.IsCompletedSuccessfully || !task.Result.Exists)
                {
                    Debug.LogWarning($"❌ Could not retrieve jars or none exist for child: {childUid}");
                    onResult?.Invoke(false);
                    return;
                }

                bool hasJars = task.Result.ChildrenCount > 0;
                Debug.Log($"✅ Jar existence for {childUid}: {hasJars}");
                onResult?.Invoke(hasJars);
            });
        }
        
        public void GetJars(string childUid, Action<List<SavingJarModel>> onResult)
        {
            var jarRef = GetJarRef(childUid);
            if (jarRef == null)
            {
                onResult?.Invoke(new List<SavingJarModel>());
                return;
            }

            jarRef.GetValueAsync().ContinueWithOnMainThread(task =>
            {
                var result = new List<SavingJarModel>();

                if (!task.IsCompletedSuccessfully || !task.Result.Exists)
                {
                    Debug.LogWarning($"⚠️ No jars found for child: {childUid}");
                    onResult?.Invoke(result);
                    return;
                }


                foreach (var jarSnapshot in task.Result.Children)
                {
                    var json = jarSnapshot.GetRawJsonValue();
                    var jar = JsonUtility.FromJson<SavingJarModel>(json);
                    jar.Id = jarSnapshot.Key; // ✅ Set ID from Firebase key
                    result.Add(jar);
                }
                
                onResult?.Invoke(result);
            });
        }
        
        public void SaveOrUpdateJar(string childUid, SavingJarModel jar, Action<bool> onComplete = null)
        {
            if (string.IsNullOrEmpty(jar.Id))
                jar.Id = Guid.NewGuid().ToString();

            var jarRef = GetJarRef(childUid)?.Child(jar.Id);
            if (jarRef == null)
            {
                onComplete?.Invoke(false);
                return;
            }

            jarRef.GetValueAsync().ContinueWithOnMainThread(fetchTask =>
            {
                // Preserve existing history if jar exists
                if (fetchTask.IsCompletedSuccessfully && fetchTask.Result.Exists)
                {
                    var existingJson = fetchTask.Result.GetRawJsonValue();
                    var existingJar = JsonUtility.FromJson<SavingJarModel>(existingJson);

                    // Preserve or merge history
                    jar.History = existingJar.History ?? jar.History;
                }

                string json = JsonUtility.ToJson(jar);
                jarRef.SetRawJsonValueAsync(json).ContinueWithOnMainThread(setTask =>
                {
                    bool success = setTask.IsCompletedSuccessfully;
                    Debug.Log(success
                        ? $"✅ Saved or updated jar: {jar.Name} ({jar.Id})"
                        : $"❌ Failed to save or update jar: {setTask.Exception}");
                    onComplete?.Invoke(success);
                });
            });
        }
        
        public void DeleteJar(string childUid, string jarId, Action<bool> onComplete)
        {
            FirebaseInit.DbRef
                .Child(AppConstants.Children)
                .Child(childUid)
                .Child(AppConstants.SavingJars)
                .Child(jarId)
                .RemoveValueAsync()
                .ContinueWithOnMainThread(task =>
                {
                    Debug.Log(task.IsCompletedSuccessfully
                        ? $"🗑️ Deleted jar: {jarId}"
                        : $"❌ Failed to delete jar: {task.Exception}");

                    onComplete?.Invoke(task.IsCompletedSuccessfully);
                });
        }
        
        public void CreditJar(string childUid, string jarId, float amount, string reason, bool recordHistory, Action<bool> onComplete)
        {
            AdjustJarBalance(childUid, jarId, amount, reason, recordHistory, onComplete);
        }

        public void DebitJar(string childUid, string jarId, float amount, string reason, bool recordHistory, Action<bool> onComplete)
        {
            AdjustJarBalance(childUid, jarId, -amount, reason, recordHistory, onComplete);
        }

        
        private void AdjustJarBalance(string childUid, string jarId, float amount, string reason, bool recordHistory, Action<bool> onComplete)
        {
            var jarRef = FirebaseInit.DbRef
                .Child(AppConstants.Children)
                .Child(childUid)
                .Child(AppConstants.SavingJars)
                .Child(jarId);

            jarRef.GetValueAsync().ContinueWithOnMainThread(task =>
            {
                if (!task.IsCompletedSuccessfully || !task.Result.Exists)
                {
                    Debug.LogError($"❌ Failed to retrieve jar: {task.Exception}");
                    onComplete?.Invoke(false);
                    return;
                }

                var jar = JsonUtility.FromJson<SavingJarModel>(task.Result.GetRawJsonValue());
                jar.SavedAmount = (float)Math.Round(jar.SavedAmount + amount, 2);

                // 🧾 Optional: Record history if requested
                if (recordHistory)
                {
                    jar.History ??= new List<JarHistoryEntry>();

                    jar.History.Add(new JarHistoryEntry
                    {
                        Amount = amount,
                        Reason = reason,
                        Timestamp = DateTime.UtcNow.ToString("s")
                    });

                    if (jar.History.Count > 100)
                        jar.History.RemoveRange(0, jar.History.Count - 100);
                }

                jarRef.SetRawJsonValueAsync(JsonUtility.ToJson(jar)).ContinueWithOnMainThread(setTask =>
                {
                    if (setTask.IsCompletedSuccessfully)
                        Debug.Log($"✅ Jar updated: {jar.Name} | Δ {amount} | Reason: {reason}");
                    else
                        Debug.LogError($"❌ Failed to update jar: {setTask.Exception}");

                    onComplete?.Invoke(setTask.IsCompletedSuccessfully);
                });
            });
        }
    }
}