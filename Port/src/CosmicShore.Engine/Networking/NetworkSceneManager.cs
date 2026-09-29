using System;
using System.Collections.Generic;
using CosmicShore.Engine.SceneManagement;

namespace CosmicShore.Engine.Networking
{
    public enum SceneEventType
    {
        Load = 0, Unload = 1, Synchronize = 2, ReSynchronize = 3, LoadEventCompleted = 4, UnloadEventCompleted = 5,
        LoadComplete = 6, UnloadComplete = 7, SynchronizeComplete = 8, ActiveSceneChanged = 9, ObjectSceneChanged = 10,
    }

    public enum SceneEventProgressStatus
    {
        None = 0, Started = 1, SceneNotLoaded = 2, SceneEventInProgress = 3, InvalidSceneName = 4,
        SceneFailedVerification = 5, InternalNetcodeError = 6, SceneManagementNotEnabled = 7, ServerOnlyAction = 8,
    }

    /// <summary>One scene-event notification (original: Unity.Netcode.SceneEvent).</summary>
    public class SceneEvent
    {
        public SceneEventType SceneEventType;
        public string SceneName;
        public LoadSceneMode LoadSceneMode;
        public ulong ClientId;
        public Scene Scene;
        public AsyncOperation AsyncOperation;
        public List<ulong> ClientsThatCompleted;
        public List<ulong> ClientsThatTimedOut;
    }

    /// <summary>
    /// Netcode's server-authoritative scene manager (original: NetworkManager.SceneManager).
    /// In the single-process port a "network" scene load IS a local load on this machine
    /// (the host), raising the same event sequence Netcode raises for the host client:
    /// Load → LoadComplete → LoadEventCompleted.
    /// </summary>
    public sealed class NetworkSceneManager
    {
        public delegate void SceneEventDelegate(SceneEvent sceneEvent);
        public delegate void OnLoadDelegateHandler(ulong clientId, string sceneName, LoadSceneMode loadSceneMode, AsyncOperation asyncOperation);
        public delegate void OnLoadCompleteDelegateHandler(ulong clientId, string sceneName, LoadSceneMode loadSceneMode);
        public delegate void OnEventCompletedDelegateHandler(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut);
        public delegate void OnUnloadDelegateHandler(ulong clientId, string sceneName, AsyncOperation asyncOperation);
        public delegate void OnUnloadCompleteDelegateHandler(ulong clientId, string sceneName);
        public delegate void OnSynchronizeDelegateHandler(ulong clientId);
        public delegate void OnSynchronizeCompleteDelegateHandler(ulong clientId);
        public delegate bool VerifySceneBeforeLoadingDelegateHandler(int sceneIndex, string sceneName, LoadSceneMode loadSceneMode);

        public event SceneEventDelegate OnSceneEvent;
        public event OnLoadDelegateHandler OnLoad;
        public event OnLoadCompleteDelegateHandler OnLoadComplete;
        public event OnEventCompletedDelegateHandler OnLoadEventCompleted;
        public event OnUnloadDelegateHandler OnUnload;
        public event OnUnloadCompleteDelegateHandler OnUnloadComplete;
        public event OnEventCompletedDelegateHandler OnUnloadEventCompleted;
        public event OnSynchronizeDelegateHandler OnSynchronize;
        public event OnSynchronizeCompleteDelegateHandler OnSynchronizeComplete;

        public VerifySceneBeforeLoadingDelegateHandler VerifySceneBeforeLoading;
        public bool ActiveSceneSynchronizationEnabled { get; set; }
        public LoadSceneMode ClientSynchronizationMode { get; private set; } = LoadSceneMode.Single;
        public void SetClientSynchronizationMode(LoadSceneMode mode) => ClientSynchronizationMode = mode;
        public void DisableValidationWarnings(bool disabled) { }

        /// <summary>Local client id the port reports for the host.</summary>
        public ulong LocalClientId { get; set; }

        public SceneEventProgressStatus LoadScene(string sceneName, LoadSceneMode loadSceneMode)
        {
            if (string.IsNullOrEmpty(sceneName)) return SceneEventProgressStatus.InvalidSceneName;
            if (VerifySceneBeforeLoading != null && !VerifySceneBeforeLoading(-1, sceneName, loadSceneMode))
                return SceneEventProgressStatus.SceneFailedVerification;

            var op = SceneManagement.SceneManager.LoadSceneAsync(sceneName, loadSceneMode);
            ulong id = LocalClientId;
            Raise(new SceneEvent { SceneEventType = SceneEventType.Load, SceneName = sceneName, LoadSceneMode = loadSceneMode, ClientId = id, AsyncOperation = op });
            OnLoad?.Invoke(id, sceneName, loadSceneMode, op);
            op.completed += _ =>
            {
                NetworkManager.Singleton?.SpawnInSceneObjects();
                var scene = SceneManagement.SceneManager.GetActiveScene();
                Raise(new SceneEvent { SceneEventType = SceneEventType.LoadComplete, SceneName = sceneName, LoadSceneMode = loadSceneMode, ClientId = id, Scene = scene });
                OnLoadComplete?.Invoke(id, sceneName, loadSceneMode);
                var done = new List<ulong> { id };
                var timedOut = new List<ulong>();
                Raise(new SceneEvent { SceneEventType = SceneEventType.LoadEventCompleted, SceneName = sceneName, LoadSceneMode = loadSceneMode, ClientId = id, ClientsThatCompleted = done, ClientsThatTimedOut = timedOut });
                OnLoadEventCompleted?.Invoke(sceneName, loadSceneMode, done, timedOut);
            };
            return SceneEventProgressStatus.Started;
        }

        public SceneEventProgressStatus UnloadScene(Scene scene)
        {
            if (scene == null) return SceneEventProgressStatus.SceneNotLoaded;
            ulong id = LocalClientId;
            Raise(new SceneEvent { SceneEventType = SceneEventType.Unload, SceneName = scene.name, ClientId = id });
            OnUnload?.Invoke(id, scene.name, null);
            SceneManagement.SceneManager.UnloadSceneAsync(scene);
            Raise(new SceneEvent { SceneEventType = SceneEventType.UnloadComplete, SceneName = scene.name, ClientId = id });
            OnUnloadComplete?.Invoke(id, scene.name);
            var done = new List<ulong> { id };
            Raise(new SceneEvent { SceneEventType = SceneEventType.UnloadEventCompleted, SceneName = scene.name, ClientId = id, ClientsThatCompleted = done, ClientsThatTimedOut = new List<ulong>() });
            OnUnloadEventCompleted?.Invoke(scene.name, LoadSceneMode.Additive, done, new List<ulong>());
            return SceneEventProgressStatus.Started;
        }

        /// <summary>Raised by the host path when a client finishes initial synchronization.</summary>
        public void NotifySynchronized(ulong clientId)
        {
            Raise(new SceneEvent { SceneEventType = SceneEventType.Synchronize, ClientId = clientId });
            OnSynchronize?.Invoke(clientId);
            Raise(new SceneEvent { SceneEventType = SceneEventType.SynchronizeComplete, ClientId = clientId });
            OnSynchronizeComplete?.Invoke(clientId);
        }

        void Raise(SceneEvent e) => OnSceneEvent?.Invoke(e);
    }
}
