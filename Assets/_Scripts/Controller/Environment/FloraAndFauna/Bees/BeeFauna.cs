using System.Collections;
using CosmicShore.Core;
using CosmicShore.Data;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A BEE (Garrett, 2026-10-31; Docs/ECOSYSTEM.md §66): the first POLLINATOR (<see cref="FaunaDiet.Pollinator"/>), a
    /// fauna whose effect on the ecology is productive. It flies as a <see cref="Boid"/> (the school's flocking, its body
    /// prisms registered, moving mass) but it is no forager and has no collision effects, so the boid never eats: a bee
    /// eats no flora and no prism mass, and it never starves (a producer, like the plant slot it belongs to).
    ///
    /// <para>What it does, decided by <see cref="BeeMath.Decide"/> on its own clock:</para>
    /// <list type="bullet">
    /// <item><b>Pollinate</b>: fly to a flower near the hive and land on it: the plant grows faster for a while, through its
    /// OWN growth path (<see cref="Flora.Hasten"/>, the same door the Angler's lure uses), never a parallel one.</item>
    /// <item><b>Raid</b>: an opposing creature near the hive is flown at and stung: one of its body prisms is consumed toward
    /// the bee (the ordinary <see cref="Prism.Consume"/> path, attributed to the bee), and its volume is carried. That is
    /// how a hive is fed and how it is defended, in one act. Shielded body prisms are passed over, as every forager
    /// passes them.</item>
    /// <item><b>Return</b>: the load is handed to the hive's store, which builds comb from exactly that much
    /// (<see cref="BeeHiveFlora.Deposit"/>).</item>
    /// </list>
    /// <para>A bee whose hive is gone keeps pollinating round where it is and carries what it holds; nothing times it out.</para>
    /// </summary>
    public class BeeFauna : Boid
    {
        [Header("Bee")]
        [Tooltip("Mass a bee carries before it must go home (prism volume; a nominal prism is 16).")]
        [SerializeField, Min(0f)] float carryCapacity = 32f;
        [Tooltip("Flowers a bee visits on a trip before it heads home with whatever it carries.")]
        [SerializeField, Min(1)] int visitsPerTrip = 3;
        [Tooltip("A bee is 'at' a flower, the hive or its quarry within this distance.")]
        [SerializeField, Min(1f)] float arriveRadius = 14f;
        [Tooltip("How far from a quarry's body a sting reaches.")]
        [SerializeField, Min(1f)] float stingReach = 16f;
        [Tooltip("A pollinated plant grows this many times as often...")]
        [SerializeField, Min(1f)] float pollinationHaste = 1.6f;
        [Tooltip("...for this many seconds after a bee's visit (a refresh extends it).")]
        [SerializeField, Min(0f)] float pollinationSeconds = 10f;
        [Tooltip("Seconds between a bee's decisions (pollinate, raid, return) and its arrival checks.")]
        [SerializeField, Min(0.05f)] float decideSeconds = 0.4f;
        [Tooltip("Seconds between two stings of one bee.")]
        [SerializeField, Min(0f)] float stingCooldown = 1.2f;

        [Header("Audio (empty = silent; wired by the audio owner)")]
        [SerializeField] EventReference pollinateSfx;
        [SerializeField] EventReference stingSfx;

        BeeHiveFlora _hive;
        BeeIntent _intent;
        float _carried;
        int _visits, _trip, _salt;
        Flora _flower;
        Fauna _quarry;
        float _nextStingAt;
        string _stingerName;

        public BeeHiveFlora Hive => _hive;
        public BeeIntent Intent => _intent;
        public float Carried => _carried;

        /// <summary>Bind this bee to its hive (before Initialize), or let it go (null) when the hive is gone.</summary>
        public void BindHive(BeeHiveFlora hive)
        {
            if (_hive && _hive != hive) _hive.Unbind(this);
            _hive = hive;
        }

        public override void Initialize(Cell cell)
        {
            diet = FaunaDiet.Pollinator;
            base.Initialize(cell);
            _salt = GetInstanceID();
            _stingerName = name + " bee";
            if (IsSimAuthority) StartCoroutine(BeeLoop());
        }

        /// <summary>The goal is the trip's: the flower, the quarry or the hive (re-read on the base goal clock too).</summary>
        protected override Vector3 ResolveGoal()
        {
            var at = TargetPoint(out bool ok);
            return ok ? at : base.ResolveGoal();
        }

        Vector3 TargetPoint(out bool ok)
        {
            ok = true;
            switch (_intent)
            {
                case BeeIntent.Raid when _quarry && _quarry.IsAlivePrey: return _quarry.transform.position;
                case BeeIntent.Return when _hive: return _hive.HeartTransform.position;
                case BeeIntent.Pollinate when _flower && !_flower.IsDying: return _flower.HeartTransform.position;
            }
            ok = _hive;
            return _hive ? _hive.transform.position : transform.position;
        }

        IEnumerator BeeLoop()
        {
            var wait = new WaitForSeconds(decideSeconds);
            while (true)
            {
                yield return wait;
                if (isKilled || !IsSimAuthority) continue;
                Think();
            }
        }

        void Think()
        {
            Fauna intruder = null;
            bool raid = _hive && _hive.TryGetIntruder(out intruder);
            var intent = BeeMath.Decide(_carried, _hive ? carryCapacity : 0f, raid, _visits, visitsPerTrip);
            if (intent == BeeIntent.Return && !_hive) intent = BeeIntent.Pollinate;   // homeless: keep pollinating
            if (intent != _intent)
            {
                _intent = intent;
                _flower = null;
            }
            if (intent == BeeIntent.Raid) _quarry = intruder;

            var target = TargetPoint(out _);
            if (intent == BeeIntent.Pollinate && (!_flower || _flower.IsDying)) PickFlower();
            target = TargetPoint(out _);
            Goal = target;

            float reach = intent == BeeIntent.Raid ? stingReach : arriveRadius;
            if ((target - transform.position).sqrMagnitude > reach * reach) return;
            switch (intent)
            {
                case BeeIntent.Pollinate:
                    if (_flower && !_flower.IsDying)
                    {
                        _flower.Hasten(pollinationHaste, pollinationSeconds);
                        Play(pollinateSfx, transform.position);
                        _visits++;
                    }
                    _flower = null;
                    break;
                case BeeIntent.Raid:
                    Sting(_quarry);
                    break;
                case BeeIntent.Return:
                    if (_hive && _carried > 0f) _hive.Deposit(_carried);
                    _carried = 0f;
                    _visits = 0;
                    _trip++;
                    break;
            }
        }

        void PickFlower()
        {
            _flower = null;
            if (_hive) { if (_hive.TryPickFlower(_salt + _trip * 7919, _visits, out var f)) _flower = f; return; }
            // homeless: the nearest live plant within reach of where the bee is
            var live = FloraHeartRegistry.Live;
            float best = 400f * 400f;
            for (int i = 0; i < live.Count; i++)
            {
                var f = live[i];
                if (!f || f is BeeHiveFlora || f.IsDying) continue;
                float d = (f.transform.position - transform.position).sqrMagnitude;
                if (d < best) { best = d; _flower = f; }
            }
        }

        // A sting takes one body prism of the quarry: the nearest unshielded one, through the ordinary consume path.
        void Sting(Fauna quarry)
        {
            if (!quarry || !quarry.IsAlivePrey || Time.time < _nextStingAt) return;
            var index = PrismSpatialIndex.EnsureInstance();
            if (index == null || !index.IsAvailable) return;
            int n = index.QuerySphere(transform.position, stingReach, PrismScratch);
            HealthPrism bite = null;
            float best = float.PositiveInfinity;
            for (int i = 0; i < n; i++)
            {
                if (PrismScratch[i] is not HealthPrism hp || !hp || hp.destroyed) continue;
                if (hp.ResolveOwnerFauna() != quarry || IsShieldedMass(hp)) continue;
                float d = (hp.transform.position - transform.position).sqrMagnitude;
                if (d < best) { best = d; bite = hp; }
            }
            if (!bite) return;
            _nextStingAt = Time.time + stingCooldown;
            float volume = Mathf.Max(0f, bite.Volume);
            bite.Consume(transform, domain, _stingerName, false, true);
            _carried += volume;
            Play(stingSfx, transform.position);
        }

        protected override void OnDestroy()
        {
            if (_hive) _hive.Unbind(this);
            base.OnDestroy();
        }

        static void Play(EventReference evt, Vector3 at)
        {
            if (evt.IsNull) return;
            var audio = AudioSystem.Instance;
            if (audio) audio.PlaySFXEvent(evt, at);
        }
    }
}
