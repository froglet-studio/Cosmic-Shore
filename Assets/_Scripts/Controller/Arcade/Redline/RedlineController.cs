using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Redline - the Manta-only circuit race. A closed loop of switch rings is cut through the
    /// cell and every pilot flies LAPS of it in order; the first domain whose lead runner threads
    /// the last gate of the last lap wins.
    ///
    /// <para><b>The mode teaches the Manta's Time ability, Soar, and nothing else</b> (Yash,
    /// 2026-11-04). Soar is the overlap of the two triggers: both buried is four times cruise, and
    /// easing both together turns tighter, at the price of about two seconds to wind the speed back
    /// up. Every corner asks <i>how much Soar is this corner worth?</i>, and no corner is tighter
    /// than Soar alone can make, so the race never needs the trigger difference (Yastri, Mass), the
    /// Sting or the Kabloom. Intensity 1 is flat out end to end; 2 asks one corner, 3 two, and 4
    /// three (one with the triggers off, one at a third, a knife-edge held flat only by being
    /// exact) through 44 u mouths. See <see cref="RedlineCourse"/> for the curve and the ladder.</para>
    ///
    /// <para><b>Time crystals</b> (<see cref="RedlineTimeCrystals"/>, from intensity 2): one Time
    /// level each, which on the Manta is Soar's top speed. On the line at I2-I3, inside the apexes
    /// at I4. They are laid on every peer from the broadcast course (the elemental set's Time
    /// prefab, Serpent Gulag's recipe) and grow back in place; the cell's omni crystal is off
    /// (the scene's CrystalManager spawns none), so no other element is ever fed.</para>
    ///
    /// <para><b>Why the Manta and not another hull.</b> Cruise 180, x4 on Soar to 720 (x1.3
    /// again at Time 10), and a turning circle that CONVERGES (RotationThrottleScaler 0.2 -
    /// 237 u at full boost, 286 u asymptotically). It is the fleet's fast hull, and a course cut
    /// around its bounded radius is a course it flies at the speed the mode is named for.</para>
    ///
    /// <para><b>What the mode does NOT add:</b> no new weapon, no new ability, no change to the
    /// vessel (CLAUDE.md, "A game mode never changes the vessel"), no cell of its own, no new
    /// scoring metric, and no generator of its own - the circuit solver is
    /// <see cref="HeadlongCircuit"/>, shared, with <see cref="RedlineCourse"/> supplying the
    /// Manta's cut.</para>
    /// </summary>
    public class RedlineController : GateRaceController
    {
        [Header("Redline circuit")]
        [Tooltip("Laps of the ring set that make one race. The authored gate target is the RACE " +
                 "length, so the circuit is laid with target/laps rings - one number authored " +
                 "once, in the end-condition overrides, and the two can never disagree.")]
        [SerializeField, Min(1)] int laps = RedlineCourseSource.DefaultLaps;

        [Header("Time crystals (RedlineTimeCrystals)")]
        [Tooltip("Seconds a taken Time crystal stays gone before it grows back in its place. Shorter than a lap, " +
                 "so every lap offers it again.")]
        [SerializeField, Min(0.5f)] float timeCrystalRegrowSeconds = 5f;

        [Tooltip("Seconds between checks for a taken Time crystal to grow back.")]
        [SerializeField, Min(0.1f)] float timeCrystalCheckSeconds = 0.5f;

        [Tooltip("Extra distance (u) an AI Manta flies to take a Time crystal on its way to its next ring (the crystal " +
                 "must be ahead of its nose). 0 = the AIs never steer for one, and take only those their line crosses. " +
                 "Shipped 0: with the fleet's AI nose lag a faster Manta misses more mouths, and at 150 the offline AI " +
                 "finished 1 of 6 I4 races against 6 of 6 (manta_redline_sim.py, 2026-11-04). Raise it once the AI " +
                 "nose rate is decided (AI_PLAN.md decision 7).")]
        [SerializeField, Min(0f)] float aiTimeCrystalSlack;

        protected override string ModeName => "Redline";

        /// <summary>
        /// The AI Mantas fly the Regatta steering (commit from the approach point, through the ring's centre, break off
        /// and re-attack after a missed mouth), as ten other gate races do. On the old steering they lined up on a
        /// point 320 u past the mouth on its AXIS and circled after a miss, and offline
        /// (<c>Tools/Build/manta_redline_sim.py</c>, which ports this AI line for line) they could not finish I3 or I4,
        /// where the mouths are 72 and 44 u (REDLINE.md section 5.1). The AI's throttle stays the skill throttle.
        /// </summary>
        protected override bool AICommitFromApproachPoint => true;

        protected override RaceCourseSource CreateCourseSource() =>
            new RedlineCourseSource { Laps = laps };

        /// <summary>One Time crystal's place, and the crystal standing there now (null while it is gone).</summary>
        sealed class CrystalSlot
        {
            public Vector3 Position;
            public Crystal Crystal;
            public ElementalCrystalImpactor Impactor;
            public float RegrowAt = -1f;
        }

        readonly List<CrystalSlot> _timeCrystals = new();
        readonly List<Vector3> _placeScratch = new();
        float _nextCrystalCheck;

        public override void OnNetworkDespawn()
        {
            ClearTimeCrystals();
            base.OnNetworkDespawn();
        }

        // ── The Time crystals (every peer, from the broadcast course) ─────────

        protected override void OnCourseRaised()
        {
            ClearTimeCrystals();
            RedlineTimeCrystals.Place(_course, Intensity, _placeScratch);
            if (_placeScratch.Count == 0) return;
            if (ElementalCrystalSetSO.Load() == null)
            {
                CSDebug.LogWarning("[Redline] no ElementalCrystalSet in Resources - the course has no Time crystals.");
                return;
            }
            for (int i = 0; i < _placeScratch.Count; i++)
            {
                var slot = new CrystalSlot { Position = _placeScratch[i] };
                _timeCrystals.Add(slot);
                Mint(slot);
            }
        }

        /// <summary>The elemental set's Time crystal at <paramref name="slot"/>, sized and collected like every
        /// elemental crystal (one petal), exactly as <c>SerpentGulagController.Mint</c> lays its field.</summary>
        void Mint(CrystalSlot slot)
        {
            var set = ElementalCrystalSetSO.Load();
            var prefab = set != null ? set.GetPrefab(Element.Time) : null;
            if (prefab == null) return;

            var crystal = Instantiate(prefab, slot.Position, Quaternion.identity);
            // Sized BEFORE Start(): Crystal stamps crystalValue from lossyScale.
            crystal.transform.localScale = Vector3.one * ElementalCrystalSetSO.CrystalWorldScale;
            crystal.enabled = true;
            crystal.gameObject.SetActive(true);

            var impactor = crystal.gameObject.AddComponent<ElementalCrystalImpactor>();
            impactor.Crystal = crystal;
            if (set.CollectionEffects is { Length: > 0 })
                impactor.SetCollectionEffects(set.CollectionEffects);
            crystal.gameObject.AddComponent<ImpactCollider>().SetImpactor(impactor);

            slot.Crystal = crystal;
            slot.Impactor = impactor;
            slot.RegrowAt = -1f;
        }

        static bool IsLive(CrystalSlot slot) => slot.Crystal && slot.Impactor && !slot.Impactor.HasBeenCollected;

        /// <summary>
        /// An AI Manta takes a Time crystal on its way (server, every frame): the live crystal ahead of its nose that costs
        /// least between it and its next ring, when that costs no more than <see cref="aiTimeCrystalSlack"/> over flying
        /// straight to the ring. The platform's crystal detour cannot see these (it reads MANAGED crystals only,
        /// <c>GateRaceController.FindDetourCrystal</c>), and without this the AI's line passes them 8-150 u off and takes
        /// almost none (manta_redline_sim.py). A choice of the mode's AI, not of the vessel. Off as shipped
        /// (<see cref="aiTimeCrystalSlack"/> 0) until the AI can fly the speed a crystal buys.
        /// </summary>
        protected override bool TryOverrideAim(IPlayer pilot, out Vector3 target)
        {
            target = default;
            if (aiTimeCrystalSlack <= 0f || _timeCrystals.Count == 0 || _course.Count == 0) return false;
            var tf = pilot?.Vessel?.Transform;
            if (tf == null) return false;
            int index = pilot.RoundStats?.SwitchesThreaded ?? 0;
            if (index < 0 || index >= RaceLength) return false;

            Vector3 self = tf.position, forward = tf.forward;
            Vector3 ring = _course[RingIndexFor(index)].Position;
            float direct = Vector3.Distance(self, ring), best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < _timeCrystals.Count; i++)
            {
                var slot = _timeCrystals[i];
                if (!IsLive(slot)) continue;
                Vector3 c = slot.Position;
                if (Vector3.Dot(c - self, forward) <= 0f) continue;   // behind the nose: passed
                float cost = Vector3.Distance(self, c) + Vector3.Distance(c, ring);
                if (cost > direct + aiTimeCrystalSlack || cost >= best) continue;
                best = cost;
                target = c;
                found = true;
            }
            return found;
        }

        // A subclass must not declare Update (GateRaceController.Update runs the race); LateUpdate is free.
        void LateUpdate()
        {
            if (_timeCrystals.Count == 0 || Time.time < _nextCrystalCheck) return;
            _nextCrystalCheck = Time.time + timeCrystalCheckSeconds;
            float now = Time.time;
            for (int i = 0; i < _timeCrystals.Count; i++)
            {
                var slot = _timeCrystals[i];
                if (IsLive(slot)) continue;
                if (slot.RegrowAt < 0f) { slot.RegrowAt = now + timeCrystalRegrowSeconds; continue; }
                if (now < slot.RegrowAt) continue;
                // A collected crystal destroys itself at the end of its capture; one still flying is left to finish.
                if (slot.Crystal) continue;
                Mint(slot);
            }
        }

        void ClearTimeCrystals()
        {
            for (int i = 0; i < _timeCrystals.Count; i++)
                if (_timeCrystals[i].Crystal) Destroy(_timeCrystals[i].Crystal.gameObject);
            _timeCrystals.Clear();
        }
    }
}
