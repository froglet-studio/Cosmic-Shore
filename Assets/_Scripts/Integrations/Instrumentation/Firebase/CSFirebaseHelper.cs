#if !UNITY_WEBGL
using Firebase;
using Firebase.Analytics;
using UnityEngine;

namespace CosmicShore.Integrations.Instrumentation.Firebase
{
    public class CSFirebaseHelper
    {
        // Firebase dependency status flag, mark as available when dependencies are resolved
        private static DependencyStatus _dependencyStatus;
    
        /// <summary>
        /// Initialize dependency status
        /// The status is marked as Unavailable from the start
        /// </summary>
        public static void InitializeDependencyStatus()
        {
            _dependencyStatus = DependencyStatus.UnavailableOther;
        }

        public static void Set(bool isCollectable)
        {
            FirebaseAnalytics.SetAnalyticsCollectionEnabled(isCollectable);
        }
        /// <summary>
        /// Check Dependencies
        /// Check for necessary dependencies and try to resolve them for Android and IOS
        /// </summary>
        public static void CheckDependencies()
        {
#if UNITY_64
            CheckPcDependencies();
#endif
#if UNITY_ANDROID
            CheckAndroidDependencies();
#endif

#if UNITY_IOS
            CheckIOSDependencies();
#endif
        }

        private static void CheckPcDependencies()
        {
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(
                fixTask =>
                {
                    if (fixTask.IsCompleted)
                    {
                        _dependencyStatus = fixTask.Result;

                        if (_dependencyStatus == DependencyStatus.Available)
                        {
                            Debug.Log("Firebase PC dependencies are available.");
                        }
                        else
                        {
                            Debug.LogError($"Firebase dependencies are not resolved: {_dependencyStatus}");
                            Debug.LogWarning("Please ensure Firebase SDKs are correctly imported and configured.");
                        }
                    }
                    else
                    {
                        Debug.LogError("Failed to check Firebase dependencies.");
                    }
                });
        }
        /// <summary>
        /// Check Android Dependencies 
        /// Check for necessary dependencies and try to resolve them for Android
        /// </summary>
        private static void CheckAndroidDependencies()
        {
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(
                fixTask =>
                {
                    if (fixTask.IsCompleted)
                    {
                        _dependencyStatus = fixTask.Result;

                        if (_dependencyStatus == DependencyStatus.Available)
                        {
                            Debug.Log("Firebase Android dependencies are available.");
                        }
                        else
                        {
                            Debug.LogError($"Firebase dependencies are not resolved: {_dependencyStatus}");
                            Debug.LogWarning("Please ensure Firebase SDKs are correctly imported and configured.");
                        }
                    }
                    else
                    {
                        Debug.LogError("Failed to check Firebase dependencies.");
                    }
                });
        }

        /// <summary>
        /// Check iOS Dependencies 
        /// Check for necessary dependencies and try to resolve them for iOS
        /// </summary>
        private static void CheckIOSDependencies()
        {
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(
                fixTask =>
                {
                    if (fixTask.IsCompleted)
                    {
                        _dependencyStatus = fixTask.Result;

                        if (_dependencyStatus == DependencyStatus.Available)
                        {
                            Debug.Log("Firebase IOS dependencies are available.");
                        }
                        else
                        {
                            Debug.LogError($"Firebase dependencies are not resolved: {_dependencyStatus}");
                            Debug.LogWarning("Please ensure Firebase SDKs are correctly imported and configured.");
                        }
                    }
                    else
                    {
                        Debug.LogError("Failed to check Firebase dependencies.");
                    }
                });
        }
    }
}
#endif