using System;
using System.Collections.Generic;
using UnityEngine;
using Firebase.Extensions;
using _App.Bootstrap;

public class SmartContractSeeder : MonoBehaviour
{
    [ContextMenu("Seed Sample Smart Contracts")]
    public void SeedContracts()
    {
        if (!FirebaseInit.IsReady)
        {
            Debug.LogWarning("⏳ Firebase not ready. Wait a few seconds after entering Play Mode.");
            return;
        }

        var auth = FirebaseInit.Auth;
        string adminUID = auth.CurrentUser?.UserId;

        if (string.IsNullOrEmpty(adminUID))
        {
            Debug.LogError("❌ Admin not signed in. Cannot seed contracts.");
            return;
        }

        string childUID = "D2eeKyqEJyat2Q54VtfOMMaljA13"; // Replace with real child UID if needed

        if (string.IsNullOrEmpty(childUID))
        {
            Debug.LogError("❌ Child UID is not set.");
            return;
        }

        List<SmartContractModel> contracts = new()
        {
            new SmartContractModel
            {
                Id = Guid.NewGuid().ToString(),
                Title = "Clean Your Room",
                Description = "Put toys in the box and vacuum the carpet.",
                IconPath = "Icons/ContractIcons/CleanRoom",
                AssignedToUid = childUID,
                RewardAmount = 25,
                DueDate = DateTime.UtcNow.AddDays(2).ToString("o"),
                State = SmartContractState.ReadyToSell
            },
            new SmartContractModel
            {
                Id = Guid.NewGuid().ToString(),
                Title = "Take Out Trash",
                Description = "Empty the trash before dinner.",
                IconPath = "Icons/ContractIcons/Trash",
                AssignedToUid = childUID,
                RewardAmount = 15,
                DueDate = DateTime.UtcNow.AddDays(1).ToString("o"),
                State = SmartContractState.ReadyToSell
            }
        };

        foreach (var contract in contracts)
        {
            string json = JsonUtility.ToJson(contract);

            FirebaseInit.DbRef
                .Child(AppConstants.SmartContracts)
                .Child(contract.Id)
                .SetRawJsonValueAsync(json)
                .ContinueWithOnMainThread(task =>
                {
                    if (task.IsCompletedSuccessfully)
                        Debug.Log($"✅ Contract '{contract.Title}' seeded.");
                    else
                        Debug.LogError($"❌ Failed to seed contract '{contract.Title}': {task.Exception}");
                });
        }
    }
}
