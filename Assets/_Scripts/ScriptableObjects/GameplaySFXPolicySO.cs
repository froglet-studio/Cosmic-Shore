using System;
using System.Collections.Generic;
using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Per-category burst policy for gameplay SFX one-shots — the single source of truth for
    /// "how many of this sound may play at once, how close together, and how loud".
    ///
    /// Why this exists: every gameplay SFX is fired per-EVENT, and several event classes are
    /// inherently bursty — a vessel flying through a crystal shower, an AOE blast destroying a
    /// field of crystals, a fauna die-off dropping its hearts, a trail collapsing. Each one-shot
    /// is a separate FMOD instance (see <c>FMODOneShotVolumeHelper</c>: CreateInstance / setVolume /
    /// set3DAttributes / start / release), so N events in one frame means N voices — and because
    /// they are the SAME asset started within the same frame, they sum COHERENTLY: N identical
    /// copies is +20·log10(N) dB (10 crystals = +20 dB), and the sub-millisecond offsets between
    /// them comb-filter into the harsh metallic phasing that reads as "the game tried to combine
    /// far too many sounds". Attenuating each voice does not fix that — the phasing is a function
    /// of SIMULTANEITY, not level.
    ///
    /// So the policy has three distinct levers, and they do three different jobs:
    ///   • <see cref="GameplaySFXCategoryPolicy.minRetriggerSeconds"/> — the DECOHERENCE lever.
    ///     Guarantees two voices of a category never start within the same few milliseconds, which
    ///     is what actually removes the comb filtering. A burst becomes a legible rattle of
    ///     distinct hits instead of one phased blast.
    ///   • <see cref="GameplaySFXCategoryPolicy.maxVoicesPerWindow"/> — the BUDGET lever (CPU and
    ///     FMOD voices). Hard ceiling on how many voices a category may start per window.
    ///   • <see cref="GameplaySFXCategoryPolicy.maxPendingVoices"/> +
    ///     <see cref="GameplaySFXCategoryPolicy.burstMagnitudeGain"/> — the MAGNITUDE levers.
    ///     Events blocked by the two above are not thrown away: up to <c>maxPendingVoices</c> of
    ///     them are folded into pending aggregates and replayed, spaced out, at the centroid of
    ///     the events they stand for — and scaled UP by how many that is
    ///     (<c>1 + gain · log2(represents)</c>, clamped). A 300-prism Dolphin blast still SOUNDS
    ///     like 300 prisms; it just arrives as a short spaced crunch rather than a wall.
    ///
    /// <para><b>The magnitude lever is not optional garnish — it replaces something the fix
    /// removes.</b> Before the limiter, a big burst was loud precisely BECAUSE its voices stacked,
    /// which is the defect; that is why <c>BlockDestroy</c> was attenuated to 0.35 in the first
    /// place ("dozens of prisms can break in a single frame"). Kill the stacking and that
    /// attenuation, left alone, would just make the blast quiet. So loudness is restored
    /// deliberately and logarithmically on the ONE voice that speaks for the crowd, instead of
    /// accidentally and coherently across N voices.</para>
    ///
    /// Set <c>maxPendingVoices = 0</c> for the older pure-drop behaviour.
    ///
    /// Per CLAUDE.md config separation, all tuning lives here rather than as serialized fields on
    /// <see cref="AudioSystem"/>. The shared asset is loaded from
    /// <c>Resources/<see cref="ResourcePath"/></c> so a scene needs no wiring.
    /// </summary>
    [CreateAssetMenu(
        fileName = "GameplaySFXPolicy",
        menuName = "ScriptableObjects/Audio/Gameplay SFX Policy")]
    public class GameplaySFXPolicySO : ScriptableObject
    {
        /// <summary>Resources path of the shared asset (zero-wire default).</summary>
        public const string ResourcePath = "GameplaySFXPolicy";

        [Header("Default (categories with no explicit entry below)")]
        [Tooltip(
            "Applied to any GameplaySFXCategory not listed in Category Policies. Deliberately " +
            "permissive: a category nobody has identified as bursty should behave exactly as it " +
            "did before this policy existed.")]
        [SerializeField] GameplaySFXCategoryPolicy defaultPolicy = GameplaySFXCategoryPolicy.Unlimited();

        [Header("Per-category overrides")]
        [Tooltip(
            "One entry per category that can fire in bursts. First matching entry wins; a " +
            "category listed twice logs a warning on load.")]
        [SerializeField] List<GameplaySFXCategoryPolicy> categoryPolicies = new()
        {
            // ── Crystals ────────────────────────────────────────────────────────────────────
            // The reported bug. A vessel can sweep a crystal shower, an AOE blast can destroy a
            // whole field at once, and every lifeform drops a heart on death — so a fauna die-off
            // or a mass-kill mode (Wildlife Liberation, Rampage) detonates dozens in one frame,
            // all at nearly the same world position.
            new()
            {
                category            = GameplaySFXCategory.CrystalCollect,
                volumeScale         = 0.8f,
                maxVoicesPerWindow  = 3,
                windowSeconds       = 0.12f,
                minRetriggerSeconds = 0.045f,
                burstVolumeFalloff  = 0.6f,
                minBurstVolume      = 0.3f,
                burstMagnitudeGain  = 0.15f,
                maxBurstMagnitude   = 1.6f,
                maxPendingVoices    = 2,
            },
            new()
            {
                category            = GameplaySFXCategory.CrystalSkim,
                volumeScale         = 0.8f,
                maxVoicesPerWindow  = 3,
                windowSeconds       = 0.1f,
                minRetriggerSeconds = 0.04f,
                burstVolumeFalloff  = 0.65f,
                minBurstVolume      = 0.3f,
                burstMagnitudeGain  = 0.15f,
                maxBurstMagnitude   = 1.6f,
                maxPendingVoices    = 1,
            },

            // ── Elemental receive stingers ──────────────────────────────────────────────────
            // The worst case in the whole table, because these are played 2D
            // (AudioSystem.PlayGameplaySFX(category) with no position), so unlike the 3D
            // categories they get ZERO spatial decorrelation — every copy lands at the listener
            // dead centre and sums perfectly. One crystal pickup fires one of these ON TOP of a
            // CrystalCollect, so a shower stacks two coherent families at once. Held to a
            // near-single voice: this is a reward stinger, not a physical event, and it reads
            // fine as one.
            new()
            {
                category            = GameplaySFXCategory.ElementChargeReceived,
                volumeScale         = 1f,
                maxVoicesPerWindow  = 2,
                windowSeconds       = 0.15f,
                minRetriggerSeconds = 0.07f,
                burstVolumeFalloff  = 0.5f,
                minBurstVolume      = 0.35f,
                burstMagnitudeGain  = 0.0f,
                maxBurstMagnitude   = 1.0f,
                maxPendingVoices    = 1,
            },
            new()
            {
                category            = GameplaySFXCategory.ElementMassReceived,
                volumeScale         = 1f,
                maxVoicesPerWindow  = 2,
                windowSeconds       = 0.15f,
                minRetriggerSeconds = 0.07f,
                burstVolumeFalloff  = 0.5f,
                minBurstVolume      = 0.35f,
                burstMagnitudeGain  = 0.0f,
                maxBurstMagnitude   = 1.0f,
                maxPendingVoices    = 1,
            },
            new()
            {
                category            = GameplaySFXCategory.ElementSpaceReceived,
                volumeScale         = 1f,
                maxVoicesPerWindow  = 2,
                windowSeconds       = 0.15f,
                minRetriggerSeconds = 0.07f,
                burstVolumeFalloff  = 0.5f,
                minBurstVolume      = 0.35f,
                burstMagnitudeGain  = 0.0f,
                maxBurstMagnitude   = 1.0f,
                maxPendingVoices    = 1,
            },
            new()
            {
                category            = GameplaySFXCategory.ElementTimeReceived,
                volumeScale         = 1f,
                maxVoicesPerWindow  = 2,
                windowSeconds       = 0.15f,
                minRetriggerSeconds = 0.07f,
                burstVolumeFalloff  = 0.5f,
                minBurstVolume      = 0.35f,
                burstMagnitudeGain  = 0.0f,
                maxBurstMagnitude   = 1.0f,
                maxPendingVoices    = 1,
            },

            // ── Prisms / mass ───────────────────────────────────────────────────────────────
            // Carries forward the hand-rolled BlockDestroy throttle this policy replaced
            // (0.35 volume, 4 voices per 0.1s) and adds the retrigger spacing it lacked, so the
            // 4 admitted voices can no longer all start on the same frame.
            new()
            {
                category            = GameplaySFXCategory.BlockDestroy,
                volumeScale         = 0.35f,
                maxVoicesPerWindow  = 4,
                windowSeconds       = 0.1f,
                minRetriggerSeconds = 0.02f,
                burstVolumeFalloff  = 0.8f,
                minBurstVolume      = 0.4f,
                burstMagnitudeGain  = 0.3f,
                maxBurstMagnitude   = 2.4f,
                maxPendingVoices    = 2,
            },
            new()
            {
                category            = GameplaySFXCategory.FloraCollision,
                volumeScale         = 0.5f,
                maxVoicesPerWindow  = 3,
                windowSeconds       = 0.1f,
                minRetriggerSeconds = 0.03f,
                burstVolumeFalloff  = 0.7f,
                minBurstVolume      = 0.35f,
                burstMagnitudeGain  = 0.3f,
                maxBurstMagnitude   = 2.4f,
                maxPendingVoices    = 1,
            },
            new()
            {
                category            = GameplaySFXCategory.CreatureBlockHit,
                volumeScale         = 0.5f,
                maxVoicesPerWindow  = 3,
                windowSeconds       = 0.1f,
                minRetriggerSeconds = 0.03f,
                burstVolumeFalloff  = 0.7f,
                minBurstVolume      = 0.35f,
                burstMagnitudeGain  = 0.3f,
                maxBurstMagnitude   = 2.4f,
                maxPendingVoices    = 1,
            },

            // ── Explosions / deaths ─────────────────────────────────────────────────────────
            new()
            {
                category            = GameplaySFXCategory.Explosion,
                volumeScale         = 0.6f,
                maxVoicesPerWindow  = 3,
                windowSeconds       = 0.1f,
                minRetriggerSeconds = 0.03f,
                burstVolumeFalloff  = 0.7f,
                minBurstVolume      = 0.35f,
                burstMagnitudeGain  = 0.25f,
                maxBurstMagnitude   = 2.0f,
                maxPendingVoices    = 1,
            },
            new()
            {
                category            = GameplaySFXCategory.MineExplode,
                volumeScale         = 0.7f,
                maxVoicesPerWindow  = 3,
                windowSeconds       = 0.1f,
                minRetriggerSeconds = 0.035f,
                burstVolumeFalloff  = 0.7f,
                minBurstVolume      = 0.35f,
                burstMagnitudeGain  = 0.25f,
                maxBurstMagnitude   = 2.0f,
                maxPendingVoices    = 1,
            },
            new()
            {
                // A mass-kill mode can starve or shoot out a whole swarm at once.
                category            = GameplaySFXCategory.CreatureDeath,
                volumeScale         = 0.8f,
                maxVoicesPerWindow  = 3,
                windowSeconds       = 0.12f,
                minRetriggerSeconds = 0.05f,
                burstVolumeFalloff  = 0.65f,
                minBurstVolume      = 0.35f,
                burstMagnitudeGain  = 0.25f,
                maxBurstMagnitude   = 2.0f,
                maxPendingVoices    = 2,
            },

            // ── Shields ─────────────────────────────────────────────────────────────────────
            // Both are 2D (PrismStateManager plays them with no position), so like the
            // elemental stingers they get no spatial decorrelation at all. ShieldActivate is
            // one voice PER PRISM — an AOE blast shields a whole neighbourhood in one frame.
            // ShieldDeactivate is worse, and worse BY CONSTRUCTION: PrismTimerManager drains
            // every expired shield timer in a single Update, so the prisms shielded together
            // at t all expire together at t + duration. That is a perfectly synchronised burst
            // of an identical 2D one-shot — the theoretical worst case for coherent summation.
            new()
            {
                category            = GameplaySFXCategory.ShieldActivate,
                volumeScale         = 0.6f,
                maxVoicesPerWindow  = 2,
                windowSeconds       = 0.12f,
                minRetriggerSeconds = 0.06f,
                burstVolumeFalloff  = 0.55f,
                minBurstVolume      = 0.35f,
                burstMagnitudeGain  = 0.2f,
                maxBurstMagnitude   = 1.8f,
                maxPendingVoices    = 1,
            },
            new()
            {
                category            = GameplaySFXCategory.ShieldDeactivate,
                volumeScale         = 0.6f,
                maxVoicesPerWindow  = 2,
                windowSeconds       = 0.12f,
                minRetriggerSeconds = 0.06f,
                burstVolumeFalloff  = 0.55f,
                minBurstVolume      = 0.35f,
                burstMagnitudeGain  = 0.2f,
                maxBurstMagnitude   = 1.8f,
                maxPendingVoices    = 1,
            },

            // ── Vessel contact ──────────────────────────────────────────────────────────────
            // A vessel grazing a trail generates one of these per prism contact per frame.
            new()
            {
                category            = GameplaySFXCategory.VesselImpact,
                volumeScale         = 1f,
                maxVoicesPerWindow  = 3,
                windowSeconds       = 0.1f,
                minRetriggerSeconds = 0.04f,
                burstVolumeFalloff  = 0.7f,
                minBurstVolume      = 0.4f,
                burstMagnitudeGain  = 0.15f,
                maxBurstMagnitude   = 1.6f,
                maxPendingVoices    = 1,
            },
            new()
            {
                category            = GameplaySFXCategory.TrackImpact,
                volumeScale         = 1f,
                maxVoicesPerWindow  = 3,
                windowSeconds       = 0.1f,
                minRetriggerSeconds = 0.04f,
                burstVolumeFalloff  = 0.7f,
                minBurstVolume      = 0.4f,
                burstMagnitudeGain  = 0.15f,
                maxBurstMagnitude   = 1.6f,
                maxPendingVoices    = 1,
            },
        };

        Dictionary<GameplaySFXCategory, GameplaySFXCategoryPolicy> _byCategory;

        /// <summary>
        /// The policy governing <paramref name="category"/>, or <see cref="Default"/> when the
        /// category has no explicit entry.
        /// </summary>
        public GameplaySFXCategoryPolicy For(GameplaySFXCategory category)
        {
            _byCategory ??= BuildLookup();
            return _byCategory.TryGetValue(category, out var policy) ? policy : defaultPolicy;
        }

        /// <summary>Policy applied to categories with no explicit entry.</summary>
        public GameplaySFXCategoryPolicy Default => defaultPolicy;

        /// <summary>Every explicitly-authored category policy, in authoring order.</summary>
        public IReadOnlyList<GameplaySFXCategoryPolicy> CategoryPolicies => categoryPolicies;

        Dictionary<GameplaySFXCategory, GameplaySFXCategoryPolicy> BuildLookup()
        {
            var map = new Dictionary<GameplaySFXCategory, GameplaySFXCategoryPolicy>();
            if (categoryPolicies == null) return map;

            foreach (var policy in categoryPolicies)
            {
                if (policy == null) continue;
                if (!map.TryAdd(policy.category, policy))
                {
                    Debug.LogWarning(
                        $"[GameplaySFXPolicySO] '{name}' lists GameplaySFXCategory.{policy.category} " +
                        $"more than once. The first entry wins; remove the duplicate.");
                }
            }
            return map;
        }

        void OnValidate() => _byCategory = null;

        static GameplaySFXPolicySO _default;

        /// <summary>
        /// Loads the shared policy from Resources. Falls back to code defaults (the field
        /// initializers above) so gameplay audio stays governed even if the asset goes missing —
        /// warned, because the asset is the authorable surface.
        /// </summary>
        public static GameplaySFXPolicySO LoadDefault()
        {
            if (_default) return _default;

            _default = Resources.Load<GameplaySFXPolicySO>(ResourcePath);
            if (!_default)
            {
                Debug.LogWarning(
                    $"[GameplaySFXPolicySO] No policy asset at Resources/{ResourcePath} - running " +
                    $"on code defaults. Restore the asset so the burst limits stay authorable.");
                _default = CreateInstance<GameplaySFXPolicySO>();
            }
            return _default;
        }

        /// <summary>Editor/test hook: drops the cached shared instance so the next load re-reads it.</summary>
        public static void ClearCachedDefault() => _default = null;

        /// <summary>
        /// Builds an in-memory policy from explicit entries, replacing the authored defaults.
        /// For tools and tests that need a known table rather than the shipped one.
        /// </summary>
        public static GameplaySFXPolicySO Create(params GameplaySFXCategoryPolicy[] policies)
        {
            var instance = CreateInstance<GameplaySFXPolicySO>();
            instance.categoryPolicies = new List<GameplaySFXCategoryPolicy>(
                policies ?? System.Array.Empty<GameplaySFXCategoryPolicy>());
            instance._byCategory = null;
            return instance;
        }
    }

    /// <summary>
    /// Burst rules for one <see cref="GameplaySFXCategory"/>. See
    /// <see cref="GameplaySFXPolicySO"/> for what each lever is actually for.
    /// </summary>
    [Serializable]
    public class GameplaySFXCategoryPolicy
    {
        [Tooltip("The gameplay SFX category these rules govern.")]
        public GameplaySFXCategory category = GameplaySFXCategory.BlockDestroy;

        [Range(0f, 1f)]
        [Tooltip(
            "Baseline volume multiplier for this category, applied on top of the SFX slider. " +
            "Categories that fire en masse are attenuated so even the admitted voices don't clip.")]
        public float volumeScale = 1f;

        [Min(1)]
        [Tooltip(
            "Hard ceiling on voices this category may START per Window Seconds. This is the CPU / " +
            "FMOD-voice budget.")]
        public int maxVoicesPerWindow = 8;

        [Min(0.01f)]
        [Tooltip("Sliding window (seconds) over which Max Voices Per Window is counted.")]
        public float windowSeconds = 0.1f;

        [Min(0f)]
        [Tooltip(
            "Minimum seconds between two voices of this category. THE decoherence lever: identical " +
            "one-shots only comb-filter when they start within a few milliseconds of each other, " +
            "so spacing them is what removes the harsh phasing. 0 disables spacing.")]
        public float minRetriggerSeconds;

        [Range(0.05f, 1f)]
        [Tooltip(
            "Volume multiplier compounded per voice already played in the current window, so a " +
            "sustained burst decays instead of piling up. 1 = every admitted voice at full volume.")]
        public float burstVolumeFalloff = 1f;

        [Range(0f, 1f)]
        [Tooltip("Floor for the Burst Volume Falloff decay, so late voices in a burst stay audible.")]
        public float minBurstVolume = 0.25f;

        [Min(0)]
        [Tooltip(
            "How many blocked events may be held as pending aggregates and replayed later, spaced " +
            "out, at the centroid of the events they represent. This is what keeps a big burst " +
            "SOUNDING big without stacking it. 0 = drop blocked events outright.")]
        public int maxPendingVoices;

        [Min(0f)]
        [Tooltip(
            "Loudness bonus for a replayed voice, per DOUBLING of the events it stands for: " +
            "volume *= 1 + gain * log2(represents). A voice speaking for 47 prisms should be " +
            "louder than one speaking for 1 - this is what carries blast magnitude once the " +
            "voices can no longer stack. 0 disables it (every replay at the falloff volume). " +
            "Immediate voices represent 1 event, so log2(1)=0 and they are never affected.")]
        public float burstMagnitudeGain;

        [Min(1f)]
        [Tooltip("Ceiling on the Burst Magnitude Gain multiplier, so a huge burst cannot clip.")]
        public float maxBurstMagnitude = 2f;

        /// <summary>
        /// A permissive policy: no spacing, no coalescing, effectively no cap. The default for
        /// categories nobody has identified as bursty, so they behave exactly as they always have.
        /// </summary>
        public static GameplaySFXCategoryPolicy Unlimited() => new()
        {
            volumeScale         = 1f,
            maxVoicesPerWindow  = int.MaxValue,
            windowSeconds       = 0.1f,
            minRetriggerSeconds = 0f,
            burstVolumeFalloff  = 1f,
            minBurstVolume      = 1f,
            maxPendingVoices    = 0,
        };
    }
}
