using UnityEngine;
using TMPro;

namespace _App.Scripts.SignIns
{
    public class EmailSignUpHandler : MonoBehaviour
    {
        public FirebaseAuthHandler authHandler;

        public TMP_InputField emailField;
        public TMP_InputField passwordField;
        public TMP_InputField displayNameField;

        public void OnSignUpButtonPressed()
        {
            var email = emailField.text;
            var password = passwordField.text;
            var displayName = displayNameField.text;
            
            authHandler.SignUp(email, password, displayName);
        }
    }
}