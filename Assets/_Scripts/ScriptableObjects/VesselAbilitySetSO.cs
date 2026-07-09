using System;
using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// One player-facing ability slot. A vessel ALWAYS presents exactly four of these to the player
    /// (see <see cref="VesselAbilitySetSO"/>), regardless of how the underlying kit is wired: a slot
    /// may map to a single shallow passive, or bundle several kit parts into one "ability" for
    /// vessels that are mechanically busier. The icon is the mnemonic; the input is what lights it up.
    /// </summary>
    [Serializable]
    public struct VesselAbilitySlot
    {
        [Tooltip("Short player-facing name shown with the icon.")]
        public string Label;

        [Tooltip("One-line description of what the ability does (tooltip / codex).")]
        [TextArea(1, 3)] public string Description;

        [Tooltip("The input that triggers this ability — its icon lights up while held. " +
                 "Passives can leave this at the default and simply read as always-on.")]
        public InputEvents Input;

        [Tooltip("The ability icon. LEAVE EMPTY to show the obvious 'unassigned ability' placeholder " +
                 "until a real ability + icon is authored.")]
        public Sprite Icon;

        [Tooltip("Force the placeholder even when an Icon is set — flags a work-in-progress ability.")]
        public bool IsPlaceholder;

        /// <summary>True only when a real icon is authored and the slot isn't flagged WIP.</summary>
        public bool HasIcon => Icon != null && !IsPlaceholder;
    }

    /// <summary>
    /// The four player-facing abilities of a vessel. This is a HARD contract: a vessel always
    /// exposes exactly four ability slots so the HUD can always show four icons, setting one
    /// consistent expectation for the player. Any unfilled slot renders as an obvious placeholder,
    /// so it is impossible for a vessel to present fewer than four ability icons.
    ///
    /// The player-facing layer is deliberately decoupled from the under-the-hood wiring: a slot can
    /// correspond to one shallow passive or bundle several kit parts — some vessels are simply more
    /// complicated than others. This set is only the mnemonic layer the player reads.
    /// </summary>
    [CreateAssetMenu(fileName = "VesselAbilitySet", menuName = "ScriptableObjects/Vessel/Ability Set (4 icons)")]
    public sealed class VesselAbilitySetSO : ScriptableObject
    {
        public const int SlotCount = 4;

        [SerializeField] private VesselClassType vesselClass = VesselClassType.Any;

        [Tooltip("Exactly four player-facing ability slots. The size is enforced to 4.")]
        [SerializeField] private List<VesselAbilitySlot> slots = new();

        public VesselClassType VesselClass => vesselClass;
        public int Count => SlotCount;

        /// <summary>Returns the slot at <paramref name="index"/> (0..3); out-of-range → an empty
        /// (placeholder) slot, so callers can always ask for four.</summary>
        public VesselAbilitySlot GetSlot(int index)
        {
            if (slots == null || slots.Count == 0) return default;
            if (index < 0 || index >= slots.Count) return default;
            return slots[index];
        }

        void EnsureSize()
        {
            slots ??= new List<VesselAbilitySlot>(SlotCount);
            while (slots.Count < SlotCount) slots.Add(default);
            if (slots.Count > SlotCount) slots.RemoveRange(SlotCount, slots.Count - SlotCount);
        }

#if UNITY_EDITOR
        // Enforce the four-slot contract in the editor: you cannot author a set with more or fewer.
        void OnValidate() => EnsureSize();
#endif
    }
}
