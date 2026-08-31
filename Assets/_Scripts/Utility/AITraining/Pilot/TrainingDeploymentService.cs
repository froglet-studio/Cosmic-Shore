using System.Collections;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// Puts trained personalities into NORMAL play, so a human starting any match
    /// against AI meets the latest graduates of overnight training.
    ///
    /// Lifecycle:
    ///   - One instance auto-creates at runtime (RuntimeInitializeOnLoadMethod)
    ///     and persists across scenes.
    ///   - It listens to gameData.OnPlayerPairInitialized. When an AI player pairs
    ///     with its vessel — and we are NOT in a training session — it samples a
    ///     personality from the archive's roster for (vessel × mode × intensity 4)
    ///     and applies it through TrainingModulator: the genome tunes the shipped
    ///     AIPilot, and the match's selected intensity drives input dithering plus
    ///     tempo factors.
    ///   - Sampling the ROSTER (not just the champion) is the replayability
    ///     feature: tonight's opponents might be an Ace Drifter and a Rookie
    ///     Rammer, tomorrow's a Steady Cruiser — same archive, different match.
    ///
    /// The shipped AIPilot keeps flying throughout; with no archive entry the AI
    /// is exactly the hand-authored pilot. Deployment can only ever RE-TUNE, so
    /// its floor is the game as shipped.
    ///
    /// Disable globally via TrainingControlSO.DeployArchiveInNormalPlay.
    /// </summary>
    public class TrainingDeploymentService : MonoBehaviour
    {
        TrainingControlSO _control;
        GameDataSO _gameData;
        bool _hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoInstall()
        {
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

        void HandlePlayerPairInitialized(ulong playerNetId)
        {
            if (_control == null || !_control.DeployArchiveInNormalPlay) return;
            if (_control.Archive == null) return;
            if (_gameData == null) return;
            // A live training session owns its vessels; deployment stands down.
            if (_gameData.IsTraining) return;

            StartCoroutine(InstallAfterFrame(playerNetId));
        }

        IEnumerator InstallAfterFrame(ulong playerNetId)
        {
            // One frame so the vessel transform / status finish wiring.
            yield return null;
            for (int i = 0; i < _gameData.Players.Count; i++)
            {
                var p = _gameData.Players[i];
                if (p == null) continue;
                if (p.PlayerNetId != playerNetId && p.OwnerClientNetId != playerNetId) continue;
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

            VesselClassType vesselType = vessel.VesselStatus?.VesselType ?? VesselClassType.Any;
            GameModes mode = _gameData.GameMode;
            int intensity = _gameData.SelectedIntensity != null
                ? Mathf.Clamp(_gameData.SelectedIntensity.Value, 1, 4)
                : 4;

            // Sample a personality from the flawless bucket; the modulator applies
            // the match intensity on top (skill/cadence factors + input dithering).
            var genome = _control.Archive.SampleRoster(vesselType, mode, 4, out string personality);
            if (genome == null) return;

            var modulator = go.GetComponent<TrainingModulator>();
            if (modulator == null) modulator = go.AddComponent<TrainingModulator>();
            if (!modulator.BindVessel(vessel)) return;

            modulator.ApplyGenome(genome, intensity);
            modulator.BeginEpisode();

            Debug.Log($"[Deploy] {player.Name} flies trained personality '{personality}' " +
                      $"({vesselType}/{mode}, intensity {intensity}).");
        }
    }
}
