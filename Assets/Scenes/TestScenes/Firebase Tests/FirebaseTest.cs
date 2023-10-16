using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Firebase;
using Firebase.Analytics;
using Firebase.Auth;
using StarWriter.Utility.Singleton;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Events;

namespace Scenes.TestScenes.Firebase_Tests
{
    public class YieldTask : CustomYieldInstruction
    {
        public YieldTask(Task task)
        {
            Task = task;
        }

        public override bool keepWaiting => !Task.IsCanceled;
        public Task Task { get; }
    }
    public class FirebaseTest : SingletonPersistent<FirebaseTest>
    {
        private FirebaseAuth _auth;

        private FirebaseApp _app;
        public UnityEvent FirebaseInitialized = new();

        private Queue<Action> _actionQueue = new();
        private void Start()
        {
            // CheckAndFixDependencies();
            // CheckAndFixAlt();
            // AuthAfterDependencyCheck(); // works dandy
            // GetSuccesses(); // works
            // await CheckFixAndAuth();// Crashes Unity, don't recommend
            // StartCoroutine(DoTheThing());// Doesn't quite work
            // QueueActions(); // works as well
            AnonymousLogin(); // works, only returns user id, no user name (of course it's not set)
            
        }

        private void Update()
        {
            // UpdateWithAction();
        }

        private void AnonymousLogin()
        {
            _auth = FirebaseAuth.DefaultInstance;

            _auth.SignInAnonymouslyAsync().ContinueWith(
                task =>
                {
                    if (task.IsCanceled)
                    {
                        Debug.LogError("Anonymous login was canceled.");
                        return;
                    }

                    if (task.IsFaulted)
                    {
                        Debug.LogError($"Anonymous login encountered an error {task.Exception}");
                        return;
                    }

                    var result = task.Result;
                    if (result != null)
                        Debug.LogFormat("User signed in successfully: {0} - {1}", result.User.DisplayName,
                            result.User.UserId);
                });
        }

        private void UpdateWithAction()
        {
            while (_actionQueue.Any())
            {
                Action action;
                lock (_actionQueue)
                {
                    action = _actionQueue.Dequeue();
                }

                action();
            }
        }

        private void EnqueueAction(Action action)
        {
            lock (_actionQueue)
            {
                _actionQueue.Enqueue(action);
            }
        }

        private void QueueActions()
        {
            Debug.Log("Checking Dependencies");
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(fixTask =>
            {
                Assert.IsNull(fixTask.Exception);
                Debug.Log("Authenticating");
                _auth = FirebaseAuth.DefaultInstance;
                _auth.SignInAnonymouslyAsync().ContinueWith(authTask =>
                {
                    EnqueueAction(() =>
                    {
                        Assert.IsNull(authTask.Exception);
                        Debug.Log("Welcome!");
                        GetSuccesses();
                        _auth.SignOut();
                        Debug.Log("Fare thee well.");
                    });
                });
            });
        }

        private IEnumerator DoTheThing()
        {
            Debug.Log("Checking Dependencies.");
            yield return new YieldTask(FirebaseApp.CheckAndFixDependenciesAsync());
            
            Debug.Log("Authenticating");
            _auth = FirebaseAuth.DefaultInstance;
            yield return new YieldTask(_auth.SignInAnonymouslyAsync());
            
            Debug.Log("Welcome!");
            
            // GetSuccesses();
            
            _auth.SignOut();
            Debug.Log("Fare thee well!");
        }

        private async Task CheckFixAndAuth()
        {
            Debug.Log("Checking Dependencies.");
            await FirebaseApp.CheckAndFixDependenciesAsync();
            
            Debug.Log("Authenticating...");
            _auth = FirebaseAuth.DefaultInstance;
            await _auth.SignInAnonymouslyAsync();
            
            Debug.Log("Signed in!");
            
            GetSuccesses();
            
            _auth.SignOut();
            Debug.Log("Signed out!");
            
        }

        private void GetSuccesses()
        {
            var successes = PlayerPrefs.GetInt("Successes", 0);
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
            // var credential = Firebase.Auth.
        }
    }
}

