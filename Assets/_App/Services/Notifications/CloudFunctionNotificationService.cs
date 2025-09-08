using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using _App.Bootstrap;
using Firebase.Functions;
using UnityEngine;

namespace _App.Services.Notifications
{
    /// <summary>Sends notifications via Callable Cloud Function "sendNotification".</summary>
    public class CloudFunctionNotificationService : INotificationService
    {
        // Change if your function is deployed elsewhere (e.g., "us-central1")
        private const string Region = "us-central1";

        //private FirebaseFunctions _functions;
        private bool _initialized;
        private Task _initTask;

        /// <summary>Call once (e.g., from a MonoBehaviour's Awake/Start).</summary>
        public async Task InitAsync()
        {
            if (_initialized) return;
            if (_initTask != null) { await _initTask; return; }

            _initTask = InitInner();
            await _initTask;

            async Task InitInner()
            {
                await FirebaseInit.WaitUntilReady(); // ensures DefaultInstance
                //_functions = FirebaseFunctions.GetInstance(FirebaseInit.App, Region);
                _initialized = true;
                Debug.Log($"[CloudFunctionNotificationService] Ready (region: {Region})");
            }
        }

        // Fire-and-forget onto async flow
        public void Notify(string targetUid, NotificationEventType type, SmartContractModel contract, string actorUid, string actorRole)
        {
            _ = NotifyInternalAsync(targetUid, type, contract, actorUid, actorRole);
        }

        public void NotifyMany(IEnumerable<string> targetUids, NotificationEventType type, SmartContractModel contract, string actorUid, string actorRole)
        {
            _ = NotifyManyInternalAsync(targetUids, type, contract, actorUid, actorRole);
        }

        private async Task NotifyManyInternalAsync(IEnumerable<string> targetUids, NotificationEventType type, SmartContractModel contract, string actorUid, string actorRole)
        {
            if (targetUids == null) return;
            foreach (var uid in targetUids)
            {
                try
                {
                    await NotifyInternalAsync(uid, type, contract, actorUid, actorRole);
                    // If you want to be extra nice to quotas, add a tiny delay:
                    // await Task.Delay(25);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[CloudFunctionNotificationService] NotifyMany → uid:{uid} failed: {ex.Message}");
                }
            }
        }

        private async Task NotifyInternalAsync(string targetUid, NotificationEventType type, SmartContractModel contract, string actorUid, string actorRole)
        {
            try
            {
                if (string.IsNullOrEmpty(targetUid) || contract == null)
                {
                    Debug.LogWarning("⚠️ Notify called with missing targetUid or contract.");
                    return;
                }

                if (!_initialized) await InitAsync(); // ensure Functions is ready

                var payload = new Dictionary<string, object>
                {
                    { "targetUid", targetUid },
                    { "type",       type.ToString() },
                    { "actorUid",   actorUid ?? string.Empty },
                    { "actorRole",  actorRole ?? string.Empty },
                    { "contractId", contract.Id ?? string.Empty },
                    { "contractTitle", contract.Title ?? string.Empty },
                    { "amount",     contract.RewardAmount },
                    { "isSurprise", contract.IsSurprise },
                };

                //var callable = _functions.GetHttpsCallable("sendNotification");
                var callable = FirebaseInit.Functions.GetHttpsCallable("sendNotification");
                var result   = await callable.CallAsync(payload);
                LogCallableResult("[CloudFunctionNotificationService] sendNotification", result);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"❌ sendNotification failed: {e.Message}");
            }
        }

        private static void LogCallableResult(string label, HttpsCallableResult result)
        {
            var raw = result?.Data;

            if (raw is IDictionary dict)
            {
                int Sent()   => ToInt(dict["sent"]);
                int Failed() => ToInt(dict["failed"]);
                int Pruned() => ToInt(dict["pruned"]);
                string Msg() => dict.Contains("message") ? dict["message"]?.ToString() ?? "" : "";

                Debug.Log($"{label} ✅ OK → sent:{Sent()} failed:{Failed()} pruned:{Pruned()} msg:'{Msg()}'");
            }
            else
            {
                Debug.Log($"{label} OK (unparsed): {raw}");
            }

            static int ToInt(object o)
            {
                if (o == null) return 0;
                if (o is int i) return i;
                if (o is long l) return (int)l;
                return int.TryParse(o.ToString(), out var p) ? p : 0;
            }
        }
    }
}
