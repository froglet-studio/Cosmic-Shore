using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Stoat's ANNIHILATION RIPPLE — its Charge ability, passive (tuning: <see cref="StoatRippleConfigSO"/>, design:
    /// <c>R_VesselActions/STOAT_DIPOLE.md</c> §1c). Whenever this hull's pair annihilates — the field dipole let go
    /// (<see cref="StoatDipoleExecutor"/>), the orbit's pair closing after a slingshot (<see cref="StoatSlingExecutor"/>)
    /// — the meeting point throws out a space-time ripple, and as its FRONT sweeps outward it strips every rival vessel
    /// and kills every creature it passes. Nothing else: it touches no prism.
    ///
    /// <para><b>Where it runs.</b> On every peer, from that peer's own copy of the pair: the dipole's copies all collapse
    /// (<c>StepCollapses</c>), the orbit's pair is laid on every peer from the replicated press and closes there. The
    /// strip runs on each peer's own copy of the rival's petals — elemental levels are simulated per peer, the stance the
    /// wormhole toll and the dipole's own crystal strip take (<see cref="BlackHoleCrystalStrip"/>) — and reads Charge at
    /// the REPLICATED level and the Siphon on the replicated unlock bit, so every peer sweeps the same sphere with the same
    /// rule. Creatures die on the fauna's simulating machine only (<see cref="FaunaNetworkSync.IsSimAuthority"/>), whose
    /// death replicates.</para>
    ///
    /// <para><b>Each victim once per ripple.</b> A ripple remembers whom it has struck, so a rival riding the front is
    /// stripped once, not every frame it stays inside.</para>
    /// </summary>
    public sealed class StoatRippleExecutor : ShipActionExecutorBase
    {
        [Header("Config")]
        [Tooltip("The ripple's numbers. A passive ability's config is wired on its executor (vessel rule 20).")]
        [SerializeField] StoatRippleConfigSO config;

        static readonly Element[] StripElements = { Element.Charge, Element.Mass, Element.Space, Element.Time };
        /// <summary>The vessel list is re-read once per ripple (a birth is rare; never per frame).</summary>
        static readonly List<VesselStatus> s_vessels = new();

        sealed class Wave
        {
            public Vector3 Centre;
            public float Radius, Age;
            public readonly HashSet<Object> Struck = new();
            public readonly List<VesselStatus> Candidates = new();
        }

        readonly List<Wave> _waves = new();
        readonly Stack<Wave> _spare = new();
        IVesselStatus _status;

        public StoatRippleConfigSO Config => config;
        /// <summary>True while one of this hull's ripples is still sweeping.</summary>
        public bool IsRippling => _waves.Count > 0;

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            ClearWaves();
            if (config == null)
                CSDebug.LogError($"[Stoat] {name}: StoatRippleExecutor has no StoatRippleConfig — annihilations throw no ripple. " +
                                 "Wire Assets/_SO_Assets/VesselActions/Stoat/StoatRippleConfig.asset on the prefab.");
        }

        /// <summary>
        /// This hull's pair just annihilated at <paramref name="at"/>, its poles <paramref name="separation"/> apart when
        /// they were let go. Every peer calls this from its own copy of the pair.
        /// </summary>
        public void Detonate(Vector3 at, float separation)
        {
            if (config == null || _status == null) return;
            var wave = _spare.Count > 0 ? _spare.Pop() : new Wave();
            wave.Centre = at;
            wave.Radius = config.Radius(separation, _status);
            wave.Age = 0f;
            wave.Struck.Clear();
            wave.Candidates.Clear();
            // The rivals that could be reached at all, once: anything further than the full radius plus a frame's travel
            // is out of reach for this ripple's whole life.
            s_vessels.Clear();
            s_vessels.AddRange(VesselStatus.Live);
            float reach = wave.Radius + 200f;
            for (int i = 0; i < s_vessels.Count; i++)
            {
                var v = s_vessels[i];
                if (v && (v.transform.position - at).sqrMagnitude <= reach * reach) wave.Candidates.Add(v);
            }
            s_vessels.Clear();
            _waves.Add(wave);

            StoatRipple.Spawn(at, wave.Radius, config.ExpandSeconds, config.FadeSeconds, config.BandWidth, config.Bend,
                config.ChromaticSplit, DomainLight(), config.RimGlow);
            PlayOneShot(config.RippleEvent, at);
            CSDebug.LogVerbose(CSLogChannel.BlackHole, $"[Stoat] ripple at {at} radius {wave.Radius:F0} u (separation {separation:F0} u)");
        }

        void Update()
        {
            if (config == null || _status == null || _waves.Count == 0) return;
            float dt = Time.deltaTime;
            for (int i = _waves.Count - 1; i >= 0; i--)
            {
                var w = _waves[i];
                w.Age += dt;
                float front = StoatRippleMath.Front(w.Age, config.ExpandSeconds, w.Radius);
                Sweep(w, front);
                if (w.Age >= config.ExpandSeconds)
                {
                    _waves.RemoveAt(i);
                    w.Candidates.Clear();
                    w.Struck.Clear();
                    _spare.Push(w);
                }
            }
        }

        void Sweep(Wave w, float front)
        {
            float front2 = front * front;
            for (int i = 0; i < w.Candidates.Count; i++)
            {
                var v = w.Candidates[i];
                if (!v || w.Struck.Contains(v)) continue;
                if ((v.transform.position - w.Centre).sqrMagnitude > front2) continue;
                w.Struck.Add(v);
                Strip(v, w.Centre);
            }
            if (!config.KillsFauna || !FaunaNetworkSync.IsSimAuthority || _status.Player == null) return;
            string killer = _status.PlayerName;
            var cells = Cell.ActiveCellsSnapshot;
            for (int c = 0; c < cells.Count; c++)
            {
                var cell = cells[c];
                if (!cell) continue;
                var fauna = cell.LiveFauna;
                for (int i = fauna.Count - 1; i >= 0; i--)
                {
                    var f = fauna[i];
                    if (!f || f.IsDying || !f.isActiveAndEnabled || w.Struck.Contains(f)) continue;
                    if ((f.transform.position - w.Centre).sqrMagnitude > front2) continue;
                    w.Struck.Add(f);
                    f.Predated(killer);
                }
            }
        }

        /// <summary>
        /// The front reached <paramref name="victimComponent"/>: if it is a rival, take <see cref="StoatRippleConfigSO.PetalsPerElement"/>
        /// whole petals of every element it holds — shed around it as crystals, or with the Charge-5 Siphon handed to
        /// this hull. Conserved either way (<see cref="ResourceSystem.AccrueElementalLoss"/> settles whole petals and
        /// honours a ward against <see cref="ElementalDebuffSources.Explosion"/>).
        /// </summary>
        void Strip(VesselStatus victimComponent, Vector3 centre)
        {
            IVesselStatus victim = victimComponent;
            bool isOwner = _status is Component self && self && self.transform == victimComponent.transform;
            bool known = victim.Player != null && _status.Player != null;
            if (!StoatRippleMath.Strips(isOwner, known, known ? victim.Domain : Domains.Blue, known ? _status.Domain : Domains.Blue))
                return;
            var resources = victim.ResourceSystem;
            if (!resources || config.PetalsPerElement <= 0) return;
            bool siphon = config.Siphons(_status);
            var mine = _status.ResourceSystem;
            var at = victimComponent.transform.position;
            int total = 0;
            for (int i = 0; i < StripElements.Length; i++)
            {
                var element = StripElements[i];
                int petals = resources.AccrueElementalLoss(element, config.PetalsPerElement * ResourceSystem.PetalNormalized,
                    ElementalDebuffSources.Explosion);
                if (petals <= 0) continue;
                total += petals;
                if (siphon && mine) mine.GrantPetals(element, petals);
                else ElementalCrystalEjector.ShedOntoSphere(at, 4f, at + (at - centre), 80f, element, petals, config.ShedSpeed,
                    victim.PlayerName);
            }
            if (total > 0) PlayOneShot(config.StripEvent, at);
        }

        /// <summary>The owner domain's LIGHT colour (the dipole's source core wears it), white without a palette.</summary>
        Color DomainLight()
        {
            if (_status?.Player == null) return Color.white;
            var colors = PrismLit.ColorSet;
            return colors && colors.TryGetPrismKindColors(_status.Domain, PrismKind.Plain, out var light, out _) ? light : Color.white;
        }

        void PlayOneShot(EventReference reference, Vector3 at)
        {
            if (reference.IsNull) return;
            var audio = AudioSystem.Instance;
            if (audio) audio.PlaySFXEvent(reference, at);
        }

        void ClearWaves()
        {
            for (int i = 0; i < _waves.Count; i++)
            {
                _waves[i].Candidates.Clear();
                _waves[i].Struck.Clear();
                _spare.Push(_waves[i]);
            }
            _waves.Clear();
        }

        // A vessel swap or despawn ends its sweeps (the drawn waves finish on their own).
        void OnDisable() => ClearWaves();
    }
}
