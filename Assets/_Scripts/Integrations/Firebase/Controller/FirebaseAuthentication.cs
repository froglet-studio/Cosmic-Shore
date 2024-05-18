using System;
using Firebase.Auth;
using CosmicShore.Utility.Singleton;
using CosmicShore.Integrations.PlayFab.Event_Models;
using UnityEngine;

namespace CosmicShore.Integrations.Firebase.Controller
{
    public class FirebaseAuthentication : SingletonPersistent<FirebaseAuthentication>
    {
        // Player/User authentication
        private static FirebaseAuth _userAuthentication;
        
        // Developer authentication, recommended use: in UNITY_EDITOR directive
        private static FirebaseAuth _devAuthentication;

        private AuthMethods _firebaseAuthMethods;
        private string email;
        private string password;
        private FirebaseHelper _firebaseHelper = new();

        /// <summary>
        /// On Enable
        /// Set auth method to default and subscribe on dependency resolved event
        /// </summary>
        private void OnEnable()
        {
            // Set Firebase auth methods to default
            _firebaseAuthMethods = AuthMethods.Default;
            
            // Initialize authentication instance upon dependency resolved
            _firebaseHelper.DependencyResolved += OnDependencyResolved;
        }

        /// <summary>
        /// On Disable
        /// Set auth method to default and unsubscribe an dependency resolved event
        /// </summary>
        private void OnDisable()
        {
            _firebaseAuthMethods = AuthMethods.Default;
            _firebaseHelper.DependencyResolved -= OnDependencyResolved;
        }

        /// <summary>
        /// On Dependency Resolved Event
        /// Set auth method to anonymous and run default authentication
        /// <param name="sender">sender</param>
        /// <param name="eventArgs">event args</param>
        /// </summary>
        private void OnDependencyResolved()
        {
            _firebaseAuthMethods = AuthMethods.Anonymous;
            AuthenticateDefault();
        }

        /// <summary>
        /// Authenticate Default
        /// Default authentication on user or developer firebase instance
        /// </summary>
        private void AuthenticateDefault()
        {
            // #if UNITY_EDITOR
            // _devAuthentication = FirebaseAuth.DefaultInstance;
            // #else
            // _userAuthentication = FirebaseAuth.DefaultInstance;
            // #endif
            _userAuthentication = FirebaseAuth.DefaultInstance;
            AuthenticateDefault(_firebaseAuthMethods);
        }

        /// <summary>
        /// Authenticate Default Overload
        /// Choose authentication methods upon auth methods status
        /// </summary>
        /// <param name="authMethods"></param>
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
                    EmailLogin(email, password);
                    break;
                case AuthMethods.Register:
                    RegisterAccount(email, password);
                    break;
                default:
                    Debug.Log("The other authentication methods are coming soon.");
                    break;
            }
        }
        /// <summary>
        /// Default Login
        /// TODO: figure out the functionality later
        /// </summary>
        private void DefaultLogin()
        {
            Debug.Log($"{nameof(FirebaseAuthentication)} - Firebase default login method here.");
        }
        
        /// <summary>
        /// Anonymous Login
        /// </summary>
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

        /// <summary>
        /// Email Login
        /// </summary>
        /// <param name="email">email address</param>
        /// <param name="password">password</param>
        private void EmailLogin(string email, string password)
        {
            _userAuthentication.SignInWithEmailAndPasswordAsync(email, password).ContinueWith(task => {
                if (task.IsCanceled) {
                    Debug.LogError("SignInWithEmailAndPasswordAsync was canceled.");
                    return;
                }
                if (task.IsFaulted) {
                    Debug.LogError("SignInWithEmailAndPasswordAsync encountered an error: " + task.Exception);
                    return;
                }

                var result = task.Result;
                Debug.LogFormat("User signed in successfully: {0} ({1})",
                    result.User.DisplayName, result.User.UserId);
            });
        }

        /// <summary>
        /// Register Account
        /// </summary>
        /// <param name="email">email</param>
        /// <param name="password">password</param>
        private void RegisterAccount(string email, string password)
        {
            _userAuthentication.CreateUserWithEmailAndPasswordAsync(email, password).ContinueWith(task => {
                if (task.IsCanceled) {
                    Debug.LogError("CreateUserWithEmailAndPasswordAsync was canceled.");
                    return;
                }
                if (task.IsFaulted) {
                    Debug.LogError("CreateUserWithEmailAndPasswordAsync encountered an error: " + task.Exception);
                    return;
                }

                // Firebase user has been created.
                var result = task.Result;
                Debug.LogFormat("Firebase user created successfully: {0} ({1})",
                    result.User.DisplayName, result.User.UserId);
            });
        }
    }
    
    
}
