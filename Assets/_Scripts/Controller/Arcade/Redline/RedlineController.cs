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
    /// than Soar alone can make, so the race never needs the trigger difference (Yawstery, Mass), the
    /// Sting or the Kabloom. Intensity 1 is flat out end to end; 2 asks one corner, 3 two, and 4
    /// three (one with the triggers off, one at a third, a knife-edge held flat only by being
    /// exact) through 44 u mouths. See <see cref="RedlineCourse"/> for the curve and the ladder.</para>
    ///
    /// <para><b>Time crystals</b> (<see cref="RedlineTimeCrystals"/>, from intensity 2): one Time
    /// level each, which on the Manta is Soar's top speed. On the line at I2-I3, inside the apexes
    /// at I4. They are laid on every peer from the broadcast course (the elemental set's Time
    /// prefab, Serpent Gulag's recipe, now Hood's) and grow back in place; the cell's omni crystal is off
    /// (the scene's CrystalManager spawns none), so no other element is ever fed.</para>
    ///
    /// <para><b>Soar from the gun (2026-11-11).</b> Since 2026-11-07 Soar is the Manta's Time LEVEL-5 upgrade, and until
    /// this card seeded a Time level every Manta - human and AI - raced at 180 u/s with Soar dark. The card now starts
    /// every Manta at Time 6 (<see cref="RedlineCourse.StartingTimeLevelByIntensity"/>): 1104 u/s flat out, 1404 at
    /// Time 10. The race is 8 laps, the AIs fly a race line through the mouths' centres (<see cref="TryRaceLine"/>) at a
    /// throttle floor by difficulty, and the local pilot's threads are graded and called in the world
    /// (<c>RedlineController.Heat</c>). REDLINE.md section 0.</para>
    ///
    /// <para><b>Why the Manta and not another hull.</b> The fleet's fastest cruise (180 u/s at Time 0, +5% a Time
    /// level, Soar x4 on top and +3% a level) and a turning circle that CONVERGES (RotationThrottleScaler 0.2 - 252 u
    /// at full Soar at Time 6, 286 u asymptotically). A course cut around its bounded radius is a course it flies at
    /// the speed the mode is named for.</para>
    ///
    /// <para><b>What the mode does NOT add:</b> no new weapon, no new ability, no change to the
    /// vessel (CLAUDE.md, "A game mode never changes the vessel"), no cell of its own, no new
    /// scoring metric, and no generator of its own - the circuit solver is
    /// <see cref="HeadlongCircuit"/>, shared, with <see cref="RedlineCourse"/> supplying the
    /// Manta's cut.</para>
    /// </summary>
    public partial class RedlineController : GateRaceController
    {
        [Header("AI nose lead")]
        [Tooltip("Each AI's nose lead by hull (AIPilot.NoseLeadGain), set as it is armed. A hull with no row flies with " +
                 "the lead off. Manta 0: off, the lead does not help the Manta (its fix is a faster nose).")]
        [SerializeField] AINoseLeadRow[] aiNoseLead = { new AINoseLeadRow(VesselClassType.Manta, 0.0f) };

        protected override AINoseLeadRow[] AINoseLeadTable => aiNoseLead;

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

        [Header("AI nose lag by difficulty")]
        [Tooltip("The AI nose's follow rate per second, one row per intensity (I1..I4), by difficulty Easy/Medium/Hard/" +
                 "Legendary: 1.5 = the fleet lag every AI flies with elsewhere, higher = faster, 0 = no lag (as a human). " +
                 "Switchback's knob (SwitchbackController.aiNoseFollowByIntensity). The lag is the AI Manta's biggest " +
                 "handicap here: at the fleet lag its I4 takes ~540 s offline, at 9/s ~97 s (Docs/Reports/" +
                 "REDLINE_MANTA_NOSE.md, the Legendary model: 2/s ~156-272 s, 3/s ~134-160, 6/s ~116-125, 9/s ~97-111 " +
                 "over I1-I4). A negative entry leaves that level on the fleet lag.")]
        [SerializeField] SwitchbackNoseFollow[] aiNoseFollowByIntensity =
        {
            new(2f, 3f, 6f, 9f),   // I1
            new(2f, 3f, 6f, 9f),   // I2
            // I3-I4: Easy (and at I4 Medium) a step quicker (2026-11-11). With Soar lit (1104 u/s) a 2/s nose could not
            // hold the 72 / 44 u mouths: offline it finished 5 of 6 I3 races (6 of 6 at 2.5/s) and 2-4 of 6 I4 races in
            // 500 s (6 of 6 at 4/s) (manta_redline_sim.py).
            new(2.5f, 3f, 6f, 9f), // I3
            new(4f, 5f, 6f, 9f),   // I4
        };

        [Header("AI race line (2026-11-11)")]
        [Tooltip("The AI Mantas aim straight at the next mouth's centre (TryRaceLine), the line a person flies, instead " +
                 "of the gate way's approach point; after a missed mouth the gate way re-attacks it. Offline, with Soar " +
                 "lit, the gate way's Legendary took 90-104 s for five laps and failed I3-I4 at Easy; the race line takes " +
                 "38-59 s, near a person's 24-53 (manta_redline_sim.py). Off = the gate way for every ring.")]
        [SerializeField] bool aiRaceLine = true;

        [Tooltip("The least throttle an AI flies, by its seat's difficulty: Easy, Medium, Hard, Legendary. The AI's own " +
                 "skill throttle is 0.375 at I1 (0.2-0.9 by intensity), which flew a Soaring Manta at a third of its speed. " +
                 "A floor only ever raises it.")]
        [SerializeField] float[] aiThrottleFloorByDifficulty = { 0.7f, 0.85f, 1f, 1f };

        protected override string ModeName => "Redline";

        /// <summary>
        /// Each AI Manta's nose follow rate for this race's intensity and its seat's difficulty
        /// (<see cref="aiNoseFollowByIntensity"/>), set on every arming as Switchback does, so a hull never carries another
        /// mode's rate into this race. A mode setting on the AI's skill: the vessel's own code and assets are untouched.
        /// </summary>
        protected override void OnRacerArmed(IPlayer pilot)
        {
            var transformer = pilot?.Vessel?.VesselStatus?.VesselTransformer;
            if (transformer == null) return;
            transformer.AINoseFollowPerSecond = SwitchbackController.NoseFollowFor(aiNoseFollowByIntensity, Intensity,
                                                                                    (int)gameData.AIDifficultyFor(pilot));
        }

        /// <summary>
        /// The AI Mantas fly the Regatta steering (commit from the approach point, through the ring's centre, break off
        /// and re-attack after a missed mouth), as ten other gate races do. On the old steering they lined up on a
        /// point 320 u past the mouth on its AXIS and circled after a miss, and offline
        /// (<c>Tools/Build/manta_redline_sim.py</c>, which ports this AI line for line) they could not finish I3 or I4,
        /// where the mouths are 72 and 44 u (REDLINE.md section 5.1). The AI's throttle stays the skill throttle.
        /// </summary>
        protected override bool AICommitFromApproachPoint => true;

        /// <summary>The AI's throttle floor for its seat's difficulty (<see cref="aiThrottleFloorByDifficulty"/>): a mode
        /// setting on the AI's input, the vessel untouched.</summary>
        protected override float AIThrottleFloorFor(IPlayer pilot) =>
            ThrottleFloorFor(aiThrottleFloorByDifficulty, gameData ? (int)gameData.AIDifficultyFor(pilot) : 0);

        /// <summary>The floor for difficulty <paramref name="level"/> (1 Easy .. 4 Legendary), or 0 (the skill throttle)
        /// for any other level or a missing row.</summary>
        public static float ThrottleFloorFor(float[] byDifficulty, int level) =>
            byDifficulty != null && level >= 1 && level <= byDifficulty.Length
                ? Mathf.Clamp01(byDifficulty[level - 1])
                : 0f;

        /// <summary>
        /// The AI Manta's race line: straight at the next mouth's centre, with the ring after it as the next leg. The line
        /// a person flies; with Soar lit it threads where the gate way's approach point (a lead on the mouth's axis, then
        /// a through point) loses seconds lining up at 1100 u/s. The platform asks this only while the pilot is not
        /// flying a difficulty mistake and has not just missed this mouth, so a miss is still re-attacked the gate way
        /// (<see cref="GateRaceController.TryRaceLine"/>). An aim, so input only: the stick and the Soar drive are the
        /// vessel's own.
        /// </summary>
        protected override bool TryRaceLine(IPlayer pilot, Vector3 self, out Vector3 aim, out Vector3 after)
        {
            aim = after = default;
            if (!aiRaceLine || _course.Count == 0) return false;
            int index = pilot?.RoundStats?.SwitchesThreaded ?? -1;
            if (!RaceLinePoints(index, RaceLength, out int ring, out int next)) return false;
            aim = _course[RingIndexFor(ring)].Position;
            after = _course[RingIndexFor(next)].Position;
            return true;
        }

        /// <summary>The race-line pair for a pilot at <paramref name="threaded"/> of <paramref name="raceLength"/>: the
        /// threading it flies at now and the one after (the last gate is its own "after"). False once it has finished.</summary>
        public static bool RaceLinePoints(int threaded, int raceLength, out int ring, out int next)
        {
            ring = next = -1;
            if (threaded < 0 || threaded >= raceLength) return false;
            ring = threaded;
            next = Mathf.Min(threaded + 1, raceLength - 1);
            return true;
        }

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
            ClearHeat();
            base.OnNetworkDespawn();
        }

        // ── The Time crystals (every peer, from the broadcast course) ─────────

        protected override void OnCourseRaised()
        {
            ResetHeat();
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
        /// elemental crystal (one petal), exactly as <c>SerpentHoodController.Mint</c> lays its field.</summary>
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
            TickHeat();
            TickTimeCrystals();
        }

        void TickTimeCrystals()
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
