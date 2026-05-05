using System.Collections;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// Auto-installs trained pilots on AI vessels at game-scene load so that a
    /// human player flying a normal HexRace match plays against the latest
    /// trained genomes from the archive.
    ///
    /// Lifecycle:
    ///   - A single instance is auto-created at runtime via RuntimeInitializeOnLoadMethod.
    ///   - It persists across scene loads (DontDestroyOnLoad).
    ///   - It listens to gameData.OnPlayerPairInitialized. When an AI player
    ///     gets paired with its vessel, it looks up the best matching genome
    ///     for (vessel × game mode × intensity) and installs a TrainingPilot
    ///     with intensity dithering applied.
    ///   - Plain (non-AI) players are ignored — training only spoofs input,
    ///     never overrides the human's controls.
    ///   - The training-mode session runner (which also installs TrainingPilots
    ///     during AI-vs-AI training) takes precedence: if a vessel already has
    ///     a TrainingPilot, this service skips it.
    ///
    /// Disable globally by setting TrainingControlSO.DeployArchiveInNormalPlay = false.
    /// </summary>
    public class TrainingDeploymentService : MonoBehaviour
    {
        TrainingControlSO _control;
        GameDataSO _gameData;
        CellRuntimeDataSO _cellData;
        bool _hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoInstall()
        {
            // Only install once per process — domain-reload safe via the singleton check.
            if (FindAnyObjectByType<TrainingDeploymentService>() != null) return;
            var go = new GameObject("[Training Deployment Service]");
            DontDestroyOnLoad(go);
            go.AddComponent<TrainingDeploymentService>();
        }

        void Awake()
        {
            ResolveAssets();
        }

        void Start()
        {
            HookGameData();
        }

        void OnDestroy() => UnhookGameData();

        void ResolveAssets()
        {
#if UNITY_EDITOR
            _control = FirstAsset<TrainingControlSO>();
            _gameData = FirstAsset<GameDataSO>();
            _cellData = FirstAsset<CellRuntimeDataSO>();
#endif
        }

#if UNITY_EDITOR
        static T FirstAsset<T>() where T : ScriptableObject
        {
            var guids = UnityEditor.AssetDatabase.FindAssets("t:" + typeof(T).Name);
            if (guids == null || guids.Length == 0) return null;
            return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
        }
#endif

        void HookGameData()
        {
            if (_hooked || _gameData == null) return;
            if (_gameData.OnPlayerPairInitialized != null)
                _gameData.OnPlayerPairInitialized.OnRaised += HandlePlayerPairInitialized;
            _hooked = true;
        }

        void UnhookGameData()
        {
            if (!_hooked || _gameData == null) return;
            if (_gameData.OnPlayerPairInitialized != null)
                _gameData.OnPlayerPairInitialized.OnRaised -= HandlePlayerPairInitialized;
            _hooked = false;
        }

        void HandlePlayerPairInitialized(ulong clientId)
        {
            // Cheap pre-check: deployment is opt-in via the control asset, and only
            // happens outside of training. The runner sets gameData.IsTraining = true
            // when it's running, so we won't double-install.
            if (_control == null || !_control.DeployArchiveInNormalPlay) return;
            if (_control.Archive == null) return;
            if (_gameData == null) return;
            if (_gameData.IsTraining) return;

            StartCoroutine(InstallAfterFrame(clientId));
        }

        IEnumerator InstallAfterFrame(ulong clientId)
        {
            // Wait one frame so the vessel transform / status are wired up.
            yield return null;
            for (int i = 0; i < _gameData.Players.Count; i++)
            {
                var p = _gameData.Players[i];
                if (p == null) continue;
                if (p.OwnerClientNetId != clientId && (p.PlayerNetId != clientId)) continue;
                if (!p.IsInitializedAsAI) continue;

                InstallOn(p);
            }
        }

        void InstallOn(IPlayer player)
        {
            var vessel = player.Vessel;
            if (vessel == null) return;
            var go = vessel.Transform != null ? vessel.Transform.gameObject : null;
            if (go == null) return;

            // Don't fight an already-installed TrainingPilot — that one is owned by
            // the runner during a training session and has the live genome.
            if (go.GetComponent<TrainingPilot>() != null) return;

            VesselClassType vesselType = vessel.VesselStatus?.VesselType ?? VesselClassType.Any;
            GameModes mode = _gameData.GameMode;
            int intensity = _gameData.SelectedIntensity != null
                ? Mathf.Clamp(_gameData.SelectedIntensity.Value, 1, 4)
                : 4;

            // Always look up the intensity-4 genome and let the ditherer produce the
            // requested intensity at runtime. Lower-intensity entries in the archive
            // override this if explicitly added.
            int lookupIntensity = 4;
            var genome = _control.Archive.FindBestAvailable(vesselType, mode, lookupIntensity, out int matchScore);
            if (genome == null) return;

            // No exact match? Quietly fall back; we'd rather show a reasonable AI
            // than no deployment at all. Match score < 4 means partial vessel/mode match.
            if (matchScore < 4)
                Debug.Log($"[Deploy] Partial archive match for {vesselType}/{mode}/I{lookupIntensity} (score {matchScore}) — using nearest available genome.");

            var aiPilot = go.GetComponentInChildren<AIPilot>();
            if (aiPilot != null && aiPilot.AutoPilotEnabled) aiPilot.StopAIPilot();
            if (aiPilot != null) aiPilot.enabled = false;

            var pilot = go.AddComponent<TrainingPilot>();
            pilot.BindVessel(vessel, _gameData, _cellData);
            pilot.Intensity = intensity;
            pilot.LoadGenome(genome);
            pilot.BeginEpisode();
            Debug.Log($"[Deploy] Installed trained pilot on {player.Name} ({vesselType}, intensity {intensity}).");
        }
    }
}
