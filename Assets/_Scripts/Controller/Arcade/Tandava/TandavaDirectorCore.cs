// Tandava's STAGE DIRECTOR (Assets/_Scripts/Controller/Arcade/TANDAVA.md §4) - pure C#: no UnityEngine, System.Numerics
// only, so the SAME file compiles and RUNS in Tools/Build/swarm_core_harness beside the shipped sort core (mode
// `tandava`). The Unity side (TandavaSwarmDirector) feeds it the swarm's state once per tick and applies what it decides.
//
// It owns three decisions and nothing else:
//   * the ROUTE - which oasis the swarm swims to next, when it has fed enough to move on, and finally the exit;
//   * the FORM - when the body is full AND the stomach has banked the next form's surplus, it commits the next form
//     (the swarm then re-sorts through the sort core's ordinary plan commit: stale fates, molts, the lay ramp);
//   * the OUTCOME - the swarm crossed the exit plane (the pilots lose, scored by the form it escaped as), or it was
//     wiped out / starved, or its final form was cut below the break threshold (the pilots win).
// The swarm still EVOLVES BY EATING: a form is committed only when the body the swarm grew from eaten flora is full
// and its stomach holds the surplus - the director names which shape comes next, never when, and never kills anyone.
using System;
using System.Collections.Generic;
using System.Numerics;

namespace CosmicShore.Gameplay
{
    /// <summary>The ways a Tandava match can end, from the swarm's side. Explicit values: replicated as an int.</summary>
    public enum TandavaOutcome
    {
        Running = 0,
        /// <summary>The swarm crossed the exit membrane. The pilots lose (scored by the form it escaped as).</summary>
        Escaped = 1,
        /// <summary>Every member is dead.</summary>
        Wiped = 2,
        /// <summary>Every member is dead and the last ones starved (the pilots denied the oases).</summary>
        Starved = 3,
        /// <summary>The final form was cut below its break threshold: the echo loses the shape and scatters.</summary>
        Broken = 4,
    }

    public enum TandavaEventKind
    {
        FormCommitted = 0,
        OasisReached = 1,
        OasisLeft = 2,
        OasisSkipped = 3,
        HeadingForExit = 4,
        Ended = 5,
    }

    public struct TandavaEvent
    {
        public TandavaEventKind Kind;
        /// <summary>FormCommitted: the form left. Oasis*: the oasis index.</summary>
        public int A;
        /// <summary>FormCommitted: the form entered. Ended: the <see cref="TandavaOutcome"/>.</summary>
        public int B;
    }

    /// <summary>One form the swarm can take, in order. Plan counts are in GAME members (the plan's units x density).</summary>
    public sealed class TandavaForm
    {
        public string Name = "";
        /// <summary>Index of this form's body plan in the swarm's scripted plan list.</summary>
        public int PlanIndex;
        /// <summary>Members of this form's full body.</summary>
        public int PlanCount;
        /// <summary>The body must be at least this share of <see cref="PlanCount"/> alive (hatched) to evolve on.</summary>
        public float FillToEvolve = 0.9f;
        /// <summary>Banked stomach volume per research element (0 Charge .. 3 Time) the swarm must hold to evolve on.
        /// Zero for an element the form does not need.</summary>
        public readonly float[] BankToEvolve = new float[4];
        /// <summary>What one stop eats in this form (flora volume): the swarm leaves an oasis once it has eaten this
        /// much THERE. The generator authors it as the form's whole stage (grow the body + bank the surplus) over the
        /// design's "two to three oases per stage", so a denied oasis is a meal the stage is short.</summary>
        public float MealVolume = float.PositiveInfinity;
    }

    /// <summary>A feeding ground on the route: where it is, how near counts as "there", and whether pilots denied it.</summary>
    public struct TandavaOasis
    {
        public Vector3 Centre;
        public float Radius;
        /// <summary>Pilots burned or claimed it: nothing there is food for the swarm any more. Set by the glue.</summary>
        public bool Denied;
    }

    public sealed class TandavaDirectorSettings
    {
        /// <summary>The swarm is AT an oasis when its anchor is within the oasis radius plus this (world units).</summary>
        public float ArriveMargin = 60f;
        /// <summary>It leaves an oasis once its stomach is this full (0..1), whatever its meal - there is no room left.</summary>
        public float LeaveWhenStomachFill = 0.98f;
        /// <summary>It leaves an oasis it has taken no bite from for this long (grazed bare, or denied around it).</summary>
        public float GiveUpSeconds = 8f;
        /// <summary>It never sits at one oasis longer than this, fed or not - the race keeps moving.</summary>
        public float MaxFeedSeconds = 40f;
        /// <summary>A point on the exit membrane, and its normal pointing OUT of the course.</summary>
        public Vector3 ExitPoint, ExitNormal = Vector3.UnitX;
        /// <summary>The final form is broken when fewer than this share of its plan count are alive.</summary>
        public float BreakFraction = 0.35f;
        /// <summary>The break threshold arms only once the final form has held this share of its plan count - so the
        /// moment of its commit (a body still growing into its new shape) can never read as "broken".</summary>
        public float BreakArmFraction = 0.6f;
    }

    /// <summary>What the director needs to know about the swarm, sampled once per tick by the glue.</summary>
    public struct TandavaSwarmState
    {
        public int Alive;
        public Vector3 Anchor;
        /// <summary>Banked eaten volume per research element (0 Charge .. 3 Time).</summary>
        public float Stomach0, Stomach1, Stomach2, Stomach3;
        /// <summary>The stomach's fill, 0..1 (the swarm's own StomachFill).</summary>
        public float StomachFill;
        /// <summary>Seconds since the swarm last took a bite.</summary>
        public float SinceBite;
        /// <summary>Flora volume the swarm has eaten since it hatched (monotone; the glue sums its deposits).</summary>
        public float EatenTotal;
        /// <summary>True while the swarm is starving (the host's StarvationSeconds clock has run out).</summary>
        public bool Starving;
        public float Stomach(int e) => e switch { 0 => Stomach0, 1 => Stomach1, 2 => Stomach2, _ => Stomach3 };
    }

    public sealed class TandavaDirectorCore
    {
        public readonly IReadOnlyList<TandavaForm> Forms;
        public readonly TandavaOasis[] Oases;
        public readonly TandavaDirectorSettings S;
        public readonly List<TandavaEvent> Events = new();

        public int FormIx { get; private set; }
        /// <summary>The oasis the swarm is heading to or feeding at; == Oases.Length once it heads for the exit.</summary>
        public int OasisIx { get; private set; }
        public bool Feeding { get; private set; }
        public TandavaOutcome Outcome { get; private set; } = TandavaOutcome.Running;
        public Vector3 Goal { get; private set; }
        public float Clock { get; private set; }
        /// <summary>The form the swarm escaped as (1 = the first form) - the pilots' loss score. 0 while running or won.</summary>
        public int EscapedAsForm => Outcome == TandavaOutcome.Escaped ? FormIx + 1 : 0;
        public bool IsFinalForm => FormIx >= Forms.Count - 1;

        float _feedSince = -1f, _eatenAtArrival;
        bool _breakArmed;

        /// <summary>Volume eaten at the oasis the swarm is feeding at (0 while travelling) - the meal so far.</summary>
        public float EatenHere { get; private set; }

        public TandavaDirectorCore(IReadOnlyList<TandavaForm> forms, TandavaOasis[] oases, TandavaDirectorSettings settings)
        {
            if (forms == null || forms.Count == 0) throw new ArgumentException("Tandava needs at least one form", nameof(forms));
            Forms = forms; Oases = oases ?? Array.Empty<TandavaOasis>(); S = settings ?? new TandavaDirectorSettings();
            Goal = Oases.Length > 0 ? Oases[0].Centre : S.ExitPoint;
        }

        /// <summary>How far the current form is toward evolving, 0..1: the lesser of its body fill and its banked surplus
        /// (the HUD's "how close is it to the next form"). 1 in the final form.</summary>
        public float EvolveProgress(in TandavaSwarmState s)
        {
            if (IsFinalForm) return 1f;
            var f = Forms[FormIx];
            float fill = f.PlanCount > 0 ? s.Alive / (f.FillToEvolve * f.PlanCount) : 1f;
            float bank = 1f;
            for (int e = 0; e < 4; e++)
                if (f.BankToEvolve[e] > 0f) bank = MathF.Min(bank, s.Stomach(e) / f.BankToEvolve[e]);
            return Math.Clamp(MathF.Min(fill, bank), 0f, 1f);
        }

        /// <summary>The glue marks an oasis denied (its flora burned or claimed). A swarm heading there skips it.</summary>
        public void SetDenied(int oasis, bool denied)
        {
            if (oasis >= 0 && oasis < Oases.Length) Oases[oasis].Denied = denied;
        }

        /// <summary>One director tick: route, form, outcome. Events of this tick are appended to <see cref="Events"/>.</summary>
        public void Tick(float dt, in TandavaSwarmState s)
        {
            if (Outcome != TandavaOutcome.Running) return;
            Clock += dt;

            // ── outcome first: an escape or a wipe ends the match whatever else this tick would decide
            if (s.Alive <= 0) { End(s.Starving ? TandavaOutcome.Starved : TandavaOutcome.Wiped); return; }
            if (Vector3.Dot(s.Anchor - S.ExitPoint, S.ExitNormal) >= 0f) { End(TandavaOutcome.Escaped); return; }
            if (IsFinalForm)
            {
                var fin = Forms[FormIx];
                if (!_breakArmed && s.Alive >= S.BreakArmFraction * fin.PlanCount) _breakArmed = true;
                if (_breakArmed && s.Alive < S.BreakFraction * fin.PlanCount) { End(TandavaOutcome.Broken); return; }
            }

            // ── form: a full body with the surplus banked takes the next shape
            if (!IsFinalForm && EvolveProgress(s) >= 1f)
            {
                int from = FormIx;
                FormIx++;
                Events.Add(new TandavaEvent { Kind = TandavaEventKind.FormCommitted, A = from, B = FormIx });
                // the body it commits with already counts toward the final form's arming (a cull the very next second
                // must still be able to break it)
                if (IsFinalForm && s.Alive >= S.BreakArmFraction * Forms[FormIx].PlanCount) _breakArmed = true;
            }

            // ── route: travel -> feed -> leave, oasis by oasis, then the exit
            while (OasisIx < Oases.Length && !Feeding && Oases[OasisIx].Denied)
            {
                Events.Add(new TandavaEvent { Kind = TandavaEventKind.OasisSkipped, A = OasisIx });
                Advance();
            }
            if (OasisIx < Oases.Length)
            {
                ref var o = ref Oases[OasisIx];
                if (!Feeding)
                {
                    if (Vector3.Distance(s.Anchor, o.Centre) <= o.Radius + S.ArriveMargin)
                    {
                        Feeding = true; _feedSince = Clock; _eatenAtArrival = s.EatenTotal; EatenHere = 0f;
                        Events.Add(new TandavaEvent { Kind = TandavaEventKind.OasisReached, A = OasisIx });
                    }
                }
                else
                {
                    float sat = Clock - _feedSince;
                    EatenHere = s.EatenTotal - _eatenAtArrival;
                    bool fed = EatenHere >= Forms[FormIx].MealVolume;
                    bool full = s.StomachFill >= S.LeaveWhenStomachFill;
                    bool bare = sat > S.GiveUpSeconds && s.SinceBite > S.GiveUpSeconds;
                    if (fed || full || bare || o.Denied || sat > S.MaxFeedSeconds)
                    {
                        Events.Add(new TandavaEvent { Kind = TandavaEventKind.OasisLeft, A = OasisIx });
                        Advance();
                    }
                }
            }
            Goal = OasisIx < Oases.Length ? Oases[OasisIx].Centre : S.ExitPoint + S.ExitNormal * 200f;
        }

        void Advance()
        {
            Feeding = false; _feedSince = -1f; EatenHere = 0f;
            OasisIx++;
            if (OasisIx == Oases.Length) Events.Add(new TandavaEvent { Kind = TandavaEventKind.HeadingForExit, A = OasisIx });
        }

        void End(TandavaOutcome outcome)
        {
            Outcome = outcome;
            Feeding = false;
            Events.Add(new TandavaEvent { Kind = TandavaEventKind.Ended, A = FormIx, B = (int)outcome });
        }
    }
}
