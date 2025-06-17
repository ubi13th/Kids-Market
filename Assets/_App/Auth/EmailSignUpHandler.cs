using System;
using UnityEngine;
using TMPro;

namespace _App.Scripts.SignIns
{
    public class EmailSignUpHandler : MonoBehaviour
    {
        public AdminAuthHandler authHandler;

        public TMP_InputField emailField;
        public TMP_InputField passwordField;
        public TextMeshProUGUI statusText;

        private void Start()
        {
            emailField.onSelect.AddListener(_ => ActivateCaret(emailField));
            passwordField.onSelect.AddListener(_ => ActivateCaret(passwordField));
        }

        private void ActivateCaret(TMP_InputField field)
        {
            field.ActivateInputField();
            field.caretPosition = field.text.Length;
        }
        
        public void OnSignUpButtonPressed()
        {
            var email = emailField.text.Trim();
            var password = passwordField.text;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                statusText.text = "Please fill in all fields.";
                return;
            }

            authHandler.SignUp(email, password);
        }
    }
}