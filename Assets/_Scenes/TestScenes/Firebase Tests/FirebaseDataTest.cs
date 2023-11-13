using System;
using System.Collections;
using Firebase;
using CosmicShore.Utility.Singleton;
using Firebase.Analytics;
using UnityEngine;

namespace Scenes.TestScenes.Firebase_Tests
{
    public class FirebaseDataTest : SingletonPersistent<FirebaseDataTest>
    {
        
        private const string TestName = nameof(FirebaseDataTest);
        private FirebaseApp _app;
        
        private void Start()
        {
            InitFirebaseAuth();
        }

        private void OnEnable()
        {
            LogEventLevelStart();
        }

        private void OnDisable()
        {
            LogEventLevelEnd();
        }

        private void InitFirebaseAuth()
        {
            FirebaseAuthTest.Instance.FirebaseInitialized.AddListener(OnFirebaseInitialized);
        }

        private void OnFirebaseInitialized()
        {
            Debug.Log("Firebase dependency checked and initialized. Now you can do your shit here.");
            _app = FirebaseApp.DefaultInstance;
            FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
            // LogEventNoParameters(); // Didn't appear in analytics dashboard
            // StartCoroutine(LogEventOneParameter()); // Doesn't quite work
            LogEventFloatParameter();
        }

        private void LogEventNoParameters()
        {
            FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventLogin);
            Debug.LogFormat("{0} logged an anonymous event.", TestName);
        }

        IEnumerator LogEventOneParameter()
        {
            for (int i = 0; i != 10; i++)
            {
                FirebaseAnalytics.LogEvent("level_complete");
                Debug.LogFormat("{0} logged a named event at {1} second.", TestName, (i+1).ToString());
                yield return new WaitForSeconds(1);
            }
        }
        private void LogEventFloatParameter()
        {
            FirebaseAnalytics.LogEvent("progress", "percent", 0.4f);
            Debug.LogFormat("{0} logged an float parameter event.", TestName);
        }

        private void LogEventLevelStart()
        {
            Debug.Log("Firebase logs an event on level start.");
            FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventLevelStart);
        }

        private void LogEventLevelEnd()
        {
            Debug.Log("Firebase logs an event on level end.");
            FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventLevelEnd);
        }
    }
}
