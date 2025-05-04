using System;
using System.Collections.Generic;
using _App.Bootstrap;
using Firebase.Extensions;
using UnityEngine;

public static class ContractLoader
{
    public static void LoadContractsForChild(string childId, Action<List<SmartContractModel>> onContractsLoaded)
    {
        FirebaseInit.DbRef
            .Child(AppConstants.SmartContracts)
            .OrderByChild("AssignedToUid")
            .EqualTo(childId)
            .GetValueAsync()
            .ContinueWithOnMainThread(task =>
            {
                List<SmartContractModel> contracts = new();

                if (task.IsCompletedSuccessfully && task.Result.Exists)
                {
                    foreach (var contractSnapshot in task.Result.Children)
                    {
                        try
                        {
                            var contract = JsonUtility.FromJson<SmartContractModel>(contractSnapshot.GetRawJsonValue());
                            contracts.Add(contract);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"❌ Error parsing contract: {ex.Message}");
                        }
                    }
                }

                onContractsLoaded?.Invoke(contracts);
            });
    }

    
    public static void LoadContractsForChild2(string childId, Action<List<SmartContractModel>> onContractsLoaded)
    {
        FirebaseInit.DbRef
            .Child(AppConstants.Children)
            .Child(childId)
            .Child(AppConstants.Contracts)
            .GetValueAsync()
            .ContinueWithOnMainThread(task =>
            {
                List<SmartContractModel> contracts = new();

                if (task.IsCompletedSuccessfully && task.Result.Exists)
                {
                    foreach (var contractSnapshot in task.Result.Children)
                    {
                        try
                        {
                            var contract = JsonUtility.FromJson<SmartContractModel>(contractSnapshot.GetRawJsonValue());
                            contracts.Add(contract);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"❌ Error parsing contract: {ex.Message}");
                        }
                    }
                }

                onContractsLoaded?.Invoke(contracts);
            });
    }
}