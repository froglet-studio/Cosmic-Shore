using System;
using System.Threading.Tasks;
using Firebase;
using StarWriter.Utility.Singleton;
using UnityEngine;

namespace _Scripts._Core.Firebase.Controller
{
    public class FirebaseHelper : SingletonPersistent<FirebaseHelper>
    {
        // Firebase dependency status flag, mark as available when dependencies are resolved
        private static DependencyStatus _dependencyStatus;
    
        // public event when all dependency resolved 
        public static event EventHandler OnDependencyResolved; 
    
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
            FirebaseApp.CheckDependenciesAsync().ContinueWith(
                checkTask =>
                {
                    _dependencyStatus = checkTask.Result;
                    if (_dependencyStatus != DependencyStatus.Available)
                    {
                        return FirebaseApp.FixDependenciesAsync().ContinueWith(
                            fixTask => FirebaseApp.CheckDependenciesAsync()).Unwrap();
                    }
                    else
                    {
                        return checkTask;
                    }
                }).Unwrap().ContinueWith(
                resultTask =>
                {
                    _dependencyStatus = resultTask.Result;
                    if (_dependencyStatus == DependencyStatus.Available)
                    {
                        OnDependencyResolved?.Invoke(null, null);
                    }
                    else
                    {
                        Debug.LogError($"Unable to resolve all Firebase Android dependencies: {_dependencyStatus}");
                    }
                });
        }

        /// <summary>
        /// Check iOS Dependencies 
        /// Check for necessary dependencies and try to resolve them for iOS
        /// </summary>
        private void CheckIOSDependencies()
        {
            FirebaseApp.CheckDependenciesAsync().ContinueWith(
                checkTask =>
                {
                    _dependencyStatus = checkTask.Result;
                    if (_dependencyStatus != DependencyStatus.Available)
                    {
                        return FirebaseApp.FixDependenciesAsync().ContinueWith(
                            fixTask => FirebaseApp.CheckDependenciesAsync()).Unwrap();
                    }
                    else
                    {
                        return checkTask;
                    }
                }).Unwrap().ContinueWith(
                resultTask =>
                {
                    _dependencyStatus = resultTask.Result;
                    if (_dependencyStatus == DependencyStatus.Available)
                    {
                        OnDependencyResolved?.Invoke(null, null);
                    }
                    else
                    {
                        Debug.LogError($"Unable to resolve all Firebase iOS dependencies: {_dependencyStatus}");
                    }
                });
        }
    }
}
