#if !UNITY_WEBGL
using System.Threading.Tasks;
using CosmicShore.Integrations.Instrumentation.Interfaces;
using Firebase;
using Firebase.Analytics;
using UnityEngine;

namespace CosmicShore.Integrations.Instrumentation.Firebase
{
    public class CSUtilitiesFirebase : IAnalyzable
    {
        // Firebase dependency status flag, mark as available when dependencies are resolved
        private static DependencyStatus _dependencyStatus;

        /// <summary>
        /// IAnalyzable interface
        /// All SDK initialization happens here
        /// </summary>
        public void InitSDK()
        {
            InitializeDependencyStatus();
            CheckDependencies();
            Set(true);
            SetUserId();
        }

        /// <summary>
        /// Initialize dependency status
        /// The status is marked as Unavailable from the start
        /// </summary>
        private static void InitializeDependencyStatus()
        {
            _dependencyStatus = DependencyStatus.UnavailableOther;
        }

        /// <summary>
        /// Turn data collection on and off
        /// If isCollectable is true, data collection is enabled.
        /// If isCollectable is false, data collection is disabled.
        /// </summary>
        /// <param name="isCollectable"></param>
        private static void Set(bool isCollectable)
        {
            FirebaseAnalytics.SetAnalyticsCollectionEnabled(isCollectable);
        }

        private static void SetUserId()
        {
            FirebaseAnalytics.SetUserId(SystemInfo.deviceUniqueIdentifier);
        }

        public static async Task<string> GetSessionIdAsync()
        {
            var sessionId = await FirebaseAnalytics.GetSessionIdAsync();
            return sessionId.ToString();
        }

        /// <summary>
        /// Check Dependencies
        /// Check for necessary dependencies and try to resolve them for Android and IOS
        /// </summary>
        private static void CheckDependencies()
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

        /// <summary>
        /// Check PC Standalone Dependencies 
        /// Check for necessary dependencies for PC Standalone and try to resolve them for Android 
        /// </summary>
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
                            Debug.Log("Firebase for PC (Standalone) dependencies are available.");
                        }
                        else
                        {
                            Debug.LogError($"Firebase dependencies are not resolved: {_dependencyStatus}");
                            Debug.LogWarning("Please ensure Firebase SDKs for PC (Standalone) are correctly imported and configured.");
                        }
                    }
                    else
                    {
                        Debug.LogError("Failed to check Firebase PC (Standalone) dependencies.");
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
                            Debug.LogError($"Firebase dependencies for Android are not resolved: {_dependencyStatus}");
                            Debug.LogWarning("Please ensure Firebase SDKs are correctly imported and configured.");
                        }
                    }
                    else
                    {
                        Debug.LogError("Failed to check Firebase for Android dependencies.");
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
                            Debug.LogError($"Firebase dependencies for IOS are not resolved: {_dependencyStatus}");
                            Debug.LogWarning("Please ensure Firebase SDKs are correctly imported and configured.");
                        }
                    }
                    else
                    {
                        Debug.LogError("Failed to check Firebase IOS dependencies.");
                    }
                });
        }
    }
}
#endif