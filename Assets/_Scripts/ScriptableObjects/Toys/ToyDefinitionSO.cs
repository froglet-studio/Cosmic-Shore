using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Config for a single freestyle <b>Toy</b> - a world-space interactive station the
    /// local player's vessel flies into to activate. Toys have <i>no score and no end
    /// condition</i>; they are things to play with indefinitely (the toybox is what makes
    /// freestyle freestyle, the way party games make the rest of Cosmic Shore a party game).
    ///
    /// "Toy" is a first-class fundamental (added at the prompter's request): it composes with
    /// <b>Vessel</b> (vessel-changer), <b>Domain</b> (domain-changer) and <b>Prisms/Mass</b>
    /// (the painting toy lays a conserved-mass prism pattern). Toys impose no decay, no timer,
    /// and no win/lose - consistent with "don't cheat emergence".
    ///
    /// Subclass per toy kind and override <see cref="Spawn"/> to build the runtime
    /// behaviour. Shared metadata (id, display name, unlock flag, placement, accent colour)
    /// lives here so <see cref="ToyboxSO"/> can list, gate, and place every toy uniformly.
    /// </summary>
    public abstract class ToyDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField, Tooltip("Stable unique id, used as the unlock key. Never reuse or rename once shipped. " +
                                 "Falls back to the asset name when empty.")]
        string id;

        [SerializeField, Tooltip("Player-facing name shown on the toy's world label.")]
        string displayName = "Toy";

        [SerializeField, TextArea, Tooltip("Short description of what the toy does.")]
        string description;

        [Header("Unlock")]
        [SerializeField, Tooltip("Whether this toy starts in the player's toybox. Unlock CONDITIONS are deferred - " +
                                 "for now every toy ships unlocked; flip this off and gate via ToyboxSO.SetToyUnlocked later.")]
        bool unlockedByDefault = true;

        [Header("Placement")]
        [SerializeField, Tooltip("Preferred angle (degrees, around the cell's vertical axis) at which the toybox " +
                                 "places this toy near the membrane. Set < 0 to auto-distribute evenly with the " +
                                 "other auto-placed toys (the default - keeps them far apart).")]
        float placementAngleDegrees = -1f;

        [Header("Look")]
        [SerializeField, Tooltip("Accent colour for the toy's world body + label.")]
        Color accentColor = Color.white;

        /// <summary>
        /// What KIND of toy this is, keyed on the fundamental it composes with. Declared per
        /// subclass in code rather than serialized on the asset, because a toy's category is a
        /// property of what the toy DOES and an authored field is a field that can disagree with
        /// the behaviour underneath it.
        ///
        /// <para>Abstract on purpose: a new toy cannot be added without saying which fundamental
        /// it reaches for, and a toy that fits none of them is the signal to have the
        /// fundamentals conversation (CLAUDE.md, "Process for curating fundamentals") rather than
        /// to widen the enum.</para>
        /// </summary>
        public abstract ToyCategory Category { get; }

        public string Id => string.IsNullOrEmpty(id) ? name : id;
        public string DisplayName => displayName;
        public string Description => description;
        public bool UnlockedByDefault => unlockedByDefault;
        public float PlacementAngleDegrees => placementAngleDegrees;
        public Color AccentColor => accentColor;

        /// <summary>
        /// Build the runtime toy(s) for this definition under <paramref name="parent"/>. A definition
        /// may spawn a single <see cref="Toy"/> (painting) or a whole coordinated set of them
        /// (vessel/domain changers). The <see cref="ToyboxController"/> destroys the parent on
        /// teardown, so implementations don't need to track what they create.
        /// </summary>
        public abstract void Spawn(Transform parent, ToyPlacement placement, ToyContext context);

        /// <summary>
        /// Seed metadata on a runtime-synthesised definition (see <see cref="ToyboxSO"/>'s
        /// default-toybox fallback). Editor-authored assets set these fields in the inspector;
        /// this exists only for the code-built defaults that ship before any assets are authored.
        /// </summary>
        internal void SetRuntimeMetadata(string toyId, string toyName, string toyDescription, Color accent)
        {
            id = toyId;
            displayName = toyName;
            description = toyDescription;
            accentColor = accent;
            unlockedByDefault = true;
        }
    }
}
