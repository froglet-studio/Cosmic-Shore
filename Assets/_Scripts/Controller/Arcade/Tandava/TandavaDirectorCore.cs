// Tandava's STAGE DIRECTOR (Assets/_Scripts/Controller/Arcade/TANDAVA.md §3) - pure C#: no UnityEngine, System.Numerics
// only, so the SAME file compiles and RUNS in Tools/Build/swarm_core_harness beside the shipped sort core (mode
// `tandava`). TandavaController feeds it the swarm's state once per tick and applies what it decides.
//
// It owns four decisions and nothing else:
//   * the ROUTE - which oasis the swarm swims to next and when it has fed enough to move on; once the route is eaten,
//     it FORAGES whatever the pilots left (nearest oasis with food first), and with nothing left anywhere it presses on
//     the sealed membrane, STARVING;
//   * the FORM - when the body is full AND the stomach has banked the next form's surplus, it commits the next form
//     (the swarm re-sorts through the sort core's ordinary plan commit: stale fates, molts, the lay ramp). The last
//     EATING form (the Bull) does not eat on to anything: banked, it RISES into the dance form (the Lord of the Dance,
//     its attendant packs patrolling) and swims to the DANCE GROUND as it assembles;
//   * the ASCENSION - at the dance ground the ring of fire lights round the figure, a drum runs, and pilots put out
//     flames by threading them. Enough flames out and the dance is BROKEN; the drum ends first and the swarm takes its
//     FINAL form;
//   * the OUTCOME - the exit is SEALED to every form but the final one: crossing it as the final form is the pilots'
//     loss. Wiped out, starved at the membrane, the dance broken, or the final form cut below its break threshold is
//     the pilots' win.
// The swarm still EVOLVES BY EATING: a form is committed only when the body the swarm grew from eaten flora is full
// and its stomach holds the surplus. The director names which shape comes next, never when, and never kills anyone:
// starvation is the swarm's own metabolism (SwarmFaunaConfigSO.StarvationSeconds), the director only stops it leaving.
using System;
using System.Collections.Generic;
using System.Numerics;

namespace CosmicShore.Gameplay
{
    /// <summary>The ways a Tandava match can end, from the swarm's side. Explicit values: replicated as an int.</summary>
    public enum TandavaOutcome
    {
        Running = 0,
        /// <summary>The final form crossed the exit membrane. The pilots lose.</summary>
        Escaped = 1,
        /// <summary>Every member is dead.</summary>
        Wiped = 2,
        /// <summary>Nothing left to eat and no form left it could take: it starved at the sealed membrane.</summary>
        Starved = 3,
        /// <summary>The final form was cut below its break threshold.</summary>
        Broken = 4,
        /// <summary>The pilots put out enough of the ring of fire before the drum stopped.</summary>
        DanceBroken = 5,
    }

    /// <summary>Where the swarm is in its story. Explicit values: replicated as an int.</summary>
    public enum TandavaPhase
    {
        /// <summary>Eating its way down the route, oasis by oasis.</summary>
        Route = 0,
        /// <summary>The route is eaten; foraging what the pilots left, nearest food first.</summary>
        Forage = 1,
        /// <summary>Nothing left to eat anywhere: pressing on the sealed membrane.</summary>
        Starving = 2,
        /// <summary>The last eating form is banked: it has risen into the dance form and is going to the dance ground.</summary>
        ToDance = 3,
        /// <summary>The ascension: the ring of fire, the drum.</summary>
        Dance = 4,
        /// <summary>The final form, and the exit open to it.</summary>
        Final = 5,
        Over = 6,
    }

    public enum TandavaEventKind
    {
        FormCommitted = 0,
        OasisReached = 1,
        OasisLeft = 2,
        OasisSkipped = 3,
        /// <summary>The route's last oasis is behind it.</summary>
        HeadingForExit = 4,
        Ended = 5,
        /// <summary>Foraging: the route is eaten.</summary>
        RouteEaten = 6,
        /// <summary>No food left anywhere.</summary>
        Starving = 7,
        /// <summary>The last eating form is banked: it takes the dance form and turns for the dance ground. A: the form
        /// left, B: the dance form.</summary>
        ReadyToDance = 8,
        /// <summary>At the dance ground: the ring of fire lights and the drum starts. B: the dance form.</summary>
        AscensionBegun = 9,
        /// <summary>A flame of the ring of fire went out. A: the flame. B: flames out so far.</summary>
        FlameOut = 10,
        /// <summary>A non-final form reached the exit and the membrane held.</summary>
        Sealed = 11,
    }

    public struct TandavaEvent
    {
        public TandavaEventKind Kind;
        /// <summary>FormCommitted: the form left. Oasis*: the oasis index. FlameOut: the flame.</summary>
        public int A;
        /// <summary>FormCommitted: the form entered. Ended: the <see cref="TandavaOutcome"/>. FlameOut: flames out.</summary>
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
        /// Zero for an element the form does not need. The last eating form's bank is the ascension's.</summary>
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
        /// <summary>Nothing there is food for the swarm any more (burned, claimed, or grazed bare). Set by the glue.</summary>
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
        /// <summary>A non-final form is "at the sealed membrane" within this of the exit plane (world units).</summary>
        public float SealMargin = 60f;
        /// <summary>The sealed membrane holds a non-final form's anchor at least this far inside the exit plane
        /// (<see cref="TandavaDirectorCore.SealCorrection"/>). At or above <see cref="SealMargin"/>'s reach, so a held swarm
        /// keeps raising the Sealed beat.</summary>
        public float SealHold = 60f;
        /// <summary>Where a starving swarm waits: this far inside the exit plane, pressing on the membrane.</summary>
        public float StarveStandoff = 140f;
        /// <summary>A swarm with nothing to eat is Starved after pressing on the membrane this long (counted from when it
        /// reaches the standoff), or once its body is below <see cref="StarvedBelowFraction"/> of its plan - whichever
        /// comes first. The body's own shedding is the swarm's metabolism (SwarmFaunaConfigSO.StarvationSeconds); this is
        /// the clock that says the pilots have WON, for a swarm whose banked stomach keeps it from shedding at all.</summary>
        public float StarvingSeconds = 40f;
        public float StarvedBelowFraction = 0.15f;
        /// <summary>The final form is broken when fewer than this share of its plan count are alive.</summary>
        public float BreakFraction = 0.35f;
        /// <summary>The break threshold arms only once the final form has held this share of its plan count - so the
        /// moment of its commit (a body still growing into its new shape) can never read as "broken".</summary>
        public float BreakArmFraction = 0.6f;

        /// <summary>The ascension: Forms[^2] is the dance form and Forms[^1] the final one. Off, the last form is final
        /// and the last-but-one commits on to it by eating, as phase A shipped.</summary>
        public bool Ascension = true;
        public Vector3 DancePoint;
        /// <summary>The swarm is at the dance ground within this (world units).</summary>
        public float DanceArrive = 70f;
        /// <summary>How long the drum runs: the pilots' window to break the ring of fire.</summary>
        public float DrumSeconds = 30f;
        /// <summary>Flames in the ring of fire, and how many must go out to break the dance.</summary>
        public int FlameCount = 12;
        public int FlamesToBreak = 9;
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
        public TandavaPhase Phase { get; private set; } = TandavaPhase.Route;
        /// <summary>The oasis the swarm is heading to or feeding at (-1 when none); on the route it walks 0..Oases.Length.</summary>
        public int Target { get; private set; }
        /// <summary>The route's progress, kept for logs and the HUD: the oasis index the route has reached.</summary>
        public int OasisIx => Math.Max(0, Math.Min(Target, Oases.Length));
        public bool Feeding { get; private set; }
        public TandavaOutcome Outcome { get; private set; } = TandavaOutcome.Running;
        public Vector3 Goal { get; private set; }
        public float Clock { get; private set; }
        /// <summary>Seconds the drum has run (0 outside the dance).</summary>
        public float DanceTime { get; private set; }
        public float DrumRemaining => Phase == TandavaPhase.Dance ? MathF.Max(0f, S.DrumSeconds - DanceTime) : 0f;
        /// <summary>Which flames are out, as a bitmask (replicated as an int).</summary>
        public int FlamesOutMask { get; private set; }
        public int FlamesOut { get; private set; }
        /// <summary>Seconds left before a swarm starving at the membrane is Starved (-1 when not pressing on it).</summary>
        public float StarveRemaining => Phase == TandavaPhase.Starving && _starvingSince >= 0f ? MathF.Max(0f, S.StarvingSeconds - (Clock - _starvingSince)) : -1f;

        public int FinalIx => Forms.Count - 1;
        /// <summary>The dance form, or -1 without the ascension.</summary>
        public int DanceIx => S.Ascension && Forms.Count >= 3 ? Forms.Count - 2 : -1;
        /// <summary>The last form the swarm reaches by EATING.</summary>
        public int LastEaterIx => DanceIx >= 0 ? DanceIx - 1 : FinalIx;
        public bool IsFinalForm => FormIx == FinalIx;
        public bool InDance => Phase == TandavaPhase.Dance;
        /// <summary>The form the swarm escaped as (1 = the first form) - the pilots' loss score. 0 while running or won.</summary>
        public int EscapedAsForm => Outcome == TandavaOutcome.Escaped ? FormIx + 1 : 0;

        float _feedSince = -1f, _eatenAtArrival, _starvingSince = -1f, _sealedAt = -999f;
        bool _breakArmed;

        /// <summary>Volume eaten at the oasis the swarm is feeding at (0 while travelling) - the meal so far.</summary>
        public float EatenHere { get; private set; }

        public TandavaDirectorCore(IReadOnlyList<TandavaForm> forms, TandavaOasis[] oases, TandavaDirectorSettings settings)
        {
            if (forms == null || forms.Count == 0) throw new ArgumentException("Tandava needs at least one form", nameof(forms));
            Forms = forms; Oases = oases ?? Array.Empty<TandavaOasis>(); S = settings ?? new TandavaDirectorSettings();
            Goal = Oases.Length > 0 ? Oases[0].Centre : S.ExitPoint;
        }

        /// <summary>How far the current form is toward its next, 0..1: the lesser of its body fill and its banked surplus
        /// while it eats; the drum's progress in the dance; 1 in the final form.</summary>
        public float EvolveProgress(in TandavaSwarmState s)
        {
            if (InDance) return S.DrumSeconds > 0f ? Math.Clamp(DanceTime / S.DrumSeconds, 0f, 1f) : 1f;
            if (FormIx > LastEaterIx || (FormIx == LastEaterIx && DanceIx < 0)) return 1f;
            var f = Forms[FormIx];
            float fill = f.PlanCount > 0 ? s.Alive / (f.FillToEvolve * f.PlanCount) : 1f;
            float bank = 1f;
            for (int e = 0; e < 4; e++)
                if (f.BankToEvolve[e] > 0f) bank = MathF.Min(bank, s.Stomach(e) / f.BankToEvolve[e]);
            return Math.Clamp(MathF.Min(fill, bank), 0f, 1f);
        }

        /// <summary>The glue marks an oasis denied (nothing there the swarm can eat). A swarm heading there skips it.</summary>
        public void SetDenied(int oasis, bool denied)
        {
            if (oasis >= 0 && oasis < Oases.Length) Oases[oasis].Denied = denied;
        }

        /// <summary>The sealed exit as a correction every peer applies to its OWN swarm (the swarms are client-local): a
        /// non-final form whose anchor is past <see cref="TandavaDirectorSettings.SealHold"/> of the exit plane is pushed
        /// straight back to it - the membrane holds. Zero for the final form, and inside the hold.</summary>
        public Vector3 SealCorrection(Vector3 anchor) => SealCorrection(anchor, IsFinalForm, S);

        /// <summary>The same rule for a peer that knows only the replicated form (a client).</summary>
        public static Vector3 SealCorrection(Vector3 anchor, bool finalForm, TandavaDirectorSettings s)
        {
            if (finalForm || s == null) return Vector3.Zero;
            float over = Vector3.Dot(anchor - s.ExitPoint, s.ExitNormal) + s.SealHold;
            return over > 0f ? -s.ExitNormal * over : Vector3.Zero;
        }

        public bool FlameIsOut(int k) => k >= 0 && k < 32 && (FlamesOutMask & (1 << k)) != 0;

        /// <summary>A pilot threaded flame <paramref name="k"/> while nothing guarded it (the glue decides both). Returns
        /// true when it went out. Enough flames out ends the match: the dance is broken.</summary>
        public bool BreakFlame(int k)
        {
            if (!InDance || Outcome != TandavaOutcome.Running || k < 0 || k >= S.FlameCount || FlameIsOut(k)) return false;
            FlamesOutMask |= 1 << k; FlamesOut++;
            Events.Add(new TandavaEvent { Kind = TandavaEventKind.FlameOut, A = k, B = FlamesOut });
            if (FlamesOut >= S.FlamesToBreak) End(TandavaOutcome.DanceBroken);
            return true;
        }

        /// <summary>One director tick: outcome, form, route, ascension. Events of this tick are appended to <see cref="Events"/>.</summary>
        public void Tick(float dt, in TandavaSwarmState s)
        {
            if (Outcome != TandavaOutcome.Running) return;
            Clock += dt;

            // ── outcome first
            if (s.Alive <= 0) { End(Phase == TandavaPhase.Starving || s.Starving ? TandavaOutcome.Starved : TandavaOutcome.Wiped); return; }
            float outward = Vector3.Dot(s.Anchor - S.ExitPoint, S.ExitNormal);
            if (IsFinalForm && outward >= 0f) { End(TandavaOutcome.Escaped); return; }
            if (!IsFinalForm && outward >= -S.SealMargin && Clock - _sealedAt > 5f)
            {
                _sealedAt = Clock;
                Events.Add(new TandavaEvent { Kind = TandavaEventKind.Sealed, A = FormIx });
            }
            if (IsFinalForm)
            {
                var fin = Forms[FormIx];
                if (!_breakArmed && s.Alive >= S.BreakArmFraction * fin.PlanCount) _breakArmed = true;
                if (_breakArmed && s.Alive < S.BreakFraction * fin.PlanCount) { End(TandavaOutcome.Broken); return; }
            }
            if (Phase == TandavaPhase.Starving)
            {
                if (_starvingSince < 0f && outward >= -(S.StarveStandoff + S.ArriveMargin)) _starvingSince = Clock;   // at the membrane
                if ((_starvingSince >= 0f && Clock - _starvingSince >= S.StarvingSeconds) ||
                    s.Alive < S.StarvedBelowFraction * Forms[FormIx].PlanCount)
                { End(TandavaOutcome.Starved); return; }
            }

            // ── form: a full body with the surplus banked takes the next shape (or, banked as the last eater, the dance)
            bool eating = Phase is TandavaPhase.Route or TandavaPhase.Forage or TandavaPhase.Starving;
            if (eating && FormIx <= LastEaterIx && !(FormIx == LastEaterIx && DanceIx < 0 && IsFinalForm) && EvolveProgress(s) >= 1f)
            {
                if (FormIx < LastEaterIx || DanceIx < 0) Commit(FormIx + 1, s);
                else
                {
                    // the banked Bull rises into the dance form NOW and assembles it on the way, so the figure and its
                    // attendants are standing when the ring lights (the shape needs its time; the drum is the pilots')
                    int from = FormIx;
                    Phase = TandavaPhase.ToDance; Feeding = false; FormIx = DanceIx;
                    Events.Add(new TandavaEvent { Kind = TandavaEventKind.ReadyToDance, A = from, B = DanceIx });
                }
            }

            // ── route, forage, starve
            if (Phase is TandavaPhase.Route or TandavaPhase.Forage or TandavaPhase.Starving) Route(s);

            // ── the ascension
            if (Phase == TandavaPhase.ToDance && Vector3.Distance(s.Anchor, S.DancePoint) <= S.DanceArrive)
            {
                Phase = TandavaPhase.Dance; DanceTime = 0f; FlamesOut = 0; FlamesOutMask = 0;
                Events.Add(new TandavaEvent { Kind = TandavaEventKind.AscensionBegun, A = FormIx, B = DanceIx });
            }
            else if (Phase == TandavaPhase.Dance)
            {
                DanceTime += dt;
                if (DanceTime >= S.DrumSeconds) { Phase = TandavaPhase.Final; Commit(FinalIx, s); }
            }

            Goal = Phase switch
            {
                TandavaPhase.ToDance or TandavaPhase.Dance => S.DancePoint,
                TandavaPhase.Final => S.ExitPoint + S.ExitNormal * 200f,
                TandavaPhase.Starving => S.ExitPoint - S.ExitNormal * S.StarveStandoff,
                _ => Target >= 0 && Target < Oases.Length ? Oases[Target].Centre : S.ExitPoint - S.ExitNormal * S.StarveStandoff,
            };
        }

        void Commit(int to, in TandavaSwarmState s)
        {
            int from = FormIx; FormIx = to;
            Events.Add(new TandavaEvent { Kind = TandavaEventKind.FormCommitted, A = from, B = to });
            // the body it commits with already counts toward the final form's arming (a cull the very next second
            // must still be able to break it)
            if (IsFinalForm && s.Alive >= S.BreakArmFraction * Forms[FormIx].PlanCount) _breakArmed = true;
            if (IsFinalForm) Phase = TandavaPhase.Final;
        }

        int NearestFood(Vector3 at)
        {
            int best = -1; float bd = float.MaxValue;
            for (int k = 0; k < Oases.Length; k++)
            {
                if (Oases[k].Denied) continue;
                float d = Vector3.Distance(at, Oases[k].Centre);
                if (d < bd) { bd = d; best = k; }
            }
            return best;
        }

        void Route(in TandavaSwarmState s)
        {
            if (Phase == TandavaPhase.Route)
            {
                while (Target < Oases.Length && !Feeding && Oases[Target].Denied)
                {
                    Events.Add(new TandavaEvent { Kind = TandavaEventKind.OasisSkipped, A = Target });
                    Target++;
                }
                if (Target >= Oases.Length) { Phase = TandavaPhase.Forage; Events.Add(new TandavaEvent { Kind = TandavaEventKind.RouteEaten }); }
            }
            if (Phase != TandavaPhase.Route && !Feeding)
            {
                int k = NearestFood(s.Anchor);
                if (k < 0)
                {
                    if (Phase != TandavaPhase.Starving)
                    {
                        Phase = TandavaPhase.Starving; _starvingSince = -1f;
                        Events.Add(new TandavaEvent { Kind = TandavaEventKind.Starving });
                    }
                    Target = -1;
                    return;
                }
                if (Phase == TandavaPhase.Starving) { Phase = TandavaPhase.Forage; _starvingSince = -1f; }
                Target = k;
            }
            if (Target < 0 || Target >= Oases.Length) return;
            ref var o = ref Oases[Target];
            if (!Feeding)
            {
                if (Vector3.Distance(s.Anchor, o.Centre) <= o.Radius + S.ArriveMargin)
                {
                    Feeding = true; _feedSince = Clock; _eatenAtArrival = s.EatenTotal; EatenHere = 0f;
                    Events.Add(new TandavaEvent { Kind = TandavaEventKind.OasisReached, A = Target });
                }
                return;
            }
            float sat = Clock - _feedSince;
            EatenHere = s.EatenTotal - _eatenAtArrival;
            bool fed = EatenHere >= Forms[Math.Min(FormIx, LastEaterIx)].MealVolume;
            bool full = s.StomachFill >= S.LeaveWhenStomachFill;
            bool bare = sat > S.GiveUpSeconds && s.SinceBite > S.GiveUpSeconds;
            if (fed || full || bare || o.Denied || sat > S.MaxFeedSeconds)
            {
                Events.Add(new TandavaEvent { Kind = TandavaEventKind.OasisLeft, A = Target });
                Feeding = false; _feedSince = -1f; EatenHere = 0f;
                if (Phase == TandavaPhase.Route)
                {
                    Target++;
                    if (Target == Oases.Length) Events.Add(new TandavaEvent { Kind = TandavaEventKind.HeadingForExit, A = Target });
                }
                // foraging: a stop that yields nothing is written off, so the forage moves on rather than circling
                else if (bare || sat > S.MaxFeedSeconds) o.Denied = true;
            }
        }

        void End(TandavaOutcome outcome)
        {
            if (Outcome != TandavaOutcome.Running) return;
            Outcome = outcome;
            Feeding = false;
            Phase = TandavaPhase.Over;
            Events.Add(new TandavaEvent { Kind = TandavaEventKind.Ended, A = FormIx, B = (int)outcome });
        }
    }
}
