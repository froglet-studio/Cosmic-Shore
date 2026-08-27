using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The set of modes that have a playable preview, resolved by <see cref="GameModes"/>.
    ///
    /// <para>Opt-in by design: a mode with no entry here simply has no diorama and no Test
    /// Flight button, and its card falls back to the authored <c>CardBackground</c> sprite. That
    /// matters because the arcade lists 42 <c>SO_ArcadeGame</c> assets while only ~15 have a
    /// scene on disk — a preview system that assumed every card was previewable would surface
    /// dead modes as broken ones.</para>
    ///
    /// <para><b>Maelstrom (Tournament) is deliberately excluded</b> and is enforced in code
    /// (<see cref="IsPreviewable"/>) rather than left to authoring: it is a session-level meta
    /// that draws other modes, so "a mini version of it" is a different design, not a smaller
    /// arena. An entry for it is ignored and reported.</para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "ModePreviewLibrary",
        menuName = "ScriptableObjects/Game/Mode Preview Library",
        order = 3)]
    public class ModePreviewLibrarySO : ScriptableObject
    {
        /// <summary>Resources path the arcade UI falls back to when nothing is wired.</summary>
        public const string ResourcePath = "ModePreviewLibrary";

        [Tooltip("The omni crystal a preview arena mints for its crystal-scored modes " +
                 "(Assets/_Prefabs/Environment/Crystal.prefab). The prefab already carries " +
                 "OmniCrystalImpactor + ImpactCollider, and the manager-less guards on Crystal " +
                 "make a local mint collectible without a CrystalManager - the same contract the " +
                 "Wanderway conveyor relies on. Unwired = previews simply have no pickups.")]
        [SerializeField] Crystal omniCrystalPrefab;

        /// <summary>The pickup a preview arena mints, or null when none is wired.</summary>
        public Crystal OmniCrystalPrefab => omniCrystalPrefab;

        [Tooltip("Prism-lay stride for the FLIGHT arena: at N, every dense environment/track " +
                 "trail lays every Nth prism - the same shape at 1/N of the prisms, colliders " +
                 "and spatial-index load, so tapping into (and back out of) a preview does not " +
                 "hitch the menu. 1 = build the world in full. Short trails (under 25 prisms) " +
                 "always lay complete.")]
        [SerializeField, Range(1, 6)] int flightPrismStride = 4;

        /// <summary>Every-Nth-prism thinning for preview flight arenas (1 = full density).</summary>
        public int FlightPrismStride => Mathf.Max(1, flightPrismStride);

        [Tooltip("One definition per previewable mode. Duplicates are reported and the first " +
                 "entry wins.")]
        public List<ModePreviewDefinitionSO> Definitions = new();

        Dictionary<GameModes, ModePreviewDefinitionSO> _byMode;

        /// <summary>
        /// Modes the preview system refuses on principle rather than for want of authoring.
        /// Tournament/Maelstrom draws OTHER modes per round, so it has no arena of its own to
        /// shrink.
        /// </summary>
        public static bool IsPreviewable(GameModes mode) =>
            mode != GameModes.Tournament && mode != GameModes.Random;

        /// <summary>
        /// The definition for <paramref name="mode"/>, or null when the mode has no preview.
        /// Null is an ordinary answer here, not a fault - most modes have no entry.
        /// </summary>
        public ModePreviewDefinitionSO Resolve(GameModes mode)
        {
            if (!IsPreviewable(mode)) return null;

            EnsureIndex();
            return _byMode.TryGetValue(mode, out var def) ? def : null;
        }

        void EnsureIndex()
        {
            if (_byMode != null) return;

            _byMode = new Dictionary<GameModes, ModePreviewDefinitionSO>(Definitions.Count);
            for (int i = 0; i < Definitions.Count; i++)
            {
                var def = Definitions[i];
                if (!def) continue;

                if (!IsPreviewable(def.Mode))
                {
                    Utility.CSDebug.LogWarning(
                        $"[ModePreview] '{def.name}' targets {def.Mode}, which has no preview by " +
                        "design (a meta-mode draws other modes; it has no arena of its own). Ignored.");
                    continue;
                }

                if (!_byMode.TryAdd(def.Mode, def))
                    Utility.CSDebug.LogWarning(
                        $"[ModePreview] Two definitions claim {def.Mode} " +
                        $"('{_byMode[def.Mode].name}' and '{def.name}'). Keeping the first.");
            }
        }

        /// <summary>Drop the cached lookup so an edit-time change to Definitions is picked up.</summary>
        void OnValidate() => _byMode = null;
    }
}
