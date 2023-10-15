using System;
using Firebase.Auth;
using StarWriter.Utility.Singleton;
using _Scripts._Core.Playfab_Models.Event_Models;
using UnityEngine;

namespace _Scripts._Core.Firebase.Controller
{
    public class FirebaseAuthentication : SingletonPersistent<FirebaseAuthentication>
    {
        // Player/User authentication
        private static FirebaseAuth _userAuthentication;
        
        // Developer authentication, recommended use: in UNITY_EDITOR directive
        private static FirebaseAuth _devAuthentication;

        private AuthMethods _firebaseAuthMethods;

        private void OnEnable()
        {
            _firebaseAuthMethods = AuthMethods.Default;
            FirebaseHelper.OnDependencyResolved += OnDependencyResolved;
        }

        private void OnDisable()
        {
            FirebaseHelper.OnDependencyResolved -= OnDependencyResolved;
        }

        private void OnDependencyResolved(object sender, EventArgs eventArgs)
        {
            _firebaseAuthMethods = AuthMethods.Anonymous;
            AuthenticateDefault();
        }

        private void AuthenticateDefault()
        {
            #if UNITY_EDITOR
            _devAuthentication = FirebaseAuth.DefaultInstance;
            #else
            _userAuthentication = FirebaseAuth.DefaultInstance;
            #endif
            AuthenticateDefault(_firebaseAuthMethods);
        }

        private void AuthenticateDefault(AuthMethods authMethods)
        {
            switch (authMethods)
            {
                case AuthMethods.Default:
                    DefaultLogin();
                    break;
                case AuthMethods.Anonymous:
                    AnonymousLogin();
                    break;
                case AuthMethods.EmailLogin:
                    EmailLogin();
                    break;
                case AuthMethods.Register:
                    RegisterAccount();
                    break;
                default:
                    Debug.Log("The other authentication methods are coming soon.");
                    break;
            }
        }

        private void DefaultLogin()
        {
            Debug.Log($"{nameof(FirebaseAuthentication)} - Firebase default login method here.");
        }

        private void AnonymousLogin()
        {
            _userAuthentication.SignInAnonymouslyAsync().ContinueWith(task => {
                if (task.IsCanceled) {
                    Debug.LogError("SignInAnonymouslyAsync was canceled.");
                    return;
                }
                if (task.IsFaulted) {
                    Debug.LogError("SignInAnonymouslyAsync encountered an error: " + task.Exception);
                    return;
                }

                var result = task.Result;
                Debug.LogFormat("User signed in successfully: {0} ({1})",
                    result.User.DisplayName, result.User.UserId);
            });
        }

        private void EmailLogin()
        {
            
        }

        private void RegisterAccount()
        {
            
        }
    }
    
    
}
