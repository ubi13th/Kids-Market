using System;
using System.Collections.Generic;
using _App.Bootstrap;
using Firebase.Database;
using UnityEngine;

namespace _App.Services.BalanceService
{
    public class FirebaseBalanceListenerService : IBalanceListenerService
    {
        private readonly Dictionary<string, EventHandler<ValueChangedEventArgs>> _activeListeners = new();

        public void ListenToBalance(string childUid, Action<float> onBalanceChanged)
        {
            if (_activeListeners.ContainsKey(childUid))
                return;

            var listener = new EventHandler<ValueChangedEventArgs>((sender, args) =>
            {
                if (args.DatabaseError != null)
                {
                    Debug.LogWarning($"❌ Balance listener error: {args.DatabaseError.Message}");
                    return;
                }

                if (args.Snapshot.Exists && float.TryParse(args.Snapshot.Value.ToString(), out float newBalance))
                    onBalanceChanged?.Invoke(newBalance);
            });

            FirebaseInit.DbRef
                .Child(AppConstants.Children)
                .Child(childUid)
                .Child(AppConstants.Balance)
                .ValueChanged += listener;

            _activeListeners[childUid] = listener;
        }

        public void StopListening(string childUid)
        {
            if (_activeListeners.TryGetValue(childUid, out var listener))
            {
                FirebaseInit.DbRef
                    .Child(AppConstants.Children)
                    .Child(childUid)
                    .Child(AppConstants.Balance)
                    .ValueChanged -= listener;

                _activeListeners.Remove(childUid);
            }
        }
    }
}