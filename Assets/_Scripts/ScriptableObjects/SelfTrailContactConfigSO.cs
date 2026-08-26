using System;
using UnityEngine;
using CosmicShore.Gameplay;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The self-trail contact grace: for a short window after a pilot lays a prism, THAT PILOT
    /// does not collide with it or skim it. Everyone else does, immediately.
    ///
    /// The problem it solves is geometric, not one of intent. A trail prism is laid a fixed
    /// <c>offset</c> behind the vessel and the vessel then has to leave it — but three things
    /// make that departure slower than the spawner assumes:
    ///
    ///   * a DRIFT slides the hull sideways across the ribbon it is extruding, so the prism
    ///     exits along the course while the vessel's long axis is still lying over it;
    ///   * MASS scaling stretches the prism (<c>VesselPrismController.CreateBlock</c> cube-roots
    ///     the volume multiplier into every axis), so an upgraded vessel lays mass that reaches
    ///     further back than the un-upgraded geometry the clearance delay was sized against;
    ///   * a skimmer's trigger sphere is far larger than the hull (the Squirrel's is 15–30 u),
    ///     so "outside the ship" and "outside the skimmer" are seconds apart at low speed.
    ///
    /// The consequences were real gameplay, not cosmetics: a Squirrel fed itself skim energy off
    /// the ribbon it was extruding, and a Dolphin RAMMED its own fresh trail — losing half its
    /// banked skim energy and half its charged boost to
    /// <see cref="VesselChangeResourceByPrismEffectSO"/> / <see cref="VesselChangeBoostByPrismEffectSO"/>,
    /// neither of which carries a self-guard.
    ///
    /// The Rhino is NOT one of the cases this exists for: its sword's damage effect is equally
    /// unguarded, so the grace formally applies to it, but the vessel cannot come about onto the
    /// ribbon it just laid inside the window, so it never fires. Cutting your own OLDER trail to
    /// bank sword energy is a signed-off design (RHINO_ENERGY_SWORD.md) and is untouched.
    ///
    /// **The gate is OWNER-scoped and TIME-boxed — deliberately not domain-scoped.** The existing
    /// <c>Skimmer.affectSelf</c> flag compares DOMAINS, so switching it off would also blind a
    /// vessel to its teammates' trails, and it is evaluated after the effect loop anyway (it
    /// gates only the skim bookkeeping). What a pilot must not touch is *their own mass in the
    /// instant they are making it* — so the test is the prism's <c>ownerID</c> against the
    /// vessel's own player name, for <see cref="HullGraceSeconds"/> / <see cref="SkimGraceSeconds"/>
    /// after it was laid. A trailing Squirrel therefore keeps closing on someone else's ribbon,
    /// keeps skimming it, and keeps reaching joust range, and a pilot's own older trail comes
    /// back to life a second later — a self-lay tube is still rideable, it just cannot be
    /// ridden while it is still coming out of the ship.
    ///
    /// Fleet-wide by design: a prism should read the same whichever hull is next to it, so
    /// there is deliberately no per-vessel override. Place the asset at
    /// <c>Resources/SelfTrailContactConfig</c>; with no asset the defaults below apply, so the
    /// rule holds with zero authoring (the <c>SpeedTunnelConfigSO</c> / <c>PrismOcclusionConfigSO</c>
    /// precedent). Set a grace to 0 to disable that half.
    ///
    /// This is not a prism lifetime, a cull, or a decay — nothing is removed, delayed into
    /// existence, or hidden from anyone else. The mass is fully live for the whole world from
    /// the frame it is laid; one vessel simply declines to act on it. Conserved mass is intact.
    /// </summary>
    [CreateAssetMenu(
        fileName = "SelfTrailContactConfig",
        menuName = "ScriptableObjects/Impact Effects/Self Trail Contact Config")]
    public class SelfTrailContactConfigSO : ScriptableObject
    {
        [Header("Grace windows (seconds since the prism was laid)")]
        [Tooltip("How long a pilot's HULL ignores a prism they just laid. Covers the ram: the " +
                 "damage, the impact SFX, the speed bite, and the Dolphin's energy/boost " +
                 "forfeit. Raise this if vessels still clip their own fresh ribbon mid-drift; " +
                 "0 disables the hull half entirely.")]
        [Min(0f)]
        [SerializeField] float hullGraceSeconds = 1f;

        [Tooltip("How long a pilot's SKIMMER ignores a prism they just laid. This is what stops " +
                 "a drifting vessel from harvesting the ribbon it is extruding. It does NOT " +
                 "affect anyone else's skimmer, so a pursuing vessel still skims this trail from " +
                 "the frame it appears. 0 disables the skim half entirely.")]
        [Min(0f)]
        [SerializeField] float skimGraceSeconds = 1f;

        public float HullGraceSeconds => Mathf.Max(0f, hullGraceSeconds);
        public float SkimGraceSeconds => Mathf.Max(0f, skimGraceSeconds);

        // ------------------------------------------------------------------
        // Instance

        const string ResourcePath = "SelfTrailContactConfig";
        static SelfTrailContactConfigSO s_instance;
        static bool s_loadAttempted;

        // If s_instance ever goes null after the first attempt, the latch would otherwise skip
        // Resources.Load forever and silently serve CreateInstance code defaults.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_instance = null;
            s_loadAttempted = false;
        }

        /// <summary>
        /// The fleet's one config. Falls back to an in-memory instance carrying the authored
        /// defaults above, so the rule is never silently off just because the asset is missing.
        /// </summary>
        public static SelfTrailContactConfigSO Instance
        {
            get
            {
                if (s_instance) return s_instance;
                if (!s_loadAttempted)
                {
                    s_loadAttempted = true;
                    s_instance = Resources.Load<SelfTrailContactConfigSO>(ResourcePath);
                }
                if (!s_instance)
                    s_instance = CreateInstance<SelfTrailContactConfigSO>();
                return s_instance;
            }
        }

        // ------------------------------------------------------------------
        // The rule — written ONCE. Every dispatch site that can act on a prism on a vessel's
        // behalf asks one of these two, rather than re-deriving "is this mine and is it fresh",
        // which is exactly the kind of predicate the next call site forgets to copy.

        /// <summary>True when this vessel's HULL should ignore this prism because the vessel
        /// itself laid it moments ago.</summary>
        public static bool SuppressesHullContact(Prism prism, IVesselStatus vessel) =>
            IsOwnFreshMass(prism, vessel, Instance.HullGraceSeconds);

        /// <summary>True when this vessel's SKIMMER should ignore this prism because the vessel
        /// itself laid it moments ago.</summary>
        public static bool SuppressesSkimContact(Prism prism, IVesselStatus vessel) =>
            IsOwnFreshMass(prism, vessel, Instance.SkimGraceSeconds);

        static bool IsOwnFreshMass(Prism prism, IVesselStatus vessel, float graceSeconds)
        {
            if (graceSeconds <= 0f) return false;

            // Unity's implicit bool, not `!= null` — a destroyed prism is not C# null.
            if (!prism) return false;
            if (vessel is null) return false;

            // Environment mass (flora, fauna, authored cell structure, the HexRace track) has no
            // pilot behind it and is never anyone's "own trail", however it is named.
            if (prism.IsEnvironmentOwned) return false;

            // ownerID is stamped by VesselPrismController.CreateBlock / Prism.RegisterProjectileCreated
            // and records WHO LAID IT — unlike Prism.PlayerName, which a steal reassigns. A prism
            // stolen from an opponent was never yours to be making, so it stays interactable.
            string owner = prism.ownerID;
            if (string.IsNullOrEmpty(owner)) return false;
            if (!string.Equals(owner, vessel.PlayerName, StringComparison.Ordinal)) return false;

            var props = prism.prismProperties;
            if (props == null) return false;

            return Time.time - props.TimeCreated < graceSeconds;
        }
    }
}
