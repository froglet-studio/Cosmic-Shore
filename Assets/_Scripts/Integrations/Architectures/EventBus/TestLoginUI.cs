using System;
using System.Security;
using CosmicShore.Integrations.PlayFab.Authentication;
using PlayFab;
using UnityEngine;
using UnityEngine.Events;

namespace CosmicShore.Integrations.Architectures.EventBus
{
    public class TestLoginUI : MonoBehaviour
    {
        // private static readonly LoginEventBus LoginEventBus = new();
        // Start is called before the first frame update
        private void OnDisable()
        {
            LoginEventBus.Unsubscribe(LoginType.Anonymous, GetAction(LoginType.Anonymous));
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
            GUILayout.Label(GetText(LoginType.Anonymous));
            GUILayout.Space(10);
        }

        private void AddButton(LoginType loginType)
        {
            if (GUILayout.Button(GetText(loginType)))
            {
                // Do login stuff here
            }
            GUILayout.Space(10);
        }

        private static string GetText(LoginType loginType)
        {
            return loginType switch
            {
                LoginType.Anonymous => "Anonymous",
                LoginType.Email => "Email Login",
                LoginType.Username => "Username Login",
                LoginType.Other => "Undefined Login",
                _ => "Unknown Nightmare"
            };
        }

        private static UnityAction GetAction(LoginType loginType)
        {
            return loginType switch
            {
                LoginType.Anonymous => AnonymousLogin,
                LoginType.Email => EmailLogin,
                _ => NoneAction
            };
        }

        private static void AnonymousLogin()
        {
            Debug.Log("TestLoginUI - AnonymousLogin()");
        }

        private static void EmailLogin()
        {
            Debug.Log("TestLoginUI - EmailLogin()");
        }

        private static void NoneAction()
        {
            Debug.Log("TestLoginUI - NoneAction()");
        }
    }
}
