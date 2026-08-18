using System;
using CosmicShore.Data;
using CosmicShore.Utility;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The one place a ball is minted (design: R_VesselActions/SCARAB.md §4.1). Two things forge
    /// balls — a vessel flying through a crystal
    /// (<see cref="ScarabBallForgeByCrystalEffectSO"/>) and a blast engulfing one
    /// (<see cref="ScarabBallForgeByExplosionEffectSO"/>) — and they must produce the SAME object
    /// from the same rules, so the spawn, the network gate and the SPACE size stamp live here
    /// rather than being written twice and drifting.
    ///
    /// BOTH PATHS ARE SERVER-AUTHORITATIVE, AND THE BLAST PATH IS THEREFORE HOST-ONLY TODAY.
    /// The ball is a NetworkObject, so only the server may spawn one. The vessel-impact path
    /// reaches the server for free through <c>NetworkVesselImpactor</c>'s ServerRpc → ClientRpc
    /// fan-out, so it works for every pilot. The BLAST path does not: <c>ScarabJukeController</c>
    /// reads local input in <c>Update</c>, so a client's cavitation explosion exists only on that
    /// client — and the crystal it engulfs cannot be spent there either, because
    /// <c>OmniCrystalImpactor.CanBlastConsume</c> refuses on a network client exactly as every
    /// other crystal collect does. A client's blast therefore forges nothing.
    ///
    /// A client→server hop was written for this and REMOVED, because it could never be reached:
    /// the crystal-consumption gate runs first and returns before the forge effect executes, so
    /// the RPC was plumbing that described a fix it could not deliver. Closing the gap properly
    /// needs ONE round-trip that carries the crystal's id and lets the SERVER do both halves
    /// (consume + forge) — the crystal is the authoritative object, not the ball. Recorded as a
    /// follow-up in SCARAB.md §4.1; do not re-add a forge-only RPC.
    /// </summary>
    public static class ScarabBallForge
    {
        /// <summary>
        /// Optional MODE POLICY on whether this pilot may forge right now — null (the default,
        /// and every mode-less context: freestyle, the menu) means always. Scarab Scramble
        /// installs its per-domain live-ball cap here and clears it on despawn, the
        /// <c>AIPilot.SetExternalTargetProvider</c> shape: a server-side hook a mode owns for
        /// its lifetime, never left behind. A refusal is quiet by design — the crystal simply
        /// collects normally instead (its sibling effects still run), so "at the cap" degrades
        /// to ordinary crystal income rather than a dead pickup.
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

            var ball = Object.Instantiate(prefab, at, Quaternion.identity);
            if (!ball.TryGetComponent(out NetworkObject netObj))
            {
                CSDebug.LogError("[ScarabBallForge] Ball prefab has no NetworkObject — destroying.");
                Object.Destroy(ball.gameObject);
                return null;
            }

            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening)
                netObj.Spawn(destroyWithScene: true);

            // Stamped once — the ball keeps the size it was born with.
            ball.SetSizeScale(sizeScale);
            ball.LaunchServer(at, velocity, domain);
            return ball;
        }

    }
}
