using System.Collections.Generic;
using System.Threading;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using FMODUnity;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Runtime executor for <see cref="UrchinTrackActionSO"/> — the Urchin's Track Projector.
    /// See <c>URCHIN_TRACK_PROJECTOR.md</c>.
    ///
    /// Lays a straight, single-lane stretch of trail out in front of the nose so the vessel has
    /// something to latch onto in open space. Every prism goes through
    /// <see cref="BoostRingBuilder.LayOne"/> — the shared pooled-prism primitive behind the
    /// omnicrystal ring, the joust ring and the Squirrel's tube — which is what buys the two
    /// properties this ability lives or dies on:
    ///
    ///  1. <b>A FULL-SIZE collider from frame 0</b> (<see cref="PrismType.Boost"/>, waitTime 0 +
    ///     <c>HoldColliderAtFullSize</c>). A ramp you cannot hit at grind speed is not a ramp,
    ///     and the ordinary trail-prism spawn delay exists precisely to let a vessel get CLEAR
    ///     of the mass it lays — the opposite of what this ability wants.
    ///  2. <b>Trail membership stamped AFTER Initialize</b>. Pool reuse clears membership, so a
    ///     stamp made earlier is silently wiped and the prisms read as container-less
    ///     Singletons — which the dimension ladder correctly refuses to rail-grind. The builder
    ///     honours that contract already; do not lay these prisms by hand.
    ///
    /// The prisms are ordinary conserved mass in the pilot's own domain: the ride reads them as
    /// friendly terrain, <c>FinalBlockSlideEffects</c> GROWS them as it passes, and fauna graze
    /// them like any other trail. Nothing here removes mass on a clock — the pooled prisms are
    /// returned only at a turn boundary, the same active, explicit event class as a scene load.
    /// </summary>
    public sealed class UrchinTrackActionExecutor : ShipActionExecutorBase
    {
        [Header("Scene Refs")]
        [Tooltip("Pooled-prism spawn channel (EventOnSpawnPrismAndReturn) - the same asset the " +
                 "vessel trail uses. BoostRingBuilder routes the lay to the dedicated Boost pool " +
                 "(fast bloom, full-size collider from frame 0). Never Instantiated.")]
        [SerializeField] PrismEventChannelWithReturnSO prismSpawnChannel;

        [Header("Events")]
        [SerializeField] ScriptableEventNoParam OnMiniGameTurnEnd;

        [Header("Audio")]
        [Tooltip("FMOD event when a track is projected. Leave empty for silence - never point " +
                 "it at a borrowed event to hear something.")]
        [SerializeField] EventReference deployEvent;

        float _cooldownEndTime;
        float _activeCooldown;

        CancellationTokenSource _spawnCts;

        // Pooled prisms laid by this executor, tracked so they can be returned to the pool at a
        // turn boundary. ReturnToPool self-unsubscribes, so it is safe on an already-returned
        // prism (one grazed by fauna, or destroyed by another player).
        readonly List<Prism> _trackPrisms = new();

        // The ribbons those prisms belong to - ONE per deploy, and the identity check that makes
        // the teardown safe. A track prism that was destroyed and RECYCLED is, by then, someone
        // else's mass: pool reuse clears trail membership (Prism.ResetState) and the next lay
        // stamps its own, so "is this still in a trail I laid?" is the exact question, and the
        // alternative (returning every prism this executor ever touched) would yank a live prism
        // out of whatever it had become.
        readonly List<Trail> _trackTrails = new();

        /// <summary>
        /// Cooldown remaining as a 0-1 fraction: 1 immediately after a deploy, 0 when ready
        /// again. Nothing reads it yet — the Urchin has no HUD prefab — but it is the surface a
        /// cooldown icon binds to the day it gets one, and it costs nothing to expose.
        /// </summary>
        public float CooldownRemaining01 =>
            _activeCooldown <= 0f ? 0f : Mathf.Clamp01((_cooldownEndTime - Time.time) / _activeCooldown);

        /// <summary>True when the track can be projected again (off cooldown).</summary>
        public bool TrackReady => Time.time >= _cooldownEndTime;

        void OnEnable()
        {
            OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
        }

        void OnDisable()
        {
            OnMiniGameTurnEnd.OnRaised -= OnTurnEndOfMiniGame;
            Cleanup();
        }

        void OnTurnEndOfMiniGame() => Cleanup();

        /// <summary>
        /// Initialize re-runs on a LIVE component (a vessel swap, a Cellular Duel ownership
        /// change), so a lay still in flight must be stopped here — unconditionally, above any
        /// pilot gate — or it finishes in the previous pilot's name and domain. The prisms
        /// already laid are NOT recalled: they are conserved mass that a pilot actively placed.
        /// The cooldown resets, because it belonged to the pilot who spent it.
        /// </summary>
        public override void Initialize(IVesselStatus shipStatus)
        {
            CancelSpawn();
            _cooldownEndTime = 0f;
            _activeCooldown = 0f;
        }

        // ---------------- API ----------------

        /// <summary>Press: project the track straight out in front of the vessel.</summary>
        public void Begin(UrchinTrackActionSO so, IVesselStatus status)
        {
            if (!so || status?.Vessel?.Transform == null) return;
            if (Time.time < _cooldownEndTime) return;   // on cooldown → no-op

            if (!prismSpawnChannel)
            {
                CSDebug.LogWarning("[UrchinTrack] prismSpawnChannel not wired - cannot project a track.");
                return;
            }

            var vessel = status.Vessel.Transform;

            // Lead the mouth by the vessel's speed so a fast Urchin gets room to line up,
            // floored so it never forms inside the hull when slow. The axis is the NOSE, not
            // Course: the ramp goes where the pilot is AIMING, which is what makes placing it a
            // decision rather than a reflex.
            float offset = Mathf.Max(so.ForwardOffset, status.Speed * so.LeadSeconds);
            var pose = new Pose(vessel.position + vessel.forward * offset, vessel.rotation);

            CancelSpawn();
            _spawnCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            LayTrackAsync(so, status, pose, so.ResolveLength(status), _spawnCts.Token).Forget();

            _activeCooldown = so.Cooldown;
            _cooldownEndTime = Time.time + so.Cooldown;

            if (!deployEvent.IsNull)
            {
                var audio = AudioSystem.Instance;
                if (audio) audio.PlaySFXEvent(deployEvent, pose.position);
            }
        }

        // ---------------- The lay ----------------

        /// <summary>
        /// One deploy = one <see cref="Trail"/>, so the stretch is a ribbon in its own right:
        /// the rider latches onto it, grinds its whole length and launches off its end rather
        /// than finding itself halfway along some earlier deploy. Left OPEN (not a loop) on
        /// purpose — running out of rail is the ability's ending, and the end-of-ribbon launch
        /// is what turns that into the payoff.
        /// </summary>
        async UniTaskVoid LayTrackAsync(UrchinTrackActionSO so, IVesselStatus status, Pose pose,
                                        float length, CancellationToken ct)
        {
            var trail = new Trail { Dimension = PrismscapeDimension.Trail };
            _trackTrails.Add(trail);

            int count = so.PrismCountForLength(length);
            string playerName = status.PlayerName;
            Domains domain = status.Domain;

            // The prism's local +z runs ALONG the track - the authored-z invariant the whole 1D
            // ride rests on (Trail.HeadingAt reads the geometry, and a track laid broadside
            // would be ridden sideways). pose.rotation is the vessel's, whose forward IS the
            // track axis, so no extra construction is needed or wanted.
            for (int i = 0; i < count; i++)
            {
                if (ct.IsCancellationRequested) return;

                Vector3 position = pose.position + pose.rotation * (Vector3.forward * (i * so.PrismSpacing));

                BoostRingBuilder.LayOne(prismSpawnChannel, position, pose.rotation,
                    so.PrismScale, PrismKind.Plain, domain, playerName,
                    $"{playerName}::Track::{i}", trail, _trackPrisms);

                if ((i + 1) % so.SpawnPerFrame == 0)
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }

        // ---------------- Cleanup ----------------

        void CancelSpawn()
        {
            _spawnCts?.Cancel();
            _spawnCts?.Dispose();
            _spawnCts = null;
        }

        void Cleanup()
        {
            CancelSpawn();

            // Return pooled prisms rather than destroying them. ReturnToPool self-unsubscribes,
            // so an already-returned prism is a safe no-op; the trail-identity test is what keeps
            // a RECYCLED prism - one that died here and was handed out to another lay site - from
            // being pulled out from under its new owner.
            for (int i = 0; i < _trackPrisms.Count; i++)
            {
                var p = _trackPrisms[i];
                if (!p || p.destroyed) continue;
                if (p.Trail == null || !_trackTrails.Contains(p.Trail)) continue;
                PrismKinds.Clear(p);
                p.ReturnToPool();
            }
            _trackPrisms.Clear();
            _trackTrails.Clear();
        }
    }
}
