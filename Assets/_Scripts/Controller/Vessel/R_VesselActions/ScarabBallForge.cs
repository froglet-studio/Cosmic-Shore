using System;
using CosmicShore.Data;
using CosmicShore.Utility;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The one place a ball is minted (design: R_VesselActions/SCARAB.md §4.1). Two things forge
    /// balls — a Scarab's SKIMMER touching a crystal
    /// (<see cref="ScarabBallForgeBySkimmerCrystalEffectSO"/>, the primary path) and a blast
    /// engulfing one (<see cref="ScarabBallForgeByExplosionEffectSO"/>) — and they must produce the
    /// SAME object from the same rules, so the spawn, the network gate and the SPACE size stamp
    /// live here rather than being written twice and drifting.
    ///
    /// BOTH PATHS ARE SERVER-AUTHORITATIVE. The ball is a NetworkObject, so only the server may
    /// spawn one. The SKIMMER path needs no round-trip to be fair: the server simulates every
    /// vessel, so the server's copy of any pilot's skimmer overlaps the crystal and converts it,
    /// remote clients included. The BLAST path is host-only today: <c>ScarabJukeController</c>
    /// reads local input in <c>Update</c>, so a client's cavitation explosion exists only on that
    /// client — and the crystal it engulfs cannot be spent there either, because
    /// <c>OmniCrystalImpactor.CanBlastConsume</c> refuses on a network client exactly as every
    /// other crystal collect does. A client's blast therefore forges nothing.
    ///
    /// A client→server hop was written for the blast and REMOVED, because it could never be
    /// reached: the crystal-consumption gate runs first and returns before the forge effect
    /// executes, so the RPC was plumbing that described a fix it could not deliver. Closing that
    /// gap properly needs ONE round-trip carrying the crystal's id, letting the SERVER do both
    /// halves (consume + forge) — the crystal is the authoritative object, not the ball. Recorded
    /// as a follow-up in SCARAB.md §4.1; do not re-add a forge-only RPC.
    ///
    /// A THIRD path was deleted: a hull-collision forge that spawned the ball ahead of the vessel
    /// carrying a fraction of its velocity, trying to make collecting a crystal feel like striking
    /// a ball. The skimmer conversion makes the feeling real instead of imitating it — see
    /// <see cref="ScarabBallForgeBySkimmerCrystalEffectSO"/> — so do not reintroduce launch
    /// velocity, minimum launch speed, or a forward clearance offset at the forge.
    /// </summary>
    public static class ScarabBallForge
    {
        /// <summary>
        /// Optional MODE POLICY on whether this pilot may forge right now — null (the default,
        /// and every mode-less context: freestyle, the menu) means always. A refusal is quiet by
        /// design — the crystal simply collects normally instead (its sibling effects still run),
        /// so a refusal degrades to ordinary crystal income rather than a dead pickup.
        ///
        /// **NOTHING INSTALLS THIS TODAY.** Scarab Scramble used to hang its per-domain live-ball
        /// cap here; that cap was replaced by the per-CELL ball limit on the ball itself
        /// (<c>AstroLeagueBall.cellBallLimit</c>), because a gate at the FORGE could only ever see
        /// balls that were forged — it was blind to the ones a Scarab knocks loose out of the
        /// nucleus. The hook is kept as the <c>AIPilot.SetExternalTargetProvider</c> shape (a
        /// server-side policy slot a mode owns for its lifetime and clears on despawn) for a mode
        /// that genuinely wants to gate FORGING specifically. Do not reach for it to bound a ball
        /// POPULATION — that lesson is already paid for.
        /// </summary>
        public static Func<IVesselStatus, bool> ForgeGate;

        /// <summary>
        /// Raised on the peer that minted a ball (the server, or a no-network local session),
        /// immediately after launch. This is how a mode ADOPTS a forged ball — boundary handoff,
        /// ownership lock, per-ball attribution — without the vessel knowing any mode exists.
        /// The forger is the ball's maker; the ball already carries their domain.
        /// </summary>
        public static event Action<AstroLeagueBall, IVesselStatus> OnForged;

        /// <summary>True on the server, and also in a session with no NetworkManager at all
        /// (a local single-machine mint — the freestyle toys do this).</summary>
        public static bool CanSpawnLocally
        {
            get
            {
                var nm = NetworkManager.Singleton;
                return nm == null || !nm.IsListening || nm.IsServer;
            }
        }

        /// <summary>
        /// SPACE → ball SIZE (SCARAB.md §7): ×1 at rest, ×4 at Space 10. The map's generic
        /// multiplier IS the carrier, so there is no authored field to double-dip against.
        /// </summary>
        public static float SizeScaleFor(IVesselStatus status) =>
            status != null && status.ElementalAbilityHandler != null
                ? Mathf.Max(0.1f, status.ElementalAbilityHandler.Multiplier(Element.Space))
                : 1f;

        /// <summary>
        /// Forge a ball for <paramref name="status"/>'s domain, on a peer that may spawn one.
        /// Returns null (silently) on a client — see the class note on why that is the honest
        /// shape rather than a round-trip that cannot be reached — and null when the installed
        /// <see cref="ForgeGate"/> refuses. This is the ONE entry point both forge paths use;
        /// call it rather than <see cref="Spawn"/> so the gate and <see cref="OnForged"/> can
        /// never be bypassed by one path and honoured by the other.
        /// </summary>
        public static AstroLeagueBall Request(IVesselStatus status, AstroLeagueBall prefab,
                                              Vector3 at, Vector3 velocity)
        {
            if (status == null || !CanSpawnLocally) return null;
            if (ForgeGate != null && !ForgeGate(status)) return null;

            var ball = Spawn(prefab, at, velocity, status.Domain, SizeScaleFor(status));
            if (ball != null) OnForged?.Invoke(ball, status);
            return ball;
        }

        /// <summary>Server-side (or local-only) spawn — the one place a ball is instantiated.</summary>
        public static AstroLeagueBall Spawn(AstroLeagueBall prefab, Vector3 at, Vector3 velocity,
                                            Domains domain, float sizeScale)
        {
            if (prefab == null)
            {
                CSDebug.LogError("[ScarabBallForge] No ball prefab — cannot forge. Wire the " +
                                 "forge effect's _ballPrefab.");
                return null;
            }

            // Fully qualified: this file's `using System;` (for Func/event Action on the forge
            // gate) makes a bare `Object` ambiguous with System.Object.
            var ball = UnityEngine.Object.Instantiate(prefab, at, Quaternion.identity);
            if (!ball.TryGetComponent(out NetworkObject netObj))
            {
                CSDebug.LogError("[ScarabBallForge] Ball prefab has no NetworkObject — destroying.");
                UnityEngine.Object.Destroy(ball.gameObject);
                return null;
            }

            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening)
                netObj.Spawn(destroyWithScene: true);

            // Stamped once — the ball keeps the size it was born with.
            ball.SetSizeScale(sizeScale);
            ball.LaunchServer(at, velocity, domain);

            // PLATFORM RULE (SCARAB.md §4.2), not a mode policy: a ball a Scarab forged is its
            // maker's from birth to death. Every later claim site refuses — a strike moves it,
            // a blast moves it, neither re-colours it — with ONE exception, the juke-dash STEAL.
            // Locking here rather than in a controller is what makes the pair "permanent colour
            // + dash steals it" travel with the VESSEL into every context a Scarab can forge in
            // (freestyle, the menu, any future mode), instead of existing only where a mode
            // remembered to install it. Astro League's scene-placed match ball never routes
            // through the forge, so its last-touch colouring is untouched.
            ball.SetOwnershipLockedServer(true);
            return ball;
        }

    }
}
