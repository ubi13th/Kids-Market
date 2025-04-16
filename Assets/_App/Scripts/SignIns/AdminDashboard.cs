using System;
using System.Collections;
using UnityEngine;
using Firebase.Auth;
using Firebase.Database;
using TMPro;
using System.Collections.Generic;
using Firebase;
using Firebase.Extensions;

public class AdminDashboard : MonoBehaviour
{
    private FirebaseAuth auth;
    private DatabaseReference dbRef;
    private Query childrenQuery;
    private string adminUID = "0";

    [Header("UI References")]
    [SerializeField] private Transform childListContainer;
    [SerializeField] private TextMeshProUGUI childrenListText;
    [SerializeField] private GameObject childEntryPrefab;

    [Header("Task Prefab")]
    [SerializeField] private GameObject taskEntryPrefab;

    private Dictionary<string, GameObject> childUIEntries = new();

    
    private async void Start()
    {
        await FirebaseInit.WaitUntilReady();

        auth = FirebaseInit.Auth;
        dbRef = FirebaseInit.DbRef;

        // Use Firebase safely

        TryStartListeningForChildren();
    }
    
    // private void Start()
    // {
    //     InitializeFirebase();
    //     //TryStartListeningForChildren();
    // }
    //
    // private void InitializeFirebase2()
    // {
    //     auth = FirebaseAuth.DefaultInstance;
    //     dbRef = FirebaseDatabase.DefaultInstance.RootReference;
    // }
    
    /*private void InitializeFirebase()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            var dependencyStatus = task.Result;
            if (dependencyStatus == Firebase.DependencyStatus.Available)
            {
                FirebaseApp app = FirebaseApp.DefaultInstance;

                auth = FirebaseAuth.DefaultInstance;

                // ✅ This line MUST be after Firebase is fully ready
                dbRef = FirebaseDatabase.GetInstance(app).RootReference;

                Debug.Log("Firebase Initialized Successfully.");

                TryStartListeningForChildren();
            }
            else
            {
                Debug.LogError($"Could not resolve Firebase dependencies: {dependencyStatus}");
            }
        });
    }*/

    private void TryStartListeningForChildren()
    {
        FirebaseUser user = auth.CurrentUser;
        if (user == null)
        {
            Debug.LogError("Admin not signed in.");
            childrenListText.text = "Not signed in.";
            return;
        }

        adminUID = user.UserId;

        if (string.IsNullOrEmpty(adminUID) || adminUID == "0")
        {
            Debug.LogError("Invalid adminUID. Skipping query.");
            return;
        }

        Debug.Log("Setting up query for adminUID: " + adminUID);

        try
        {
            var childrenRef = dbRef.Child("children");
            childrenQuery = childrenRef.OrderByChild("adminUID").EqualTo(adminUID);
            childrenQuery.ValueChanged += HandleChildrenChanged;
        }
        catch (Exception ex)
        {
            Debug.LogError("Exception during Firebase query setup: " + ex.Message);
        }
    }


    private void HandleChildrenChanged(object sender, ValueChangedEventArgs args)
    {
        Debug.Log("HandleChildrenChanged called.");
        
        if (args.DatabaseError != null)
        {
            Debug.LogError("Firebase error: " + args.DatabaseError.Message);
            childrenListText.text = "Failed to fetch children.";
            return;
        }
        
        Debug.Log("Snapshot Exists: " + args.Snapshot.Exists);
        Debug.Log("Children Count: " + args.Snapshot.ChildrenCount);

        ClearChildUI();

        if (!args.Snapshot.Exists || args.Snapshot.ChildrenCount == 0)
        {
            childrenListText.text = "No children linked to this account.";
            return;
        }

        DisplayChildren(args.Snapshot);
    }

    private void ClearChildUI()
    {
        foreach (var entry in childUIEntries.Values)
        {
            Destroy(entry);
        }
        childUIEntries.Clear();
    }

    private void DisplayChildren(DataSnapshot snapshot)
    {
        List<string> childNames = new();

        foreach (var childSnapshot in snapshot.Children)
        {
            string childId = childSnapshot.Key;
            string childName = childSnapshot.Child("name").Value?.ToString() ?? "Unnamed";

            Debug.Log($"Loaded child: {childId} - {childName}");

            GameObject entry = Instantiate(childEntryPrefab, childListContainer);
            entry.GetComponentInChildren<TextMeshProUGUI>().text = childName;
            childUIEntries[childId] = entry;
            childNames.Add(childName);

            AddTasksToChildUI(childSnapshot, entry.transform);
        }

        childrenListText.text = "Your Children:\n" + string.Join("\n", childNames);
    }

    private void AddTasksToChildUI(DataSnapshot childSnapshot, Transform entryTransform)
    {
        Transform taskContainer = entryTransform.Find("TaskContainer");
        if (taskContainer == null)
        {
            Debug.LogWarning("TaskContainer not found in prefab for child.");
            return;
        }

        var tasksSnapshot = childSnapshot.Child("tasks");
        if (!tasksSnapshot.Exists) return;

        foreach (var taskSnapshot in tasksSnapshot.Children)
        {
            string taskTitle = taskSnapshot.Child("title").Value?.ToString() ?? "Unnamed Task";
            bool isComplete = bool.TryParse(taskSnapshot.Child("isComplete").Value?.ToString(), out bool done) && done;

            GameObject taskEntry = Instantiate(taskEntryPrefab, taskContainer);
            taskEntry.GetComponentInChildren<TextMeshProUGUI>().text = (isComplete ? "✅ " : "🔲 ") + taskTitle;
        }
    }

    private void OnDestroy()
    {
        if (childrenQuery != null)
        {
            childrenQuery.ValueChanged -= HandleChildrenChanged;
        }
    }
}
