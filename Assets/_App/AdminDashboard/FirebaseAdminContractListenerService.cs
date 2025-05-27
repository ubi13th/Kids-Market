using System;
using System.Collections.Generic;
using _App.Bootstrap;
using _App.Services;
using Firebase.Database;
using UnityEngine;

namespace _App.AdminDashboard
{
    public class FirebaseAdminContractListenerService : IAdminContractListenerService
    {
        private Query _contractsQuery;
        private EventHandler<ValueChangedEventArgs> _listener;
        
        private bool _isReady = false;

        public FirebaseAdminContractListenerService() => 
            Init();

        private async void Init()
        {
            await FirebaseInit.WaitUntilReady();
            _isReady = true;
        }

        public void ListenToAdminContracts(string adminUID, Action<List<SmartContractModel>> onChanged)
        {
            if (!UserSession.IsAdmin)
                return;
            
            if (!_isReady)
            {
                Debug.LogWarning("🟡 FirebaseContractService not ready yet.");
                return;
            }

            _contractsQuery = FirebaseInit.DbRef
                .Child(AppConstants.Contracts)
                .OrderByChild(AppConstants.AdminUID)
                .EqualTo(adminUID);

            StopListening();
            
            _listener = (sender, args) =>
            {
                List<SmartContractModel> contracts = new();

                foreach (var snapshot in args.Snapshot.Children)
                {
                    var json = snapshot.GetRawJsonValue();
                    //Debug.Log($"📄 Raw snapshot JSON: {json}");

                    var contract = JsonUtility.FromJson<SmartContractModel>(json);
                    if (contract != null)
                    {
                        contract.Id = snapshot.Key;
                        contracts.Add(contract);
                    }
                    else
                    {
                        Debug.LogWarning($"⚠️ Failed to parse JSON: {json}");
                    }
                }

                //Debug.Log($"✅ Listener found {contracts.Count} contracts");

                onChanged?.Invoke(contracts);
            };

            _contractsQuery.ValueChanged += _listener;
        }

        public void StopListening()
        {
            if (_contractsQuery == null || _listener == null) return;
            _contractsQuery.ValueChanged -= _listener;
            _listener = null;
            
            Debug.Log("🛑 Stopped listening to admin contract updates.");
        }
    }
}