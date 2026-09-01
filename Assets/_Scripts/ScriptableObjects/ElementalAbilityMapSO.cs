using System;
using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    public enum UnlockLatchPolicy
    {
        /// <summary>Re-lock when the effective level drops below RelockBelowLevel (hysteresis).
        /// Debuffs stripping an upgrade is legible elemental counterplay — the codebase-consistent
        /// default (quantitative scaling already degrades symmetrically through debuffs).</summary>
        Relock = 0,

        /// <summary>Once earned, the upgrade stays for the life of the vessel instance.</summary>
        LatchForLife = 1,
    }

    /// <summary>
    /// One element→ability slot: which element quantitatively scales the ability, and what
    /// qualitative upgrade unlocks at the threshold level (5 = the all-five-petals-white flower).
    /// </summary>
    [Serializable]
    public class ElementalAbilityEntry
    {
        [Tooltip("The element that owns this ability slot.")]
        public Element Element;

        [Header("Presentation")]
        [Tooltip("Player-facing ability name (e.g. 'Pulsefire Cannons').")]
        public string AbilityLabel;
        [TextArea] public string AbilityDescription;
        [Tooltip("The InputEvents binding this ability rides (matches the prefab's R_VesselActionHandler map).")]
        public InputEvents Input;

        [Header("Quantitative scaling")]
        [Tooltip("ElementalScaling multiplier at integer level 10 (normalized 1.0). 1 = no scaling. " +
                 "Anchored at exactly 1x at the resting level, so authored baselines are untouched at spawn.")]
        public float MultiplierAtFullLevel = 1.5f;
        [Tooltip("Floor for the multiplier so deficit levels can't invert or zero the parameter.")]
        public float MinMultiplier = 0.25f;

        [Header("Qualitative unlock")]
        [Tooltip("Effective integer level at/above which the upgrade unlocks. 5 = all petals white.")]
        public int UnlockLevel = 5;
        [Tooltip("Hysteresis: once unlocked, the upgrade re-locks only when the effective level " +
                 "drops BELOW this (Relock policy). Keeps decaying debuffs from flickering the unlock.")]
        public int RelockBelowLevel = 4;
        public UnlockLatchPolicy LatchPolicy = UnlockLatchPolicy.Relock;
        [Tooltip("Player-facing upgrade name (e.g. 'Piercing Bullets').")]
        public string UpgradeLabel;
        [TextArea] public string UpgradeDescription;
    }

    /// <summary>
    /// The per-vessel data home for the "four abilities × four elements" contract: each of the
    /// four elements quantitatively scales one ability parameter and unlocks one qualitative
    /// upgrade at the threshold level. One asset declares both, so the mapping can't drift
    /// between gameplay and presentation.
    ///
    /// Assets live in Resources/ElementalAbilityMaps/{VesselClassType}.asset and are auto-loaded
    /// by R_VesselElementalAbilityHandler (zero-wire, the ElementalBarsView pattern). A vessel
    /// without a map simply has no elemental scaling (all multipliers 1x) and no unlocks —
    /// opt-in rollout.
    /// </summary>
    [CreateAssetMenu(fileName = "ElementalAbilityMap",
        menuName = "ScriptableObjects/Vessel/Elemental Ability Map")]
    public class ElementalAbilityMapSO : ScriptableObject
    {
        public const int SlotCount = 4;
        public const string ResourceFolder = "ElementalAbilityMaps";

        [SerializeField] VesselClassType vesselClass;
        [SerializeField] List<ElementalAbilityEntry> entries = new();

        public VesselClassType VesselClass => vesselClass;
        public IReadOnlyList<ElementalAbilityEntry> Entries => entries;

        public ElementalAbilityEntry GetEntry(Element element)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].Element == element)
                    return entries[i];
            return null;
        }

        public static ElementalAbilityMapSO LoadFor(VesselClassType vesselClass)
            => Resources.Load<ElementalAbilityMapSO>($"{ResourceFolder}/{vesselClass}");

        void OnValidate()
        {
            while (entries.Count > SlotCount)
                entries.RemoveAt(entries.Count - 1);
        }
    }
}
