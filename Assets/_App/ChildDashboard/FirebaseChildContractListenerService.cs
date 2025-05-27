using System;
using System.Collections.Generic;
using _App.Bootstrap;
using Firebase.Database;
using UnityEngine;

namespace _App.ChildDashboard
{
    public class FirebaseChildContractListenerService : IChildContractListenerService
    {
        private Query _contractsQuery;
        private EventHandler<ValueChangedEventArgs> _listener;
        
        private bool _isReady = false;

        public FirebaseChildContractListenerService() => 
            Init();

        private async void Init()
        {
            await FirebaseInit.WaitUntilReady();
            _isReady = true;
        }

        public void ListenToChildContracts(string childUID, Action<List<SmartContractModel>> onChanged)
        {
            if (UserSession.IsAdmin)
                return;
            
            if (!_isReady)
            {
                Debug.LogWarning("🟡 FirebaseContractService not ready yet.");
                return;
            }

            _contractsQuery = FirebaseInit.DbRef
                .Child(AppConstants.Contracts)
                .OrderByChild(AppConstants.AssignedToUid)
                .EqualTo(childUID);

            StopListening();

            _listener = (sender, args) =>
            {
                if (args.DatabaseError != null)
                {
                    Debug.LogError("❌ Firebase error while listening to child contracts: " + args.DatabaseError.Message);
                    onChanged?.Invoke(null);
                    return;
                }

                List<SmartContractModel> contracts = new();
                foreach (var snapshot in args.Snapshot.Children)
                {
                    var json = snapshot.GetRawJsonValue();
                    var contract = JsonUtility.FromJson<SmartContractModel>(json);
                    if (contract != null)
                    {
                        contract.Id = snapshot.Key;
                        contracts.Add(contract);
                    }
                }

                onChanged?.Invoke(contracts);
            };

            _contractsQuery.ValueChanged += _listener;
        }

        public void StopListening()
        {
            if (_contractsQuery != null && _listener != null)
            {
                _contractsQuery.ValueChanged -= _listener;
                _listener = null;
                
                Debug.Log("🛑 Stopped listening to child contract updates.");
            }
        }
    }
}