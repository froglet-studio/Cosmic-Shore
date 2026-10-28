using System;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Data;
namespace CosmicShore.Gameplay
{
    public class OmniCrystalImpactor : CrystalImpactor
    {
        [SerializeField]
        VesselCrystalEffectSO[] omniCrystalShipEffects;

        [SerializeField]
        ScriptableEventCrystalStats OnCrystalCollected;

        bool IsImpacting;

        // An Ark's reactor is drawing this crystal in: its flight outlasts the impact latch, and nothing else may
        // take it (or the Ark take it twice) on the way.
        bool _harvesting;

        void OnEnable() => _harvesting = false;

        /// <summary>
        /// Omni crystals are collectible by any vessel regardless of domain.
        /// Override in <see cref="TeamCrystalImpactor"/> for strict domain matching.
        /// </summary>
        protected virtual bool IsDomainMatching(Domains domain) => true;

        /// <summary>
        /// May a vessel of <paramref name="domain"/> collect this crystal? The same predicate the
        /// impact path enforces, exposed so a collector that is NOT a physics contact (a
        /// <see cref="CrystalRaceRing"/> threaded by its mouth) can skip a pilot the crystal would
        /// refuse anyway - rather than running the vessel's half of a pickup that never happens.
        /// </summary>
        public bool AcceptsDomain(Domains domain) => IsDomainMatching(domain);

        // Null manager = a manager-less local mint (e.g. the freestyle conveyor toy's omni pickups):
        // treat it as a local single-machine collect, never a network client.
        bool IsNetworkClient() => Crystal.CrystalManager != null && Crystal.CrystalManager.IsSpawned && !Crystal.CrystalManager.IsServer;

        protected override void AcceptImpactee(IImpactor impactee)
        {
            if (IsNetworkClient())
                return;

            if (IsImpacting)
                return;

            if (Crystal.IsExploding)
                return;

            // a hull whose touch leaves crystals alone (the Jacks netting, IHullContactGate) does not collect by contact
            if (!IsSweepDispatch && impactee is VesselImpactor gated && !gated.TakesCrystalsByContact)
                return;

            // Always enforce domain matching - vessels can only collect
            // crystals that belong to their domain (or unowned crystals).
            if (!IsDomainMatching(impactee.OwnDomain))
                return;
                
            
            switch (impactee)
            {
                case VesselImpactor shipImpactee:
                {
                    IsImpacting = true;
                    WaitForImpact().Forget();
                    
                    ExecuteEffect(shipImpactee);
                    RunVesselEffects(shipImpactee);

                    // The collecting pilot must FEEL the pickup on their own machine. Collection
                    // resolves server-only (above), so for a remote client's vessel every vessel
                    // effect - the Dolphin's jaw blast, the resource spend, the elemental level -
                    // ran on the server and nowhere else: that pilot saw the crystal vanish, no
                    // cone, and a meter that never emptied. Replay them on the owner. The server
                    // stays the sole authority for collection, respawn and every stat.
                    ReplayOnRemoteOwner(shipImpactee);

                    Crystal.Respawn();
                    break;
                }
            }
        }

        /// <summary>
        /// Runs this crystal's vessel-side effects against one vessel, locally. Split out so the
        /// owning client can run the identical list on its own machine (see
        /// <see cref="ReplayOnRemoteOwner"/>) instead of a second, drifting implementation.
        /// </summary>
        public void RunVesselEffects(VesselImpactor shipImpactee)
        {
            if (!DoesEffectExist(omniCrystalShipEffects)) return;

            CrystalImpactData data = CrystalImpactData.FromCrystal(Crystal);
            foreach (var effect in omniCrystalShipEffects)
                effect.Execute(shipImpactee, data);
        }

        void ReplayOnRemoteOwner(VesselImpactor shipImpactee)
        {
            var manager = Crystal.CrystalManager;
            if (manager == null || !manager.IsSpawned) return;

            var vesselObject = shipImpactee.GetComponentInParent<NetworkObject>();
            if (vesselObject == null || !vesselObject.IsSpawned) return;

            // Server-owned vessels (the host's own, and every AI) already ran the effects here.
            if (vesselObject.IsOwner) return;

            manager.ReplayVesselCrystalEffects(Crystal.Id, vesselObject.NetworkObjectId,
                                               vesselObject.OwnerClientId);
        }
        
        /// <summary>
        /// True when a blast of this domain may spend this crystal. Routes through the same
        /// <see cref="IsDomainMatching"/> predicate a vessel collect uses, so
        /// <see cref="TeamCrystalImpactor"/>'s strict domain rule holds for blasts too — a blast
        /// must not be a way around a gate a hull cannot pass.
        /// </summary>
        public bool CanBlastConsume(Domains blastDomain) =>
            !IsNetworkClient() && !IsImpacting && !_harvesting
            && Crystal != null && !Crystal.IsExploding
            && IsDomainMatching(blastDomain);

        /// <summary>
        /// Spend this crystal because a BLAST engulfed it (the Scarab's cavitation punch forging
        /// a ball). Same retirement as a vessel collect — the crystal blooms out through its own
        /// explode path and respawns, rather than popping out of existence — and the same
        /// half-second impact latch, so a growing blast cannot collect it twice.
        ///
        /// It takes the blast's domain and position instead of a vessel's, because there is no
        /// collector here: the thing that consumed the crystal is a wavefront.
        /// </summary>
        public void ConsumeByBlast(Domains blastDomain, Vector3 blastOrigin)
        {
            if (!CanBlastConsume(blastDomain)) return;

            IsImpacting = true;
            WaitForImpact().Forget();

            OnCrystalCollected?.Raise(
                new CrystalStats()
                {
                    PlayerName = string.Empty,   // a blast has no pilot standing on the crystal
                    Element = Crystal.crystalProperties.Element,
                    Value = Crystal.crystalProperties.crystalValue,
                }
            );

            Vector3 away = Crystal.transform.position - blastOrigin;
            var explode = new Crystal.ExplodeParams
            {
                Course = away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.forward,
                Speed = 0f,
                PlayerName = string.Empty,
            };
            if (Crystal.CrystalManager != null)
                Crystal.NotifyManagerToExplodeCrystal(explode);
            else
                Crystal.Explode(explode);

            Crystal.Respawn();
        }

        /// <summary>
        /// True when an Ark of <paramref name="arkDomain"/> may harvest this crystal now: the gate a blast or a vessel
        /// passes (domain, not mid-impact, not exploding, not a network client), and never a living heart.
        /// </summary>
        public bool CanHarvest(Domains arkDomain) => CanBlastConsume(arkDomain) && !Crystal.IsEmbedded;

        /// <summary>
        /// An ARK takes this crystal into its reactor (<see cref="CosmicShore.ScriptableObjects.ArkReactorSO"/>; Garrett,
        /// 2026-10-28). An Ark is the one body that contains an omni crystal and harvests its power instead of morphing
        /// it into something else, so no vessel effect runs: the crystal flies into <paramref name="reactor"/> on the
        /// project's capture feel (<see cref="CrystalCaptureConfigSO"/>: snatch, suction, absorb) and dissolves there,
        /// with its pickup sound and no husk (its body went into the well). The caller credits the power at this call,
        /// as scoring is credited at contact, never at the end of a flourish. A crystal a manager owns is the manager's
        /// to retire and respawn, so it takes the blast path's explode-and-respawn instead of a flight it would fight.
        /// Returns false, changing nothing, when the crystal cannot be taken.
        /// </summary>
        public bool HarvestInto(Domains arkDomain, Transform reactor)
        {
            if (!reactor || !CanHarvest(arkDomain)) return false;

            IsImpacting = true;
            WaitForImpact().Forget();

            OnCrystalCollected?.Raise(
                new CrystalStats()
                {
                    PlayerName = string.Empty,   // an Ark is not a pilot: nobody scores the harvest
                    Element = Crystal.crystalProperties.Element,
                    Value = Crystal.crystalProperties.crystalValue,
                }
            );

            if (Crystal.CrystalManager != null)
            {
                Vector3 toward = reactor.position - Crystal.transform.position;
                Crystal.NotifyManagerToExplodeCrystal(new Crystal.ExplodeParams
                {
                    Course = toward.sqrMagnitude > 1e-4f ? toward.normalized : Vector3.forward,
                    Speed = 0f,
                    PlayerName = string.Empty,
                });
                Crystal.Respawn();
                return true;
            }

            _harvesting = true;
            FlyIntoReactor(reactor).Forget();
            return true;
        }

        async UniTaskVoid FlyIntoReactor(Transform reactor)
        {
            var crystal = Crystal;
            if (crystal.TryGetComponent(out Collider col)) col.enabled = false;
            // The idle tumble and the tint block are the flight's for its duration (the capture's own rule).
            foreach (var spinner in crystal.GetComponentsInChildren<CosmicShore.UI.JustRotate>(true)) spinner.canRotate = false;
            crystal.BeginCaptureVisual();

            var cfg = CrystalCaptureConfigSO.Load();
            var t = crystal.transform;
            Vector3 baseScale = t.localScale;
            Quaternion baseRotation = t.rotation;
            float radius = Mathf.Max(0.01f, t.lossyScale.x);
            Vector3 start = t.position;
            Vector3 target = reactor ? reactor.position : start;
            Vector3 toReactor = target - start;
            Vector3 away = toReactor.sqrMagnitude > 1e-4f ? -toReactor.normalized : UnityEngine.Random.onUnitSphere;
            Vector3 anchor = start + away * (cfg.SnatchRecoilRadii * radius);
            Vector3 arcAxis = Vector3.Cross(toReactor.sqrMagnitude > 1e-4f ? toReactor : away, Vector3.up);
            arcAxis = arcAxis.sqrMagnitude > 1e-4f ? arcAxis.normalized : Vector3.right;
            Vector3 spinAxis = UnityEngine.Random.onUnitSphere;

            float elapsed = 0f;
            float total = cfg.TotalDuration;
            while (elapsed < total)
            {
                if (!crystal) return;   // its cell was struck mid-flight: the power was already credited
                var phase = cfg.ResolvePhase(elapsed, out float u);
                if (phase == CrystalCaptureConfigSO.Phase.Done) break;

                // Track the reactor live: the Ark is under way.
                if (reactor) target = reactor.position;
                Vector3 position = phase switch
                {
                    CrystalCaptureConfigSO.Phase.Snatch =>
                        Vector3.LerpUnclamped(start, anchor, CrystalCaptureConfigSO.SnatchProgress01(u)),
                    CrystalCaptureConfigSO.Phase.Suction =>
                        Vector3.LerpUnclamped(anchor, target, cfg.FlightProgress01(u))
                        + arcAxis * (CrystalCaptureConfigSO.ArcOffset01(u) * cfg.SuctionArcRadii * radius),
                    _ => target,
                };
                t.SetPositionAndRotation(position, baseRotation * Quaternion.AngleAxis(cfg.SpinDegrees(phase, u), spinAxis));
                t.localScale = baseScale * cfg.ScaleMultiplier(phase, u);
                crystal.ApplyCaptureVisual(cfg.FlareMultiplier(phase, u), CrystalCaptureConfigSO.Opacity(phase, u));

                elapsed += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update);
            }
            if (!crystal) return;

            crystal.Explode(new Crystal.ExplodeParams
            {
                Course = reactor ? reactor.forward : Vector3.forward,
                Speed = 0f,
                PlayerName = string.Empty,
                SuppressHusk = true,
            });
            crystal.Respawn();
        }

        void ExecuteEffect(VesselImpactor vesselImpactee)
        {
            var shipStatus = vesselImpactee.Vessel.VesselStatus;

            OnCrystalCollected?.Raise(
                new CrystalStats()
                {
                    PlayerName = shipStatus.PlayerName,
                    Element =  Crystal.crystalProperties.Element,
                    Value =  Crystal.crystalProperties.crystalValue,
                }
            );

            // Every vessel plays the spent-crystal payoff. (A legacy guard used to skip it for
            // the Manta — a vestige of the pre-bomb flower design that muted the exact beat
            // KABLOOM is built on; retired with the Manta spec re-cut.)
            var explode = new Crystal.ExplodeParams
            {
                Course = shipStatus.Course,
                Speed = shipStatus.Speed,
                PlayerName = shipStatus.PlayerName,
                // A hull with its own retirement carries the crystal's body itself (the Squirrel
                // morphs it into its boost ring), so only the spray is withheld — the sound and the
                // latch belong to the pickup. That animation runs from
                // VesselImpactor.ExecuteOmniCrystalImpact on every peer; this flag reaches every peer
                // on the payload the manager already broadcasts.
                SuppressHusk = vesselImpactee.OmniCrystalRetirement != null,
            };
            // Manager-less local mint (conveyor toy): explode locally so the spent-crystal
            // VFX still plays (continuity of existence) instead of the crystal popping out.
            if (Crystal.CrystalManager != null)
                Crystal.NotifyManagerToExplodeCrystal(explode);
            else
                Crystal.Explode(explode);
        }
        
        /// <summary>
        /// This is to forbid multiple impacts due to multiple vessel colliders
        /// </summary>
        async UniTask WaitForImpact()
        {
            await UniTask.WaitForSeconds(0.5f);
            IsImpacting = false;
        }
    }
}