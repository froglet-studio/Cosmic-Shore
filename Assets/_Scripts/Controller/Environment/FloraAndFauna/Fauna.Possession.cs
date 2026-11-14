using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    // POSSESSION (IFaunaPossession; the Jewel Wasp, Controller/Vessel/R_VesselActions/JEWEL_WASP.md): a pilot rides a
    // creature. It is the leash with three more things - any diet can be taken, every shield is food, and the rider's
    // triggers reach the creature's own mouth and its attack. One holder at a time, on the peer that SIMULATES the creature.
    public abstract partial class Fauna
    {
        /// <summary>The rider possessing this creature, or null.</summary>
        public IFaunaPossession Possessor => Leash as IFaunaPossession;

        /// <summary>True while a rider possesses this creature: its own steering and deciding stand down and the rider's
        /// sticks steer it every frame (<see cref="IFaunaLeash.TrySteer"/>).</summary>
        public bool IsPossessed => Leash is IFaunaPossession;

        /// <summary>
        /// The creature a rider possessing THIS one actually takes: itself, or the body it belongs to (a worm segment's
        /// colony, whose head the rider steers - Garrett, 2026-11-14: "just take over the head when any segment is
        /// entered"; a swarm member's whole murmuration).
        /// </summary>
        public virtual Fauna PossessionHost => this;

        /// <summary>Which creatures a rider can take. Any diet (Garrett, 2026-11-14: "any host should always be available");
        /// a species whose body a rider cannot steer (a colony that is a grid of agents) says no.</summary>
        protected virtual bool CanBePossessed => true;

        /// <summary>Whether a rider can take this creature now: it can be possessed, it is alive and this peer simulates it.</summary>
        public bool Possessable => CanBePossessed && IsSimAuthority && !_diedThisLife;

        /// <summary>
        /// Possess this creature (call it on <see cref="PossessionHost"/>). Any diet, and always from another holder, who is
        /// told it lost the creature (<see cref="IFaunaLeash.Released"/>). False when it cannot be possessed, is dying, or
        /// is a puppet on this peer.
        /// </summary>
        public bool TryPossess(IFaunaPossession rider)
        {
            if (rider == null || !Possessable) return false;
            if (ReferenceEquals(Leash, rider)) return true;
            var previous = Leash;
            Leash = rider;
            previous?.Released(this);
            ShowLeashMark();
            OnPossessionChanged(true);
            return true;
        }

        /// <summary>Let the rider off (a no-op unless <paramref name="rider"/> holds it). The creature takes up its own life
        /// again at the speed it was swimming.</summary>
        public void Unpossess(IFaunaPossession rider)
        {
            if (!ReferenceEquals(Leash, rider)) return;
            Unleash(rider);
            if (!_diedThisLife) OnPossessionChanged(false);
        }

        /// <summary>The rider got on (true) or off (false). A species that keeps its own motion state resets it here.</summary>
        protected virtual void OnPossessionChanged(bool possessed) { }

        // ── where the rider sits ─────────────────────────────────────────────

        /// <summary>
        /// Where a rider nests: beside the creature's heart crystal, the inactive crystal it carries (Garrett, 2026-11-14:
        /// "an onlooker sees a fauna, but an observant onlooker could notice the wasp nested in beside the crystal").
        /// Its own root when it carries none.
        /// </summary>
        public virtual Transform PossessionNest => crystal ? crystal.transform : transform;

        /// <summary>The point the rider nests at (<see cref="PossessionNest"/>'s, or a murmuration's centre).</summary>
        public virtual Vector3 PossessionNestPoint => (PossessionNest ? PossessionNest : transform).position;

        /// <summary>How big the creature's body is (u, a radius): a rider's camera stands back by this, and a rider's reach
        /// for a vessel adds it.</summary>
        public virtual float PossessionBodyRadius
        {
            get
            {
                var s = transform.lossyScale;
                return Mathf.Max(2f, Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z))));
            }
        }

        /// <summary>The way the creature faces now (a murmuration: its body's heading).</summary>
        public virtual Vector3 PossessionForward => transform.forward;

        /// <summary>How fast the creature swims flat out on its own (u/s): the base a rider's speed scales.</summary>
        public virtual float PossessionTopSpeed => LeashTopSpeed > 0f ? LeashTopSpeed : Mathf.Max(10f, CurrentSpeed);

        // ── the mouth ────────────────────────────────────────────────────────

        /// <summary>The mouth a possessed creature's suction pulls prisms into.</summary>
        public virtual Transform PossessionMouth => transform;

        /// <summary>How far the creature's own suction reaches (u) before a rider's Space scales it.</summary>
        public virtual float PossessionMouthReach => Mathf.Max(LeashMouthReach, PossessionBodyRadius * 2f, 6f);

        static readonly List<Prism> s_possessionScratch = new(96);

        /// <summary>The scratch <see cref="EatNearest"/> reads; a species that queries the index itself fills it.</summary>
        protected static List<Prism> PossessionScratch => s_possessionScratch;

        /// <summary>
        /// One mouthful a rider asked for (the Jewel Wasp's right trigger at prisms; Garrett, 2026-11-14: "against prisms it
        /// should consume the prisms with the suction effect"): up to <paramref name="maxBites"/> of the nearest rival
        /// prisms within this creature's mouth reach times <paramref name="reachScale"/> - ahead of the mouth, or all round
        /// it when <paramref name="allSides"/> - are pulled into its mouth by the ordinary consume (the suction implosion)
        /// and fill its stomach. Every shield is food (<see cref="IFaunaPossession"/>). Mass of the rider's own team and
        /// creatures' bodies are never food, and nor is the race corridor (<see cref="Cell.FaunaPathCorridor"/>). Returns
        /// the prisms eaten. On the simulating peer only.
        /// </summary>
        public virtual int PossessedSuction(float reachScale, int maxBites, bool allSides)
        {
            var rider = Possessor;
            if (rider == null || !IsSimAuthority || _diedThisLife || maxBites <= 0) return 0;
            var index = PrismSpatialIndex.EnsureInstance();
            if (index == null || !index.IsAvailable) return 0;
            var mouth = PossessionMouth ? PossessionMouth : transform;
            float reach = PossessionMouthReach * Mathf.Max(0.1f, reachScale);
            Vector3 at = mouth.position;
            Vector3 centre = allSides ? at : at + PossessionForward * (reach * 0.5f);
            float radius = allSides ? reach : reach * 0.75f;
            int found = index.QuerySphere(centre, radius, s_possessionScratch);
            return EatNearest(at, found, maxBites, rider, mouth);
        }

        /// <summary>The bites of <see cref="PossessedSuction"/> out of <see cref="s_possessionScratch"/>, nearest
        /// <paramref name="from"/> first.</summary>
        protected int EatNearest(Vector3 from, int found, int maxBites, IFaunaPossession rider, Transform mouth)
        {
            int bites = 0;
            var scratch = s_possessionScratch;
            if (found > scratch.Count) found = scratch.Count;
            for (int b = 0; b < maxBites; b++)
            {
                int best = -1;
                float bestSqr = float.PositiveInfinity;
                for (int i = 0; i < found; i++)
                {
                    var p = scratch[i];
                    if (!PossessedCanEat(p, rider)) continue;
                    float d = (p.transform.position - from).sqrMagnitude;
                    if (d < bestSqr) { bestSqr = d; best = i; }
                }
                if (best < 0) break;
                var prism = scratch[best];
                scratch[best] = null;
                float meal = Mathf.Max(0f, prism.Volume);   // read before the consume (the stomach is conserved)
                if (BiteMass(prism, mouth, rider.PossessorDomain, rider.PossessorName, true) == BiteResult.Ate)
                {
                    NotifyFed(meal);
                    bites++;
                }
            }
            scratch.Clear();
            return bites;
        }

        /// <summary>Whether a possessed creature eats <paramref name="prism"/> at its rider's word: live mass of another
        /// team, not a creature's body, not on a race corridor. Shields included (<see cref="IFaunaPossession"/>).</summary>
        protected bool PossessedCanEat(Prism prism, IFaunaPossession rider)
        {
            if (!prism || prism.destroyed) return false;
            if (prism.Domain == rider.PossessorDomain) return false;
            if (prism is HealthPrism hp && hp.ResolveOwnerFauna() != null) return false;
            var c = cell;
            return !(c && c.FaunaPathCorridor.IsActive && c.FaunaPathCorridor.Contains(prism.transform.position));
        }

        // ── the attack ───────────────────────────────────────────────────────

        /// <summary>
        /// Play this creature's ATTACK toward <paramref name="at"/> over <paramref name="seconds"/> (Garrett, 2026-11-14:
        /// "right trigger should animate the fauna with an attack animation against vessels (all the brittlestar's legs whip
        /// out). Fauna missing this should have this"). Presentation only: whoever asked decides what the attack hits. The
        /// default whips the body's limbs through <see cref="FaunaAttackDriver"/>, added to any creature that has none; a
        /// species with an attack of its own (the worm's lunge) plays that instead.
        /// </summary>
        public virtual void PlayPossessionAttack(Vector3 at, float seconds)
        {
            if (!TryGetComponent(out FaunaAttackDriver driver)) driver = gameObject.AddComponent<FaunaAttackDriver>();
            driver.Play(at, seconds);
        }

        /// <summary>The point the attack reaches from (u): the mouth, or the tip of the whipping limbs.</summary>
        public virtual Vector3 PossessionAttackOrigin => PossessionMouth ? PossessionMouth.position : transform.position;
    }
}
