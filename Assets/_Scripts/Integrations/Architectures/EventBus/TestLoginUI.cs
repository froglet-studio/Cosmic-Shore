using System;
using CosmicShore.Integrations.PlayFab.Authentication;
using UnityEngine;
using UnityEngine.Events;

namespace CosmicShore.Integrations.Architectures.EventBus
{
    public class TestLoginUI : MonoBehaviour
    {
        // private static readonly LoginEventBus LoginEventBus = new();
        // Start is called before the first frame update
        private static string _displayMessage = String.Empty;
        private void OnEnable()
        {
            LoginEventBus.Subscribe(LoginType.Success, GetAction(LoginType.Success));
            LoginEventBus.Subscribe(LoginType.Fail, GetAction(LoginType.Fail));
        }

        private void OnDisable()
        {
            LoginEventBus.Unsubscribe(LoginType.Success, GetAction(LoginType.Success));
            LoginEventBus.Unsubscribe(LoginType.Fail, GetAction(LoginType.Fail));
        }

        private void OnGUI()
        {
            AddWindow("Login Window", LoginWindow);
        }

        private void AddWindow(string windowTitle, GUI.WindowFunction windowFunction)
        {
            GUILayout.Window(0, new Rect(0, 0, 300, 0), windowFunction, windowTitle);
        }

        private void LoginWindow(int windowID)
        {
            GUILayout.Label("Who are you");
            GUILayout.Space(10);

            if (GUILayout.Button("Sign In"))
            {
                AuthenticationManager.Instance.AnonymousLogin();
            }
            
            GUILayout.Space(10);
            
            if (!string.IsNullOrEmpty(_displayMessage))
            {
                GUILayout.Label(_displayMessage);
            }
            
            GUILayout.Space(10);
        }

        private static string GetText(LoginType loginType)
        {
            return loginType switch
            {
                LoginType.Success => "Successful logged in.",
                LoginType.Fail => "Had problem logging in.",
                _ => "Unknown Nightmare"
            };
        }

        private static UnityAction GetAction(LoginType loginType)
        {
            return loginType switch
            {
                LoginType.Success => DisplayLoginMessage,
                LoginType.Fail => DisplayErrorMessage,
                _ => NoneAction
            };
        }

        private static void DisplayLoginMessage()
        {
            _displayMessage = GetText(LoginType.Success);
        }

        private static void DisplayErrorMessage()
        {
            _displayMessage = GetText(LoginType.Fail);
        }

        private static void NoneAction()
        {
            _displayMessage = GetText(LoginType.Other);
        }
    }
}
