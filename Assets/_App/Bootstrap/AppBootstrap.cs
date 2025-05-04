using UnityEngine;
using System.Threading.Tasks;

namespace _App.Bootstrap
{
    public class AppBootstrap : MonoBehaviour
    {
        [SerializeField] private GameObject loadingScreen;
        private async void Awake()
        {
            DontDestroyOnLoad(gameObject);

            Debug.Log("🚀 App Bootstrap starting...");

            // Step 1: Initialize Firebase
            await FirebaseInit.WaitUntilReady();

            // Step 2: Initialize other systems (optional stubs for now)
            await InitializeServices();

            Debug.Log("✅ App Bootstrap complete.");
            
            // Load user profile if already signed in
            await UserSession.LoadCurrentUser();

            loadingScreen.SetActive(false);

            SceneLoader.LoadAppropriateScene(); // based on login/session
        }

        private async Task InitializeServices()
        {
            // Example of where you'd initialize other systems
            // await GameDataManager.Instance.InitializeAsync();
            // await AudioManager.Instance.InitializeAsync();

            await Task.Yield(); // placeholder for now
        }
        
        private void Update() //$$$$$$$$$$$$$$
        {
#if UNITY_EDITOR
            if (Input.GetKeyDown(KeyCode.F15))
            {
                Debug.Log("printscreen");
                string timeStamp = System.DateTime.Now.ToString("dd-MM-yyyy-HH-mm-ss");
                string fileName = "Screenshot" + timeStamp + ".png";
                ScreenCapture.CaptureScreenshot("D:\\Kid's Market\\Screenshots\\" + fileName);
            }
#endif
        }
    }
}