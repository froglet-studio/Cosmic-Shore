using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Utility;
using Reflex.Core;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Everything a planted Manta bomb needs to bloom WITHOUT its planter: bombs outlive a vessel swap and a pilot's
    /// despawn, so the payload is snapshotted at PLANT time (the per-use-snapshot rule - Contagion is what the planter
    /// had earned when the bomb went on, not what they have when it goes off).
    /// </summary>
    public struct MantaBombSnapshot
    {
        public MantaStingConfigSO Config;
        public GameDataSO GameData;
        public string PlanterName;
        public Domains PlanterDomain;
        public IVessel PlanterVessel;          // may die before the fuse runs out - always guarded
        public MantaStingActionExecutor Owner; // ditto; the shared fuse lives here while it does
        public Container DiContainer;
        public bool Contagion;                 // CHARGE 5 snapshot
        public float FuseDeadline;             // the shared fuse as it stood at plant (the owner moves it on)
        public bool LocalHumanPlanter;         // may this machine draw the planter's markers?
    }

    /// <summary>Why a board went off - each pays its own bloom.</summary>
    public enum MantaDetonation
    {
        /// <summary>The shared fuse ran out - the small bloom.</summary>
        Fuse = 0,
        /// <summary>The Manta hit a prism and set the board off early - the small bloom.</summary>
        PrismImpact = 1,
        /// <summary>An omni crystal cashed the board - the big bloom, times the bomb multiplier.</summary>
        OmniCrystal = 2,
    }

    /// <summary>
    /// One silent Manta bomb riding a target - the deliverable of STING. Added to the carrier's root GameObject on the
    /// PLANTER's simulation machine only (bombs are local objects, like projectiles); its presence IS the
    /// one-bomb-per-target immunity. The target gets NO indication - the component renders nothing.
    ///
    /// <para><b>The 2026-11 overhaul.</b> Bombs no longer carry fuses of their own: every bomb a Manta has planted rides
    /// that Manta's ONE shared fuse (<see cref="MantaStingActionExecutor"/>), which each new bomb resets, so the board
    /// always goes off together. Bombs can no longer be scraped off - the knock-off counterplay is gone.</para>
    ///
    /// <para><b>Bombs travel.</b> A creature that dies carrying a bomb drops its heart crystal still bombed
    /// (<see cref="MoveTo"/>); whoever picks that crystal up - any vessel, the planter and the planter's allies
    /// included, who are spared by the bloom - picks the bomb up with it.</para>
    /// </summary>
    public class MantaBomb : MonoBehaviour
    {
        static readonly List<Prism> ContagionPrismScratch = new();

        MantaBombSnapshot _snap;
        Fauna _carrierFauna;              // set only for FAUNA carriers (body-prism liveness)
        ILifeFormEntity _carrierLifeform; // fauna OR flora; null for vessel and crystal carriers
        Crystal _carrierCrystal;          // set while the bomb rides a dropped heart crystal
        bool _ridesCrystal;               // the flag, not the reference: a destroyed crystal compares equal to null
        Crystal _heart;                   // a lifeform carrier's heart: where the bomb goes when it dies
        bool _resolved;
        bool _cascading;                  // committed to an omni cascade, waiting its turn
        float _cascadeAt;
        float _cascadeScale;
        MantaBombMarker _marker;

        public string PlanterName => _snap.PlanterName;
        public Domains PlanterDomain => _snap.PlanterDomain;

        /// <summary>Seconds left on the shared fuse this bomb rides.</summary>
        public float FuseRemaining => Mathf.Max(0f, (_snap.Owner ? _snap.Owner.FuseDeadline : _snap.FuseDeadline) - Time.time);

        /// <summary>Waiting its turn in an omni cascade - the marker reads it as fully critical.</summary>
        public bool IsCascading => _cascading;

        /// <summary>Riding a dropped crystal, waiting for whoever collects it.</summary>
        public bool IsOnCrystal => _ridesCrystal;

        /// <summary>Is this root already carrying a live bomb? (Tagging is denial.)</summary>
        public static bool IsBombed(GameObject root) => root && root.TryGetComponent<MantaBomb>(out _);

        /// <summary>
        /// Plants a bomb on <paramref name="targetRoot"/>. Returns null when the target is already bombed.
        /// <paramref name="resetsFuse"/> is false for a bomb that is only MOVING (a dropped heart, a pickup): moving a
        /// bomb is not planting one, so it must not buy the board more time.
        /// </summary>
        public static MantaBomb Plant(GameObject targetRoot, in MantaBombSnapshot snapshot,
                                      ILifeFormEntity carrierLifeform = null, Crystal carrierCrystal = null,
                                      bool resetsFuse = true)
        {
            if (!targetRoot || IsBombed(targetRoot)) return null;

            var bomb = targetRoot.AddComponent<MantaBomb>();
            bomb._snap = snapshot;
            bomb._carrierLifeform = carrierLifeform;
            bomb._carrierFauna = carrierLifeform as Fauna;   // flora carriers leave this null
            bomb._carrierCrystal = carrierCrystal;
            bomb._ridesCrystal = carrierCrystal;
            if (carrierLifeform != null) bomb._heart = HeartOf(carrierLifeform);

            // The planter's own read on the bomb: where it is, and how long it has. Local to the planter's machine and
            // their human eyes only - the target sees nothing.
            bomb._marker = MantaBombMarker.Attach(targetRoot, bomb, snapshot.Config, snapshot.LocalHumanPlanter);

            if (resetsFuse)
                bomb.PlayCue(snapshot.Config != null ? snapshot.Config.BombPlantedEvent : default);

            if (snapshot.Owner) snapshot.Owner.NotifyBombPlanted(bomb, resetsFuse);
            return bomb;
        }

        static Crystal HeartOf(ILifeFormEntity lifeform)
        {
            if (lifeform is Fauna fauna && fauna.HeartCrystal) return fauna.HeartCrystal;
            var root = lifeform.GetGameObject();
            return root ? root.GetComponentInChildren<Crystal>(true) : null;
        }

        /// <summary>The shared fuse moved (a new bomb went on): carry the deadline, for when the owner is gone.</summary>
        public void SyncFuse(float deadline) => _snap.FuseDeadline = deadline;

        /// <summary>
        /// Commits this bomb to an omni cascade <paramref name="delay"/> seconds from now at <paramref name="scale"/>.
        /// The marker holds critical for the wait.
        /// </summary>
        public void CommitToCascade(float delay, float scale)
        {
            if (_resolved || _cascading) return;
            _cascading = true;
            _cascadeAt = Time.time + Mathf.Max(0f, delay);
            _cascadeScale = scale;
        }

        void Update()
        {
            if (_resolved) return;

            // A creature that died with a bomb on it drops its heart STILL BOMBED: the bomb rides the crystal to
            // whoever picks it up. (A flora carrier gets the same through OnDestroy.)
            if (_carrierFauna && !_carrierFauna.HasLiveBodyPrisms)
            {
                MoveToHeartOrShed();
                return;
            }

            // A dropped crystal that is gone (taken by a path that never touched us, despawned, pooled away) takes
            // nothing with it but the bomb.
            if (_ridesCrystal && (!_carrierCrystal || !_carrierCrystal.gameObject.activeInHierarchy))
            {
                Shed();
                return;
            }

            if (_cascading)
            {
                if (Time.time >= _cascadeAt) Detonate(_cascadeScale, _snap.Config ? _snap.Config.OmniBloomGrowSeconds : 0f,
                                                      MantaDetonation.OmniCrystal);
                return;
            }

            // The shared fuse is the owner's to fire (so the whole board goes off together); a bomb only watches its
            // own copy of the deadline once the Manta that planted it is gone.
            if (!_snap.Owner && Time.time >= _snap.FuseDeadline && _snap.Config)
                Detonate(_snap.Config.FuseBlastScale, _snap.Config.BloomGrowSeconds, MantaDetonation.Fuse);
        }

        void OnTriggerEnter(Collider other)
        {
            // Only a bombed CRYSTAL listens: its collector picks the bomb up. Every vessel counts - the planter and
            // allies too, because the bloom spares them, which makes a bombed crystal a delivery for the team that
            // set it.
            if (_resolved || !_ridesCrystal || !_carrierCrystal) return;
            // A heart still waiting out its creature's wither is not a pickup yet.
            if (_carrierCrystal.IsEmbedded) return;
            if (!other.TryGetComponent<ImpactCollider>(out var ic)) return;

            IVesselStatus collector = ic.Impactor switch
            {
                VesselImpactor vi => vi.Vessel?.VesselStatus,
                SkimmerImpactor si => si.Skimmer ? si.Skimmer.VesselStatus : null,
                _ => null,
            };
            var root = collector?.Vessel?.Transform ? collector.Vessel.Transform.gameObject : null;
            if (!root) return;

            MoveTo(root, carrierLifeform: null, carrierCrystal: null);
        }

        void MoveToHeartOrShed()
        {
            var heart = _heart;
            if (!heart || !heart.gameObject.activeInHierarchy)
            {
                Shed();
                return;
            }
            // Not dropped yet (a death re-homes the heart to the cell as it starts, a frame or a wither later): ride
            // the husk until it lets go. A husk destroyed with the heart still inside it ends in OnDestroy.
            if (heart.transform.IsChildOf(transform)) return;
            MoveTo(heart.gameObject, carrierLifeform: null, carrierCrystal: heart);
        }

        /// <summary>
        /// Moves this bomb onto another carrier, keeping its planter, its snapshot and its place on the shared fuse.
        /// A carrier that is already bombed refuses it (one bomb per target), and the moving bomb is shed.
        /// </summary>
        public void MoveTo(GameObject root, ILifeFormEntity carrierLifeform, Crystal carrierCrystal)
        {
            if (_resolved) return;
            var snap = _snap;
            ResolveQuietly();
            Plant(root, snap, carrierLifeform, carrierCrystal, resetsFuse: false);
        }

        /// <summary>Removes the bomb without a bloom (a carrier that is gone, a move that was refused).</summary>
        public void Shed()
        {
            if (_resolved) return;
            ResolveQuietly();
        }

        void ResolveQuietly()
        {
            _resolved = true;
            RetireMarker();
            if (_snap.Owner) _snap.Owner.NotifyBombResolved(this, detonated: false);
            Destroy(this);
        }

        /// <summary>
        /// Blooms at <paramref name="scale"/>, growing over <paramref name="growSeconds"/> (slow on purpose: many big
        /// blooms at once read better, and cost less a frame, spread out). Runs only on the planter's simulation
        /// machine; the owner relays the bloom to peers so the chain reads everywhere.
        /// </summary>
        public void Detonate(float scale, float growSeconds, MantaDetonation reason)
        {
            if (_resolved) return;
            _resolved = true;

            var cfg = _snap.Config;
            if (cfg != null)
            {
                if (reason == MantaDetonation.OmniCrystal) PlayCue(cfg.CascadeBloomEvent);

                Vector3 position = transform.position;
                SpawnBloom(cfg, _snap.GameData, position, transform.rotation, scale, growSeconds,
                           _snap.PlanterDomain, _snap.PlanterVessel, _snap.DiContainer);

                if (_snap.Owner) _snap.Owner.RelayBloomToPeers(position, scale, growSeconds);

                if (_snap.Contagion)
                    SpreadContagion(position, scale * 0.5f * cfg.ContagionRadiusFraction);
            }

            RetireMarker();
            if (_snap.Owner) _snap.Owner.NotifyBombResolved(this, detonated: true);
            Destroy(this);
        }

        void OnDestroy()
        {
            // Carrier GameObject died under us (a plant withering, a despawn, a pool return).
            if (_resolved) return;
            if (_carrierLifeform != null && Application.isPlaying && gameObject.scene.isLoaded)
            {
                // A dying plant's heart re-homes to the cell before the husk goes; the bomb follows it.
                var heart = _heart;
                if (heart && heart.gameObject.activeInHierarchy && !heart.transform.IsChildOf(transform))
                {
                    _resolved = true;
                    RetireMarker();
                    if (_snap.Owner) _snap.Owner.NotifyBombResolved(this, detonated: false);
                    Plant(heart.gameObject, _snap, carrierLifeform: null, carrierCrystal: heart, resetsFuse: false);
                    return;
                }
            }
            _resolved = true;
            RetireMarker();
            if (_snap.Owner) _snap.Owner.NotifyBombResolved(this, detonated: false);
        }

        void RetireMarker()
        {
            if (_marker) _marker.Retire();
            _marker = null;
        }

        /// <summary>
        /// One-shot FMOD cue at the bomb, for the planter's machine only. Empty references are silent by design.
        /// </summary>
        void PlayCue(FMODUnity.EventReference reference)
        {
            if (!_snap.LocalHumanPlanter || reference.IsNull) return;
            var audio = AudioSystem.Instance;
            if (audio) audio.PlaySFXEvent(reference, transform.position);
        }

        /// <summary>
        /// The one bloom spawner - the planter's machine and every peer's relayed copy both go through here, so the two
        /// cannot drift. Attributed (never anonymous), and DOMAINED always: the planter and the planter's allies are
        /// never caught, which is what lets a friendly carry a bomb.
        /// </summary>
        public static void SpawnBloom(MantaStingConfigSO cfg, GameDataSO gameData,
                                      Vector3 position, Quaternion rotation, float maxScale, float growSeconds,
                                      Domains domain, IVessel vessel, Container container)
        {
            if (cfg == null || cfg.AoePrefabs == null) return;

            var init = new AOEExplosion.InitializeStruct
            {
                OwnDomain = domain,
                Vessel = vessel,
                MaxScale = maxScale,
                OverrideMaterial = cfg.BloomMaterial ? cfg.BloomMaterial
                    : vessel?.VesselStatus?.AOEExplosionMaterial,
                AnnonymousExplosion = false,
                SpawnPosition = position,
                SpawnRotation = rotation,
                AffectSelfOverride = false,
                DurationOverride = Mathf.Max(0f, growSeconds),
            };

            ExplosionHelper.CreateExplosion(cfg.AoePrefabs, init, container);
        }

        /// <summary>
        /// CHARGE 5 - Contagion: everything caught in the bloom is itself bombed, free. Vessels come off the roster,
        /// creatures off the canonical prism spatial index. Contagion bombs join the planter's shared fuse.
        /// </summary>
        void SpreadContagion(Vector3 center, float radius)
        {
            if (radius <= 0f) return;

            var planterRoot = _snap.PlanterVessel?.Transform ? _snap.PlanterVessel.Transform.gameObject : null;

            var players = _snap.GameData ? _snap.GameData.Players : null;
            if (players != null)
            {
                for (int i = 0; i < players.Count; i++)
                {
                    var p = players[i];
                    var vesselStatus = p?.Vessel?.VesselStatus;
                    if (vesselStatus?.Vessel?.Transform == null) continue;

                    var root = vesselStatus.Vessel.Transform.gameObject;
                    if (!root.activeInHierarchy || root == planterRoot || IsBombed(root)) continue;
                    if (p.Domain == _snap.PlanterDomain) continue;
                    if ((root.transform.position - center).sqrMagnitude > radius * radius) continue;

                    Plant(root, _snap);
                }
            }

            var index = PrismSpatialIndex.Instance;
            if (index == null) return;

            index.QuerySphere(center, radius, ContagionPrismScratch);
            for (int i = 0; i < ContagionPrismScratch.Count; i++)
            {
                if (ContagionPrismScratch[i] is not HealthPrism hp) continue;
                var fauna = hp.ResolveOwnerFauna();
                if (fauna == null || !fauna.HasLiveBodyPrisms) continue;
                if (IsBombed(fauna.gameObject)) continue;

                Plant(fauna.gameObject, _snap, carrierLifeform: fauna);
            }
            ContagionPrismScratch.Clear();
        }
    }
}
