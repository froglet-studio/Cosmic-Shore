using System;
using System.Threading.Tasks;
using Firebase;
using Firebase.Analytics;
using Firebase.Auth;
using JetBrains.Annotations;
using StarWriter.Utility.Singleton;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Events;
using UnityEngine.Rendering;

namespace Scenes.TestScenes.Firebase_Tests
{
    public class FirebaseTest : SingletonPersistent<FirebaseTest>
    {
        private FirebaseAuth _auth
        {
            get => _auth;
            set => _auth = value ?? FirebaseAuth.DefaultInstance;
        }
        
        private FirebaseApp _app
        {
            get => _app;
            set => _app = value ?? FirebaseApp.DefaultInstance;
        }
        
        public UnityEvent FirebaseInitialized = new();
        private void Start()
        {
            CheckAndFixDependencies();
            // CheckAndFixAlt();
            // AuthAfterDependencyCheck();
            // GetSuccesses();
        }

        private void GetSuccesses()
        {
            var successes = PlayerPrefs.GetInt("Successes", 0);
            Debug.Log($"successes: {successes.ToString()}");
            PlayerPrefs.SetInt("Successes", ++successes);
            Debug.Log($"Successes after {successes}");
        }

        private void OnEnable()
        {
            FirebaseInitialized.AddListener(OnFirebaseInitialized);
        }

        private void OnDisable()
        {
            // FirebaseInitialized.RemoveListener(OnFirebaseInitialized);
            FirebaseInitialized.RemoveAllListeners();
        }

        private void CheckAndFixDependencies()
        {
            var taskScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(
                fixTask =>
                {
                    Assert.IsNull(fixTask.Exception);
                    Debug.Log("Authenticating");
                    var auth = FirebaseAuth.DefaultInstance;
                    auth.SignInAnonymouslyAsync().ContinueWith(
                        authTask =>
                        {
                            Debug.Log("Starting anonymous login.");
                            Assert.IsNull(authTask.Exception);
                            Debug.Log("Signed in!");
                            
                            var successes = PlayerPrefs.GetInt("Successes", 0);
                            PlayerPrefs.SetInt("Successes", ++successes);
                            Debug.Log($"Successes after {successes}");
                            
                            auth.SignOut();
                            Debug.Log("Signed Out.");
                        }, taskScheduler);
                });
        }

        private void CheckAndFixAlt()
        {
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(
                fixTask =>
                {
                    FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
                    Debug.Log("Analytics enabled.");
                });
        }

        private async void AuthAfterDependencyCheck()
        {
            var dependencyResult = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (dependencyResult == DependencyStatus.Available)
            {
                _auth = FirebaseAuth.DefaultInstance;
                FirebaseInitialized?.Invoke();
            }
            else
            {
                Debug.LogError($"Failed to initialize Firebase with {dependencyResult}");
            }
        }

        private void OnFirebaseInitialized()
        {
            Debug.Log($"Firebase initialized. Now let's do some cool stuff");
        }

        private void LinkWithDeviceIdentifier()
        {
            var taskScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            var credential = Firebase.Auth.
        }
    }
}

