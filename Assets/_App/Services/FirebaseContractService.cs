using Firebase.Database;
using System;
using _App.Bootstrap;
using Firebase.Extensions;
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

        /*public void HideAnyoneContractForToday(string title, string adminUid, string confirmingChildUid, DateTime onDate)
        {
            ContractsRef.GetValueAsync().ContinueWithOnMainThread(task =>
            {
                if (!task.IsCompletedSuccessfully || !task.Result.Exists)
                {
                    Debug.LogWarning("❌ Failed to fetch contracts for hiding");
                    return;
                }

                var snapshot = task.Result;
                foreach (var child in snapshot.Children)
                {
                    var json = child.GetRawJsonValue();
                    if (string.IsNullOrEmpty(json))
                        continue;

                    var contract = JsonUtility.FromJson<SmartContractModel>(json);
                    contract.Id = child.Key;

                    if (contract == null || contract.IsCopy)
                        continue;

                    if (contract.AssignmentMode != AssignMode.Anyone)
                        continue;

                    if (contract.Title != title || contract.AdminUID != adminUid)
                        continue;

                    if (contract.AssignedToUid == confirmingChildUid)
                        continue; // Don't hide for the confirming child

                    if (contract.GetStartDate() > onDate.Date)
                        continue; // Skip future contracts

                    // ✅ Hide this contract for today
                    contract.SetStateOnDate(onDate, SmartContractState.Hidden);
                    SaveContract(contract, _ => { });
                }
            });
        }
        
        public void RestoreAnyoneContractForToday(string parentId, string undoingChildUid, DateTime day)
        {
            if (!_isReady)
            {
                Debug.LogWarning("🟡 FirebaseContractService not ready.");
                return;
            }

            FirebaseInit.DbRef.Child(AppConstants.Contracts)
                .OrderByChild("ParentId").EqualTo(parentId)
                .GetValueAsync()
                .ContinueWithOnMainThread(task =>
                {
                    if (!task.IsCompletedSuccessfully || !task.Result.Exists)
                    {
                        Debug.LogWarning($"❌ No child contracts found for ParentId: {parentId}");
                        return;
                    }

                    foreach (var snapshot in task.Result.Children)
                    {
                        var json = snapshot.GetRawJsonValue();
                        var contract = JsonUtility.FromJson<SmartContractModel>(json);
                        contract.Id = snapshot.Key;
                        contract.LoadStateHistory();

                        if (contract.GetStartDate().Date != day.Date)
                            continue;

                        SmartContractState restoredState = contract.AssignedToUid == undoingChildUid
                            ? SmartContractState.ReadyToSell
                            : SmartContractState.ReadyToConfirm;

                        contract.SetStateOnDate(day, restoredState);

                        SaveContract(contract, success =>
                        {
                            if (success)
                                Debug.Log($"✅ Restored contract for child: {contract.AssignedToUid}");
                            else
                                Debug.LogWarning($"❌ Failed to restore contract: {contract.Id}");
                        });
                    }
                });
        }
        */



        
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

            string json = JsonUtility.ToJson(contract);
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
                        var json = task.Result.GetRawJsonValue();
                        var contract = JsonUtility.FromJson<SmartContractModel>(json);
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
