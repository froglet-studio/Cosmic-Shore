using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Lane a kit entry is filed under in the Game Mode Prefab Kit window. Presentation only.
    /// </summary>
    public enum GameModePrefabRole
    {
        /// <summary>Must be in every game-mode scene or the mode cannot boot.</summary>
        Essential = 0,
        /// <summary>The mode's UI surface.</summary>
        Interface = 1,
        /// <summary>Players, vessels, spawners.</summary>
        Spawning = 2,
        /// <summary>Cell, environment, ecology.</summary>
        Environment = 3,
        /// <summary>Netcode plumbing.</summary>
        Networking = 4,
        /// <summary>Nice to have, mode-dependent.</summary>
        Optional = 5,
    }

    /// <summary>
    /// One prefab a new game-mode scene is expected to carry.
    /// </summary>
    [Serializable]
    public class GameModePrefabEntry
    {
        [Tooltip("The prefab asset. This is the source of truth the tool adds, opens and validates.")]
        public GameObject Prefab;

        [Tooltip("Shown in the tool. Leave empty to use the prefab's own name.")]
        public string DisplayName;

        [Tooltip("One line on why a game-mode scene needs this.")]
        [TextArea(1, 3)]
        public string Notes;

        [Tooltip("Lane + accent colour in the tool.")]
        public GameModePrefabRole Role = GameModePrefabRole.Essential;

        [Tooltip("Required entries are reported as errors when missing; optional ones only as hints.")]
        public bool Required = true;

        [Tooltip("Only one instance of this prefab is allowed per scene - the tool flags duplicates.")]
        public bool Singleton = true;

        [Tooltip("Scene paths (or path fragments) this entry does NOT apply to, e.g. \"Tools/\". " +
                 "Used to keep recording / benchmark scenes out of the drift report.")]
        public List<string> ExcludeScenesContaining = new();

        public string ResolvedName =>
            !string.IsNullOrWhiteSpace(DisplayName) ? DisplayName :
            Prefab != null ? Prefab.name : "(unassigned)";
    }

    /// <summary>
    /// The list of prefabs every new game-mode scene needs, authored once and surfaced by
    /// <c>FrogletTools &gt; Game Modes &gt; Game Mode Prefab Kit</c>.
    ///
    /// This asset is pure editor configuration - nothing reads it at runtime. It exists as a
    /// ScriptableObject (rather than a hard-coded list in the tool) so the set of "required
    /// prefabs" is data a designer can edit, in line with the project's config-separation rule.
    ///
    /// Lives at <c>Assets/Resources/GameModePrefabKit.asset</c>; the tool creates it on first open
    /// and seeds it from <see cref="DefaultSeedPaths"/>.
    /// </summary>
    [CreateAssetMenu(
        fileName = "GameModePrefabKit",
        menuName = "ScriptableObjects/Tooling/Game Mode Prefab Kit",
        order = 0)]
    public class GameModePrefabKitSO : ScriptableObject
    {
        public const string ResourcePath = "GameModePrefabKit";

        [Tooltip("The prefabs a new game-mode scene is expected to carry.")]
        public List<GameModePrefabEntry> Entries = new();

        [Header("Validation scope")]
        [Tooltip("Folders scanned for scenes when checking whether a prefab has drifted " +
                 "(instances carrying overrides that were never applied back to the prefab).")]
        public List<string> SceneSearchFolders = new() { "Assets/_Scenes" };

        [Tooltip("Scene path fragments excluded from the drift scan for every entry.")]
        public List<string> GloballyExcludedScenes = new() { "/Tools/" };

        [Tooltip("Property paths that are legitimately per-scene and never count as drift. " +
                 "Matched as a prefix against the modification's propertyPath.")]
        public List<string> IgnoredPropertyPaths = new()
        {
            "m_RootOrder",
            "m_LocalPosition",
            "m_LocalRotation",
            "m_LocalEulerAnglesHint",
            "m_ConstrainProportionsScale",
        };

        /// <summary>
        /// Best-effort seed used the first time the asset is created. Missing paths are skipped
        /// silently - the list is a convenience, not a contract.
        /// </summary>
        public static readonly (string path, GameModePrefabRole role, bool required, string note)[]
            DefaultSeedPaths =
            {
                ("Assets/_Prefabs/CORE/GameCanvas.prefab", GameModePrefabRole.Interface, true,
                    "The shared in-game canvas: HUD, scoreboard, pause, countdown. One source of truth for every mode."),
                ("Assets/_Prefabs/GameCanvas-HexRace.prefab", GameModePrefabRole.Interface, false,
                    "Forked canvas used by the six domain modes. Being retired into the base - see Docs/GAMECANVAS.md."),
                ("Assets/_Prefabs/CORE/ContainerScope.prefab", GameModePrefabRole.Essential, true,
                    "Reflex DI scope. Without it every [Inject] field in the scene stays null."),
                ("Assets/_Prefabs/CORE/Player and Vessel Spawner.prefab", GameModePrefabRole.Spawning, true,
                    "Single-player spawn path (PlayerSpawner + VesselSpawner)."),
                ("Assets/_Prefabs/CORE/NetworkVesselSpawner.prefab", GameModePrefabRole.Networking, true,
                    "Server-authoritative vessel spawner used by every multiplayer mode."),
                ("Assets/_Prefabs/CORE/CameraManager.prefab", GameModePrefabRole.Essential, true,
                    "Per-vessel Cinemachine rigs and end-game camera handoff."),
                ("Assets/_Prefabs/CORE/HUDContainer.prefab", GameModePrefabRole.Interface, false,
                    "Container vessel HUDs reparent into."),
                ("Assets/_Prefabs/CORE/ThemeManager.prefab", GameModePrefabRole.Essential, false,
                    "Domain colour sets used to theme vessels and UI."),
                ("Assets/_Prefabs/CORE/AudioSystem.prefab", GameModePrefabRole.Optional, false,
                    "Wwise audio entry point."),
            };
    }
}
