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
    /// from the same rules, so the spawn, the network gate, the SPACE size stamp and the
    /// client→server route live here rather than being written twice and drifting.
    ///
    /// THE NETWORK ROUTE IS THE WHOLE REASON THIS IS SHARED. The ball is a NetworkObject: only
    /// the server may spawn one. The vessel-impact path gets to the server for free, because it
    /// arrives through <c>NetworkVesselImpactor</c>'s ServerRpc → ClientRpc fan-out. A BLAST does
    /// not: <c>ScarabJukeController</c> reads local input in <c>Update</c>, so the cavitation
    /// explosion exists only on the pilot's own machine, and a plain server gate would silently
    /// mean "only the host can forge from a blast". <see cref="Request"/> therefore falls back to
    /// <see cref="Player.RequestForgeBall_ServerRpc"/>, which mirrors the fauna-kill and
    /// combat-hit precedents: the client asks, the SERVER decides everything that matters
    /// (domain, size, and the prefab itself, read off that player's own vessel), so a client can
    /// only ever forge its own ball.
    /// </summary>
    public static class ScarabBallForge
    {
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
        /// Forge a ball if this peer may, otherwise ask the server to. Returns the ball only when
        /// it was spawned inline.
        /// </summary>
        public static AstroLeagueBall Request(IVesselStatus status, AstroLeagueBall prefab,
                                              Vector3 at, Vector3 velocity)
        {
            if (status == null) return null;

            if (CanSpawnLocally)
                return Spawn(prefab, at, velocity, status.Domain, SizeScaleFor(status));

            // Client: the server owns the spawn. It re-derives domain and size from its own copy
            // of this player's vessel, so nothing authoritative rides the wire.
            var player = status.Player as Player;
            if (player == null || !player.IsSpawned || !player.IsOwner) return null;
            player.RequestForgeBall_ServerRpc(at, velocity);
            return null;
        }

        /// <summary>Server-side (or local-only) spawn. Public so the ServerRpc handler can reuse
        /// it without re-entering the routing decision it has already made.</summary>
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

        /// <summary>
        /// The ball prefab this vessel forges with, read off its OWN crystal-effect container —
        /// the only place it is authored. This is what lets the ServerRpc handler mint a ball for
        /// a client without the prefab reference travelling the wire.
        /// </summary>
        public static AstroLeagueBall ResolvePrefabFor(IVessel vessel)
        {
            if (vessel?.Transform == null) return null;
            if (!vessel.Transform.TryGetComponent(out VesselImpactor impactor)) return null;

            var effects = impactor.CrystalEffects;
            if (effects == null) return null;
            for (int i = 0; i < effects.Length; i++)
                if (effects[i] is ScarabBallForgeByCrystalEffectSO forge && forge.BallPrefab != null)
                    return forge.BallPrefab;
            return null;
        }
    }
}
