using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using Reflex.Core;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Everything a planted Manta bomb needs to bloom WITHOUT its planter: bombs outlive a
    /// vessel swap and a pilot's despawn, so the whole payload is snapshotted at PLANT time
    /// (the per-use-snapshot rule — Contagion, friendly fire and the Space radius are what the
    /// planter had earned when the bomb went on, not what they have when it goes off).
    /// </summary>
    public struct MantaBombSnapshot
    {
        public MantaStingConfigSO Config;
        public GameDataSO GameData;
        public string PlanterName;
        public Domains PlanterDomain;
        public IVessel PlanterVessel;          // may die before the fuse runs out — always guarded
        public MantaStingActionExecutor Owner; // ditto; HUD bookkeeping only
        public Container DiContainer;
        public bool Contagion;                 // CHARGE 5 snapshot
        public bool AffectSelf;                // true below SPACE 5: blooms catch allies
        public float SpaceScaleMultiplier;     // SPACE radius snapshot
        public float FuseSeconds;
        public bool LocalHumanPlanter;         // may this machine draw the planter's markers?
    }

    /// <summary>
    /// One silent Manta bomb riding a target — the deliverable of STING, the payload of
    /// KABLOOM. Added to the carrier's root GameObject on the PLANTER's simulation machine
    /// only (bombs are local objects, like projectiles); its presence IS the one-bomb-per-
    /// target immunity, and destroying it (detonation, knock-off, carrier death) is what ends
    /// that immunity. The target gets NO indication — the component renders nothing.
    ///
    /// A vessel carrier can scrape the bomb off against geometry: the carrier root's Rigidbody
    /// receives prism trigger contacts (the same callback surface VesselImpactor rides), and a
    /// prism hit after the plant grace sheds the bomb without a bloom. The carrier's own FRESH
    /// ribbon is exempt (owner + age, the SelfTrailContactConfig shape) — a vessel is always
    /// touching the trail it is laying, and flying straight must not be counterplay.
    /// </summary>
    public class MantaBomb : MonoBehaviour
    {
        static readonly List<Prism> ContagionPrismScratch = new();

        MantaBombSnapshot _snap;
        Fauna _carrierFauna;              // set only for FAUNA carriers (body-prism liveness)
        ILifeFormEntity _carrierLifeform; // fauna OR flora; null for vessel carriers
        string _carrierPlayerName;        // null for lifeform carriers
        float _plantedAt;
        float _fuseDeadline;
        bool _resolved;
        bool _cascading;                  // committed to a Kabloom cascade, waiting its turn
        float _cascadeAt;
        MantaBombMarker _marker;

        public string PlanterName => _snap.PlanterName;
        public float FuseRemaining => Mathf.Max(0f, _fuseDeadline - Time.time);
        public float FuseSeconds => _snap.FuseSeconds;

        /// <summary>Waiting its turn in a crystal-cashed cascade — the marker reads it as
        /// fully critical, because it is about to bloom.</summary>
        public bool IsCascading => _cascading;

        /// <summary>Is this root already carrying a live bomb? (Tagging is denial.)</summary>
        public static bool IsBombed(GameObject root) => root && root.TryGetComponent<MantaBomb>(out _);

        /// <summary>
        /// Plants a bomb on <paramref name="targetRoot"/>. Returns null when the target is
        /// already bombed — the caller decides whether that refund charges.
        /// </summary>
        public static MantaBomb Plant(GameObject targetRoot, in MantaBombSnapshot snapshot,
                                      ILifeFormEntity carrierLifeform = null,
                                      string carrierPlayerName = null)
        {
            if (!targetRoot || IsBombed(targetRoot)) return null;

            var bomb = targetRoot.AddComponent<MantaBomb>();
            bomb._snap = snapshot;
            bomb._carrierLifeform = carrierLifeform;
            bomb._carrierFauna = carrierLifeform as Fauna;   // flora carriers leave this null
            bomb._carrierPlayerName = carrierPlayerName;
            bomb._plantedAt = Time.time;
            bomb._fuseDeadline = Time.time + Mathf.Max(1f, snapshot.FuseSeconds);

            // The planter's own read on the bomb: where it is, and how long it has. Local to
            // the planter's machine and their human eyes only — the target sees nothing.
            bomb._marker = MantaBombMarker.Attach(targetRoot, bomb, snapshot.Config,
                                                  snapshot.LocalHumanPlanter);

            bomb.PlayCue(snapshot.Config != null ? snapshot.Config.BombPlantedEvent : default);

            if (snapshot.Owner) snapshot.Owner.NotifyBombPlanted(bomb);
            return bomb;
        }

        /// <summary>
        /// Commits this bomb to a Kabloom cascade <paramref name="delay"/> seconds from now.
        /// The wait is what turns a cashed board into a chain rolling outward from the pilot
        /// instead of one flat bang, and the marker holds critical for the whole of it — the
        /// "watch every fuse turn into an explosion" beat. Its own fuse can no longer fire.
        /// </summary>
        public void CommitToCascade(float delay)
        {
            if (_resolved || _cascading) return;
            _cascading = true;
            _cascadeAt = Time.time + Mathf.Max(0f, delay);
        }

        void Update()
        {
            if (_resolved) return;

            // A creature that died with a bomb on it takes the bomb with it — there is nothing
            // left to destroy, and a bloom on a withering husk would double-kill conserved mass.
            // (Flora carriers need no equivalent test: a plant that dies destroys this
            // component with its GameObject, which OnDestroy already handles.)
            if (_carrierFauna && !_carrierFauna.HasLiveBodyPrisms)
            {
                Shed();
                return;
            }

            if (_cascading)
            {
                if (Time.time >= _cascadeAt) Detonate(byCrystal: true);
                return;                                  // a cascading fuse cannot also expire
            }

            if (Time.time >= _fuseDeadline)
            {
                // The fuse the pilot did NOT beat. Its own cue, because it is the opposite
                // outcome to a cashed bloom and must not sound like one.
                PlayCue(_snap.Config != null ? _snap.Config.FuseExpiredEvent : default);
                Detonate(byCrystal: false);
            }
        }

        void OnTriggerEnter(Collider other)
        {
            // Knock-off counterplay — vessel carriers only (a creature cannot deliberately
            // scrape). The compound-collider split is load-bearing: the skimmer child carries
            // its own kinematic Rigidbody, so only HULL contacts arrive here.
            if (_resolved || _cascading || _carrierLifeform != null || _snap.Config == null) return;
            if (Time.time - _plantedAt < _snap.Config.KnockOffGraceSeconds) return;

            if (!other.TryGetComponent<ImpactCollider>(out var ic)) return;
            if (ic.Impactor is not PrismImpactor prismImpactor) return;

            var prism = prismImpactor.Prism;
            if (prism == null || prism.destroyed) return;

            // The carrier's own fresh ribbon never counts as "geometry".
            if (!string.IsNullOrEmpty(_carrierPlayerName)
                && prism.ownerID == _carrierPlayerName
                && Time.time - prism.prismProperties.TimeCreated < _snap.Config.OwnFreshTrailGraceSeconds)
                return;

            Shed();
        }

        /// <summary>Removes the bomb without a bloom (knock-off / carrier death).</summary>
        public void Shed()
        {
            if (_resolved) return;
            _resolved = true;
            RetireMarker();
            if (_snap.Owner) _snap.Owner.NotifyBombResolved(this, detonated: false, byCrystal: false);
            Destroy(this);
        }

        /// <summary>
        /// Blooms. Fuse timeouts pay the small blast; a crystal (Kabloom) pays the medium one —
        /// "beat the fuse" is a blast-size fact, not a scoring special case. Runs only on the
        /// planter's simulation machine; the owner executor relays the bloom to peers so the
        /// arena-wide chain reads everywhere.
        /// </summary>
        public void Detonate(bool byCrystal)
        {
            if (_resolved) return;
            _resolved = true;

            var cfg = _snap.Config;
            if (cfg != null)
            {
                if (byCrystal) PlayCue(cfg.CascadeBloomEvent);

                float scale = (byCrystal ? cfg.KabloomBlastScale : cfg.FuseBlastScale)
                              * Mathf.Max(0.05f, _snap.SpaceScaleMultiplier);
                Vector3 position = transform.position;

                SpawnBloom(cfg, _snap.GameData, position, transform.rotation, scale,
                           _snap.PlanterDomain, _snap.PlanterVessel, _snap.AffectSelf,
                           _snap.DiContainer);

                if (_snap.Owner) _snap.Owner.RelayBloomToPeers(position, scale, _snap.AffectSelf);

                if (_snap.Contagion)
                    SpreadContagion(position, scale * 0.5f * cfg.ContagionRadiusFraction);
            }

            RetireMarker();
            if (_snap.Owner) _snap.Owner.NotifyBombResolved(this, detonated: true, byCrystal: byCrystal);
            Destroy(this);
        }

        void OnDestroy()
        {
            // Carrier GameObject died under us (despawn, pool return): clear the HUD pip.
            if (_resolved) return;
            _resolved = true;
            RetireMarker();
            if (_snap.Owner) _snap.Owner.NotifyBombResolved(this, detonated: false, byCrystal: false);
        }

        void RetireMarker()
        {
            if (_marker) _marker.Retire();
            _marker = null;
        }

        /// <summary>
        /// One-shot FMOD cue at the bomb, for the planter's machine only. Empty references are
        /// silent by design — every one of these is an authoring slot, never a borrowed event
        /// (the audio law). Resolved through the live singleton rather than an injected field:
        /// a bomb is added to somebody else's GameObject at runtime and is never injected.
        /// </summary>
        void PlayCue(FMODUnity.EventReference reference)
        {
            if (!_snap.LocalHumanPlanter || reference.IsNull) return;
            var audio = AudioSystem.Instance;
            if (audio) audio.PlaySFXEvent(reference, transform.position);
        }

        /// <summary>
        /// The one bloom spawner — the planter's machine and every peer's relayed copy both go
        /// through here, so the two cannot drift. Attributed (never anonymous): the planter's
        /// vessel is what makes the destruction score and the kill credit land.
        /// </summary>
        public static void SpawnBloom(MantaStingConfigSO cfg, GameDataSO gameData,
                                      Vector3 position, Quaternion rotation, float maxScale,
                                      Domains domain, IVessel vessel, bool affectSelf,
                                      Container container)
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
                AffectSelfOverride = affectSelf,
            };

            ExplosionHelper.CreateExplosion(cfg.AoePrefabs, init, container);
        }

        /// <summary>
        /// CHARGE 5 — Contagion: everything caught in the bloom is itself bombed, free.
        /// Vessels come off the roster (the same radius the blast reaches), creatures off the
        /// canonical prism spatial index (never Physics — fresh prisms are collider-blind).
        /// Contagion bombs inherit this bomb's snapshot, fuse restarted.
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
                    if (!_snap.AffectSelf && p.Domain == _snap.PlanterDomain) continue;
                    if ((root.transform.position - center).sqrMagnitude > radius * radius) continue;

                    Plant(root, _snap, carrierLifeform: null, carrierPlayerName: p.Name);
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
