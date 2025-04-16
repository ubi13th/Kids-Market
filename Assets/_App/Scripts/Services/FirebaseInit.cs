using Firebase;
using Firebase.Auth;
using UnityEngine;
using System;
using System.Threading.Tasks;
using Firebase.Database;

public class FirebaseInit : MonoBehaviour
{
    public static FirebaseInit Instance { get; private set; }
    public static FirebaseAuth Auth { get; private set; }
    public static DatabaseReference DbRef { get; private set; }
    public static bool IsReady { get; private set; } = false;
    public static event Action OnFirebaseReady;

    async void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        await InitializeFirebase();
    }

    private async Task InitializeFirebase2()
    {
        Debug.Log("Checking Firebase dependencies...");
        var dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();

        if (dependencyStatus == DependencyStatus.Available)
        {
            Auth = FirebaseAuth.DefaultInstance;
            IsReady = true;
            Debug.Log("✅ Firebase Initialized");
            OnFirebaseReady?.Invoke();
        }
        else
        {
            Debug.LogError("❌ Firebase Dependency Error: " + dependencyStatus);
        }
    }
    
    private async Task InitializeFirebase()
    {
        Debug.Log("🔄 Checking Firebase dependencies...");
        var dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();

        if (dependencyStatus == DependencyStatus.Available)
        {
            FirebaseApp app = FirebaseApp.DefaultInstance;

            Auth = FirebaseAuth.DefaultInstance;
            try
            {
                var dbUrl = "https://kids-market-e481b-default-rtdb.firebaseio.com";
                DbRef = FirebaseDatabase.GetInstance(app, dbUrl).RootReference;
            }
            catch (Exception ex)
            {
                Debug.LogError("🔥 FirebaseDatabase crash reason: " + ex);
            }
            
            IsReady = true;
            Debug.Log("✅ Firebase Initialized");

            OnFirebaseReady?.Invoke();
        }
        else
        {
            Debug.LogError("❌ Firebase Dependency Error: " + dependencyStatus);
        }
    }
    
    // Optional: For scripts using async/await pattern
    public static async Task WaitUntilReady()
    {
        while (!IsReady)
        {
            await Task.Yield();
        }
    }
}
