using Firebase;
using Firebase.Auth;
using Firebase.Database;
using UnityEngine;
using System;
using System.Threading.Tasks;
using Firebase.Extensions;

namespace _App.Bootstrap
{
    public class FirebaseInit : MonoBehaviour
    {
        public static FirebaseInit Instance { get; private set; }

        public static FirebaseAuth Auth { get; private set; }
        public static DatabaseReference DbRef { get; private set; }
        public static FirebaseApp App { get; private set; }

        public static bool IsReady { get; private set; }

        public static event Action OnFirebaseReady;

        [Header("Config")]
        [SerializeField] private string databaseUrl = "https://kids-market-e481b-default-rtdb.firebaseio.com";

        private async void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

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
                DbRef = FirebaseDatabase.GetInstance(App, databaseUrl).RootReference;
                
                IsReady = true;
                Debug.Log("✅ Firebase Initialized");
                OnFirebaseReady?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError("🔥 Firebase initialization failed: " + ex);
            }
        }

        // Async-safe hook for other services
        public static async Task WaitUntilReady()
        {
            while (!IsReady)
            {
                await Task.Yield(); // non-blocking wait
            }
        }
    }
}