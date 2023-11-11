using System;
using System.Threading.Tasks;
using Firebase;
using StarWriter.Utility.Singleton;
using UnityEngine;
using UnityEngine.Events;

namespace _Scripts._Core.Firebase.Controller
{
    public class FirebaseHelper : SingletonPersistent<FirebaseHelper>
    {
        // Firebase dependency status flag, mark as available when dependencies are resolved
        private static DependencyStatus _dependencyStatus;
    
        // public event when all dependency resolved 
        public static UnityEvent DependencyResolved;

        // Start is called before the first frame update
        void Start()
        {
            InitializeDependencyStatus();
            CheckDependencies();
        }

    
        /// <summary>
        /// Initialize Dependency Status
        /// Dependency status is marked as Unavailable from the start
        /// </summary>
        private void InitializeDependencyStatus()
        {
            _dependencyStatus = DependencyStatus.UnavailableOther;
        }

        /// <summary>
        /// Check Dependencies
        /// Check for necessary dependencies and try to resolve them for Android and IOS
        /// </summary>
        private void CheckDependencies()
        {
#if UNITY_ANDROID
            CheckAndroidDependencies();
#endif
        
#if UNITY_IOS
            CheckIOSDependencies();
#endif
        }

        /// <summary>
        /// Check Android Dependencies 
        /// Check for necessary dependencies and try to resolve them for Android
        /// </summary>
        private void CheckAndroidDependencies()
        {
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(
                fixTask =>
                {
                    _dependencyStatus = fixTask.Result;
                    if (_dependencyStatus == DependencyStatus.Available)
                    {
                        Debug.Log("Dependency resolved, now proceed with Firebase");
                        DependencyResolved?.Invoke();
                    }
                    else
                    {
                        Debug.LogErrorFormat("Firebase dependency not resolved. {0}", _dependencyStatus);
                    }
                });
        }

        /// <summary>
        /// Check iOS Dependencies 
        /// Check for necessary dependencies and try to resolve them for iOS
        /// </summary>
        private void CheckIOSDependencies()
        {
            // TODO: check out how to resolve dependencies for iOS
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(
                fixTask =>
                {
                    _dependencyStatus = fixTask.Result;
                    if (_dependencyStatus == DependencyStatus.Available)
                    {
                        Debug.Log("Dependency resolved, now proceed with Firebase");
                        DependencyResolved?.Invoke();
                    }
                    else
                    {
                        Debug.LogErrorFormat("Firebase dependency not resolved. {0}", _dependencyStatus);
                    }
                });
        }
    }
}
