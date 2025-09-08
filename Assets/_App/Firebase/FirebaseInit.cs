using Firebase;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Functions;
using UnityEngine;
using System;
using System.Threading.Tasks;

namespace _App.Bootstrap
{
    public class FirebaseInit : MonoBehaviour
    {
        public static FirebaseInit Instance { get; private set; }

        public static FirebaseAuth Auth { get; private set; }
        public static DatabaseReference DbRef { get; private set; }
        private static FirebaseApp App { get; set; }
        
        public static FirebaseFunctions Functions { get; private set; }
        private const string Region = "us-central1";
        
        public static bool IsReady { get; private set; }
        public static event Action OnFirebaseReady;

        // If google-services.json already has the DB URL, this can stay as a fallback.
        private string databaseUrl = "https://kids-market-e481b-default-rtdb.firebaseio.com";

        private async void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            await InitializeFirebaseAsync();
        }

        private async Task InitializeFirebaseAsync()
        {
            Debug.Log("🔄 Checking Firebase dependencies...");
            var dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (dependencyStatus != DependencyStatus.Available)
            {
                Debug.LogError($"❌ Firebase dependency error: {dependencyStatus}");
                return;
            }

            App = FirebaseApp.DefaultInstance;

            try
            {
                Auth = FirebaseAuth.DefaultInstance;

                // ✅ Works with both older/newer Database APIs:
                DbRef = ResolveDbRoot(App, databaseUrl);
                //DbRef = FirebaseDatabase.GetInstance(App, databaseUrl).RootReference;
                
                Functions = FirebaseFunctions.GetInstance(App, Region);

                IsReady = true;
                Debug.Log("✅ Firebase Initialized");
                OnFirebaseReady?.Invoke(); // we awaited, so this runs on main thread in practice
            }
            catch (Exception ex)
            {
                Debug.LogError("🔥 Firebase initialization failed: " + ex);
            }
        }

        // Async-safe hook for other services
        public static async Task WaitUntilReady()
        {
            while (!IsReady) { await Task.Yield(); }
        }

        // ✅ Avoid property pattern; widest compatibility
        public static string CurrentUserId =>
            (Auth != null && Auth.CurrentUser != null) ? Auth.CurrentUser.UserId : string.Empty;

        // ---- Helpers ----
        private static DatabaseReference ResolveDbRoot(FirebaseApp app, string url)
        {
            try
            {
                // Newer SDKs: FirebaseDatabase.GetInstance(app, url)
                var mi = typeof(FirebaseDatabase).GetMethod(
                    "GetInstance", new Type[] { typeof(FirebaseApp), typeof(string) });
                if (mi != null)
                {
                    var db = (FirebaseDatabase)mi.Invoke(null, new object[] { app, url });
                    return db.RootReference;
                }
            }
            catch { /* fall through */ }

            try
            {
                // Older SDKs: GetInstance(app) + GetReferenceFromUrl(url)
                var db = FirebaseDatabase.GetInstance(app);
                if (!string.IsNullOrEmpty(url))
                    return db.GetReferenceFromUrl(url);
                return db.RootReference;
            }
            catch
            {
                // Last resort
                return FirebaseDatabase.DefaultInstance.RootReference;
            }
        }
    }
}
