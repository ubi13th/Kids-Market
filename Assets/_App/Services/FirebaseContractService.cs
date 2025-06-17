using Firebase.Database;
using System;
using System.Threading.Tasks;
using _App.Bootstrap;
using Firebase.Extensions;
using Newtonsoft.Json;
using UnityEngine;

namespace _App.Services
{
    public class FirebaseContractService : IContractService
    {
        private bool _isReady = false;

        public FirebaseContractService() => Init();

        private async void Init()
        {
            await FirebaseInit.WaitUntilReady();
            _isReady = true;
        }
        
        private DatabaseReference ContractsRef
        {
            get
            {
                if (!_isReady || FirebaseInit.DbRef == null)
                {
                    Debug.LogWarning("⚠️ FirebaseContractService is not ready or DbRef is null.");
                    return null;
                }

                return FirebaseInit.DbRef.Child(AppConstants.Contracts);
            }
        }
        
        public void SetContractStateOnDate(string contractId, DateTime date, SmartContractState state, Action<bool> onComplete)
        {
            if (!_isReady)
            {
                Debug.LogWarning("🟡 FirebaseContractService not ready.");
                onComplete?.Invoke(false);
                return;
            }

            GetContractById(contractId, contract =>
            {
                if (contract == null)
                {
                    Debug.LogWarning($"❌ Contract not found: {contractId}");
                    onComplete?.Invoke(false);
                    return;
                }

                contract.SetStateOnDate(date, state);
                SaveContract(contract, onComplete);
            });
        }

        public void GetAsNeededCopyByParentId(string parentId, Action<SmartContractModel> callback)
        {
            Debug.Log($"🔍 Looking for AsNeeded copy with ParentId = {parentId}");

            FirebaseInit.DbRef
                .Child(AppConstants.Contracts)
                .OrderByChild("ParentId").EqualTo(parentId)
                .GetValueAsync()
                .ContinueWithOnMainThread(task =>
                {
                    if (!task.IsCompletedSuccessfully)
                    {
                        Debug.LogError("❌ Firebase task failed.");
                        callback(null);
                        return;
                    }

                    if (!task.Result.Exists)
                    {
                        Debug.Log("📭 No matching contracts found.");
                        callback(null);
                        return;
                    }

                    SmartContractModel foundCopy = null;

                    foreach (var snapshot in task.Result.Children)
                    {
                        Debug.Log($"📦 Checking contract: {snapshot.Key}");
                        // var json = snapshot.GetRawJsonValue();
                        // var contract = JsonUtility.FromJson<SmartContractModel>(json);
                        var json = snapshot.GetRawJsonValue();
                        var contract = JsonConvert.DeserializeObject<SmartContractModel>(json);
                        contract.Id = snapshot.Key;

                        if (contract.IsCopy && contract.RepeatMode == RepeatType.AsNeeded)
                        {
                            if (foundCopy != null)
                            {
                                Debug.LogWarning($"⚠️ Multiple AsNeeded copies found. Using first: {foundCopy.Id}");
                                break;
                            }

                            foundCopy = contract;
                            Debug.Log($"✅ Found matching AsNeeded copy: {contract.Id}");
                        }
                        else
                        {
                            Debug.Log($"⏩ Skipped: IsCopy={contract.IsCopy}, RepeatMode={contract.RepeatMode}");
                        }
                    }

                    callback(foundCopy);
                });
        }

        public void DeleteContract(string contractId, Action<bool> onComplete)
        {
            if (!_isReady)
            {
                Debug.LogWarning("🟡 FirebaseContractService not ready yet.");
                onComplete?.Invoke(false);
                return;
            }

            ContractsRef.Child(contractId).RemoveValueAsync()
                .ContinueWithOnMainThread(task => onComplete?.Invoke(task.IsCompletedSuccessfully));
        }

        public void SaveContract(SmartContractModel contract, Action<bool> onComplete)
        {
            if (!_isReady)
            {
                Debug.LogWarning("🟡 FirebaseContractService not ready yet.");
                onComplete?.Invoke(false);
                return;
            }
            
            contract.SyncStateHistory(); // ✅ Make sure raw string is updated!
            
            // ✅ Normalize ParentId BEFORE serialization
            if (!contract.IsCopy) 
                contract.ParentId = null; // ✅ Ensure parent contracts don't save empty string

            DatabaseReference refToUse;
            if (string.IsNullOrEmpty(contract.Id))
            {
                refToUse = ContractsRef.Push();
                contract.Id = refToUse.Key;
            }
            else
            {
                refToUse = ContractsRef.Child(contract.Id);
            }

            //string json = JsonUtility.ToJson(contract);
            string json = JsonConvert.SerializeObject(contract);
            Debug.Log($"📤 Saving JSON: {json}");
            
            refToUse.SetRawJsonValueAsync(json)
                .ContinueWithOnMainThread(task =>
                {
                    if (task.IsCompletedSuccessfully)
                    {
                        Debug.Log($"✅ Contract '{contract.Title}' saved with ID {contract.Id}");
                        onComplete?.Invoke(true);
                    }
                    else
                    {
                        Debug.LogError($"❌ Failed to save contract: {task.Exception}");
                        onComplete?.Invoke(false);
                    }
                });
        }

        public void GetContractById(string contractId, Action<SmartContractModel> callback)
        {
            if (!_isReady)
            {
                Debug.LogWarning("🟡 FirebaseContractService not ready.");
                callback?.Invoke(null);
                return;
            }

            ContractsRef.Child(contractId)
                .GetValueAsync()
                .ContinueWithOnMainThread(task =>
                {
                    if (!task.IsCompletedSuccessfully || !task.Result.Exists)
                    {
                        Debug.LogError("❌ Contract not found: " + contractId);
                        callback?.Invoke(null);
                        return;
                    }

                    try
                    {
                        // var json = task.Result.GetRawJsonValue();
                        // var contract = JsonUtility.FromJson<SmartContractModel>(json);
                        var json = task.Result.GetRawJsonValue();
                        var contract = JsonConvert.DeserializeObject<SmartContractModel>(json);
                        
                        contract.Id = contractId;
    
                        // ✅ Deserialize CompletionHistory string into list
                        contract.LoadStateHistory();

                        callback?.Invoke(contract);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"❌ Error parsing contract: {ex.Message}");
                        callback?.Invoke(null);
                    }
                });
        }
    }
}
