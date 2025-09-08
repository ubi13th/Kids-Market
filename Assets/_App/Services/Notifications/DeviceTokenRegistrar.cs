// Assets/_App/Services/Notifications/DeviceTokenRegistrar.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using _App.Bootstrap;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Messaging;
using Firebase.Extensions;
using UnityEngine;

public class DeviceTokenRegistrar : MonoBehaviour
{
    private static DeviceTokenRegistrar _instance;
    private string _lastSavedKey;
    private bool _hooksSet;

    void Awake()
    {
        if (_instance != null) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        TokenOwner.OnChanged += OnTokenOwnerChanged;

        if (FirebaseInit.IsReady) 
            Attach();
        else 
            FirebaseInit.OnFirebaseReady += Attach;
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;

        FirebaseInit.OnFirebaseReady -= Attach;
        TokenOwner.OnChanged -= OnTokenOwnerChanged;

        if (_hooksSet)
        {
#if !UNITY_EDITOR && !UNITY_STANDALONE
            FirebaseMessaging.TokenReceived -= OnTokenReceived;
#endif
            if (FirebaseInit.IsReady) 
                FirebaseInit.Auth.StateChanged -= OnAuthStateChanged;
            _hooksSet = false;
        }
    }

    // ✨ Non-async handler — no CS1998
    private void Attach()
    {
        if (!_hooksSet)
        {
            if (FirebaseInit.IsReady) 
                FirebaseInit.Auth.StateChanged += OnAuthStateChanged;
#if !UNITY_EDITOR && !UNITY_STANDALONE
            FirebaseMessaging.TokenReceived += OnTokenReceived;
#endif
            _hooksSet = true;
        }

#if UNITY_EDITOR || UNITY_STANDALONE
        // Nothing to do on non-mobile
        Debug.Log("DeviceTokenRegistrar: skipping token registration on non-mobile platform.");
        return;
#else
        _ = TryFetchAndSaveCurrentTokenAsync(); // fire-and-forget
#endif
    }

    private void OnTokenOwnerChanged(string _ignored)
    {
        _lastSavedKey = null;
        _ = TryFetchAndSaveCurrentTokenAsync();
    }

    // ✨ Non-async event signature — we just kick off the Task
    private void OnAuthStateChanged(object sender, EventArgs e)
    {
        _ = TryFetchAndSaveCurrentTokenAsync();
    }

#if !UNITY_EDITOR && !UNITY_STANDALONE
    private void OnTokenReceived(object s, TokenReceivedEventArgs e)
    {
        if (IsInvalid(e.Token)) { Debug.Log("TokenReceived: ignored placeholder."); return; }
        SaveOrUpdateToken(e.Token);
    }
#endif

    // ✨ All awaits live here
    private async Task TryFetchAndSaveCurrentTokenAsync()
    {
#if !UNITY_EDITOR && !UNITY_STANDALONE
        try
        {
            var token = await FirebaseMessaging.GetTokenAsync();
            if (!IsInvalid(token)) SaveOrUpdateToken(token);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"GetTokenAsync failed: {e.Message}");
        }
#endif
    }

    private static bool IsInvalid(string token)
        => string.IsNullOrEmpty(token) || token == "StubToken" || token.Length < 30;

    private static string Base64Url(string s)
    {
        var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(s));
        return b64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private void SaveOrUpdateToken(string token)
    {
        var ownerUid = TokenOwner.Resolve();
        if (string.IsNullOrEmpty(ownerUid))
        {
            Debug.LogWarning("SaveOrUpdateToken: no owner UID yet; skipping.");
            return;
        }
        if (FirebaseInit.DbRef == null)
        {
            Debug.LogWarning("SaveOrUpdateToken: Firebase DbRef is null; skipping.");
            return;
        }

        var tokensRef = FirebaseInit.DbRef.Child(AppConstants.DeviceTokens).Child(ownerUid);
        var key = Base64Url(token);
        if (_lastSavedKey == key) { Debug.Log("Token unchanged; skipping re-save."); return; }
        _lastSavedKey = key;

        var entry = new Dictionary<string, object> {
            { "token", token },
            { "ts", ServerValue.Timestamp },
            { "platform", Application.platform.ToString() },
            { "deviceModel", SystemInfo.deviceModel },
        };

        tokensRef.Child(_lastSavedKey).SetValueAsync(entry).ContinueWithOnMainThread(t =>
        {
            if (t.IsFaulted || t.IsCanceled)
            {
                Debug.LogWarning($"Token write failed: {t.Exception}");
                return;
            }
            Debug.Log($"✅ Token saved: {AppConstants.DeviceTokens}/{ownerUid}/{_lastSavedKey}");
            
            _ = ClaimTokenGloballyAsync(ownerUid, _lastSavedKey);
            
            PruneBadAndOldTokens(tokensRef, keepMostRecent: 5);
        });
    }
    
    private async Task ClaimTokenGloballyAsync(string ownerUid, string tokenKey)
    {
        var root = FirebaseInit.DbRef.Child(AppConstants.DeviceTokens);
        var snap = await root.GetValueAsync();
        if (!snap.Exists) return;

        foreach (var userNode in snap.Children)
        {
            var otherUid = userNode.Key;
            if (otherUid == ownerUid) continue;
            if (userNode.HasChild(tokenKey))
            {
                await root.Child(otherUid).Child(tokenKey).RemoveValueAsync();
                Debug.Log($"🔒 Claimed token {tokenKey} for {ownerUid}; removed from {otherUid}");
            }
        }
    }


    private void PruneBadAndOldTokens(DatabaseReference tokensRef, int keepMostRecent)
    {
        tokensRef.GetValueAsync().ContinueWithOnMainThread(t =>
        {
            if (t.IsFaulted || !t.IsCompleted || t.Result == null || !t.Result.Exists) return;

            var snap = t.Result;
            var toRemove = new List<string>();
            var valid = new List<(string key, long ts)>();

            foreach (var child in snap.Children)
            {
                string key = child.Key;
                if (child.Value is Dictionary<string, object> obj)
                {
                    string tok = obj.TryGetValue("token", out var tv) ? tv?.ToString() : null;
                    long ts = obj.TryGetValue("ts", out var tsv) && long.TryParse(tsv?.ToString(), out var l) ? l : 0;
                    if (IsInvalid(tok)) { toRemove.Add(key); continue; }
                    valid.Add((key, ts));
                }
                else if (child.Value is bool b && b == true)
                {
                    if (IsInvalid(key)) { toRemove.Add(key); continue; }
                    valid.Add((key, 0));
                }
                else
                {
                    toRemove.Add(key);
                }
            }

            if (keepMostRecent > 0 && valid.Count > keepMostRecent)
            {
                foreach (var old in valid.OrderByDescending(v => v.ts).Skip(keepMostRecent))
                    toRemove.Add(old.key);
            }

            if (toRemove.Count == 0) return;

            var updates = toRemove.Distinct().ToDictionary(k => k, _ => (object)null);
            tokensRef.UpdateChildrenAsync(updates).ContinueWithOnMainThread(done =>
            {
                if (done.IsFaulted || done.IsCanceled)
                    Debug.LogWarning($"Prune failed: {done.Exception}");
                else
                    Debug.Log($"🧹 Pruned {toRemove.Count} token entr{(toRemove.Count == 1 ? "y" : "ies")}.");
            });
        });
    }
}
