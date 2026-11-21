using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Which crystal each AI pilot on a Skim Race team flies, decided ONCE per frame per team and read by
    /// every AI pilot on it - so two AI on one team cannot pick the same crystal from two slightly
    /// different snapshots of the same frame. The plan itself is <see cref="SkimRaceTeamAssignment"/>:
    /// every AI a different crystal, the least total distance, kept until another plan is clearly
    /// cheaper.
    ///
    /// <para><b>AI pilots only.</b> A human teammate is not planned for: the AI never leave a crystal
    /// "for" a human, because an idle or slow human would strand it and the team would lose that crystal.
    /// They simply stop doubling up on each other; a human teammate goes where they like. A team with ONE
    /// AI pilot gets no plan at all, so a lone AI flies exactly as it always has
    /// (<see cref="SkimRaceTargetTracker.Select"/>, the nearest crystal of its domain).</para>
    ///
    /// <para>It reads what every pilot can see - where the team's vessels and crystals are - and writes
    /// nothing in the game. Its memory is the last plan, held per team, so a near-tie does not flip
    /// the team's aim every frame.</para>
    /// </summary>
    public static class SkimRaceTeamPlan
    {
        sealed class Plan
        {
            public int Frame = -1;
            public readonly List<SkimRacePilot> Pilots = new(4);
            public readonly List<Vector3> PilotPositions = new(4);
            public readonly List<Crystal> Crystals = new(8);
            public readonly List<Vector3> CrystalPositions = new(8);
            public readonly List<int> Previous = new(4);
            public int[] Result = new int[4];
            public readonly Dictionary<SkimRacePilot, Crystal> Flying = new(4);
        }

        static readonly List<SkimRacePilot> s_racing = new(8);
        static readonly Dictionary<Domains, Plan> s_plans = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_racing.Clear();
            s_plans.Clear();
        }

        /// <summary>A pilot joins the plans when its race starts...</summary>
        public static void Join(SkimRacePilot pilot)
        {
            if (pilot != null && !s_racing.Contains(pilot)) s_racing.Add(pilot);
        }

        /// <summary>...and leaves when its race ends or the pilot goes away.</summary>
        public static void Leave(SkimRacePilot pilot)
        {
            s_racing.Remove(pilot);
            foreach (var plan in s_plans.Values) plan.Flying.Remove(pilot);
        }

        /// <summary>
        /// The crystal the team plan gives <paramref name="pilot"/> this frame. Null when its team has a
        /// single AI pilot, or no crystal is left for it - the caller then flies the nearest crystal.
        /// </summary>
        public static Crystal TargetFor(SkimRacePilot pilot, Domains domain)
        {
            if (!s_plans.TryGetValue(domain, out var plan)) s_plans[domain] = plan = new Plan();
            if (plan.Frame != Time.frameCount) Rebuild(plan, domain);
            return plan.Flying.TryGetValue(pilot, out var crystal) && crystal != null ? crystal : null;
        }

        static void Rebuild(Plan plan, Domains domain)
        {
            plan.Frame = Time.frameCount;

            plan.Pilots.Clear();
            plan.PilotPositions.Clear();
            for (int i = 0; i < s_racing.Count; i++)
            {
                var pilot = s_racing[i];
                if (pilot == null || !pilot.RaceActive || pilot.Domain != domain) continue;
                plan.Pilots.Add(pilot);
                plan.PilotPositions.Add(pilot.Position);
            }
            if (plan.Pilots.Count < 2)
            {
                plan.Flying.Clear();
                return;
            }

            // The team's race crystals. One taken this very frame still sits on the pilot that took it
            // until the manager moves it (Crystal.IsExploding): not a crystal to plan for yet.
            plan.Crystals.Clear();
            plan.CrystalPositions.Clear();
            var live = Crystal.Active;
            for (int i = 0; i < live.Count; i++)
            {
                var c = live[i];
                if (c == null || !c.isActiveAndEnabled || c.IsEmbedded || c.ownDomain != domain) continue;
                Vector3 at = c.transform.position;
                if (c.IsExploding && NearAnyPilot(at, plan.PilotPositions)) continue;
                plan.Crystals.Add(c);
                plan.CrystalPositions.Add(at);
            }

            plan.Previous.Clear();
            for (int i = 0; i < plan.Pilots.Count; i++)
                plan.Previous.Add(plan.Flying.TryGetValue(plan.Pilots[i], out var had) ? plan.Crystals.IndexOf(had) : -1);

            if (plan.Result.Length < plan.Pilots.Count) plan.Result = new int[Mathf.NextPowerOfTwo(plan.Pilots.Count)];
            SkimRaceTeamAssignment.Assign(plan.PilotPositions, plan.CrystalPositions, plan.Previous, plan.Result,
                SkimRaceTargetTracker.Hysteresis);

            plan.Flying.Clear();
            for (int i = 0; i < plan.Pilots.Count; i++)
                if (plan.Result[i] >= 0) plan.Flying[plan.Pilots[i]] = plan.Crystals[plan.Result[i]];
        }

        static bool NearAnyPilot(Vector3 at, List<Vector3> pilots)
        {
            float r2 = SkimRaceTargetTracker.OnTopOfPilot * SkimRaceTargetTracker.OnTopOfPilot;
            for (int i = 0; i < pilots.Count; i++)
                if ((at - pilots[i]).sqrMagnitude < r2) return true;
            return false;
        }
    }
}
