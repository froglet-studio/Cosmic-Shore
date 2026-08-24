using CosmicShore.Gameplay;
using UnityEngine;
using System.Collections;
using System;
using CosmicShore.Core;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    public enum BlockState
    {
        Normal,
        Shielded,
        SuperShielded,
        Dangerous
    }

    public class PrismStateManager : MonoBehaviour
    {
        [Header("Data Containers")] [SerializeField]
        ThemeManagerDataContainerSO _themeManagerData;

        private Prism prism;
        private MaterialPropertyAnimator materialAnimator;
        private PrismTeamManager teamManager;
        private PrismOctahedronShield octahedronShield; // auto-added in Awake so every prism gets the octahedron on shield

        // Stellated octahedron (Stella Octangula) for the SUPER-shield state - the Skim Race
        // track look. Added LAZILY on the first super-shield engage (its Awake generates the
        // stellation mesh, a cost most prisms never pay), and disengaged by ApplyNormalState so
        // a single DeactivateShields() stays the full reverse of every shield state.
        private PrismStellatedOctahedronShield stellatedShield;

        public BlockState CurrentState { get; private set; } = BlockState.Normal;

        /// <summary>
        /// True while this prism is still being CREATED — a shield engaged here is part
        /// of the prism's birth, not a transition on live mass, so it engages/disengages
        /// INSTANTLY (no per-face bloom, no shatter overlay, no state SFX).
        ///
        /// Why this is the correct reading of the continuity law rather than a shortcut:
        /// the prism has never been on screen (Prism.CreateBlockCoroutine holds the
        /// renderer off until reveal), and the grow-in bloom that follows already carries
        /// its "nothing pops into existence" transition — exactly the reasoning
        /// MaterialPropertyAnimator already applies to spawn-paint ("a creation, not a
        /// recolor"). A morph here is invisible by construction, and it still costs:
        ///   • one ShieldActivate SFX per prism laid — audible even though the morph is not;
        ///   • a shield-morph stamp on a prism whose grow-in bloom is the transition that
        ///     actually carries its continuity, so the two would animate over each other.
        /// Two costs this rule used to carry are GONE with the GPU morph migration
        /// (Docs/PRISM_ANIMATION.md §5 B4) and must not be cited as reasons any more: the
        /// morph no longer holds _exoticVisualActive across the reveal instant (which was
        /// the C13 repro — the ONE-SHOT grow stamp had no companion entity to land on and
        /// the prism snapped), and it no longer strands the prism on the un-batched
        /// GameObject renderer rebuilding a per-prism mesh every frame. The rule stands on
        /// the two reasons above.
        /// A shield engaged later, on a live prism (skim/steal/ability), still blooms.
        /// </summary>
        bool IsBirthTransition => prism != null && !prism.IsCreationComplete;

        private void Awake()
        {
            prism = GetComponent<Prism>();
            materialAnimator = GetComponent<MaterialPropertyAnimator>();
            teamManager = GetComponent<PrismTeamManager>();

            // Every prism gets an octahedron shield. If the prefab already
            // carries one (e.g. BlueBlock has it wired explicitly) we reuse
            // it; otherwise we add one at runtime so existing prefabs don't
            // need to be touched individually. The component's Awake resolves
            // BoxCollider / MeshFilter / Rigidbody from the same GameObject.
            octahedronShield = GetComponent<PrismOctahedronShield>();
            if (octahedronShield == null)
                octahedronShield = gameObject.AddComponent<PrismOctahedronShield>();
        }

        /// <summary>
        /// The shed-shard palette for a shield that is about to come off: the SO_ColorSet
        /// tier pair for what the shield was SHOWING — the octahedron wears the Shielded
        /// tier; the stellation deliberately wears the OPAQUE PLAIN team material, so its
        /// pair is Plain. Same source as PrismFactory's death debris, so shed armour can
        /// never drift from the mass it guarded. The renderer cannot be read instead:
        /// every state change below binds its END-STATE material before it disengages, so
        /// by then the renderer already wears the INCOMING tier (which is exactly the
        /// plain-coloured-shards defect this fixes). Null colours (unauthored domain) fall
        /// back to RequestShatter's renderer read.
        /// </summary>
        void GetShedColors(PrismKind wornTier, out Color? bright, out Color? dark)
        {
            bright = null;
            dark = null;
            if (_themeManagerData == null || _themeManagerData.ColorSet == null || teamManager == null) return;
            if (_themeManagerData.ColorSet.TryGetPrismKindColors(teamManager.Domain, wornTier, out var b, out var d))
            {
                bright = b;
                dark = d;
            }
        }

        public void MakeDangerous()
        {
            prism.prismProperties.IsDangerous = true;
            prism.prismProperties.speedDebuffAmount = 0.1f;
            prism.prismProperties.IsShielded = false;
            // Danger is mutually exclusive with BOTH shield tiers (matching how
            // ActivateSuperShield clears IsDangerous). Leaving IsSuperShielded set
            // makes the danger prism invulnerable AND stops AOE explosions dead
            // (both the Burst batch path and ExecuteCommonPrismCommands destroy
            // the explosion on the super-shield flag).
            prism.prismProperties.IsSuperShielded = false;

            materialAnimator.UpdateMaterial(
                _themeManagerData.GetTeamTransparentDangerousBlockMaterial(teamManager.Domain),
                _themeManagerData.GetTeamDangerousBlockMaterial(teamManager.Domain)
            );
            CurrentState = BlockState.Dangerous;

            bool birth = IsBirthTransition;
            GetShedColors(PrismKind.Shielded, out var octBright, out var octDark);
            GetShedColors(PrismKind.Plain, out var stellaBright, out var stellaDark);
            if (octahedronShield != null) octahedronShield.Disengage(birth, default, 0f, octBright, octDark);
            if (stellatedShield != null) stellatedShield.Disengage(birth, default, 0f, stellaBright, stellaDark);

            // Mirror the cleared IsShielded flag into the spatial index so the
            // shell view retires this prism's analytic shell. Without this, a
            // danger-converted ex-shielded prism keeps its stale shell entry and
            // runs the exact shell narrowphase against every probe every frame,
            // forever (its hits are filtered on the managed side, so this is a
            // pure perf leak - but an unbounded one).
            SyncAOERegistryShieldState();
        }

        public void ActivateShield(float? duration = null)
        {
            // Cancel any pending timer before applying new state
            PrismTimerManager.EnsureInstance().CancelTimers(this);

            ApplyShieldState();

            if (duration.HasValue)
            {
                PrismTimerManager.EnsureInstance().ScheduleShieldDeactivation(this, duration.Value);
            }
        }

        public void ActivateSuperShield()
        {
            PrismTimerManager.EnsureInstance().CancelTimers(this);

            prism.prismProperties.IsSuperShielded = true;
            prism.prismProperties.IsShielded = false;
            prism.prismProperties.IsDangerous = false;

            // Super-shield renders as the STELLATED octahedron (the Skim Race track look), so
            // keep the OPAQUE team material - the transparent super-shield material renders over
            // the stellation and hides it (see SegmentSpawner.SuperShieldSpawnedPrisms).
            materialAnimator.UpdateMaterial(
                _themeManagerData.GetTeamTransparentBlockMaterial(teamManager.Domain),
                _themeManagerData.GetTeamBlockMaterial(teamManager.Domain)
            );
            CurrentState = BlockState.SuperShielded;

            // Restore the box pose first (a shield→super transition would otherwise let the
            // lazily-added stellation cache the octahedron mesh as its "original"), then engage
            // the stellation - lazily added so only super-shielded prisms pay its mesh cost.
            bool birth = IsBirthTransition;
            GetShedColors(PrismKind.Shielded, out var octBright, out var octDark);
            if (octahedronShield != null) octahedronShield.Disengage(birth, default, 0f, octBright, octDark);
            if (stellatedShield == null)
                stellatedShield = GetComponent<PrismStellatedOctahedronShield>()
                                  ?? gameObject.AddComponent<PrismStellatedOctahedronShield>();
            stellatedShield.Engage(birth);

            SyncAOERegistryShieldState();
        }

        /// <param name="breakVelocity">
        /// RAW impact vector of the force that BROKE the shield, when the caller has one
        /// (Prism.Damage's vector, the Rhino sword's contact velocity). The disengage
        /// overlay is ordinary prism-explosion debris, so this is the same vector — with
        /// the same clamp semantics — a prism death hands its own debris
        /// (Docs/PRISM_ANIMATION.md §4.8.1). Zero degrades to the impactless-death puff.
        /// A DELAYED deactivation drops it — by the time that timer fires, whatever was
        /// moving when it was scheduled has moved on — and sheds isotropically instead
        /// (see <see cref="ExecuteTimerDeactivation"/>).
        /// </param>
        /// <param name="debrisSpeedLimit">True-velocity ceiling, as on Prism.Damage; 0 = authored band.</param>
        public void DeactivateShields(float? delay = null, Vector3 breakVelocity = default,
            float debrisSpeedLimit = 0f)
        {
            PrismTimerManager.EnsureInstance().CancelTimers(this);

            if (delay.HasValue)
            {
                PrismTimerManager.EnsureInstance().ScheduleShieldDeactivation(this, delay.Value);
            }
            else
            {
                ApplyNormalState(breakVelocity, debrisSpeedLimit);
            }
        }

        /// <summary>
        /// Called by PrismTimerManager when a scheduled deactivation timer expires — the
        /// end of every TEMPORARY shield (<c>ActivateShield(duration)</c> and
        /// <c>DeactivateShields(delay)</c> both land here), and the one and only place a
        /// shield comes off with no breaking force behind it.
        ///
        /// That is exactly the case the temporary shield exists for: an explosion meeting
        /// its own domain's mass shields the prism rather than passing through it, so the
        /// blast reads as ACCEPTED instead of as clipping. The pop that ends it therefore
        /// has to read as a pop — so the shards are shed along
        /// <see cref="TimedPopBreakVelocity"/>, a random direction on the unit sphere at
        /// the debris band's own authored minimum speed. Without it every timed pop handed
        /// the shatter a zero vector, which <c>GeometryUtils.ClampMagnitude</c> resolves to
        /// the stable <c>Vector3.up</c> fallback: the whole arena's shields drifting
        /// upward in lockstep at one speed.
        /// </summary>
        internal void ExecuteTimerDeactivation()
        {
            ApplyNormalState(TimedPopBreakVelocity());
        }

        /// <summary>
        /// The isotropic minimum puff a timed shield pop sheds along. Magnitude is the
        /// debris pipeline's OWN authored floor (the pooled <c>PrismExplosion</c> prefab's
        /// <c>minSpeed</c>, read through <see cref="CosmicShore.Utility.PrismDebris.TryGetExplosionConfig"/>),
        /// never a number of this class's own: a shield shard is ordinary prism-explosion
        /// debris (Docs/PRISM_ANIMATION.md §4.8.1), so "small" is already authored once for
        /// the whole game and a local constant would be a second, drifting copy of it. Only
        /// the DIRECTION is added here. If the config is unavailable the shatter is refused
        /// upstream anyway, so the unit vector that falls out is inert.
        /// </summary>
        static Vector3 TimedPopBreakVelocity()
        {
            float speed = CosmicShore.Utility.PrismDebris.TryGetExplosionConfig(out _, out float minSpeed, out _)
                ? minSpeed
                : 1f;
            return UnityEngine.Random.onUnitSphere * speed;
        }

        private void ApplyShieldState()
        {
            prism.prismProperties.IsShielded = true;
            prism.prismProperties.IsDangerous = false;

            materialAnimator.UpdateMaterial(
                _themeManagerData.GetTeamTransparentShieldedBlockMaterial(teamManager.Domain),
                _themeManagerData.GetTeamShieldedBlockMaterial(teamManager.Domain)
            );
            CurrentState = BlockState.Shielded;

            // Engage the octahedron visual/collider swap for the regular
            // shield state too, matching super shield behavior.
            bool birth = IsBirthTransition;
            if (octahedronShield != null) octahedronShield.Engage(birth);

            SyncAOERegistryShieldState();
            if (!birth) AudioSystem.Instance.PlayGameplaySFX(GameplaySFXCategory.ShieldActivate);
        }

        private void ApplyNormalState(Vector3 breakVelocity = default, float debrisSpeedLimit = 0f)
        {
            var wasShielded = prism.prismProperties.IsShielded || prism.prismProperties.IsSuperShielded;

            materialAnimator.UpdateMaterial(
                _themeManagerData.GetTeamTransparentBlockMaterial(teamManager.Domain),
                _themeManagerData.GetTeamBlockMaterial(teamManager.Domain)
            );

            prism.prismProperties.IsShielded = false;
            prism.prismProperties.IsSuperShielded = false;
            CurrentState = BlockState.Normal;

            // Only THIS teardown carries a breaking force. MakeDangerous and
            // ActivateSuperShield also disengage, but those are state changes, not
            // blows — they stay impactless, which the debris path renders as the same
            // quiet minimum-speed puff an impactless prism death gets.
            bool birth = IsBirthTransition;
            GetShedColors(PrismKind.Shielded, out var octBright, out var octDark);
            GetShedColors(PrismKind.Plain, out var stellaBright, out var stellaDark);
            if (octahedronShield != null) octahedronShield.Disengage(birth, breakVelocity, debrisSpeedLimit, octBright, octDark);
            if (stellatedShield != null) stellatedShield.Disengage(birth, breakVelocity, debrisSpeedLimit, stellaBright, stellaDark);

            SyncAOERegistryShieldState();

            if (wasShielded && !birth)
                AudioSystem.Instance.PlayGameplaySFX(GameplaySFXCategory.ShieldDeactivate);
        }

        private void SyncAOERegistryShieldState()
        {
            if (prism.SpatialIndexId < 0) return;

            PrismSpatialIndex.Instance?.UpdateShieldState(
                prism.SpatialIndexId,
                prism.prismProperties.IsShielded,
                prism.prismProperties.IsSuperShielded);

            // Shielded mass is not food (Docs/ECOSYSTEM.md §16.2), so it must not be a
            // fauna steering target either: re-file the prism in its cell's targeting
            // grids on every shield transition. The cell no-ops when the classification
            // did not actually change, so re-applying an existing shield costs a compare.
            PrismSpatialIndex.Instance?.ForwardShieldChangeToCell(prism.SpatialIndexId);
        }

        private void OnDisable()
        {
            PrismTimerManager.Instance?.CancelTimers(this);
        }

        private void OnDestroy()
        {
            PrismTimerManager.Instance?.CancelTimers(this);
        }
    }
}
