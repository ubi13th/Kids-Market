using UnityEngine;

namespace _App.Helpers
{
    public class ScreenShotTaker : MonoBehaviour
    {
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
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